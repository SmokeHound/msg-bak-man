namespace MsgBakMan.App.Services;

public sealed record AppSettings
{
    public string Theme { get; init; } = "Light";
    public string AccentPalette { get; init; } = "Blue";
    public bool AllowMaintenanceActions { get; init; } = false;

    public static AppSettings Default { get; } = new();

    public AppSettings()
    {
    }

    public AppSettings(string theme, string accentPalette, bool allowMaintenanceActions = false)
    {
        Theme = theme;
        AccentPalette = accentPalette;
        AllowMaintenanceActions = allowMaintenanceActions;
    }
}
