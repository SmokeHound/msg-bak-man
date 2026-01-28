using System.Globalization;
using System.Text.Json;
using System.Xml;
using MsgBakMan.Data.Project;
using MsgBakMan.Data.Repositories;
using MsgBakMan.ImportExport.Media;

namespace MsgBakMan.ImportExport.Export;

public sealed class SmsBackupRestoreXmlExporter
{
    private readonly ExportRepository _repo;
    private readonly MediaStore _media;

    public SmsBackupRestoreXmlExporter(ExportRepository repo, ProjectPaths paths)
    {
        _repo = repo;
        _media = new MediaStore(paths);
    }

    public async Task ExportAsync(string outputPath, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        progress?.Report("Exporting merged XML...");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");

        var settings = new XmlWriterSettings
        {
            Indent = true,
            Encoding = System.Text.Encoding.UTF8,
            NewLineHandling = NewLineHandling.Entitize
        };

        await using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 64, useAsync: true);
        await using var writer = XmlWriter.Create(fs, settings);

        writer.WriteStartDocument();
        writer.WriteProcessingInstruction("xml-stylesheet", "type=\"text/xsl\" href=\"sms.xsl\"");

        writer.WriteStartElement("smses");
        writer.WriteAttributeString("count", _repo.GetTotalMessageCount().ToString(CultureInfo.InvariantCulture));

        foreach (var sms in _repo.GetAllSms())
        {
            cancellationToken.ThrowIfCancellationRequested();
            WriteSms(writer, sms);
        }

        foreach (var mms in _repo.GetAllMms())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WriteMmsAsync(writer, mms, cancellationToken);
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
        await fs.FlushAsync(cancellationToken);

