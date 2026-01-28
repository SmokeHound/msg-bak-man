namespace MsgBakMan.Core.Models;

public sealed record MmsAddress(
    int? Type,
    string? AddressRaw,
    string? AddressNorm,
    int? Charset,
    string RawAttributesJson
);
