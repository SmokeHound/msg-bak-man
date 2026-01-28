namespace MsgBakMan.Core.Models;

public sealed record MmsPart(
    int? Seq,
    string? ContentType,
    string? Name,
    string? Charset,
    string? ContentDisposition,
    string? FileName,
    string? ContentId,
    string? ContentLocation,
    string? Text,
    MediaBlobRef? Blob,
    string RawAttributesJson
);
