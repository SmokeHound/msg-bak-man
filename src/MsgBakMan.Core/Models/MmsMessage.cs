namespace MsgBakMan.Core.Models;

public sealed record MmsMessage(
    long DateMs,
    long? DateSentMs,
    int MsgBox,
    string? AddressRaw,
    string? AddressNorm,
    string? MId,
    string? CtT,
    string? Subject,
    int? TextOnly,
    int? Locked,
    int? Read,
    int? Seen,
    IReadOnlyList<MmsAddress> Addresses,
    IReadOnlyList<MmsPart> Parts,
    string RawAttributesJson
);