        progress?.Report("Export complete.");
    }

    private static void WriteSms(XmlWriter w, ExportRepository.SmsRow sms)
    {
        var raw = ParseAttrs(sms.RawAttributesJson);
        var written = new HashSet<string>(StringComparer.Ordinal);

        w.WriteStartElement("sms");
        WriteAttr(w, written, "protocol", (sms.Protocol ?? TryGetInt(raw, "protocol") ?? 0).ToString(CultureInfo.InvariantCulture));
        WriteAttr(w, written, "address", sms.AddressRaw ?? TryGetString(raw, "address") ?? "");
        WriteAttr(w, written, "date", sms.DateMs.ToString(CultureInfo.InvariantCulture));
        WriteAttr(w, written, "type", sms.Type.ToString(CultureInfo.InvariantCulture));
        WriteAttr(w, written, "subject", sms.Subject ?? TryGetString(raw, "subject") ?? "null");
        WriteAttr(w, written, "body", sms.Body ?? TryGetString(raw, "body") ?? string.Empty);
        WriteAttr(w, written, "toa", TryGetString(raw, "toa") ?? "null");
        WriteAttr(w, written, "sc_toa", TryGetString(raw, "sc_toa") ?? "null");
        WriteAttr(w, written, "service_center", sms.ServiceCenter ?? TryGetString(raw, "service_center") ?? "null");
        WriteAttr(w, written, "read", (sms.Read ?? TryGetInt(raw, "read") ?? 0).ToString(CultureInfo.InvariantCulture));
        WriteAttr(w, written, "status", (sms.Status ?? TryGetInt(raw, "status") ?? -1).ToString(CultureInfo.InvariantCulture));
        WriteAttr(w, written, "locked", (sms.Locked ?? TryGetInt(raw, "locked") ?? 0).ToString(CultureInfo.InvariantCulture));
        if (sms.DateSentMs is not null)
        {
            WriteAttr(w, written, "date_sent", sms.DateSentMs.Value.ToString(CultureInfo.InvariantCulture));
        }

        WriteExtras(w, raw, written);
        w.WriteEndElement();
    }

    private async Task WriteMmsAsync(XmlWriter w, ExportRepository.MmsRow mms, CancellationToken cancellationToken)
    {
        var raw = ParseAttrs(mms.RawAttributesJson);
        var written = new HashSet<string>(StringComparer.Ordinal);

        w.WriteStartElement("mms");
        WriteAttr(w, written, "date", mms.DateMs.ToString(CultureInfo.InvariantCulture));
        WriteAttr(w, written, "msg_box", mms.MsgBox.ToString(CultureInfo.InvariantCulture));

        // This is required in SyncTech's XSD and appears in real backups.
        WriteAttr(w, written, "address", mms.AddressRaw ?? TryGetString(raw, "address") ?? "");

        if (mms.DateSentMs is not null)
        {
            WriteAttr(w, written, "date_sent", mms.DateSentMs.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrEmpty(mms.CtT)) WriteAttr(w, written, "ct_t", mms.CtT);
        if (!string.IsNullOrEmpty(mms.MId)) WriteAttr(w, written, "m_id", mms.MId);
        if (!string.IsNullOrEmpty(mms.Subject)) WriteAttr(w, written, "sub", mms.Subject);
        if (mms.TextOnly is not null) WriteAttr(w, written, "text_only", mms.TextOnly.Value.ToString(CultureInfo.InvariantCulture));
        if (mms.Locked is not null) WriteAttr(w, written, "locked", mms.Locked.Value.ToString(CultureInfo.InvariantCulture));
        if (mms.Read is not null) WriteAttr(w, written, "read", mms.Read.Value.ToString(CultureInfo.InvariantCulture));
        if (mms.Seen is not null) WriteAttr(w, written, "seen", mms.Seen.Value.ToString(CultureInfo.InvariantCulture));

        WriteExtras(w, raw, written);

        // Parts
        w.WriteStartElement("parts");
        foreach (var part in _repo.GetMmsParts(mms.MessageId))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await WritePartAsync(w, part, cancellationToken);
        }
        w.WriteEndElement();

        // Addresses
        w.WriteStartElement("addrs");
        foreach (var addr in _repo.GetMmsAddrs(mms.MessageId))
        {
            var addrRaw = ParseAttrs(addr.RawAttributesJson);
            var addrWritten = new HashSet<string>(StringComparer.Ordinal);

            w.WriteStartElement("addr");
            if (!string.IsNullOrEmpty(addr.AddressRaw)) WriteAttr(w, addrWritten, "address", addr.AddressRaw);
            else if (TryGetString(addrRaw, "address") is string av) WriteAttr(w, addrWritten, "address", av);

            if (addr.Type is not null) WriteAttr(w, addrWritten, "type", addr.Type.Value.ToString(CultureInfo.InvariantCulture));
            else if (TryGetString(addrRaw, "type") is string tv) WriteAttr(w, addrWritten, "type", tv);

            if (addr.Charset is not null) WriteAttr(w, addrWritten, "charset", addr.Charset.Value.ToString(CultureInfo.InvariantCulture));
            else if (TryGetString(addrRaw, "charset") is string cv) WriteAttr(w, addrWritten, "charset", cv);

            WriteExtras(w, addrRaw, addrWritten);
            w.WriteEndElement();
        }
        w.WriteEndElement();

        w.WriteEndElement();
    }

    private async Task WritePartAsync(XmlWriter w, ExportRepository.MmsPartRow part, CancellationToken cancellationToken)
    {
        var raw = ParseAttrs(part.RawAttributesJson);
        var written = new HashSet<string>(StringComparer.Ordinal);

        w.WriteStartElement("part");
        if (part.Seq is not null) WriteAttr(w, written, "seq", part.Seq.Value.ToString(CultureInfo.InvariantCulture));
        else if (TryGetString(raw, "seq") is string sv) WriteAttr(w, written, "seq", sv);

        if (!string.IsNullOrEmpty(part.ContentType)) WriteAttr(w, written, "ct", part.ContentType);
        else if (TryGetString(raw, "ct") is string ctv) WriteAttr(w, written, "ct", ctv);

        if (!string.IsNullOrEmpty(part.Name)) WriteAttr(w, written, "name", part.Name);
        if (!string.IsNullOrEmpty(part.Charset)) WriteAttr(w, written, "chset", part.Charset);
        if (!string.IsNullOrEmpty(part.ContentDisposition)) WriteAttr(w, written, "cd", part.ContentDisposition);
        if (!string.IsNullOrEmpty(part.FileName)) WriteAttr(w, written, "fn", part.FileName);
        if (!string.IsNullOrEmpty(part.ContentId)) WriteAttr(w, written, "cid", part.ContentId);
        if (!string.IsNullOrEmpty(part.ContentLocation)) WriteAttr(w, written, "cl", part.ContentLocation);
        if (!string.IsNullOrEmpty(part.Text)) WriteAttr(w, written, "text", part.Text);

        if (!string.IsNullOrEmpty(part.DataSha256))
        {
            var blob = _repo.GetMediaBlob(part.DataSha256);
            if (blob is not null)
            {
                var abs = _media.GetAbsolutePath(blob.RelativePath);
                await using var fs = new FileStream(abs, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 64, useAsync: true);
                var buffer = new byte[8192];
                int read;
                w.WriteStartAttribute("data");
                while ((read = await fs.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    w.WriteBase64(buffer, 0, read);
                }
                w.WriteEndAttribute();
                written.Add("data");
            }
        }

        WriteExtras(w, raw, written);

        w.WriteEndElement();
    }

    private static Dictionary<string, string?> ParseAttrs(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string?>(StringComparer.Ordinal);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(json) ?? new Dictionary<string, string?>(StringComparer.Ordinal);
        }
        catch
        {
            return new Dictionary<string, string?>(StringComparer.Ordinal);
        }
    }

    private static void WriteExtras(XmlWriter w, Dictionary<string, string?> raw, HashSet<string> written)
    {
        foreach (var (key, value) in raw)
        {
            if (written.Contains(key))
            {
                continue;
            }

            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            // We handle streaming binary content separately.
            if (key.Equals("data", StringComparison.Ordinal))
            {
                continue;
            }

            w.WriteAttributeString(key, value ?? string.Empty);
        }
    }

    private static void WriteAttr(XmlWriter w, HashSet<string> written, string name, string value)
    {
        if (written.Add(name))
        {
            w.WriteAttributeString(name, value);
        }
    }

    private static string? TryGetString(Dictionary<string, string?> raw, string key)
    {
        return raw.TryGetValue(key, out var v) ? v : null;
    }

    private static int? TryGetInt(Dictionary<string, string?> raw, string key)
    {
        if (!raw.TryGetValue(key, out var v) || string.IsNullOrWhiteSpace(v))
        {
            return null;
        }
        return int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i) ? i : null;
    }
}
