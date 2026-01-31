using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

static byte[] RenderPng(int size)
{
    using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);

    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.Clear(Color.Transparent);

    var bgA = Color.FromArgb(255, 139, 92, 246);   // violet
    var bgB = Color.FromArgb(255, 34, 211, 238);   // cyan
    var cutout = Color.FromArgb(255, 11, 18, 32);  // deep navy

    // Rounded square background
    var pad = Math.Max(2, size / 16);
    var rect = new Rectangle(pad, pad, size - pad * 2, size - pad * 2);
    var radius = Math.Max(8, size / 5);

    using (var path = RoundedRect(rect, radius))
    using (var brush = new LinearGradientBrush(rect, bgA, bgB, 45f))
    {
        g.FillPath(brush, path);
    }

    // Subtle border
    using (var path = RoundedRect(rect, radius))
    using (var pen = new Pen(Color.FromArgb(70, 255, 255, 255), Math.Max(1f, size / 128f)))
    {
        g.DrawPath(pen, path);
    }

    // Chat bubble mark
    var bubblePad = size < 48 ? size * 0.22f : size * 0.24f;
    var bubbleRect = new RectangleF(
        bubblePad,
        bubblePad * 0.95f,
        size - bubblePad * 2f,
        size - bubblePad * (size < 48 ? 2.05f : 2.25f));

    using (var bubblePath = RoundedRectF(bubbleRect, size < 48 ? size * 0.16f : size * 0.14f))
    using (var bubbleBrush = new SolidBrush(Color.FromArgb(248, 255, 255, 255)))
    {
        g.FillPath(bubbleBrush, bubblePath);
    }

    // Bubble tail (keep simple)
    if (size >= 24)
    {
        using var tail = new GraphicsPath();
        using var tailBrush = new SolidBrush(Color.FromArgb(248, 255, 255, 255));
        var tailX = bubbleRect.X + bubbleRect.Width * 0.22f;
        var tailY = bubbleRect.Bottom - bubbleRect.Height * 0.06f;
        tail.AddPolygon(new[]
        {
            new PointF(tailX, tailY),
            new PointF(tailX + bubbleRect.Width * 0.14f, tailY),
            new PointF(tailX + bubbleRect.Width * 0.05f, tailY + bubbleRect.Height * 0.16f)
        });
        g.FillPath(tailBrush, tail);
    }

    // Database cutout inside bubble (omit at tiny sizes)
    if (size >= 32)
    {
        var dbW = bubbleRect.Width * 0.36f;
        var dbH = bubbleRect.Height * 0.48f;
        var dbX = bubbleRect.X + (bubbleRect.Width - dbW) / 2f;
        var dbY = bubbleRect.Y + bubbleRect.Height * 0.26f;
        var capH = Math.Max(2f, dbH * 0.22f);

        using var cut = new SolidBrush(cutout);

        // Body
        g.FillRectangle(cut, dbX, dbY + capH / 2f, dbW, dbH - capH);

        // Top ellipse
        g.FillEllipse(cut, dbX, dbY, dbW, capH);

        // Bottom ellipse hint
        g.FillEllipse(cut, dbX, dbY + dbH - capH, dbW, capH);

        // Inner highlight lines
        using var linePen = new Pen(Color.FromArgb(90, 255, 255, 255), Math.Max(1f, size / 96f));
        var y1 = dbY + dbH * 0.33f;
        var y2 = dbY + dbH * 0.62f;
        g.DrawArc(linePen, dbX, y1, dbW, capH, 0, 180);
        g.DrawArc(linePen, dbX, y2, dbW, capH, 0, 180);
    }
    else
    {
        // Three dots for legibility at tiny sizes
        using var dot = new SolidBrush(cutout);
        var d = Math.Max(2f, size * 0.10f);
        var gap = d * 0.65f;
        var cx = bubbleRect.X + bubbleRect.Width / 2f;
        var cy = bubbleRect.Y + bubbleRect.Height * 0.55f;
        g.FillEllipse(dot, cx - d - gap, cy - d / 2f, d, d);
        g.FillEllipse(dot, cx - d / 2f, cy - d / 2f, d, d);
        g.FillEllipse(dot, cx + gap, cy - d / 2f, d, d);
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
