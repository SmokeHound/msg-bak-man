using System.Globalization;
using System.Diagnostics;
using System.Text.Json;
using System.Xml;
using MsgBakMan.Core.Models;
using MsgBakMan.Core.Normalization;
using MsgBakMan.Data.Project;
using MsgBakMan.Data.Repositories;
using MsgBakMan.ImportExport.Media;

namespace MsgBakMan.ImportExport.Import;

public sealed class SmsBackupRestoreXmlImporter
{
    private readonly ImportRepository _repo;
    private readonly MediaStore _media;

    public SmsBackupRestoreXmlImporter(ImportRepository repo, ProjectPaths paths)
    {
        _repo = repo;
        _media = new MediaStore(paths);
    }

    public async Task ImportAsync(string xmlPath, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var fi = new FileInfo(xmlPath);
        using var session = _repo.BeginImportSession();
        var sourceId = _repo.UpsertSource(fi.FullName, fi.Length, null, session.Transaction);

        progress?.Report($"Importing {fi.Name}...");

        var stopwatch = Stopwatch.StartNew();
        long lastReportMs = 0;
        long smsCount = 0;
        long mmsCount = 0;

        var settings = new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Ignore,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = true
        };

        await using var stream = new FileStream(xmlPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 64, useAsync: true);
        using var hashingStream = new Sha256HashingReadStream(stream);
        using var reader = XmlReader.Create(hashingStream, settings);

        while (await reader.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (reader.Name.Equals("sms", StringComparison.OrdinalIgnoreCase))
            {
                var sms = ReadSms(reader);
                _repo.UpsertSms(sourceId, sms, session.Transaction);
                smsCount++;
            }
            else if (reader.Name.Equals("mms", StringComparison.OrdinalIgnoreCase))
            {
                var mms = await ReadMmsAsync(reader, cancellationToken);
                _repo.UpsertMms(sourceId, mms, session.Transaction);
                mmsCount++;
            }

            if (progress is not null)
            {
                var elapsedMs = stopwatch.ElapsedMilliseconds;
                if (elapsedMs - lastReportMs >= 500)
                {
                    lastReportMs = elapsedMs;
                    var total = smsCount + mmsCount;
                    var rate = elapsedMs > 0 ? (total * 1000.0 / elapsedMs) : 0;

                    string pctText = string.Empty;
                    if (fi.Length > 0)
                    {
                        var pct = Math.Clamp(stream.Position / (double)fi.Length, 0.0, 1.0) * 100.0;
                        pctText = $" • {pct:0}%";
                    }

                    progress.Report($"Importing {fi.Name}... {total:n0} msgs ({smsCount:n0} SMS, {mmsCount:n0} MMS) • {rate:0} msg/s{pctText}");
                }
            }
        }

        var sha = hashingStream.GetHashHexLower();
        _repo.UpdateSourceSha256(sourceId, sha, session.Transaction);
        session.Commit();

