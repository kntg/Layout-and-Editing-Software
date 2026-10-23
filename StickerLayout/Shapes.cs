using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StickerLayout;

// Bentuk garis potong (masukan 2026-10-23). Auto = garis mengikuti artwork (desain kotak otomatis jadi Rect).
public enum CutShape { Auto, Rect, RoundRect, Circle, Oval, Triangle, Star, Heart, Hexagon }

// Preset bentuk yang mengelilingi artwork sejauh offset (artwork tidak terpotong). Koordinat: mm relatif artwork, y ke bawah.
public static class Shapes
{
    const double K = 0.5522847498;    // panjang handle Bezier untuk seperempat lingkaran (radius 1)
    // ponytail: radius sudut 3 mm dan rasio bintang 0,5 adalah titik awal; sesuaikan dengan pilihan operator AP 55.
    public const double CornerR = 3;
    const double StarInner = 0.5;

    // Kotak bersudut tajam: 4 node lurus.
    public static Node[] Box(double x0, double y0, double x1, double y1) =>
        [Node.Corner(new Point(x0, y0)), Node.Corner(new Point(x1, y0)), Node.Corner(new Point(x1, y1)), Node.Corner(new Point(x0, y1))];

    // Artwork memenuhi kotaknya: semua piksel tepi mask terisi dan isinya >= 99,5 % (foto, JPG/PNG opak, PDF berlatar
    // kotak putih). Kotak bersudut bulat yang transparan di sudut bukan kotak.
    public static bool IsBox(bool[,] m)
    {
        int w = m.GetLength(0), h = m.GetLength(1), n = 0;
        if (w == 0 || h == 0) return false;
        for (int x = 0; x < w; x++) if (!m[x, 0] || !m[x, h - 1]) return false;
        for (int y = 0; y < h; y++) if (!m[0, y] || !m[w - 1, y]) return false;
        foreach (bool b in m) if (b) n++;
        return n >= 0.995 * w * h;
    }

    // Garis kiss/die bentuk preset dari mask artwork (piksel ke mm: artW/lebar mask). Kiss mengelilingi artwork sejauh offset
    // kiss; die memakai bentuk dan pusat yang sama sejauh offset die, minimal 1 piksel (1/6 mm) di luar kiss. Halus tidak dipakai.
    public static (List<Node[]>? Kiss, List<Node[]>? Die) Lines(bool[,] mask, CutShape shape, double artW, double artH, Cut kiss, Cut die)
    {
        var hull = Hull(mask, artW / mask.GetLength(0), artH / mask.GetLength(1));
        if (hull.Length < 3 || shape == CutShape.Auto) return (null, null);
        double dk = kiss.Off, dd = kiss.On ? Math.Max(die.Off, kiss.Off + 1 / Loader.PxMm) : die.Off;
        Point? center = null;
        double size = 0;
        Node[]? k = kiss.On ? Make(shape, hull, dk, ref center, ref size) : null;
        double kSize = size;
        Node[]? d = die.On ? Make(shape, hull, dd, ref center, ref size, kSize) : null;
        return (k == null ? null : [k], d == null ? null : [d]);
    }

