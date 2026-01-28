using System.Security.Cryptography;
using System.Text;

namespace MsgBakMan.Core.Util;

public static class Hashing
{
    public static string Sha256Hex(ReadOnlySpan<byte> bytes)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(bytes, hash);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string Sha256Hex(string text)
    {
        return Sha256Hex(Encoding.UTF8.GetBytes(text));
    }
}
