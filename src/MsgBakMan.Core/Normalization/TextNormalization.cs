namespace MsgBakMan.Core.Normalization;

public static class TextNormalization
{
    public static string NormalizeBody(string? body)
    {
        if (string.IsNullOrEmpty(body) || body.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        // Normalize line endings and trim only trailing whitespace (keep leading spaces as message content).
        return body.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .TrimEnd();
    }

    public static string? NormalizeNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return value.Trim();
    }
}
