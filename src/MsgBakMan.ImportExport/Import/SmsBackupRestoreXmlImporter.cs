using System.Globalization;
using System.Security.Cryptography;
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
        var sha = ComputeFileSha256(xmlPath);
        var sourceId = _repo.UpsertSource(fi.FullName, fi.Length, sha);

        progress?.Report($"Importing {fi.Name}...");

        var settings = new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Ignore,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = true
        };

        await using var stream = new FileStream(xmlPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 64, useAsync: true);
        using var reader = XmlReader.Create(stream, settings);

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
                _repo.UpsertSms(sourceId, sms);
            }
            else if (reader.Name.Equals("mms", StringComparison.OrdinalIgnoreCase))
            {
                var mms = await ReadMmsAsync(reader, cancellationToken);
                _repo.UpsertMms(sourceId, mms);
            }
        }

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

    private static string ComputeFileSha256(string path)
    {
        using var fs = File.OpenRead(path);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(fs);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
