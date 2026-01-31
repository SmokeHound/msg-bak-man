using Dapper;
using Microsoft.Data.Sqlite;

namespace MsgBakMan.Data.Repositories;

public sealed class ExportRepository
{
    private readonly SqliteConnection _conn;

    public ExportRepository(SqliteConnection conn)
    {
        _conn = conn;
    }

    public int GetTotalMessageCount()
    {
        return _conn.ExecuteScalar<int>("SELECT COUNT(*) FROM message");
    }

    public IEnumerable<SmsRow> GetAllSms()
    {
        return _conn.Query<SmsRow>(@"
SELECT m.message_id AS MessageId,
       m.box AS Type,
       m.date_ms AS DateMs,
       m.date_sent_ms AS DateSentMs,
       s.protocol AS Protocol,
       s.address_raw AS AddressRaw,
       s.address_norm AS AddressNorm,
       s.subject AS Subject,
       s.body AS Body,
       s.service_center AS ServiceCenter,
       s.read AS Read,
       s.status AS Status,
       s.locked AS Locked,
       s.raw_attrs_json AS RawAttributesJson
FROM message m
JOIN sms s ON s.message_id = m.message_id
WHERE m.transport='sms'
ORDER BY m.date_ms, m.message_id;
");
    }

    public IEnumerable<MmsRow> GetAllMms()
    {
        return _conn.Query<MmsRow>(@"
SELECT m.message_id AS MessageId,
       m.box AS MsgBox,
       m.date_ms AS DateMs,
       m.date_sent_ms AS DateSentMs,
       m.read AS Read,
       m.seen AS Seen,
       mm.address_raw AS AddressRaw,
       mm.address_norm AS AddressNorm,
       mm.m_id AS MId,
       mm.ct_t AS CtT,
       mm.sub AS Subject,
       mm.text_only AS TextOnly,
       mm.locked AS Locked,
       mm.raw_attrs_json AS RawAttributesJson
FROM message m
JOIN mms mm ON mm.message_id = m.message_id
WHERE m.transport='mms'
ORDER BY m.date_ms, m.message_id;
");
    }

    public IEnumerable<MmsAddrRow> GetMmsAddrs(long messageId)
    {
        return _conn.Query<MmsAddrRow>(@"
SELECT type AS Type,
       address_raw AS AddressRaw,
       address_norm AS AddressNorm,
       charset AS Charset,
       raw_attrs_json AS RawAttributesJson
FROM mms_addr
WHERE message_id=@id
ORDER BY type, address_norm;
", new { id = messageId });
    }

    public IEnumerable<MmsPartRow> GetMmsParts(long messageId)
    {
        return _conn.Query<MmsPartRow>(@"
SELECT seq AS Seq,
       content_type AS ContentType,
       name AS Name,
       chset AS Charset,
       cd AS ContentDisposition,
       fn AS FileName,
       cid AS ContentId,
       cl AS ContentLocation,
       text AS Text,
       data_sha256 AS DataSha256,
       data_size AS DataSize,
       raw_attrs_json AS RawAttributesJson
FROM mms_part
WHERE message_id=@id
ORDER BY COALESCE(seq, 999999), mms_part_id;
", new { id = messageId });
    }

    public MediaBlobRow? GetMediaBlob(string sha256)
    {
        return _conn.QueryFirstOrDefault<MediaBlobRow>(@"
SELECT sha256 AS Sha256,
       size_bytes AS SizeBytes,
       mime_type AS MimeType,
       extension AS Extension,
       rel_path AS RelativePath
FROM media_blob
WHERE sha256=@sha
", new { sha = sha256 });
    }

    public IEnumerable<MediaBlobRow> GetAllMediaBlobs()
    {
        return _conn.Query<MediaBlobRow>(@"
SELECT sha256 AS Sha256,
       size_bytes AS SizeBytes,
       mime_type AS MimeType,
       extension AS Extension,
       rel_path AS RelativePath
FROM media_blob
ORDER BY sha256;
");
    }

    public IEnumerable<ConversationMediaBlobRow> GetConversationMediaBlobs()
    {
        return _conn.Query<ConversationMediaBlobRow>(@"
SELECT c.conversation_id AS ConversationId,
       c.conversation_key AS ConversationKey,
       c.display_name AS DisplayName,
       r.address_norm AS RecipientAddressNorm,
       b.sha256 AS Sha256,
       b.size_bytes AS SizeBytes,
       b.mime_type AS MimeType,
       b.extension AS Extension,
       b.rel_path AS RelativePath
FROM mms_part p
JOIN message m ON m.message_id = p.message_id
JOIN conversation c ON c.conversation_id = m.conversation_id
JOIN media_blob b ON b.sha256 = p.data_sha256
LEFT JOIN conversation_recipient cr ON cr.conversation_id = c.conversation_id
LEFT JOIN recipient r ON r.recipient_id = cr.recipient_id
WHERE p.data_sha256 IS NOT NULL
ORDER BY c.conversation_id, b.sha256;
");
    }

    public sealed record SmsRow(
        long MessageId,
        int Type,
        long DateMs,
        long? DateSentMs,
        int? Protocol,
        string? AddressRaw,
        string? AddressNorm,
        string? Subject,
        string Body,
        string? ServiceCenter,
        int? Read,
        int? Status,
        int? Locked,
        string? RawAttributesJson
    );

    public sealed record MmsRow(
        long MessageId,
        int MsgBox,
        long DateMs,
        long? DateSentMs,
        int? Read,
        int? Seen,
        string? AddressRaw,
        string? AddressNorm,
        string? MId,
        string? CtT,
        string? Subject,
        int? TextOnly,
        int? Locked,
        string? RawAttributesJson
    );

    public sealed record MmsAddrRow(
        int? Type,
        string? AddressRaw,
        string? AddressNorm,
        int? Charset,
        string? RawAttributesJson
    );

    public sealed record MmsPartRow(
        int? Seq,
        string? ContentType,
        string? Name,
        string? Charset,
        string? ContentDisposition,
        string? FileName,
        string? ContentId,
        string? ContentLocation,
        string? Text,
        string? DataSha256,
        long? DataSize,
        string? RawAttributesJson
    );

    public sealed record MediaBlobRow(
        string Sha256,
        long SizeBytes,
        string? MimeType,
        string? Extension,
        string RelativePath
    );

    public sealed record ConversationMediaBlobRow(
        long ConversationId,
        string ConversationKey,
        string? DisplayName,
        string? RecipientAddressNorm,
        string Sha256,
        long SizeBytes,
        string? MimeType,
        string? Extension,
        string RelativePath
    );
}
