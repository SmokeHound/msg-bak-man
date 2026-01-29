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

namespace MsgBakMan.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly List<ConversationListItem> _conversationItemsWithHandlers = new();

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

    public ObservableCollection<ConversationListItem> Conversations { get; } = new();

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
}
