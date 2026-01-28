using Dapper;
using Microsoft.Data.Sqlite;
using MsgBakMan.Core.Fingerprinting;
using MsgBakMan.Core.Models;

namespace MsgBakMan.Data.Repositories;

public sealed class ImportRepository
{
    private readonly SqliteConnection _conn;

    public ImportRepository(SqliteConnection conn)
    {
        _conn = conn;
    }

    public long UpsertSource(string path, long fileSize, string? fileSha256)
    {
        return _conn.ExecuteScalar<long>(@"
INSERT INTO sources(path, imported_utc, file_size, file_sha256)
VALUES (@path, @t, @size, @sha)
RETURNING source_id;
", new
        {
            path,
            t = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            size = fileSize,
            sha = fileSha256
        });
    }

    public long UpsertSms(long sourceId, SmsMessage sms)
    {
        var fp = MessageFingerprint.ForSms(sms);
        var fpv = MessageFingerprint.Version;

        using var tx = _conn.BeginTransaction();

        var messageId = _conn.ExecuteScalar<long>(@"
INSERT INTO message(transport, box, date_ms, date_sent_ms, read, seen, source_id, fingerprint_version, fingerprint)
VALUES ('sms', @box, @date_ms, @date_sent_ms, @read, NULL, @source_id, @fpv, @fp)
ON CONFLICT(transport, fingerprint_version, fingerprint)
DO UPDATE SET
  date_sent_ms = COALESCE(message.date_sent_ms, excluded.date_sent_ms),
  read = COALESCE(message.read, excluded.read)
RETURNING message_id;
", new
        {
            box = sms.Type,
            date_ms = sms.DateMs,
            date_sent_ms = sms.DateSentMs,
            read = sms.Read,
            source_id = sourceId,
            fpv,
            fp
        }, tx);

        _conn.Execute(@"
INSERT INTO sms(message_id, address_raw, address_norm, protocol, subject, body, service_center, read, status, locked, raw_attrs_json)
VALUES (@message_id, @address_raw, @address_norm, @protocol, @subject, @body, @service_center, @read, @status, @locked, @raw)
ON CONFLICT(message_id) DO UPDATE SET
  address_raw = COALESCE(sms.address_raw, excluded.address_raw),
  address_norm = COALESCE(sms.address_norm, excluded.address_norm),
  protocol = COALESCE(sms.protocol, excluded.protocol),
  subject = COALESCE(sms.subject, excluded.subject),
  body = COALESCE(sms.body, excluded.body),
  service_center = COALESCE(sms.service_center, excluded.service_center),
  read = COALESCE(sms.read, excluded.read),
  status = COALESCE(sms.status, excluded.status),
    locked = COALESCE(sms.locked, excluded.locked),
    raw_attrs_json = COALESCE(sms.raw_attrs_json, excluded.raw_attrs_json);
", new
        {
            message_id = messageId,
            address_raw = sms.AddressRaw,
            address_norm = sms.AddressNorm,
            protocol = sms.Protocol,
            subject = sms.Subject,
            body = sms.Body,
            service_center = sms.ServiceCenter,
            read = sms.Read,
            status = sms.Status,
                        locked = sms.Locked,
                        raw = sms.RawAttributesJson
        }, tx);

        tx.Commit();
        return messageId;
    }

    public long UpsertMediaBlob(MediaBlobRef blob)
    {
        return _conn.ExecuteScalar<long>(@"
INSERT INTO media_blob(sha256, size_bytes, mime_type, extension, rel_path)
VALUES (@sha, @size, @mime, @ext, @path)
ON CONFLICT(sha256) DO UPDATE SET
  size_bytes = COALESCE(media_blob.size_bytes, excluded.size_bytes),
  mime_type = COALESCE(media_blob.mime_type, excluded.mime_type),
  extension = COALESCE(media_blob.extension, excluded.extension),
    rel_path = excluded.rel_path
RETURNING 1;
", new
        {
            sha = blob.Sha256Hex,
            size = blob.SizeBytes,
            mime = blob.MimeType,
            ext = blob.Extension,
            path = blob.RelativePath
        });
    }

