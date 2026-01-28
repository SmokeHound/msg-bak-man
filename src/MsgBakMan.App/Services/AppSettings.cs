namespace MsgBakMan.App.Services;

public sealed record AppSettings
{
    public string Theme { get; init; } = "Light";
    public string AccentPalette { get; init; } = "Blue";

    public static AppSettings Default { get; } = new();

    public AppSettings()
    {
    }

    public AppSettings(string theme, string accentPalette)
    {
        Theme = theme;
        AccentPalette = accentPalette;
    }
}
