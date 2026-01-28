namespace MsgBakMan.Core.Models;

public sealed record MediaBlobRef(
    string Sha256Hex,
    long SizeBytes,
    string? MimeType,
    string? Extension,
    string RelativePath
);
