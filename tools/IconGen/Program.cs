using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

static byte[] RenderPng(int size)
{
    using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);

    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.Clear(Color.Transparent);

    var bgTop = Color.FromArgb(255, 37, 99, 235);   // blue 600
    var bgBot = Color.FromArgb(255, 29, 78, 216);   // blue 700
    var fg = Color.White;

    // Rounded square background
    var pad = Math.Max(2, size / 16);
    var rect = new Rectangle(pad, pad, size - pad * 2, size - pad * 2);
    var radius = Math.Max(8, size / 5);

    using (var path = RoundedRect(rect, radius))
    using (var brush = new LinearGradientBrush(rect, bgTop, bgBot, LinearGradientMode.Vertical))
    {
        g.FillPath(brush, path);
    }

    // Subtle top highlight
    using (var highlight = new LinearGradientBrush(
               new Rectangle(rect.X, rect.Y, rect.Width, rect.Height / 2),
               Color.FromArgb(90, 255, 255, 255),
               Color.FromArgb(0, 255, 255, 255),
               LinearGradientMode.Vertical))
    using (var path = RoundedRect(rect, radius))
    {
        g.FillPath(highlight, path);
    }

    // Chat bubble mark (simplify for small sizes for better legibility)
    var bubblePad = size < 48 ? size * 0.20f : size * 0.22f;
    var bubbleRect = new RectangleF(
        bubblePad,
        bubblePad * 0.95f,
        size - bubblePad * 2f,
        size - bubblePad * (size < 48 ? 2.0f : 2.2f));

    using (var bubblePath = RoundedRectF(bubbleRect, size < 48 ? size * 0.14f : size * 0.12f))
    using (var bubbleBrush = new SolidBrush(Color.FromArgb(245, 255, 255, 255)))
    {
        g.FillPath(bubbleBrush, bubblePath);
    }

    // Bubble tail (omit at tiny sizes)
    if (size >= 32)
    {
        using var tail = new GraphicsPath();
        using var tailBrush = new SolidBrush(Color.FromArgb(245, 255, 255, 255));
        var tailX = bubbleRect.X + bubbleRect.Width * 0.28f;
        var tailY = bubbleRect.Bottom - bubbleRect.Height * 0.06f;
        tail.AddPolygon(new[]
        {
            new PointF(tailX, tailY),
            new PointF(tailX + bubbleRect.Width * 0.10f, tailY),
            new PointF(tailX + bubbleRect.Width * 0.02f, tailY + bubbleRect.Height * 0.12f)
        });
        g.FillPath(tailBrush, tail);
    }

    // Monogram only for larger sizes
    if (size >= 48)
    {
        var fontSize = size * 0.26f;
        using var font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        var text = "MBM";
        var textSize = g.MeasureString(text, font);
        var tx = (size - textSize.Width) / 2f;
        var ty = (size - textSize.Height) / 2f + size * 0.05f;

        using var textBrush = new SolidBrush(Color.FromArgb(255, 16, 24, 40));
        g.DrawString(text, font, textBrush, tx, ty);
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
