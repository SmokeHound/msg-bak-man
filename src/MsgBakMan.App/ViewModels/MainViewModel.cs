using System.IO;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ModernWpf;
using MsgBakMan.App.Services;
using MsgBakMan.Data.Project;
using MsgBakMan.Data.Repositories;
using MsgBakMan.Data.Sqlite;
using MsgBakMan.ImportExport.Export;
using MsgBakMan.ImportExport.Import;
using MsgBakMan.ImportExport.Media;

namespace MsgBakMan.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly List<ConversationListItem> _conversationItemsWithHandlers = new();
    private readonly List<MergeSuggestionItem> _mergeSuggestionItemsWithHandlers = new();
    private bool _suppressMarkedMergeCountUpdates;

    public MainViewModel()
    {
        var settings = AppSettingsStore.Load();

        // Initialize from current theme without triggering a save.
        _isDarkTheme = ThemeManager.Current.ApplicationTheme == ApplicationTheme.Dark;
        _selectedAccentPalette = NormalizeAccentPalette(settings.AccentPalette);
    }

    [ObservableProperty]
    private string _projectFolder = GetDefaultProjectFolder();

    partial void OnProjectFolderChanged(string value)
    {
        OpenProjectFolderCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty]
    private bool _isDarkTheme;

    [ObservableProperty]
    private string _selectedAccentPalette = "Blue";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _busyText = string.Empty;

    [ObservableProperty]
    private string _logText = string.Empty;

    [ObservableProperty]
    private string _conversationSearch = string.Empty;

    [ObservableProperty]
    private string _messageSearch = string.Empty;

    [ObservableProperty]
    private int _conversationCount;

    [ObservableProperty]
    private int _conversationMessageCount;

    [ObservableProperty]
    private int _searchResultCount;

    [ObservableProperty]
    private int _mergeSuggestionCount;

    [ObservableProperty]
    private int _markedMergeCount;

    [ObservableProperty]
    private string _mergeSuggestionStatus = string.Empty;

    public ObservableCollection<ConversationListItem> Conversations { get; } = new();
    public ObservableCollection<MergeSuggestionItem> MergeSuggestions { get; } = new();

    public IReadOnlyList<string> AccentPalettes { get; } = new[]
    {
        "Blue",
        "Cyan",
        "Emerald",
        "Lime",
        "Orange",
        "Red",
        "Violet",
        "Slate"
    };

    [ObservableProperty]
    private ConversationListItem? _selectedConversation;

    public ObservableCollection<MessageListItem> ConversationMessages { get; } = new();
    public ObservableCollection<MessageListItem> SearchResults { get; } = new();

    [ObservableProperty]
    private MessageListItem? _selectedSearchResult;

    public bool IsNotBusy => !IsBusy;

    public bool CanMergeCheckedIntoSelected => SelectedConversation is not null && Conversations.Any(c => c.IsChecked && c.ConversationId != SelectedConversation.ConversationId);

    partial void OnIsDarkThemeChanged(bool value)
    {
        ThemeManager.Current.ApplicationTheme = value ? ApplicationTheme.Dark : ApplicationTheme.Light;
        global::MsgBakMan.App.App.ApplyAppThemeBrushes(
            ThemeManager.Current.ApplicationTheme ?? ApplicationTheme.Light,
            SelectedAccentPalette);

        AppSettingsStore.Save(new AppSettings(value ? "Dark" : "Light", SelectedAccentPalette));
    }

    partial void OnSelectedAccentPaletteChanged(string value)
    {
        var normalized = NormalizeAccentPalette(value);
        if (!string.Equals(normalized, value, StringComparison.Ordinal))
        {
            SelectedAccentPalette = normalized;
            return;
        }

        global::MsgBakMan.App.App.ApplyAppThemeBrushes(
            ThemeManager.Current.ApplicationTheme ?? ApplicationTheme.Light,
            SelectedAccentPalette);

        AppSettingsStore.Save(new AppSettings(IsDarkTheme ? "Dark" : "Light", SelectedAccentPalette));
    }

    private static string NormalizeAccentPalette(string? palette)
    {
        if (string.IsNullOrWhiteSpace(palette))
        {
            return "Blue";
        }

        return palette.Trim().ToLowerInvariant() switch
        {
            "blue" => "Blue",
            "cyan" => "Cyan",
            "teal" => "Cyan",
            "emerald" => "Emerald",
            "green" => "Emerald",
            "lime" => "Lime",
            "amber" => "Orange",
            "yellow" => "Orange",
            "orange" => "Orange",
            "red" => "Red",
            "rose" => "Red",
            "pink" => "Red",
            "slate" => "Slate",
            "gray" => "Slate",
            "grey" => "Slate",
            "violet" => "Violet",
            "purple" => "Violet",
            _ => "Blue"
        };
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotBusy));
        AcceptMarkedMergesCommand.NotifyCanExecuteChanged();
        MarkAllMergeSuggestionsCommand.NotifyCanExecuteChanged();
        ClearMarkedMergeSuggestionsCommand.NotifyCanExecuteChanged();
    }

    partial void OnMarkedMergeCountChanged(int value)
    {
        AcceptMarkedMergesCommand.NotifyCanExecuteChanged();
    }

    partial void OnMergeSuggestionCountChanged(int value)
    {
        MarkAllMergeSuggestionsCommand.NotifyCanExecuteChanged();
        ClearMarkedMergeSuggestionsCommand.NotifyCanExecuteChanged();
    }

    partial void OnConversationSearchChanged(string value)
    {
        // purely UI convenience; no auto-refresh
    }

    partial void OnMessageSearchChanged(string value)
    {
        // purely UI convenience; no auto-search
    }

    [RelayCommand]
    private void BrowseProjectFolder()
    {
        using var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select a project folder (contains db/ and media/)",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            SelectedPath = ProjectFolder
        };

        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
        {
            ProjectFolder = dlg.SelectedPath;
        }
    }

    private bool CanOpenProjectFolder()
        => !string.IsNullOrWhiteSpace(ProjectFolder) && Directory.Exists(ProjectFolder);

    [RelayCommand(CanExecute = nameof(CanOpenProjectFolder))]
    private void OpenProjectFolder()
    {
        var path = ProjectFolder;
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }

    [RelayCommand]
    private async Task ImportXml()
    {
        var ofd = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "SMS Backup & Restore XML (*.xml)|*.xml|All files (*.*)|*.*",
            Multiselect = true,
            Title = "Select one or more SMS Backup & Restore XML files"
        };

        if (ofd.ShowDialog() != true)
        {
            return;
        }

        await RunBusyAsync("Importing...", async ct =>
        {
            var paths = EnsureProjectInitialized();

            using var conn = new SqliteConnectionFactory(paths.DbPath).Open();
            new MigrationRunner().ApplyAll(conn);

            var repo = new ImportRepository(conn);
            var importer = new SmsBackupRestoreXmlImporter(repo, paths);

            var maintenance = new ConversationMaintenance(conn);

            var files = ofd.FileNames;
            var total = files.Length;
            for (var i = 0; i < total; i++)
            {
                var index = i + 1;
                var perFileProgress = new Progress<string>(msg => AppendLog($"[{index}/{total}] {msg}"));
                await importer.ImportAsync(files[i], perFileProgress, ct);
            }

            maintenance.BackfillConversations();
        });

        await RefreshConversations();
    }

    [RelayCommand]
    private async Task ExportXml()
    {
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "SMS Backup & Restore XML (*.xml)|*.xml",
            Title = "Export merged SMS Backup & Restore XML",
            FileName = $"sms-merged-{DateTime.Now:yyyyMMdd-HHmmss}.xml"
        };

        if (sfd.ShowDialog() != true)
        {
            return;
        }

        await RunBusyAsync("Exporting...", async ct =>
        {
            var paths = EnsureProjectInitialized();

            using var conn = new SqliteConnectionFactory(paths.DbPath).Open();
            new MigrationRunner().ApplyAll(conn);

            var repo = new ExportRepository(conn);
            var exporter = new SmsBackupRestoreXmlExporter(repo, paths);
            var progress = new Progress<string>(AppendLog);
            await exporter.ExportAsync(sfd.FileName, progress, ct);
        });
    }

    [RelayCommand]
    private async Task ExportMedia()
    {
        using var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select an output folder for exported media",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            SelectedPath = ProjectFolder
        };

        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dlg.SelectedPath))
        {
            return;
        }

        var outputRoot = dlg.SelectedPath;

        await RunBusyAsync("Exporting media...", ct =>
        {
            var paths = EnsureProjectInitialized();

            Directory.CreateDirectory(outputRoot);

            using var conn = new SqliteConnectionFactory(paths.DbPath).Open();
            new MigrationRunner().ApplyAll(conn);

            var repo = new ExportRepository(conn);
            var blobs = repo.GetAllMediaBlobs().ToList();

            var copied = 0;
            var skipped = 0;
            var missing = 0;
            var errors = 0;

            for (var i = 0; i < blobs.Count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var b = blobs[i];
                var src = Path.Combine(paths.ProjectRoot, b.RelativePath.Replace('/', '\\'));
                if (!File.Exists(src))
                {
                    missing++;
                    continue;
                }

                var ext = NormalizeExtension(b.Extension, b.MimeType);
                var dest = Path.Combine(outputRoot, b.Sha256 + ext);

                if (File.Exists(dest))
                {
                    skipped++;
                    continue;
                }

                try
                {
                    File.Copy(src, dest);
                    copied++;

                    if (copied % 250 == 0)
                    {
                        AppendLog($"Exported {copied:n0}/{blobs.Count:n0}…");
                    }
                }
                catch
                {
                    errors++;
                }
            }

            AppendLog($"Media export complete → {outputRoot}");
            AppendLog($"Copied: {copied:n0} • Skipped existing: {skipped:n0} • Missing source: {missing:n0} • Errors: {errors:n0}");
            return Task.CompletedTask;
        });
    }

    [RelayCommand]
    private async Task ExportMediaByPhone()
    {
        using var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select an output folder (subfolders will be created by phone number)",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            SelectedPath = ProjectFolder
        };

        if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK || string.IsNullOrWhiteSpace(dlg.SelectedPath))
        {
            return;
        }

        var outputRoot = dlg.SelectedPath;

        await RunBusyAsync("Exporting media...", ct =>
        {
            var paths = EnsureProjectInitialized();
            Directory.CreateDirectory(outputRoot);

            using var conn = new SqliteConnectionFactory(paths.DbPath).Open();
            new MigrationRunner().ApplyAll(conn);

            // Ensure conversation_id + recipients are populated.
            new ConversationMaintenance(conn).BackfillConversations();

            var repo = new ExportRepository(conn);
            var rows = repo.GetConversationMediaBlobs().ToList();

            var copied = 0;
            var skipped = 0;
            var missing = 0;
            var errors = 0;

            foreach (var group in rows.GroupBy(r => r.ConversationId))
            {
                ct.ThrowIfCancellationRequested();

                var recipients = group
                    .Select(r => r.RecipientAddressNorm)
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();

                var folderLabel = recipients.Count == 1
                    ? recipients[0]!
                    : $"group_{group.Key}";

                var folderName = MakeSafeFolderName(folderLabel);
                var destFolder = Path.Combine(outputRoot, folderName);
                Directory.CreateDirectory(destFolder);

                var seenInFolder = new HashSet<string>(StringComparer.Ordinal);

                foreach (var r in group)
                {
                    ct.ThrowIfCancellationRequested();

                    // De-dupe per-folder; query can return multiple rows per blob due to multiple recipients.
                    if (!seenInFolder.Add(r.Sha256))
                    {
                        continue;
                    }

                    var src = Path.Combine(paths.ProjectRoot, r.RelativePath.Replace('/', '\\'));
                    if (!File.Exists(src))
                    {
                        missing++;
                        continue;
                    }

                    var ext = NormalizeExtension(r.Extension, r.MimeType);
                    var dest = Path.Combine(destFolder, r.Sha256 + ext);

                    if (File.Exists(dest))
                    {
                        skipped++;
                        continue;
                    }

                    try
                    {
                        File.Copy(src, dest);
                        copied++;

                        if (copied % 250 == 0)
                        {
                            AppendLog($"Exported {copied:n0}…");
                        }
                    }
                    catch
                    {
                        errors++;
                    }
                }
            }

            AppendLog($"Media export (by phone) complete → {outputRoot}");
            AppendLog($"Copied: {copied:n0} • Skipped existing: {skipped:n0} • Missing source: {missing:n0} • Errors: {errors:n0}");
            return Task.CompletedTask;
        });
    }

    private static string MakeSafeFolderName(string raw)
    {
        var name = string.IsNullOrWhiteSpace(raw) ? "unknown" : raw.Trim();

        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        // Windows also dislikes trailing dots/spaces.
        name = name.TrimEnd('.', ' ');
        if (name.Length == 0)
        {
            name = "unknown";
        }

        if (name.Length > 80)
        {
            name = name[..80].TrimEnd('.', ' ');
        }

        return name;
    }

    private static string NormalizeExtension(string? extension, string? mimeType)
    {
        var ext = extension;

        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = MimeTypes.TryGetExtension(mimeType) ?? ".bin";
        }

        ext = ext.Trim();
        if (!ext.StartsWith('.'))
        {
            ext = "." + ext;
        }

        // Guard against invalid filename characters.
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            ext = ext.Replace(c.ToString(), string.Empty);
        }

        return ext.Length == 0 ? ".bin" : ext;
    }

    [RelayCommand]
    private async Task RefreshConversations()
    {
        await RunBusyAsync("Loading conversations...", ct =>
        {
            var paths = EnsureProjectInitialized();
            using var conn = new SqliteConnectionFactory(paths.DbPath).Open();
            new MigrationRunner().ApplyAll(conn);

            new ConversationMaintenance(conn).BackfillConversations();
            var repo = new ConversationRepository(conn);

            var rows = repo.ListConversations(ConversationSearch).ToList();

            foreach (var old in _conversationItemsWithHandlers)
            {
                old.PropertyChanged -= ConversationItemOnPropertyChanged;
            }
            _conversationItemsWithHandlers.Clear();

            Conversations.Clear();
            foreach (var r in rows)
            {
                var item = new ConversationListItem(r.ConversationId, r.DisplayName ?? r.ConversationKey, r.MessageCount, r.LastDateMs);
                item.PropertyChanged += ConversationItemOnPropertyChanged;
                _conversationItemsWithHandlers.Add(item);
                Conversations.Add(item);
            }

            ConversationCount = Conversations.Count;
            OnPropertyChanged(nameof(CanMergeCheckedIntoSelected));

            return Task.CompletedTask;
        });
    }

    [RelayCommand]
    private async Task RefreshMergeSuggestions()
    {
        await RunBusyAsync("Finding merge suggestions...", ct =>
        {
            var paths = EnsureProjectInitialized();
            using var conn = new SqliteConnectionFactory(paths.DbPath).Open();
            new MigrationRunner().ApplyAll(conn);

            // Ensure conversation_id + recipients are populated.
            new ConversationMaintenance(conn).BackfillConversations();

            var repo = new ConversationRepository(conn);
            var rows = repo.GetConversationRecipients().ToList();

            var convs = rows
                .GroupBy(r => r.ConversationId)
                .Select(g => new ConversationWithRecipients(
                    ConversationId: g.Key,
                    ConversationKey: g.First().ConversationKey,
                    DisplayName: g.First().DisplayName,
                    MessageCount: g.First().MessageCount,
                    LastDateMs: g.First().LastDateMs,
                    Recipients: g.Select(x => x.RecipientAddressNorm)
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .Select(x => x!)
                        .Distinct(StringComparer.Ordinal)
                        .ToList()
                ))
                .ToList();

            var candidates = new List<(string Canonical, string Original, ConversationWithRecipients Conv)>();

            foreach (var c in convs)
            {
                var number = TryGetSingleRecipientNumber(c);
                if (string.IsNullOrWhiteSpace(number))
                {
                    continue;
                }

                var clean = CleanPhoneToken(number);
                if (clean.Length == 0)
                {
                    continue;
                }

                var canonical = TryGetAustralianLocalCanonical(clean);
                if (canonical is null)
                {
                    continue;
                }

                candidates.Add((canonical, clean, c));
            }

            var suggestions = candidates
                .GroupBy(x => x.Canonical, StringComparer.Ordinal)
                .Select(g =>
                {
                    var uniqueConvs = g
                        .Select(x => x.Conv)
                        .GroupBy(x => x.ConversationId)
                        .Select(x => x.First())
                        .ToList();

                    if (uniqueConvs.Count < 2)
                    {
                        return null;
                    }

                    var hasPlus61 = g.Any(x => IsPlus61FamilyToken(x.Original));
                    var hasLocal0 = g.Any(x =>
                        x.Original.StartsWith("0", StringComparison.Ordinal)
                        || x.Original.StartsWith("+0", StringComparison.Ordinal));
                    if (!hasPlus61 || !hasLocal0)
                    {
                        return null;
                    }

                    var preferredTargetIds = g
                        .Where(x => IsPlus61FamilyToken(x.Original))
                        .OrderByDescending(x => IsPlus61Token(x.Original))
                        .Select(x => x.Conv.ConversationId)
                        .Distinct()
                        .ToHashSet();

                    var target = uniqueConvs
                        .Where(x => preferredTargetIds.Contains(x.ConversationId))
                        .OrderByDescending(x => x.MessageCount)
                        .ThenByDescending(x => x.LastDateMs ?? 0)
                        .FirstOrDefault()
                        ?? uniqueConvs
                            .OrderByDescending(x => x.MessageCount)
                            .ThenByDescending(x => x.LastDateMs ?? 0)
                            .First();

                    var mergeIds = uniqueConvs
                        .Where(x => x.ConversationId != target.ConversationId)
                        .Select(x => x.ConversationId)
                        .ToList();

                    var variants = g
                        .Select(x => x.Original)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(x => x, StringComparer.Ordinal)
                        .ToList();

                    var lines = uniqueConvs
                        .OrderByDescending(x => x.MessageCount)
                        .ThenByDescending(x => x.LastDateMs ?? 0)
                        .Select(x => $"• {FormatConversationLabel(x)} — {x.MessageCount:n0} msgs")
                        .ToList();

                    var details = $"Variants: {string.Join(", ", variants)}\n\n" + string.Join("\n", lines) + $"\n\nTarget: {FormatConversationLabel(target)}";

                    return new MergeSuggestionItem(
                        title: $"Merge suggested for {g.Key}",
                        details: details,
                        targetConversationId: target.ConversationId,
                        mergeConversationIds: mergeIds
                    );
                })
                .Where(x => x is not null)
                .Select(x => x!)
                .OrderBy(x => x.Title, StringComparer.Ordinal)
                .ToList();

            foreach (var old in _mergeSuggestionItemsWithHandlers)
            {
                old.PropertyChanged -= MergeSuggestionItemOnPropertyChanged;
            }
            _mergeSuggestionItemsWithHandlers.Clear();

            MergeSuggestions.Clear();
            foreach (var s in suggestions)
            {
                s.PropertyChanged += MergeSuggestionItemOnPropertyChanged;
                _mergeSuggestionItemsWithHandlers.Add(s);
                MergeSuggestions.Add(s);
            }

            MergeSuggestionCount = MergeSuggestions.Count;
            MarkedMergeCount = 0;
            MergeSuggestionStatus = MergeSuggestionCount == 0
                ? "No suggestions found."
                : $"Found {MergeSuggestionCount:n0} suggested merge(s).";

            return Task.CompletedTask;
        });
    }

    private void MergeSuggestionItemOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MergeSuggestionItem.IsMarked))
        {
            if (_suppressMarkedMergeCountUpdates)
            {
                return;
            }

            MarkedMergeCount = MergeSuggestions.Count(x => x.IsMarked);
        }
    }

    private bool CanBulkMarkMergeSuggestions()
        => IsNotBusy && MergeSuggestionCount > 0;

    [RelayCommand(CanExecute = nameof(CanBulkMarkMergeSuggestions))]
    private void MarkAllMergeSuggestions()
    {
        if (MergeSuggestions.Count == 0)
        {
            return;
        }

        _suppressMarkedMergeCountUpdates = true;
        try
        {
            foreach (var s in MergeSuggestions)
            {
                s.IsMarked = true;
            }
        }
        finally
        {
            _suppressMarkedMergeCountUpdates = false;
        }

        MarkedMergeCount = MergeSuggestions.Count;
    }

    [RelayCommand(CanExecute = nameof(CanBulkMarkMergeSuggestions))]
    private void ClearMarkedMergeSuggestions()
    {
        if (MergeSuggestions.Count == 0)
        {
            return;
        }

        _suppressMarkedMergeCountUpdates = true;
        try
        {
            foreach (var s in MergeSuggestions)
            {
                s.IsMarked = false;
            }
        }
        finally
        {
            _suppressMarkedMergeCountUpdates = false;
        }

        MarkedMergeCount = 0;
    }

    private bool CanAcceptMarkedMerges()
        => IsNotBusy && MarkedMergeCount > 0;

    [RelayCommand(CanExecute = nameof(CanAcceptMarkedMerges))]
    private async Task AcceptMarkedMerges()
    {
        var marked = MergeSuggestions
            .Where(x => x.IsMarked)
            .ToList();

        if (marked.Count == 0)
        {
            return;
        }

        await RunBusyAsync($"Merging {marked.Count:n0} marked suggestion(s)...", ct =>
        {
            ct.ThrowIfCancellationRequested();
            var paths = EnsureProjectInitialized();
            using var conn = new SqliteConnectionFactory(paths.DbPath).Open();
            new MigrationRunner().ApplyAll(conn);

            var repo = new ConversationRepository(conn);

            foreach (var suggestion in marked)
            {
                ct.ThrowIfCancellationRequested();

                if (suggestion.MergeConversationIds.Count == 0)
                {
                    continue;
                }

                repo.MergeConversations(suggestion.TargetConversationId, suggestion.MergeConversationIds);
            }

            new ConversationMaintenance(conn).BackfillConversations();
            return Task.CompletedTask;
        });

        await RefreshConversations();
        await RefreshMergeSuggestions();
    }

    private static bool IsPlus61Token(string original)
        => original.StartsWith("+61", StringComparison.Ordinal);

    private static bool IsPlus61FamilyToken(string original)
        => original.StartsWith("+61", StringComparison.Ordinal)
           || original.StartsWith("61", StringComparison.Ordinal)
           || original.StartsWith("0061", StringComparison.Ordinal);

    [RelayCommand]
    private async Task RepairPhoneNumbers()
    {
        await RunBusyAsync("Repairing phone numbers...", ct =>
        {
            ct.ThrowIfCancellationRequested();

            var paths = EnsureProjectInitialized();
            using var conn = new SqliteConnectionFactory(paths.DbPath).Open();
            new MigrationRunner().ApplyAll(conn);

            var result = new PhoneNormalizationMaintenance(conn).RepairLegacyPlus10Numbers();

            // Rebuild conversations/recipients based on corrected normalized addresses.
            new ConversationMaintenance(conn).BackfillConversations();

            AppendLog("Phone number repair complete:");
            AppendLog($"- sms updated: {result.SmsUpdated:n0}");
            AppendLog($"- mms updated: {result.MmsUpdated:n0}");
            AppendLog($"- mms_addr updated: {result.MmsAddrUpdated:n0}");
            AppendLog($"- recipient updated: {result.RecipientsUpdated:n0}");
            AppendLog($"- recipient merged: {result.RecipientsMerged:n0}");
            AppendLog($"- message conversation_id cleared: {result.MessagesConversationCleared:n0}");
            AppendLog($"- conversation rows deleted: {result.ConversationsDeleted:n0}");

            return Task.CompletedTask;
        });

        await RefreshConversations();
        await RefreshMergeSuggestions();
    }

    [RelayCommand]
    private async Task ApplySuggestedMerge(MergeSuggestionItem? suggestion)
    {
        if (suggestion is null)
        {
            return;
        }

        if (suggestion.MergeConversationIds.Count == 0)
        {
            return;
        }

        await RunBusyAsync("Merging...", ct =>
        {
            ct.ThrowIfCancellationRequested();
            var paths = EnsureProjectInitialized();
            using var conn = new SqliteConnectionFactory(paths.DbPath).Open();
            new MigrationRunner().ApplyAll(conn);

            var repo = new ConversationRepository(conn);
            repo.MergeConversations(suggestion.TargetConversationId, suggestion.MergeConversationIds);
            new ConversationMaintenance(conn).BackfillConversations();
            return Task.CompletedTask;
        });

        await RefreshConversations();
        await RefreshMergeSuggestions();
    }

    private static string? TryGetSingleRecipientNumber(ConversationWithRecipients c)
    {
        if (c.Recipients.Count == 1)
        {
            return c.Recipients[0];
        }

        // SMS conversations often have a usable key like "sms:+614...".
        if (c.ConversationKey.StartsWith("sms:", StringComparison.OrdinalIgnoreCase))
        {
            var raw = c.ConversationKey[4..];
            return string.IsNullOrWhiteSpace(raw) ? null : raw;
        }

        return null;
    }

    private static string CleanPhoneToken(string raw)
    {
        // Keep digits and a single leading '+'.
        raw = raw.Trim();
        var sb = new StringBuilder(raw.Length);
        for (var i = 0; i < raw.Length; i++)
        {
            var ch = raw[i];
            if (char.IsDigit(ch))
            {
                sb.Append(ch);
                continue;
            }

            if (ch == '+' && sb.Length == 0)
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    private static string? TryGetAustralianLocalCanonical(string cleaned)
    {
        // We only suggest merges for AU numbers in the +61 <-> 0 format.
        // Note: our importer normalizes digits to "+<digits>", so local numbers often look like "+0...".
        // Legacy compatibility: earlier builds applied a NANP heuristic to 10-digit numbers and would turn "0..." into "+10...".
        // Convert that back into "+0..." so we can still suggest merges.
        if (cleaned.StartsWith("+1", StringComparison.Ordinal) && cleaned.Length > 2 && cleaned[2] == '0')
        {
            cleaned = "+" + cleaned[2..];
        }

        if (cleaned.StartsWith("10", StringComparison.Ordinal) && cleaned.Length > 2 && cleaned[2] == '0')
        {
            cleaned = cleaned[1..];
        }

        if (cleaned.StartsWith("0061", StringComparison.Ordinal) && cleaned.Length > 4)
        {
            cleaned = "+61" + cleaned[4..];
        }

        if (cleaned.StartsWith("61", StringComparison.Ordinal) && cleaned.Length > 2)
        {
            cleaned = "+" + cleaned;
        }

        if (cleaned.StartsWith("+0", StringComparison.Ordinal) && cleaned.Length > 2)
        {
            cleaned = cleaned[1..]; // drop '+' so it becomes "0..."
        }

        if (cleaned.StartsWith("+61", StringComparison.Ordinal) && cleaned.Length > 3)
        {
            return "0" + cleaned[3..];
        }

        if (cleaned.StartsWith("0", StringComparison.Ordinal) && cleaned.Length > 1)
        {
            return cleaned;
        }

        return null;
    }

    private static string FormatConversationLabel(ConversationWithRecipients c)
    {
        if (!string.IsNullOrWhiteSpace(c.DisplayName))
        {
            return c.DisplayName!;
        }

        return c.ConversationKey;
    }

    partial void OnSelectedConversationChanged(ConversationListItem? value)
    {
        OnPropertyChanged(nameof(CanMergeCheckedIntoSelected));
        _ = LoadSelectedConversationMessages();
    }

    [RelayCommand]
    private async Task LoadSelectedConversationMessages()
    {
        if (SelectedConversation is null)
        {
            ConversationMessages.Clear();
            return;
        }

        await RunBusyAsync("Loading messages...", ct =>
        {
            var paths = EnsureProjectInitialized();
            using var conn = new SqliteConnectionFactory(paths.DbPath).Open();
            new MigrationRunner().ApplyAll(conn);
            var repo = new ConversationRepository(conn);

            var rows = repo.GetMessages(SelectedConversation.ConversationId).ToList();
            ConversationMessages.Clear();
            foreach (var r in rows)
            {
                ConversationMessages.Add(new MessageListItem(r.MessageId, r.ConversationId, r.Transport, checked((int)r.Box), r.DateMs, r.AddressRaw, r.SmsBody, r.MmsSubject));
            }

            ConversationMessageCount = ConversationMessages.Count;

            return Task.CompletedTask;
        });
    }

    [RelayCommand]
    private async Task SearchMessages()
    {
        var q = MessageSearch?.Trim() ?? string.Empty;
        if (q.Length == 0)
        {
            SearchResults.Clear();
            SearchResultCount = 0;
            return;
        }

        await RunBusyAsync("Searching...", ct =>
        {
            var paths = EnsureProjectInitialized();
            using var conn = new SqliteConnectionFactory(paths.DbPath).Open();
            new MigrationRunner().ApplyAll(conn);
            var repo = new ConversationRepository(conn);

            var rows = repo.SearchMessages(q).ToList();
            SearchResults.Clear();
            foreach (var r in rows)
            {
                SearchResults.Add(new MessageListItem(r.MessageId, r.ConversationId, r.Transport, checked((int)r.Box), r.DateMs, r.AddressRaw, r.SmsBody, r.MmsSubject));
            }

            SearchResultCount = SearchResults.Count;

            return Task.CompletedTask;
        });
    }

    [RelayCommand]
    private void ClearConversationSearch()
    {
        ConversationSearch = string.Empty;
    }

    [RelayCommand]
    private void ClearMessageSearch()
    {
        MessageSearch = string.Empty;
        SearchResults.Clear();
        SearchResultCount = 0;
    }

    [RelayCommand]
    private void CheckAllConversations()
    {
        foreach (var c in Conversations)
        {
            c.IsChecked = true;
        }
        OnPropertyChanged(nameof(CanMergeCheckedIntoSelected));
    }

    [RelayCommand]
    private void UncheckAllConversations()
    {
        foreach (var c in Conversations)
        {
            c.IsChecked = false;
        }
        OnPropertyChanged(nameof(CanMergeCheckedIntoSelected));
    }

    private void ConversationItemOnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ConversationListItem.IsChecked))
        {
            OnPropertyChanged(nameof(CanMergeCheckedIntoSelected));
        }
    }

    partial void OnSelectedSearchResultChanged(MessageListItem? value)
    {
        if (value?.ConversationId is null)
        {
            return;
        }

        _ = EnsureConversationSelectedAsync(value.ConversationId.Value);
    }

    private async Task EnsureConversationSelectedAsync(long conversationId)
    {
        var match = Conversations.FirstOrDefault(c => c.ConversationId == conversationId);
        if (match is not null)
        {
            SelectedConversation = match;
            return;
        }

        await RefreshConversations();
        match = Conversations.FirstOrDefault(c => c.ConversationId == conversationId);
        if (match is not null)
        {
            SelectedConversation = match;
        }
    }

    [RelayCommand]
    private async Task MergeCheckedConversationsIntoSelected()
    {
        if (SelectedConversation is null)
        {
            AppendLog("Select a target conversation first.");
            return;
        }

        var mergeIds = Conversations
            .Where(c => c.IsChecked && c.ConversationId != SelectedConversation.ConversationId)
            .Select(c => c.ConversationId)
            .ToList();

        if (mergeIds.Count == 0)
        {
            AppendLog("No conversations checked to merge.");
            return;
        }

        await RunBusyAsync("Merging conversations...", ct =>
        {
            var paths = EnsureProjectInitialized();
            using var conn = new SqliteConnectionFactory(paths.DbPath).Open();
            new MigrationRunner().ApplyAll(conn);
            var repo = new ConversationRepository(conn);
            repo.MergeConversations(SelectedConversation.ConversationId, mergeIds);
            new ConversationMaintenance(conn).BackfillConversations();
            return Task.CompletedTask;
        });

        foreach (var c in Conversations)
        {
            c.IsChecked = false;
        }

        await RefreshConversations();
    }

    private ProjectPaths EnsureProjectInitialized()
    {
        if (string.IsNullOrWhiteSpace(ProjectFolder))
        {
            throw new InvalidOperationException("Project folder is empty.");
        }

        Directory.CreateDirectory(ProjectFolder);
        var paths = new ProjectPaths(ProjectFolder);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.DbPath) ?? ProjectFolder);
        Directory.CreateDirectory(paths.MediaBlobRoot);
        Directory.CreateDirectory(paths.TempRoot);
        return paths;
    }

    private void AppendLog(string line)
    {
        var sb = new StringBuilder(LogText);
        if (sb.Length > 0)
        {
            sb.AppendLine();
        }
        sb.Append(line);
        LogText = sb.ToString();
    }

    private async Task RunBusyAsync(string busyText, Func<CancellationToken, Task> work)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        BusyText = busyText;
        try
        {
            using var cts = new CancellationTokenSource();
            await work(cts.Token);
        }
        catch (Exception ex)
        {
            AppendLog($"ERROR: {ex}");
        }
        finally
        {
            BusyText = string.Empty;
            IsBusy = false;
        }
    }

    private static string GetDefaultProjectFolder()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(root, "MsgBakManProject");
    }

    public sealed partial class ConversationListItem : ObservableObject
    {
        public ConversationListItem(long conversationId, string display, long messageCount, long? lastDateMs)
        {
            ConversationId = conversationId;
            Display = display;
            MessageCount = messageCount;
            LastDateMs = lastDateMs;
        }

        public long ConversationId { get; }
        public string Display { get; }
        public long MessageCount { get; }
        public long? LastDateMs { get; }

        public string LastWhenLocal
            => LastDateMs.HasValue
                ? DateTimeOffset.FromUnixTimeMilliseconds(LastDateMs.Value).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                : string.Empty;

        [ObservableProperty]
        private bool _isChecked;
    }

    public sealed record MessageListItem(
        long MessageId,
        long? ConversationId,
        string Transport,
        int Box,
        long DateMs,
        string? AddressRaw,
        string? SmsBody,
        string? MmsSubject
    )
    {
        public string WhenLocal
            => DateTimeOffset.FromUnixTimeMilliseconds(DateMs).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

        public string BoxLabel
            => Box switch
            {
                1 => "Inbox",
                2 => "Sent",
                3 => "Draft",
                4 => "Outbox",
                5 => "Failed",
                6 => "Queued",
                _ => Box.ToString()
            };

        public string Preview
            => !string.IsNullOrWhiteSpace(SmsBody)
                ? SmsBody!
                : (MmsSubject ?? string.Empty);
    }

    public sealed partial class MergeSuggestionItem : ObservableObject
    {
        public MergeSuggestionItem(string title, string details, long targetConversationId, IReadOnlyList<long> mergeConversationIds)
        {
            Title = title;
            Details = details;
            TargetConversationId = targetConversationId;
            MergeConversationIds = mergeConversationIds;
        }

        public string Title { get; }
        public string Details { get; }
        public long TargetConversationId { get; }
        public IReadOnlyList<long> MergeConversationIds { get; }

        [ObservableProperty]
        private bool _isMarked;
    }

    private sealed record ConversationWithRecipients(
        long ConversationId,
        string ConversationKey,
        string? DisplayName,
        long MessageCount,
        long? LastDateMs,
        IReadOnlyList<string> Recipients
    );
}
