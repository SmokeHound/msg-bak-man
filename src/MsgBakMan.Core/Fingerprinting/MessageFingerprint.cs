using System.Text;
using MsgBakMan.Core.Models;
using MsgBakMan.Core.Normalization;
using MsgBakMan.Core.Util;

namespace MsgBakMan.Core.Fingerprinting;

public static class MessageFingerprint
{
    public const int Version = 2;

    public static long BucketTimeMs(long dateMs, int bucketSizeMs = 2000)
    {
        if (bucketSizeMs <= 0)
        {
            return dateMs;
        }

        return (dateMs / bucketSizeMs) * bucketSizeMs;
    }

    public static string ForSms(SmsMessage sms)
    {
        var dateBucket = BucketTimeMs(sms.DateMs);
        var addr = sms.AddressNorm ?? PhoneNormalization.NormalizeAddress(sms.AddressRaw) ?? "";
        var body = TextNormalization.NormalizeBody(sms.Body);

        // Delimiter chosen to avoid collisions with typical message content.
        var payload = string.Join("\u001f", new[]
        {
            "sms",
            sms.Type.ToString(),
            dateBucket.ToString(),
            (sms.DateSentMs is null ? "" : BucketTimeMs(sms.DateSentMs.Value).ToString()),
            addr,
            body,
        });

        return Hashing.Sha256Hex(payload);
    }

    public static string ForMms(MmsMessage mms)
    {
        var dateBucket = BucketTimeMs(mms.DateMs);

        var topAddr = mms.AddressNorm ?? PhoneNormalization.NormalizeAddress(mms.AddressRaw) ?? "";

        var addrTokens = mms.Addresses
            .Where(a => a.AddressNorm is not null)
            .Select(a => $"{a.Type ?? 0}:{a.AddressNorm}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        var partTokens = mms.Parts
            .Select(PartToken)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var payload = new StringBuilder();
        payload.Append("mms\u001f");
        payload.Append(mms.MsgBox).Append('\u001f');
        payload.Append(dateBucket).Append('\u001f');
        payload.Append(topAddr).Append('\u001f');
        payload.Append(TextNormalization.NormalizeNullable(mms.MId) ?? "").Append('\u001f');
        payload.Append(TextNormalization.NormalizeNullable(mms.CtT) ?? "").Append('\u001f');
        payload.Append(TextNormalization.NormalizeBody(mms.Subject)).Append('\u001f');

        foreach (var a in addrTokens)
        {
            payload.Append(a).Append('\u001e');
        }
        payload.Append('\u001f');

        foreach (var p in partTokens)
        {
            payload.Append(p).Append('\u001e');
        }

        return Hashing.Sha256Hex(payload.ToString());
    }

    public static string PartFingerprint(MmsPart part)
    {
        return Hashing.Sha256Hex(PartToken(part));
    }

    private static string PartToken(MmsPart part)
    {
        var ct = TextNormalization.NormalizeNullable(part.ContentType) ?? "";
        var text = TextNormalization.NormalizeBody(part.Text);
        var blobKey = part.Blob?.Sha256Hex ?? "";
        var name = TextNormalization.NormalizeNullable(part.Name) ?? "";
        var fn = TextNormalization.NormalizeNullable(part.FileName) ?? "";

        return string.Join("\u001f", new[]
        {
            ct,
            blobKey,
            text,
            name,
            fn,
            part.Seq?.ToString() ?? "",
        });
    }
}