    // Satu kontur bentuk yang memuat hull + offset. center/size: pusat dan ukuran hasil kiss, dipakai ulang untuk die supaya
    // die tetap membungkus kiss (bentuk yang sama, pusat sama, lebih besar).
    static Node[] Make(CutShape shape, Point[] hull, double off, ref Point? center, ref double size, double minSize = 0)
    {
        double x0 = hull.Min(p => p.X), y0 = hull.Min(p => p.Y), x1 = hull.Max(p => p.X), y1 = hull.Max(p => p.Y);
        var mid = new Point((x0 + x1) / 2, (y0 + y1) / 2);
        double w = Math.Max(1e-3, x1 - x0), h = Math.Max(1e-3, y1 - y0);
        switch (shape)
        {
            case CutShape.Rect:
                return Box(x0 - off, y0 - off, x1 + off, y1 + off);
            case CutShape.RoundRect:
            {
                // Ruang tambahan e (>= offset) sampai titik uji masuk kotak bersudut bulat; radius maksimal setengah sisi.
                var probe = Probe(hull, off);
                bool Fits(double e)
                {
                    double r = Math.Min(CornerR, Math.Min(w, h) / 2 + e), ax0 = x0 - e + r, ay0 = y0 - e + r, ax1 = x1 + e - r, ay1 = y1 + e - r;
                    return probe.All(p => (p - new Point(Math.Clamp(p.X, ax0, ax1), Math.Clamp(p.Y, ay0, ay1))).Length <= r + 1e-9);
                }
                double e = Search(off, off + CornerR + 1, Fits, Math.Max(off, size));
                size = e;
                return RoundRect(x0 - e, y0 - e, x1 + e, y1 + e, Math.Min(CornerR, Math.Min(w, h) / 2 + e));
            }
            case CutShape.Circle:
            {
                var (c, r) = center is { } cc ? (cc, hull.Max(p => (p - cc).Length)) : MinCircle(hull);
                center = c;
                return Ellipse(c, r + off, r + off);
            }
            case CutShape.Oval:
            {
                var c = center ?? mid;
                center = c;
                var probe = Probe(hull, off);
                double s = probe.Max(p => Math.Sqrt(Math.Pow(2 * (p.X - c.X) / w, 2) + Math.Pow(2 * (p.Y - c.Y) / h, 2)));
                return Ellipse(c, s * w / 2, s * h / 2);
            }
            case CutShape.Triangle:
            {
                // Segitiga sama sisi menghadap atas = irisan 3 setengah bidang; support hull + offset tepat untuk tiap sisi.
                Vector[] n = [new(0, 1), new(-Math.Sqrt(3) / 2, -0.5), new(Math.Sqrt(3) / 2, -0.5)];
                var hs = n.Select(v => hull.Max(p => v * (Vector)p) + off).ToArray();
                return [.. Enumerable.Range(0, 3).Select(i => Node.Corner(Cross(n[i], hs[i], n[(i + 1) % 3], hs[(i + 1) % 3])))];
            }
            case CutShape.Hexagon:
            {
                // Segi enam beraturan, sisi atas datar. Pusat: kuadrat terkecil dari 3 pasang sisi berhadapan; jari-jari dalam =
                // support terbesar terhadap pusat.
                var n = Enumerable.Range(0, 6).Select(i => new Vector(Math.Cos((30 + 60 * i) * Math.PI / 180), Math.Sin((30 + 60 * i) * Math.PI / 180))).ToArray();
                var hs = n.Select(v => hull.Max(p => v * (Vector)p) + off).ToArray();
                Point c = center ?? LeastSquares(n.Take(3).ToArray(), [.. Enumerable.Range(0, 3).Select(i => (hs[i] - hs[i + 3]) / 2)]);
                center = c;
                double r = Enumerable.Range(0, 6).Max(i => hs[i] - n[i] * (Vector)c), R = r / Math.Cos(Math.PI / 6);
                return [.. Enumerable.Range(0, 6).Select(i => Node.Corner(c + R * new Vector(Math.Cos(i * Math.PI / 3), Math.Sin(i * Math.PI / 3))))];
            }
            default: // Star, Heart: bentuk tidak cembung, skala seragam minimal (pencarian biner, uji titik di polygon)
            {
                var unit = shape == CutShape.Star ? Star() : Heart();
                var poly = Flat(unit);
                var probe = Probe(hull, off);
                double far = probe.Max(p => (p - mid).Length);
                Point best = mid;
                double bestS = double.MaxValue;
                IEnumerable<Point> centers = center is { } c0 ? [c0] : Enumerable.Range(-6, 13).Select(i => mid + new Vector(0, i * 0.05 * h));
                foreach (var c in centers)
                {
                    bool Fits(double s) => probe.All(p => Inside(poly, new Point((p.X - c.X) / s, (p.Y - c.Y) / s)));
                    if (!Fits(far * 20)) continue;
                    double s = Search(0, far * 20, Fits, minSize);
                    if (s < bestS) (bestS, best) = (s, c);
                }
                center = best;
                size = bestS;
                return Transform(unit, best, bestS, bestS);
            }
        }
    }

