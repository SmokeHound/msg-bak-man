using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using ModernWpf;
using MsgBakMan.App.Services;

namespace MsgBakMan.App;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private static readonly object _crashLock = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Default to Light unless user explicitly chose Dark.
        var settings = AppSettingsStore.Load();
        ThemeManager.Current.ApplicationTheme = string.Equals(settings.Theme, "Dark", StringComparison.OrdinalIgnoreCase)
            ? ApplicationTheme.Dark
            : ApplicationTheme.Light;
        ApplyAppThemeBrushes(ThemeManager.Current.ApplicationTheme ?? ApplicationTheme.Light, settings.AccentPalette);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    internal static void ApplyAppThemeBrushes(ApplicationTheme theme, string? accentPalette)
    {
        var resources = Current?.Resources;
        if (resources is null)
        {
            return;
        }

        var palette = NormalizeAccentPalette(accentPalette);
        var accent = GetAccentColors(theme, palette);

        if (theme == ApplicationTheme.Dark)
        {
            SetBrush(resources, "AppBackgroundBrush", (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF0B1220"));
            SetBrush(resources, "CardBackgroundBrush", (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF0F172A"));
            SetBrush(resources, "CardBorderBrush", (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#33FFFFFF"));
            SetBrush(resources, "MutedForegroundBrush", (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#9CA3AF"));
            SetBrush(resources, "AccentBrush", accent.Accent);
            SetBrush(resources, "AccentBrushHover", accent.Hover);
            SetBrush(resources, "AccentBrushPressed", accent.Pressed);
            SetBrush(resources, "AccentSoftBrush", accent.Soft);
            SetBrush(resources, "AccentSoftHoverBrush", accent.SoftHover);
            SetBrush(resources, "AccentSoftPressedBrush", accent.SoftPressed);
            SetBrush(resources, "GhostHoverBrush", (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#12FFFFFF"));
            SetBrush(resources, "GhostPressedBrush", (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#22FFFFFF"));
            SetEffect(resources, "CardShadowEffect", new DropShadowEffect
            {
                Color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#30000000"),
                BlurRadius = 22,
                ShadowDepth = 2,
                Opacity = 1
            });
        }
        else
        {
            SetBrush(resources, "AppBackgroundBrush", (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFF2F4F8"));
            SetBrush(resources, "CardBackgroundBrush", (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFFFFFF"));
            SetBrush(resources, "CardBorderBrush", (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#22000000"));
            SetBrush(resources, "MutedForegroundBrush", (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#6B7280"));
            SetBrush(resources, "AccentBrush", accent.Accent);
            SetBrush(resources, "AccentBrushHover", accent.Hover);
            SetBrush(resources, "AccentBrushPressed", accent.Pressed);
            SetBrush(resources, "AccentSoftBrush", accent.Soft);
            SetBrush(resources, "AccentSoftHoverBrush", accent.SoftHover);
            SetBrush(resources, "AccentSoftPressedBrush", accent.SoftPressed);
            SetBrush(resources, "GhostHoverBrush", (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0F000000"));
            SetBrush(resources, "GhostPressedBrush", (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1A000000"));
            SetEffect(resources, "CardShadowEffect", new DropShadowEffect
            {
                Color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#12000000"),
                BlurRadius = 18,
                ShadowDepth = 2,
                Opacity = 1
            });
        }
    }

    private static void SetBrush(ResourceDictionary resources, string key, System.Windows.Media.Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        resources[key] = brush;
    }

    private static void SetEffect(ResourceDictionary resources, string key, Effect effect)
    {
        if (effect is Freezable f && f.CanFreeze)
        {
            f.Freeze();
        }
        resources[key] = effect;
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

    private readonly record struct AccentColors(
        System.Windows.Media.Color Accent,
        System.Windows.Media.Color Hover,
        System.Windows.Media.Color Pressed,
        System.Windows.Media.Color Soft,
        System.Windows.Media.Color SoftHover,
        System.Windows.Media.Color SoftPressed);

    private static AccentColors GetAccentColors(ApplicationTheme theme, string palette)
    {
        // Dark theme needs brighter accents.
        return (theme, palette) switch
        {
            (ApplicationTheme.Dark, "Cyan") => new(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF22D3EE"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF06B6D4"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF0891B2"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1A22D3EE"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2422D3EE"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2F22D3EE")),
            (ApplicationTheme.Dark, "Emerald") => new(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF34D399"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF10B981"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF059669"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1A34D399"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2434D399"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2F34D399")),
            (ApplicationTheme.Dark, "Lime") => new(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFA3E635"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF84CC16"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF65A30D"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1AA3E635"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#24A3E635"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2FA3E635")),
            (ApplicationTheme.Dark, "Orange") => new(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFFB923C"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFF97316"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFEA580C"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1AFB923C"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#24FB923C"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2FFB923C")),
            (ApplicationTheme.Dark, "Red") => new(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFF87171"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFEF4444"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFDC2626"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1AF87171"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#24F87171"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2FF87171")),
            (ApplicationTheme.Dark, "Slate") => new(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFCBD5E1"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF94A3B8"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF64748B"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1ACBD5E1"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#24CBD5E1"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2FCBD5E1")),
            (ApplicationTheme.Dark, "Violet") => new(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFA78BFA"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF8B5CF6"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF7C3AED"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1AA78BFA"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#24A78BFA"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2FA78BFA")),
            (ApplicationTheme.Dark, _) => new(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF60A5FA"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF3B82F6"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF2563EB"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1A60A5FA"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2460A5FA"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2F60A5FA")),

            (ApplicationTheme.Light, "Cyan") => new(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF06B6D4"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF0891B2"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF0E7490"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1406B6D4"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1F06B6D4"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2A06B6D4")),
            (ApplicationTheme.Light, "Emerald") => new(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF10B981"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF059669"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF047857"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1410B981"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1F10B981"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2A10B981")),
            (ApplicationTheme.Light, "Lime") => new(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF84CC16"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF65A30D"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF4D7C0F"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1484CC16"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1F84CC16"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2A84CC16")),
            (ApplicationTheme.Light, "Orange") => new(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFF97316"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFEA580C"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFC2410C"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#14F97316"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1FF97316"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2AF97316")),
            (ApplicationTheme.Light, "Red") => new(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFEF4444"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFDC2626"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFB91C1C"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#14EF4444"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1FEF4444"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2AEF4444")),
            (ApplicationTheme.Light, "Slate") => new(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF334155"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF1F2937"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF111827"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#14334155"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1F334155"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2A334155")),
            (ApplicationTheme.Light, "Violet") => new(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF7C3AED"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF6D28D9"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF5B21B6"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#147C3AED"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1F7C3AED"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2A7C3AED")),
            _ => new(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF2563EB"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF1D4ED8"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF1E40AF"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#142563EB"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1F2563EB"),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2A2563EB"))
        };
    }

    private static string CrashLogPath
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MsgBakMan");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "crash.log");
        }
    }

    private static void WriteCrash(string source, Exception ex)
    {
        lock (_crashLock)
        {
            var sb = new StringBuilder();
            sb.AppendLine("==== MsgBakMan crash ====");
            sb.AppendLine($"UTC: {DateTimeOffset.UtcNow:O}");
            sb.AppendLine($"Source: {source}");
            sb.AppendLine(ex.ToString());
            sb.AppendLine();
            File.AppendAllText(CrashLogPath, sb.ToString());
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            WriteCrash("DispatcherUnhandledException", e.Exception);
        }
        catch
        {
            // ignored
        }

        System.Windows.MessageBox.Show(
            $"An unexpected error occurred. Details were saved to:\n{CrashLogPath}\n\n{e.Exception.Message}",
            "MsgBakMan Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        // Prevent hard-crash so the user can keep using the app.
        e.Handled = true;
    }

    private static void OnCurrentDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            try { WriteCrash("AppDomain.UnhandledException", ex); } catch { }
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        try { WriteCrash("TaskScheduler.UnobservedTaskException", e.Exception); } catch { }
        e.SetObserved();
    }
}