        progress?.Report($"Imported {fi.Name}");
    }

    private SmsMessage ReadSms(XmlReader reader)
    {
        var rawJson = CaptureAttributesJson(reader);
        var date = ReadLongAttr(reader, "date") ?? 0;
        var dateSent = ReadLongAttr(reader, "date_sent");
        var type = ReadIntAttr(reader, "type") ?? 0;
        var addressRaw = ReadStrAttr(reader, "address");
        var addressNorm = PhoneNormalization.NormalizeAddress(addressRaw);
        var body = ReadStrAttr(reader, "body") ?? string.Empty;
        var protocol = ReadIntAttr(reader, "protocol");
        var subject = ReadStrAttr(reader, "subject");
        var serviceCenter = ReadStrAttr(reader, "service_center");
        var read = ReadIntAttr(reader, "read");
        var status = ReadIntAttr(reader, "status");
        var locked = ReadIntAttr(reader, "locked");

        return new SmsMessage(
            DateMs: date,
            DateSentMs: dateSent,
            Type: type,
            AddressRaw: addressRaw,
            AddressNorm: addressNorm,
            Body: body,
            Protocol: protocol,
            Subject: subject,
            ServiceCenter: serviceCenter,
            Read: read,
            Status: status,
            Locked: locked,
            RawAttributesJson: rawJson);
    }

    private async Task<MmsMessage> ReadMmsAsync(XmlReader reader, CancellationToken cancellationToken)
    {
        var rawJson = CaptureAttributesJson(reader);
        var date = ReadLongAttr(reader, "date") ?? 0;
        var dateSent = ReadLongAttr(reader, "date_sent");
        var msgBox = ReadIntAttr(reader, "msg_box") ?? 0;
        var addressRaw = ReadStrAttr(reader, "address");
        var addressNorm = PhoneNormalization.NormalizeAddress(addressRaw);
        var mId = ReadStrAttr(reader, "m_id");
        var ctT = ReadStrAttr(reader, "ct_t");
        var sub = ReadStrAttr(reader, "sub");
        var textOnly = ReadIntAttr(reader, "text_only");
        var locked = ReadIntAttr(reader, "locked");
        var read = ReadIntAttr(reader, "read");
        var seen = ReadIntAttr(reader, "seen");

        var addresses = new List<MmsAddress>();
        var parts = new List<MmsPart>();

        if (reader.IsEmptyElement)
        {
            return new MmsMessage(date, dateSent, msgBox, addressRaw, addressNorm, mId, ctT, sub, textOnly, locked, read, seen, addresses, parts, rawJson);
        }

        var startDepth = reader.Depth;
        while (await reader.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == startDepth && reader.Name.Equals("mms", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (reader.Name.Equals("addr", StringComparison.OrdinalIgnoreCase))
            {
                var addrRawJson = CaptureAttributesJson(reader);
                var addrRaw = ReadStrAttr(reader, "address");
                addresses.Add(new MmsAddress(
                    Type: ReadIntAttr(reader, "type"),
                    AddressRaw: addrRaw,
                    AddressNorm: PhoneNormalization.NormalizeAddress(addrRaw),
                    Charset: ReadIntAttr(reader, "charset"),
                    RawAttributesJson: addrRawJson));
            }
            else if (reader.Name.Equals("part", StringComparison.OrdinalIgnoreCase))
            {
                var part = await ReadPartAsync(reader, cancellationToken);
                parts.Add(part);
            }
        }

        return new MmsMessage(date, dateSent, msgBox, addressRaw, addressNorm, mId, ctT, sub, textOnly, locked, read, seen, addresses, parts, rawJson);
    }

    private async Task<MmsPart> ReadPartAsync(XmlReader reader, CancellationToken cancellationToken)
    {
        var rawJson = CaptureAttributesJson(reader);
        var seq = ReadIntAttr(reader, "seq");
        var ct = ReadStrAttr(reader, "ct");
        var name = ReadStrAttr(reader, "name");
        var chset = ReadStrAttr(reader, "chset");
        var cd = ReadStrAttr(reader, "cd");
        var fn = ReadStrAttr(reader, "fn");
        var cid = ReadStrAttr(reader, "cid");
        var cl = ReadStrAttr(reader, "cl");
        var text = ReadStrAttr(reader, "text");

        MediaBlobRef? blob = null;
        if (!string.IsNullOrWhiteSpace(ct) && !ct.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            // Binary content usually comes in the "data" attribute.
            blob = await _media.IngestBase64AttributeAsync(reader, "data", ct, cancellationToken);
        }

        // Restore reader position back on element.
        reader.MoveToElement();
        await Task.CompletedTask;

        return new MmsPart(
            Seq: seq,
            ContentType: ct,
            Name: name,
            Charset: chset,
            ContentDisposition: cd,
            FileName: fn,
            ContentId: cid,
            ContentLocation: cl,
            Text: text,
            Blob: blob,
            RawAttributesJson: rawJson);
    }

    private static string CaptureAttributesJson(XmlReader reader)
    {
        if (!reader.HasAttributes)
        {
            return "{}";
        }

        var dict = new Dictionary<string, string?>(StringComparer.Ordinal);
        for (var i = 0; i < reader.AttributeCount; i++)
        {
            reader.MoveToAttribute(i);
            dict[reader.Name] = reader.Value;
        }
        reader.MoveToElement();
        return JsonSerializer.Serialize(dict);
    }

    private static string? ReadStrAttr(XmlReader reader, string name) => reader.GetAttribute(name);

    private static int? ReadIntAttr(XmlReader reader, string name)
    {
        var s = reader.GetAttribute(name);
        if (string.IsNullOrWhiteSpace(s) || s.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static long? ReadLongAttr(XmlReader reader, string name)
    {
        var s = reader.GetAttribute(name);
        if (string.IsNullOrWhiteSpace(s) || s.Equals("null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        return long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private sealed class Sha256HashingReadStream : Stream
    {
        private readonly Stream _inner;
        private readonly System.Security.Cryptography.IncrementalHash _hash;
        private bool _finalized;

        public Sha256HashingReadStream(Stream inner)
        {
            _inner = inner;
            _hash = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        }

        public string GetHashHexLower()
        {
            if (_finalized)
            {
                throw new InvalidOperationException("Hash already finalized.");
            }

            _finalized = true;
            var bytes = _hash.GetHashAndReset();
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = _inner.Read(buffer, offset, count);
            if (read > 0)
            {
                _hash.AppendData(buffer.AsSpan(offset, read));
            }
            return read;
        }

        public override int Read(Span<byte> buffer)
        {
            var read = _inner.Read(buffer);
            if (read > 0)
            {
                _hash.AppendData(buffer[..read]);
            }
            return read;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var read = await _inner.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            if (read > 0)
            {
                _hash.AppendData(buffer.AsSpan(offset, read));
            }
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var read = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read > 0)
            {
                _hash.AppendData(buffer.Span[..read]);
            }
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _hash.Dispose();
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            _hash.Dispose();
            await base.DisposeAsync().ConfigureAwait(false);
        }
    }
}
