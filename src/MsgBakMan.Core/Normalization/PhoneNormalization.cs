using System.Text;

namespace MsgBakMan.Core.Normalization;

public static class PhoneNormalization
{
    public static string? NormalizeAddress(string? addressRaw)
    {
        if (string.IsNullOrWhiteSpace(addressRaw))
        {
            return null;
        }

        var trimmed = addressRaw.Trim();
        if (trimmed.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Emails appear in some MMS backups.
        if (trimmed.Contains('@'))
        {
            return trimmed.ToLowerInvariant();
        }

        var sb = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            if (char.IsDigit(ch))
            {
                sb.Append(ch);
            }
        }

        if (sb.Length == 0)
        {
            return null;
        }

        // Heuristic: if it looks like NANP without country code, prefix 1.
        // Guard: many locales have 10-digit numbers that begin with 0 (e.g., AU mobiles). Those should NOT get a leading 1.
        // This is intentionally tolerant; we only use it for dedupe keys.
        if (sb.Length == 10 && sb[0] != '0')
        {
            sb.Insert(0, '1');
        }

        return "+" + sb;
    }
}