    // Nilai terkecil di [lo, hi] (dibatasi minimal min) yang membuat fits benar; fits harus monoton.
    static double Search(double lo, double hi, Func<double, bool> fits, double min)
    {
        lo = Math.Max(lo, min);
        if (fits(lo)) return lo;
        for (int i = 0; i < 50; i++) { double m = (lo + hi) / 2; if (fits(m)) hi = m; else lo = m; }
        return hi;
    }

    // Hull cembung batas piksel artwork (mm). Bentuk cembung yang memuat hull pasti memuat artwork.
    static Point[] Hull(bool[,] m, double sx, double sy)
    {
        int w = m.GetLength(0), h = m.GetLength(1);
        var pts = new List<Point>();
        for (int y = 0; y < h; y++)
        {
            int a = -1, b = -1;
            for (int x = 0; x < w; x++) if (m[x, y]) { if (a < 0) a = x; b = x; }
            if (a < 0) continue;
            pts.AddRange([new(a * sx, y * sy), new(a * sx, (y + 1) * sy), new((b + 1) * sx, y * sy), new((b + 1) * sx, (y + 1) * sy)]);
        }
        pts = [.. pts.Distinct().OrderBy(p => p.X).ThenBy(p => p.Y)];
        if (pts.Count < 3) return [.. pts];
        static double Turn(Point o, Point a, Point b) => Vector.CrossProduct(a - o, b - o);
        var lower = new List<Point>();
        foreach (var p in pts) { while (lower.Count >= 2 && Turn(lower[^2], lower[^1], p) <= 0) lower.RemoveAt(lower.Count - 1); lower.Add(p); }
        var upper = new List<Point>();
        for (int i = pts.Count - 1; i >= 0; i--) { var p = pts[i]; while (upper.Count >= 2 && Turn(upper[^2], upper[^1], p) <= 0) upper.RemoveAt(upper.Count - 1); upper.Add(p); }
        return [.. lower.SkipLast(1).Concat(upper.SkipLast(1))];
    }

    // Titik uji hull + offset: tiap sisi hull dipecah 0,5 mm dan digeser keluar sejauh off; tiap titik sudut hull diberi
    // 16 titik di lingkaran (jari-jari off / cos 11,25°, jadi lingkaran offset sepenuhnya di dalamnya).
    static Point[] Probe(Point[] hull, double off)
    {
        var res = new List<Point>();
        double r = off / Math.Cos(Math.PI / 16);
        var mid = Centroid(hull);
        for (int i = 0; i < hull.Length; i++)
        {
            Point a = hull[i], b = hull[(i + 1) % hull.Length];
            for (int j = 0; j < 16; j++) res.Add(a + r * new Vector(Math.Cos(j * Math.PI / 8), Math.Sin(j * Math.PI / 8)));
            var e = b - a;
            if (e.Length < 1e-9) continue;
            var nrm = new Vector(e.Y, -e.X);
            nrm.Normalize();
            if (nrm * (a + e / 2 - mid) < 0) nrm = -nrm; // normal keluar: menjauhi pusat hull
            int n = (int)Math.Ceiling(e.Length / 0.5);
            for (int j = 0; j <= n; j++) res.Add(a + e * ((double)j / n) + nrm * off);
        }
        return [.. res];
    }

    static Point Centroid(Point[] p) => new(p.Average(q => q.X), p.Average(q => q.Y));

    // Lingkaran terkecil yang memuat semua titik (Welzl iteratif, urutan acak tetap supaya hasil sama setiap kali).
    static (Point C, double R) MinCircle(Point[] pts)
    {
        var rnd = new Random(1);
        var p = pts.OrderBy(_ => rnd.Next()).ToArray();
        Point c = p[0];
        double r = 0;
        for (int i = 1; i < p.Length; i++)
        {
            if ((p[i] - c).Length <= r + 1e-9) continue;
            (c, r) = (p[i], 0);
            for (int j = 0; j < i; j++)
            {
                if ((p[j] - c).Length <= r + 1e-9) continue;
                c = p[i] + (p[j] - p[i]) / 2;
                r = (p[i] - c).Length;
                for (int k = 0; k < j; k++)
                    if ((p[k] - c).Length > r + 1e-9) (c, r) = Circum(p[i], p[j], p[k]);
            }
        }
        return (c, r);
    }

