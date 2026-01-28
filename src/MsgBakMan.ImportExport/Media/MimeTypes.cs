namespace MsgBakMan.ImportExport.Media;

public static class MimeTypes
{
    private static readonly Dictionary<string, string> ExtensionByMime = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/jpg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/gif"] = ".gif",
        ["image/webp"] = ".webp",
        ["video/mp4"] = ".mp4",
        ["video/3gpp"] = ".3gp",
        ["audio/amr"] = ".amr",
        ["audio/mpeg"] = ".mp3",
        ["audio/mp4"] = ".m4a",
        ["application/pdf"] = ".pdf",
        ["text/plain"] = ".txt",
    };

    public static string? TryGetExtension(string? mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
        {
            return null;
        }

        var clean = mimeType.Trim();
        if (ExtensionByMime.TryGetValue(clean, out var ext))
        {
            return ext;
        }

        // Handle common MIME params like "text/plain; charset=utf-8".
        var semi = clean.IndexOf(';');
        if (semi > 0)
        {
            clean = clean[..semi].Trim();
            if (ExtensionByMime.TryGetValue(clean, out ext))
            {
                return ext;
            }
        }

        return null;
    }
}
