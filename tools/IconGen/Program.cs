using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

static byte[] RenderPng(int size)
{
    using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);

    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    g.Clear(Color.Transparent);

    // Simple, modern mark: solid accent background + white chat bubble.
    var accent = Color.FromArgb(255, 37, 99, 235); // #2563EB
    var bubble = Color.FromArgb(250, 255, 255, 255);
    var dot = Color.FromArgb(255, 37, 99, 235);

    // Rounded square background
    var pad = Math.Max(2, size / 16);
    var rect = new Rectangle(pad, pad, size - pad * 2, size - pad * 2);
    var radius = Math.Max(8, size / 5);

    using (var path = RoundedRect(rect, radius))
    using (var brush = new SolidBrush(accent))
    {
        g.FillPath(brush, path);
    }

    // Chat bubble
    var bubblePad = size * 0.22f;
    var bubbleRect = new RectangleF(
        bubblePad,
        bubblePad * 0.92f,
        size - bubblePad * 2f,
        size - bubblePad * 2.15f);

    using (var bubblePath = RoundedRectF(bubbleRect, size * 0.14f))
    using (var bubbleBrush = new SolidBrush(bubble))
    {
        g.FillPath(bubbleBrush, bubblePath);
    }

    // Tail
    if (size >= 20)
    {
        using var tail = new GraphicsPath();
        using var tailBrush = new SolidBrush(bubble);
        var tailX = bubbleRect.X + bubbleRect.Width * 0.22f;
        var tailY = bubbleRect.Bottom - bubbleRect.Height * 0.06f;
        tail.AddPolygon(new[]
        {
            new PointF(tailX, tailY),
            new PointF(tailX + bubbleRect.Width * 0.16f, tailY),
            new PointF(tailX + bubbleRect.Width * 0.06f, tailY + bubbleRect.Height * 0.18f)
        });
        g.FillPath(tailBrush, tail);
    }

    // Three dots
    using (var dotBrush = new SolidBrush(dot))
    {
        var d = Math.Max(2f, size * 0.085f);
        var gap = d * 0.70f;
        var cx = bubbleRect.X + bubbleRect.Width / 2f;
        var cy = bubbleRect.Y + bubbleRect.Height * 0.56f;
        g.FillEllipse(dotBrush, cx - d - gap, cy - d / 2f, d, d);
        g.FillEllipse(dotBrush, cx - d / 2f, cy - d / 2f, d, d);
        g.FillEllipse(dotBrush, cx + gap, cy - d / 2f, d, d);
    }

    using var ms = new MemoryStream();
    bmp.Save(ms, ImageFormat.Png);
    return ms.ToArray();
}

static GraphicsPath RoundedRect(Rectangle bounds, int radius)
{
    var diameter = radius * 2;
    var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
    var path = new GraphicsPath();

    // top-left
    path.AddArc(arc, 180, 90);
    // top-right
    arc.X = bounds.Right - diameter;
    path.AddArc(arc, 270, 90);
    // bottom-right
    arc.Y = bounds.Bottom - diameter;
    path.AddArc(arc, 0, 90);
    // bottom-left
    arc.X = bounds.Left;
    path.AddArc(arc, 90, 90);

    path.CloseFigure();
    return path;
}

static GraphicsPath RoundedRectF(RectangleF bounds, float radius)
{
    var diameter = radius * 2f;
    var arc = new RectangleF(bounds.Location, new SizeF(diameter, diameter));
    var path = new GraphicsPath();

    path.AddArc(arc, 180, 90);
    arc.X = bounds.Right - diameter;
    path.AddArc(arc, 270, 90);
    arc.Y = bounds.Bottom - diameter;
    path.AddArc(arc, 0, 90);
    arc.X = bounds.Left;
    path.AddArc(arc, 90, 90);

    path.CloseFigure();
    return path;
}

static void WriteMultiImageIco(string outputPath, IReadOnlyList<(int Size, byte[] Png)> images)
{
    using var fs = File.Create(outputPath);
    using var bw = new BinaryWriter(fs);

    bw.Write((ushort)0); // reserved
    bw.Write((ushort)1); // type 1 = icon
    bw.Write((ushort)images.Count);

    var entryStart = 6;
    var imageOffset = entryStart + images.Count * 16;
    var offsets = new int[images.Count];

    for (var i = 0; i < images.Count; i++)
    {
        offsets[i] = imageOffset;
        imageOffset += images[i].Png.Length;
    }

    for (var i = 0; i < images.Count; i++)
    {
        var (size, png) = images[i];
        bw.Write((byte)(size >= 256 ? 0 : size));
        bw.Write((byte)(size >= 256 ? 0 : size));
        bw.Write((byte)0);   // color count
        bw.Write((byte)0);   // reserved
        bw.Write((ushort)1); // planes
        bw.Write((ushort)32);// bit count
        bw.Write(png.Length);
        bw.Write(offsets[i]);
    }

    for (var i = 0; i < images.Count; i++)
    {
        bw.Write(images[i].Png);
    }
}

var outPath = args.Length >= 1
    ? args[0]
    : Path.Combine(Environment.CurrentDirectory, "msgbakman.ico");

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);

var sizes = new[] { 16, 24, 32, 48, 64, 128, 256 };
var images = sizes
    .Select(s => (Size: s, Png: RenderPng(s)))
    .ToList();

WriteMultiImageIco(outPath, images);

Console.WriteLine($"Wrote icon: {outPath}");
