namespace MsgBakMan.Core.Models;

public sealed record SmsMessage(
    long DateMs,
    long? DateSentMs,
    int Type,
    string? AddressRaw,
    string? AddressNorm,
    string Body,
    int? Protocol,
    string? Subject,
    string? ServiceCenter,
    int? Read,
    int? Status,
    int? Locked,
    string RawAttributesJson
);