    public long UpsertMms(long sourceId, MmsMessage mms)
    {
        var fp = MessageFingerprint.ForMms(mms);
        var fpv = MessageFingerprint.Version;

        using var tx = _conn.BeginTransaction();

        var messageId = _conn.ExecuteScalar<long>(@"
INSERT INTO message(transport, box, date_ms, date_sent_ms, read, seen, source_id, fingerprint_version, fingerprint)
VALUES ('mms', @box, @date_ms, @date_sent_ms, @read, @seen, @source_id, @fpv, @fp)
ON CONFLICT(transport, fingerprint_version, fingerprint)
DO UPDATE SET
  date_sent_ms = COALESCE(message.date_sent_ms, excluded.date_sent_ms),
  read = COALESCE(message.read, excluded.read),
  seen = COALESCE(message.seen, excluded.seen)
RETURNING message_id;
", new
        {
            box = mms.MsgBox,
            date_ms = mms.DateMs,
            date_sent_ms = mms.DateSentMs,
            read = mms.Read,
            seen = mms.Seen,
            source_id = sourceId,
            fpv,
            fp
        }, tx);

        _conn.Execute(@"
INSERT INTO mms(message_id, address_raw, address_norm, m_id, ct_t, sub, text_only, locked, raw_attrs_json)
VALUES (@message_id, @address_raw, @address_norm, @m_id, @ct_t, @sub, @text_only, @locked, @raw)
ON CONFLICT(message_id) DO UPDATE SET
    address_raw = COALESCE(mms.address_raw, excluded.address_raw),
    address_norm = COALESCE(mms.address_norm, excluded.address_norm),
  m_id = COALESCE(mms.m_id, excluded.m_id),
  ct_t = COALESCE(mms.ct_t, excluded.ct_t),
  sub = COALESCE(mms.sub, excluded.sub),
  text_only = COALESCE(mms.text_only, excluded.text_only),
    locked = COALESCE(mms.locked, excluded.locked),
    raw_attrs_json = COALESCE(mms.raw_attrs_json, excluded.raw_attrs_json);
", new
        {
            message_id = messageId,
                        address_raw = mms.AddressRaw,
                        address_norm = mms.AddressNorm,
            m_id = mms.MId,
            ct_t = mms.CtT,
            sub = mms.Subject,
            text_only = mms.TextOnly,
                        locked = mms.Locked,
                        raw = mms.RawAttributesJson
        }, tx);

        foreach (var addr in mms.Addresses)
        {
            _conn.Execute(@"
INSERT OR IGNORE INTO mms_addr(message_id, type, address_raw, address_norm, charset, raw_attrs_json)
VALUES (@message_id, @type, @address_raw, @address_norm, @charset, @raw);
", new
            {
                message_id = messageId,
                type = addr.Type,
                address_raw = addr.AddressRaw,
                address_norm = addr.AddressNorm,
                charset = addr.Charset,
                raw = addr.RawAttributesJson
            }, tx);
        }

        foreach (var part in mms.Parts)
        {
            var pf = MessageFingerprint.PartFingerprint(part);
            if (part.Blob is not null)
            {
                UpsertMediaBlob(part.Blob);
            }

            _conn.Execute(@"
INSERT OR IGNORE INTO mms_part(
    message_id, seq, content_type, name, chset, cd, fn, cid, cl, text, data_sha256, data_size, part_fingerprint, raw_attrs_json
)
VALUES (
    @message_id, @seq, @content_type, @name, @chset, @cd, @fn, @cid, @cl, @text, @data_sha256, @data_size, @pf, @raw
);
", new
            {
                message_id = messageId,
                seq = part.Seq,
                content_type = part.ContentType,
                name = part.Name,
                chset = part.Charset,
                cd = part.ContentDisposition,
                fn = part.FileName,
                cid = part.ContentId,
                cl = part.ContentLocation,
                text = part.Text,
                data_sha256 = part.Blob?.Sha256Hex,
                data_size = part.Blob?.SizeBytes,
                                pf,
                                raw = part.RawAttributesJson
            }, tx);
        }

        tx.Commit();
        return messageId;
    }
}
