using System.Globalization;
using System.IO;
using System.Security;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StickerLayout;

public static class ExportSvg
{
    // Satu file SVG per lembar. Layer (group) dipisah agar CorelDRAW membacanya sebagai layer.
    public static List<string> Svg(string path, IList<List<Placed>> sheets, double cw, IList<double> sheetH,
                                   double margin, bool regMarks = false, bool embedArtwork = true)
    {
        var files = new List<string>();
        for (int i = 0; i < sheets.Count; i++)
        {
            var file = sheets.Count == 1
                ? path
                : Path.Combine(Path.GetDirectoryName(path)!, $"{Path.GetFileNameWithoutExtension(path)}-{i + 1}.svg");
            File.WriteAllText(file, Sheet(sheets[i], cw, sheetH[i], margin, regMarks, embedArtwork), Encoding.UTF8);
            files.Add(file);
        }
        return files;
    }

    static string Sheet(List<Placed> ps, double cw, double ch, double margin, bool regMarks, bool embedArtwork)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture,
            $"""
            <?xml version="1.0" encoding="UTF-8" standalone="no"?>
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink"
                 xmlns:inkscape="http://www.inkscape.org/namespaces/inkscape"
                 width="{F(cw)}mm" height="{F(ch)}mm" viewBox="0 0 {F(cw)} {F(ch)}">
            """);
        sb.AppendLine();

        // Layer 1: Artwork
        sb.AppendLine("""  <g id="artwork" inkscape:label="Artwork" inkscape:groupmode="layer">""");
        for (int i = 0; i < ps.Count; i++) Artwork(sb, ps[i], embedArtwork, i);
        sb.AppendLine("  </g>");

        // Layer 2: Kiss Cut
        sb.AppendLine("""  <g id="kiss-cut" inkscape:label="Kiss Cut" inkscape:groupmode="layer" fill="none" stroke="#EC008C" stroke-width="0.1">""");
        foreach (var p in ps) if (p.S.KissGeo is { } k) CutPath(sb, p, k);
        sb.AppendLine("  </g>");

        // Layer 3: Die Cut
        sb.AppendLine("""  <g id="die-cut" inkscape:label="Die Cut" inkscape:groupmode="layer" fill="none" stroke="#00AEEF" stroke-width="0.1">""");
        foreach (var p in ps) if (p.S.DieGeo is { } d) CutPath(sb, p, d);
        sb.AppendLine("  </g>");

        // Layer 4: Registration Marks
        if (regMarks)
        {
            sb.AppendLine("""  <g id="SA regmark type2 auto 33 0.00 2.83 28.35 283.46 283.46 0" inkscape:label="Registration Marks" inkscape:groupmode="layer" fill="none" stroke="#000000" stroke-width="1">""");
            foreach (var (a, c, b) in RegistrationMarks.Corners(cw, ch, margin))
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"""    <path d="M {F(a.X)},{F(a.Y)} L {F(c.X)},{F(c.Y)} L {F(b.X)},{F(b.Y)}"/>""");
            sb.AppendLine("  </g>");
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    // Seperti export PDF: warna border di bawah artwork, artwork di-clip ke garis potong (ArtClip), isi even-odd seperti
    // PDFsharp. n = nomor stiker di lembar, untuk id clipPath.
    static void Artwork(StringBuilder sb, Placed p, bool embed, int n)
    {
        var s = p.S;
        var (tx, ty) = p.T;
        var xf = $"translate({F(p.X + tx)},{F(p.Y + ty)}) rotate({p.Rot})";
        // ponytail: SVG tidak punya CMYK; warna border memakai warna layar (Cmyk.Rgb). Warna cetak tetap dari export PDF.
        var border = s.BorderGeo is { } bg && s.Border is { } c
            ? $"""<path d="{D(bg)}" fill="#{c.Rgb.R:X2}{c.Rgb.G:X2}{c.Rgb.B:X2}" fill-rule="evenodd"/>""" : "";

        string href;
        Rect r;
        if (!embed)
        {
            // Referensi eksternal (file asli). Tidak selalu bisa dibuka CorelDRAW, tapi file kecil.
            // ponytail: tautan hanya pas untuk PNG/JPG apa adanya (tanpa Rapikan, bleed, halaman PDF/AI); sisipkan gambar bila mode ini masuk menu.
            href = SecurityElement.Escape(new Uri(s.Source).AbsoluteUri);
            r = s.Full;
        }
        else
        {
            // Gambar dan kotaknya sama dengan export PDF: bleed di BleedRect, gambar sumber di Full (gambar penuh, termasuk
            // margin transparan), vektor PDF/AI (Raster kosong) lewat render layar di kotak artwork.
            // ponytail: render layar paling besar 1600 px dan RGB; render ulang halaman PDF ±300 dpi bila SVG kurang tajam.
            (var bytes, r) = s.BleedPng != null ? (s.BleedPng, s.BleedRect)
                : s.Pdf == null ? (s.Raster, s.Full)
                : (Loader.Png(s.Preview), new Rect(s.Off, s.Off, s.ArtW, s.ArtH));
            var mime = bytes is [0xFF, 0xD8, ..] ? "image/jpeg" : "image/png"; // JPG asli ditempel apa adanya
            href = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
        }

        sb.AppendLine(CultureInfo.InvariantCulture,
            $"""    <g transform="{xf}">{border}<clipPath id="art{n}"><path d="{D(s.ArtClip)}" clip-rule="evenodd"/></clipPath><g clip-path="url(#art{n})"><image xlink:href="{href}" x="{F(r.X)}" y="{F(r.Y)}" width="{F(r.Width)}" height="{F(r.Height)}"/></g></g>""");
    }

    static void CutPath(StringBuilder sb, Placed p, Geometry geo)
    {
        var (tx, ty) = p.T;
        sb.AppendLine(CultureInfo.InvariantCulture,
            $"""    <g transform="translate({F(p.X + tx)},{F(p.Y + ty)}) rotate({p.Rot})"><path d="{D(geo)}"/></g>""");
    }

    // Geometry WPF -> string path SVG (mm).
    static string D(Geometry geo)
    {
        var sb = new StringBuilder();
        foreach (var f in PathGeometry.CreateFromGeometry(geo).Figures)
        {
            sb.Append('M').Append(F(f.StartPoint.X)).Append(',').Append(F(f.StartPoint.Y));
            foreach (var seg in f.Segments)
            {
                switch (seg)
                {
                    case LineSegment l:
                        sb.Append('L').Append(F(l.Point.X)).Append(',').Append(F(l.Point.Y));
                        break;
                    case PolyLineSegment pl:
                        foreach (var q in pl.Points) sb.Append('L').Append(F(q.X)).Append(',').Append(F(q.Y));
                        break;
                    case BezierSegment b:
                        sb.Append('C').Append(F(b.Point1.X)).Append(',').Append(F(b.Point1.Y))
                          .Append(' ').Append(F(b.Point2.X)).Append(',').Append(F(b.Point2.Y))
                          .Append(' ').Append(F(b.Point3.X)).Append(',').Append(F(b.Point3.Y));
                        break;
                    case PolyBezierSegment pb:
                        for (int i = 0; i + 2 < pb.Points.Count; i += 3)
                        {
                            var p1 = pb.Points[i];
                            var p2 = pb.Points[i + 1];
                            var p3 = pb.Points[i + 2];
                            sb.Append('C').Append(F(p1.X)).Append(',').Append(F(p1.Y))
                              .Append(' ').Append(F(p2.X)).Append(',').Append(F(p2.Y))
                              .Append(' ').Append(F(p3.X)).Append(',').Append(F(p3.Y));
                        }
                        break;
                }
            }
            if (f.IsClosed) sb.Append('Z');
        }
        return sb.ToString();
    }

    static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}