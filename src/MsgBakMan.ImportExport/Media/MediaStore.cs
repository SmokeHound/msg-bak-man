using System.Security.Cryptography;
using System.Xml;
using MsgBakMan.Core.Models;
using MsgBakMan.Data.Project;

namespace MsgBakMan.ImportExport.Media;

public sealed class MediaStore
{
    private readonly ProjectPaths _paths;

    public MediaStore(ProjectPaths paths)
    {
        _paths = paths;
        Directory.CreateDirectory(_paths.MediaBlobRoot);
        Directory.CreateDirectory(_paths.TempRoot);
    }

    public async Task<MediaBlobRef?> IngestBase64AttributeAsync(
        XmlReader reader,
        string attributeName,
        string? mimeType,
        CancellationToken cancellationToken)
    {
        if (!reader.MoveToAttribute(attributeName))
        {
            return null;
        }

        var ext = MimeTypes.TryGetExtension(mimeType) ?? ".bin";
        var tempPath = Path.Combine(_paths.TempRoot, Guid.NewGuid().ToString("n") + ext);

        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        var charBuffer = new char[8192];
        var leftover = string.Empty;
        long total = 0;


        // Write to a temp file first; ensure the handle is closed before move (Windows file locking).
        await using (var fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 64, useAsync: true))
        {
            // Stream attribute content in chunks.
            while (reader.ReadAttributeValue())
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (reader.NodeType != XmlNodeType.Text && reader.NodeType != XmlNodeType.CDATA)
                {
                    continue;
                }

                var chunk = reader.Value;
                if (string.IsNullOrEmpty(chunk))
                {
                    continue;
                }

                var input = leftover + chunk;
                var usableLen = input.Length - (input.Length % 4);
                if (usableLen <= 0)
                {
                    leftover = input;
                    continue;
                }

                leftover = input[usableLen..];

                var offset = 0;
                while (offset < usableLen)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Decode in bounded chunks (must be divisible by 4).
                    var take = Math.Min(charBuffer.Length, usableLen - offset);
                    take -= take % 4;
                    if (take <= 0)
                    {
                        break;
                    }

                    input.AsSpan(offset, take).CopyTo(charBuffer);
                    var bytes = Convert.FromBase64CharArray(charBuffer, 0, take);

                    await fs.WriteAsync(bytes, cancellationToken);
                    hasher.AppendData(bytes);
                    total += bytes.Length;
                    offset += take;
                }
            }

            if (!string.IsNullOrEmpty(leftover))
            {
                // Final chunk (may include padding).
                var bytes = Convert.FromBase64String(leftover);
                await fs.WriteAsync(bytes, cancellationToken);
                hasher.AppendData(bytes);
                total += bytes.Length;
            }

            await fs.FlushAsync(cancellationToken);
        }

        // Restore reader position back on element.
        reader.MoveToElement();

        var hash = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
        var rel = GetRelativeBlobPath(hash);
        var finalPath = Path.Combine(_paths.ProjectRoot, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(finalPath) ?? _paths.MediaBlobRoot);

        // Best-effort move with a couple retries for transient file locks (AV, indexer, etc.).
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                if (!File.Exists(finalPath))
                {
                    File.Move(tempPath, finalPath);
                }
                else
                {
                    File.Delete(tempPath);
                }
                break;
            }
            catch (IOException) when (attempt < 2)
            {
                await Task.Delay(50, cancellationToken);
            }
        }

        return new MediaBlobRef(hash, total, mimeType, ext, rel.Replace('\\', '/'));
    }

    public string GetAbsolutePath(string relativePath)
    {
        return Path.Combine(_paths.ProjectRoot, relativePath.Replace('/', '\\'));
    }

    // Flat layout (v4): minimize folder creation by storing all blobs directly under media/blobs/
    // Use the hash as the filename (no extension) to avoid duplicates if mime/extension changes.
    private static string GetRelativeBlobPath(string sha256Hex)
    {
        return Path.Combine("media", "blobs", sha256Hex);
    }
}