    static (Point, double) Circum(Point a, Point b, Point c)
    {
        double d = 2 * (a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y));
        if (Math.Abs(d) < 1e-12) // segaris: lingkaran dari dua titik terjauh
        {
            var (u, v) = new[] { (a, b), (a, c), (b, c) }.MaxBy(t => (t.Item1 - t.Item2).Length);
            var m = u + (v - u) / 2;
            return (m, (u - m).Length);
        }
        double a2 = a.X * a.X + a.Y * a.Y, b2 = b.X * b.X + b.Y * b.Y, c2 = c.X * c.X + c.Y * c.Y;
        var o = new Point((a2 * (b.Y - c.Y) + b2 * (c.Y - a.Y) + c2 * (a.Y - b.Y)) / d, (a2 * (c.X - b.X) + b2 * (a.X - c.X) + c2 * (b.X - a.X)) / d);
        return (o, (a - o).Length);
    }

    // Titik potong garis n1·x = h1 dan n2·x = h2.
    static Point Cross(Vector n1, double h1, Vector n2, double h2)
    {
        double det = n1.X * n2.Y - n1.Y * n2.X;
        return new Point((h1 * n2.Y - n1.Y * h2) / det, (n1.X * h2 - h1 * n2.X) / det);
    }

    static Point LeastSquares(Vector[] n, double[] m)
    {
        double a = n.Sum(v => v.X * v.X), b = n.Sum(v => v.X * v.Y), d = n.Sum(v => v.Y * v.Y);
        double u = Enumerable.Range(0, n.Length).Sum(i => n[i].X * m[i]), v = Enumerable.Range(0, n.Length).Sum(i => n[i].Y * m[i]);
        double det = a * d - b * b;
        return new Point((u * d - b * v) / det, (a * v - b * u) / det);
    }

    // ---- Kontur bentuk ----

    // Elips (4 node kurva) berpusat c dengan jari-jari rx, ry; searah jarum jam di layar.
    static Node[] Ellipse(Point c, double rx, double ry) =>
    [
        new(c + new Vector(rx, 0), c + new Vector(rx, -K * ry), c + new Vector(rx, K * ry), true),
        new(c + new Vector(0, ry), c + new Vector(K * rx, ry), c + new Vector(-K * rx, ry), true),
        new(c + new Vector(-rx, 0), c + new Vector(-rx, K * ry), c + new Vector(-rx, -K * ry), true),
        new(c + new Vector(0, -ry), c + new Vector(-K * rx, -ry), c + new Vector(K * rx, -ry), true),
    ];

    // Kotak bersudut bulat: 8 node, sisi lurus dan sudut seperempat lingkaran radius r.
    static Node[] RoundRect(double x0, double y0, double x1, double y1, double r)
    {
        double k = K * r;
        Node N(double x, double y, double ix, double iy, double ox, double oy) => new(new Point(x, y), new Point(ix, iy), new Point(ox, oy), true);
        return
        [
            N(x0 + r, y0, x0 + r - k, y0, x0 + r, y0), N(x1 - r, y0, x1 - r, y0, x1 - r + k, y0),
            N(x1, y0 + r, x1, y0 + r - k, x1, y0 + r), N(x1, y1 - r, x1, y1 - r, x1, y1 - r + k),
            N(x1 - r, y1, x1 - r + k, y1, x1 - r, y1), N(x0 + r, y1, x0 + r, y1, x0 + r - k, y1),
            N(x0, y1 - r, x0, y1 - r + k, x0, y1 - r), N(x0, y0 + r, x0, y0 + r, x0, y0 + r - k),
        ];
    }

    // Bintang 5 sudut menghadap atas, jari-jari luar 1.
    static Node[] Star() => [.. Enumerable.Range(0, 10).Select(i =>
    {
        double a = (-90 + 36 * i) * Math.PI / 180, r = i % 2 == 0 ? 1 : StarInner;
        return Node.Corner(new Point(r * Math.Cos(a), r * Math.Sin(a)));
    })];

    // Hati: 4 node (lekuk atas dan ujung bawah = sudut, sisi kiri dan kanan = halus), lebar ±1,9, tinggi ±1,7.
    static Node[] Heart() =>
    [
        new(new Point(0, -0.45), new Point(-0.15, -0.9), new Point(0.15, -0.9)),
        new(new Point(0.95, -0.25), new Point(0.95, -0.85), new Point(0.95, 0.25), true),
        new(new Point(0, 0.95), new Point(0.35, 0.6), new Point(-0.35, 0.6)),
        new(new Point(-0.95, -0.25), new Point(-0.95, 0.25), new Point(-0.95, -0.85), true),
    ];

    static Node[] Transform(Node[] unit, Point c, double sx, double sy)
    {
        Point T(Point p) => new(c.X + p.X * sx, c.Y + p.Y * sy);
        return [.. unit.Select(n => n with { P = T(n.P), In = T(n.In), Out = T(n.Out) })];
    }

    // Kontur dipecah rapat (untuk uji titik di dalam).
    static Point[] Flat(Node[] c)
    {
        var f = Geometry.Parse(Curves.PathOf([c], 0)).GetFlattenedPathGeometry(0.002, ToleranceType.Absolute).Figures[0];
        return [f.StartPoint, .. f.Segments.SelectMany(s => s is PolyLineSegment pl ? (IEnumerable<Point>)pl.Points : s is LineSegment l ? [l.Point] : [])];
    }

    static bool Inside(Point[] poly, Point q)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if ((poly[i].Y > q.Y) != (poly[j].Y > q.Y) && q.X < (poly[j].X - poly[i].X) * (q.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)
                inside = !inside;
        return inside;
    }

    // Ikon tombol bentuk (kontur dalam kotak 0..1). Otomatis: bentuk bebas.
    public static Geometry Icon(CutShape shape)
    {
        if (shape == CutShape.Auto)
        {
            var blob = Geometry.Parse("M0.15,0.55 C0.05,0.2 0.45,0.05 0.6,0.2 C0.75,0.35 1,0.25 0.95,0.6 C0.9,0.95 0.5,0.95 0.4,0.8 C0.3,0.7 0.2,0.8 0.15,0.55 Z");
            blob.Freeze();
            return blob;
        }
        Node[] c = shape switch
        {
            CutShape.Rect => Box(0, 0.15, 1, 0.85),
            CutShape.RoundRect => RoundRect(0, 0.15, 1, 0.85, 0.22),
            CutShape.Circle => Ellipse(new Point(0.5, 0.5), 0.5, 0.5),
            CutShape.Oval => Ellipse(new Point(0.5, 0.5), 0.5, 0.33),
            CutShape.Triangle => [Node.Corner(new Point(0.5, 0.05)), Node.Corner(new Point(1, 0.92)), Node.Corner(new Point(0, 0.92))],
            CutShape.Star => Transform(Star(), new Point(0.5, 0.53), 0.5, 0.5),
            CutShape.Heart => Transform(Heart(), new Point(0.5, 0.47), 0.5, 0.5),
            CutShape.Hexagon => [.. Enumerable.Range(0, 6).Select(i => Node.Corner(new Point(0.5 + 0.5 * Math.Cos(i * Math.PI / 3), 0.5 + 0.5 * Math.Sin(i * Math.PI / 3))))],
            _ => [],
        };
        var g = c.Length == 0 ? Geometry.Empty : Geometry.Parse(Curves.PathOf([c], 0));
        g.Freeze();
        return g;
    }
}

public static partial class Loader
{
    // Pratinjau bentuk di editor: silhouette dari salinan layar desain (6 px/mm), offset garis yang sedang dipakai.
    public static (List<Node[]>? Kiss, List<Node[]>? Die) ShapeLines(Sticker s, CutShape shape)
    {
        var small = new TransformedBitmap(s.Preview, new ScaleTransform(s.ArtW * PxMm / s.Preview.PixelWidth, s.ArtH * PxMm / s.Preview.PixelHeight));
        return Shapes.Lines(Silhouette(small, out _, out _), shape, s.ArtW, s.ArtH, s.LoadedKiss, s.LoadedDie);
    }
}
