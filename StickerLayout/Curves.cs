using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace StickerLayout;

// Node garis potong seperti di CorelDRAW (masukan 2026-10-23): titik P dengan handle In (sisi segmen sebelumnya) dan Out
// (sisi segmen berikutnya), mm relatif terhadap pojok kiri-atas artwork. Handle yang sama dengan P = sisi itu lurus.
// Smooth: handle In dan Out dijaga segaris saat salah satunya diseret.
public readonly record struct Node(Point P, Point In, Point Out, bool Smooth = false)
{
    public static Node Corner(Point p) => new(p, p, p);
}

public static class Curves
{
    // Segmen a -> b lurus bila kedua handle di sisi segmen itu sama dengan titiknya.
    public static bool Straight(Node a, Node b) => a.Out == a.P && b.In == b.P;

    public static int Count(List<Node[]>? lines) => lines?.Sum(c => c.Length) ?? 0;

    // Seberapa jauh garis keluar dari kotak artwork w x h (mm). Kurva Bezier selalu di dalam kerangka titik dan handle-nya.
    public static double Overhang(IEnumerable<Node[]> lines, double w, double h) => Math.Max(0, lines.SelectMany(c => c)
        .SelectMany(n => new[] { n.P, n.In, n.Out }).Select(q => Math.Max(Math.Max(-q.X, q.X - w), Math.Max(-q.Y, q.Y - h))).DefaultIfEmpty(0).Max());

    // Garis (mm relatif artwork) -> path M/L/C/Z di koordinat kotak (artwork digeser off mm). Garis lurus semua
    // menghasilkan path yang sama dengan polygon M/L/Z sebelumnya.
    public static string PathOf(List<Node[]> lines, double off)
    {
        var sb = new StringBuilder();
        void P(Point p) => sb.Append(F(p.X + off)).Append(' ').Append(F(p.Y + off));
        foreach (var c in lines)
        {
            sb.Append('M');
            P(c[0].P);
            for (int i = 0; i < c.Length; i++)
            {
                var (a, b) = (c[i], c[(i + 1) % c.Length]);
                if (!Straight(a, b))
                {
                    sb.Append('C'); P(a.Out); sb.Append(' '); P(b.In); sb.Append(' '); P(b.P);
                }
                else if (i < c.Length - 1) { sb.Append('L'); P(b.P); } // segmen penutup lurus = Z
            }
            sb.Append('Z');
        }
        return sb.ToString();
    }

    // Path kurva (koordinat kotak, mis. hasil Potrace) -> node, mm relatif artwork.
    public static List<Node[]> Parse(string path, double off)
    {
        var res = new List<Node[]>();
        if (path == "") return res;
        var v = new Vector(-off, -off);
        foreach (var f in PathGeometry.CreateFromGeometry(Geometry.Parse(path)).Figures)
        {
            var n = new List<Node> { Node.Corner(f.StartPoint + v) };
            void Line(Point q) => n.Add(Node.Corner(q + v));
            void Curve(Point c1, Point c2, Point q) { n[^1] = n[^1] with { Out = c1 + v }; n.Add(new Node(q + v, c2 + v, q + v)); }
            foreach (var seg in f.Segments)
                switch (seg)
                {
                    case LineSegment l: Line(l.Point); break;
                    case PolyLineSegment pl: foreach (var q in pl.Points) Line(q); break;
                    case BezierSegment b: Curve(b.Point1, b.Point2, b.Point3); break;
                    case PolyBezierSegment pb:
                        for (int i = 0; i + 2 < pb.Points.Count; i += 3) Curve(pb.Points[i], pb.Points[i + 1], pb.Points[i + 2]);
                        break;
                }
            if (n.Count > 1 && (n[^1].P - n[0].P).Length < 1e-9) { n[0] = n[0] with { In = n[^1].In }; n.RemoveAt(n.Count - 1); }
            for (int i = 0; i < n.Count; i++) n[i] = n[i] with { Smooth = Collinear(n[i]) };
            if (n.Count >= 2) res.Add([.. n]);
        }
        return res;
    }

    // Kedua handle ada dan segaris berlawanan arah (sambungan kurva mulus Potrace).
    static bool Collinear(Node n)
    {
        Vector a = n.In - n.P, b = n.Out - n.P;
        return a.Length > 1e-9 && b.Length > 1e-9 && Math.Abs(Vector.CrossProduct(a, b)) < 1e-6 * a.Length * b.Length && a * b < 0;
    }

    // Lebar desain berubah: garis diskala bersama artwork.
    public static List<Node[]>? Scale(List<Node[]>? lines, double k)
    {
        static Point S(Point q, double k) => new(q.X * k, q.Y * k);
        return lines?.Select(c => c.Select(n => n with { P = S(n.P, k), In = S(n.In, k), Out = S(n.Out, k) }).ToArray()).ToList();
    }

    // Kurva -> garis bertitik dengan toleransi eps mm (slider Simplify): kurva dipecah rapat (0,01 mm), lalu disederhanakan
    // Ramer-Douglas-Peucker. Titik selalu di kurva, jadi garis menyimpang dari kurva maksimal eps.
    public static List<Node[]> Simplify(string path, double off, double eps)
    {
        var res = new List<Node[]>();
        if (path == "") return res;
        foreach (var f in Geometry.Parse(path).GetFlattenedPathGeometry(0.01, ToleranceType.Absolute).Figures)
        {
            var p = new List<Point> { f.StartPoint };
            foreach (var seg in f.Segments)
                if (seg is PolyLineSegment pl) p.AddRange(pl.Points);
                else if (seg is LineSegment l) p.Add(l.Point);
            for (int i = 0; i < p.Count; i++) p[i] = new Point(p[i].X - off, p[i].Y - off);
            p.Add(p[0]); // kontur tertutup: RDP pertama memecah di titik terjauh dari titik awal
            var keep = new bool[p.Count];
            keep[0] = keep[^1] = true;
            var st = new Stack<(int, int)>();
            st.Push((0, p.Count - 1));
            while (st.Count > 0)
            {
                var (a, b) = st.Pop();
                int far = -1;
                double max = eps;
                for (int i = a + 1; i < b; i++)
                    if ((p[i] - Loader.Nearest(p[i], p[a], p[b])).Length is var d && d > max) (max, far) = (d, i);
                if (far < 0) continue;
                keep[far] = true;
                st.Push((a, far));
                st.Push((far, b));
            }
            var c = p.Where((_, i) => keep[i] && i < p.Count - 1).ToArray();
            int n = p.Count - 1;
            if (c.Length < 3 && n >= 3) c = [p[0], p[n / 3], p[2 * n / 3]]; // kontur kecil tidak boleh hilang (tidak terpotong)
            if (c.Length >= 3) res.Add([.. c.Select(Node.Corner)]);
        }
        return res;
    }

    // ---- Edit node seperti CorelDRAW ----

    // Titik di segmen a -> b pada parameter t.
    public static Point At(Node a, Node b, double t)
    {
        if (Straight(a, b)) return a.P + (b.P - a.P) * t;
        double u = 1 - t;
        return new Point(0, 0) + u * u * u * (Vector)a.P + 3 * u * u * t * (Vector)a.Out + 3 * u * t * t * (Vector)b.In + t * t * t * (Vector)b.P;
    }

    // Segmen terdekat ke q: indeks node awal, parameter t, dan jaraknya. Kurva: sampel 64 titik, lalu dipersempit.
    public static (int I, double T, double D) NearestSegment(Node[] c, Point q)
    {
        (int I, double T, double D) best = (-1, 0, double.MaxValue);
        for (int i = 0; i < c.Length; i++)
        {
            var (a, b) = (c[i], c[(i + 1) % c.Length]);
            double t;
            if (Straight(a, b))
            {
                var ab = b.P - a.P;
                t = ab.LengthSquared == 0 ? 0 : Math.Clamp((q - a.P) * ab / ab.LengthSquared, 0, 1);
            }
            else
            {
                const int N = 64;
                int k0 = 0;
                for (int k = 1; k <= N; k++) if ((At(a, b, (double)k / N) - q).Length < (At(a, b, (double)k0 / N) - q).Length) k0 = k;
                double lo = Math.Max(0, (k0 - 1.0) / N), hi = Math.Min(1, (k0 + 1.0) / N);
                for (int it = 0; it < 30; it++)
                {
                    double m1 = lo + (hi - lo) / 3, m2 = hi - (hi - lo) / 3;
                    if ((At(a, b, m1) - q).Length < (At(a, b, m2) - q).Length) hi = m2; else lo = m1;
                }
                t = (lo + hi) / 2;
            }
            double d = (At(a, b, t) - q).Length;
            if (d < best.D) best = (i, t, d);
        }
        return best;
    }

    // Node baru di segmen i pada parameter t. Kurva dibelah de Casteljau, jadi bentuk garis tidak berubah.
    public static Node[] Split(Node[] c, int i, double t)
    {
        int j = (i + 1) % c.Length;
        var (a, b) = (c[i], c[j]);
        c = (Node[])c.Clone();
        Node n;
        if (Straight(a, b)) n = Node.Corner(At(a, b, t));
        else
        {
            Point L(Point p, Point q) => p + (q - p) * t;
            Point q0 = L(a.P, a.Out), q1 = L(a.Out, b.In), q2 = L(b.In, b.P), r0 = L(q0, q1), r1 = L(q1, q2);
            c[i] = a with { Out = q0 };
            c[j] = b with { In = q2 };
            n = new Node(L(r0, r1), r0, r1, true);
        }
        return [.. c[..(i + 1)], n, .. c[(i + 1)..]];
    }

    // Jadikan kurva: kedua segmen lurus di sisi node i diberi handle di 1/3 panjangnya (bentuk belum berubah, handle siap
    // diseret). Jadikan garis: kedua segmen di sisi node i jadi lurus.
    public static Node[] ToCurve(Node[] c, int i)
    {
        c = (Node[])c.Clone();
        int h = (i - 1 + c.Length) % c.Length, j = (i + 1) % c.Length;
        if (Straight(c[h], c[i])) { var d = (c[i].P - c[h].P) / 3; c[h] = c[h] with { Out = c[h].P + d }; c[i] = c[i] with { In = c[i].P - d }; }
        if (Straight(c[i], c[j])) { var d = (c[j].P - c[i].P) / 3; c[i] = c[i] with { Out = c[i].P + d }; c[j] = c[j] with { In = c[j].P - d }; }
        return c;
    }

    public static Node[] ToLine(Node[] c, int i)
    {
        c = (Node[])c.Clone();
        int h = (i - 1 + c.Length) % c.Length, j = (i + 1) % c.Length;
        c[h] = c[h] with { Out = c[h].P };
        c[i] = c[i] with { In = c[i].P, Out = c[i].P, Smooth = false };
        c[j] = c[j] with { In = c[j].P };
        return c;
    }

    // Node halus: handle In dan Out disegariskan (panjang tetap); sisi yang lurus memakai arah ke node tetangganya.
    public static Node[] SetSmooth(Node[] c, int i, bool on)
    {
        c = (Node[])c.Clone();
        var n = c[i];
        Vector vin = n.In - n.P, vout = n.Out - n.P;
        if (!on || vin.Length < 1e-9 && vout.Length < 1e-9) { c[i] = n with { Smooth = false }; return c; } // dua sisi lurus: sudut
        Vector back = vin.Length > 1e-9 ? vin : c[(i - 1 + c.Length) % c.Length].P - n.P,
            fwd = vout.Length > 1e-9 ? vout : c[(i + 1) % c.Length].P - n.P;
        var d = fwd / fwd.Length - back / back.Length;
        if (d.Length < 1e-9) d = fwd;
        d.Normalize();
        c[i] = n with { In = vin.Length > 1e-9 ? n.P - d * vin.Length : n.In, Out = vout.Length > 1e-9 ? n.P + d * vout.Length : n.Out, Smooth = true };
        return c;
    }

    // Handle node diseret ke q (outSide = handle Out). Node halus: handle seberang ikut segaris, panjangnya tetap.
    public static Node MoveHandle(Node n, bool outSide, Point q)
    {
        n = outSide ? n with { Out = q } : n with { In = q };
        Vector moved = q - n.P, other = (outSide ? n.In : n.Out) - n.P;
        if (!n.Smooth || moved.Length < 1e-9 || other.Length < 1e-9) return n;
        var o = n.P - moved / moved.Length * other.Length;
        return outSide ? n with { In = o } : n with { Out = o };
    }

    // ---- Simplify: fit kurva Bezier ----

    // Kurva -> node kurva dengan galat maksimal eps mm (slider Simplify). Kurva dipecah tiap ±0,05 mm, lalu di-fit cubic Bezier
    // (Schneider, Graphics Gems 1990). Titik sudut garis (belokan > 45°, misalnya sudut kotak) tetap persis jadi node sudut;
    // bagian yang lurus dalam eps jadi segmen lurus. Node makin sedikit bila eps makin besar.
    public static List<Node[]> Fit(string path, double off, double eps)
    {
        var res = new List<Node[]>();
        if (path == "") return res;
        foreach (var f in Geometry.Parse(path).GetFlattenedPathGeometry(0.005, ToleranceType.Absolute).Figures)
        {
            var raw = new List<Point> { f.StartPoint };
            foreach (var seg in f.Segments)
                if (seg is PolyLineSegment pl) raw.AddRange(pl.Points);
                else if (seg is LineSegment l) raw.Add(l.Point);
            var q = new List<Point>();
            foreach (var r in raw.Select(r => new Point(r.X - off, r.Y - off)))
                if (q.Count == 0 || (r - q[^1]).Length > 1e-6) q.Add(r);
            if (q.Count > 1 && (q[^1] - q[0]).Length <= 1e-6) q.RemoveAt(q.Count - 1);
            if (q.Count < 3) continue;
            var (p, corners) = Resample(q, 0.05);
            res.Add(p.Count < 6 ? [.. q.Select(Node.Corner)] : FitClosed(p, corners, eps));
        }
        return res;
    }

    // Kontur tertutup -> titik berjarak ±step mm. Titik sudut asli (belokan > 45°) ikut persis dan dicatat indeksnya.
    static (List<Point> P, List<int> Corners) Resample(List<Point> q, double step)
    {
        int m = q.Count;
        var sharp = Enumerable.Range(0, m).Where(i => Math.Abs(Vector.AngleBetween(q[i] - q[(i - 1 + m) % m], q[(i + 1) % m] - q[i])) > 45).ToList();
        var cut = sharp.Count > 0 ? sharp : [0];
        var res = new List<Point>();
        var corners = new List<int>();
        for (int s = 0; s < cut.Count; s++)
        {
            int a = cut[s], b = s + 1 < cut.Count ? cut[s + 1] : cut[0] + m;
            var run = Enumerable.Range(a, b - a + 1).Select(i => q[i % m]).ToList();
            double len = 0;
            for (int i = 1; i < run.Count; i++) len += (run[i] - run[i - 1]).Length;
            int n = Math.Max(1, (int)Math.Round(len / step));
            if (sharp.Count > 0) corners.Add(res.Count);
            double want = 0, done = 0, ds = len / n;
            for (int i = 1, added = 0; i < run.Count && added < n; i++)
            {
                double d = (run[i] - run[i - 1]).Length;
                while (added < n && want <= done + d + 1e-12) { res.Add(run[i - 1] + (run[i] - run[i - 1]) * (d == 0 ? 0 : (want - done) / d)); want += ds; added++; }
                done += d;
            }
        }
        return (res, corners);
    }

    static Node[] FitClosed(List<Point> p, List<int> corners, double eps)
    {
        int n = p.Count, k = Math.Max(2, (int)Math.Round(0.3 / 0.05)); // arah garis diukur 0,3 mm ke depan dan ke belakang
        Point At(int i) => p[((i % n) + n) % n];
        var splits = corners.Count > 0 ? corners : [0, Enumerable.Range(0, n).MaxBy(i => (p[i] - p[0]).LengthSquared)];
        var segs = new List<(Point P0, Point P1, Point P2, Point P3, bool Line)>();
        var bound = new List<bool>(); // node awal tiap segmen adalah titik pecah (sudut atau awal kontur)
        for (int s = 0; s < splits.Count; s++)
        {
            int a = splits[s], b = s + 1 < splits.Count ? splits[s + 1] : splits[0] + n;
            var span = Enumerable.Range(a, b - a + 1).Select(At).ToArray();
            bool ca = corners.Contains(a % n), cb = corners.Contains(b % n);
            Vector t1 = ca ? At(a + k) - At(a) : At(a + k) - At(a - k), t2 = cb ? At(b - k) - At(b) : At(b - k) - At(b + k);
            t1.Normalize();
            t2.Normalize();
            int before = segs.Count;
            FitCubic(span, 0, span.Length - 1, t1, t2, eps, segs);
            for (int i = before; i < segs.Count; i++) bound.Add(i == before);
        }
        var res = new Node[segs.Count];
        for (int i = 0; i < segs.Count; i++)
        {
            var (prev, cur) = (segs[(i - 1 + segs.Count) % segs.Count], segs[i]);
            bool corner = bound[i] && corners.Count > 0;
            res[i] = new Node(cur.P0, prev.Line ? cur.P0 : prev.P2, cur.Line ? cur.P0 : cur.P1, !corner && !prev.Line && !cur.Line);
        }
        return res;
    }

    static void FitCubic(Point[] d, int first, int last, Vector t1, Vector t2, double eps, List<(Point, Point, Point, Point, bool)> outp)
    {
        // Bagian yang lurus dalam eps: segmen lurus.
        double chord = 0;
        for (int i = first + 1; i < last; i++) chord = Math.Max(chord, (d[i] - Loader.Nearest(d[i], d[first], d[last])).Length);
        if (chord <= eps || last - first < 3) { outp.Add((d[first], d[first], d[last], d[last], true)); return; }
        var u = Chord(d, first, last);
        var bz = Generate(d, first, last, u, t1, t2);
        var (err, split) = MaxError(d, first, last, bz, u);
        if (err <= eps) { outp.Add((bz[0], bz[1], bz[2], bz[3], false)); return; }
        if (err <= 4 * eps)
            for (int it = 0; it < 4; it++)
            {
                u = Reparameterize(d, first, last, u, bz);
                bz = Generate(d, first, last, u, t1, t2);
                (err, split) = MaxError(d, first, last, bz, u);
                if (err <= eps) { outp.Add((bz[0], bz[1], bz[2], bz[3], false)); return; }
            }
        var tc = d[split - 1] - d[split + 1];
        tc.Normalize();
        FitCubic(d, first, split, t1, tc, eps, outp);
        FitCubic(d, split, last, -tc, t2, eps, outp);
    }

    static double[] Chord(Point[] d, int first, int last)
    {
        var u = new double[last - first + 1];
        for (int i = first + 1; i <= last; i++) u[i - first] = u[i - first - 1] + (d[i] - d[i - 1]).Length;
        for (int i = 1; i < u.Length; i++) u[i] /= u[^1];
        return u;
    }

    static Point Bez(Point[] b, double t)
    {
        double s = 1 - t;
        return new Point(0, 0) + s * s * s * (Vector)b[0] + 3 * s * s * t * (Vector)b[1] + 3 * s * t * t * (Vector)b[2] + t * t * t * (Vector)b[3];
    }

    // Kontrol Bezier dengan arah singgung tetap: panjang handle dari kuadrat terkecil.
    static Point[] Generate(Point[] d, int first, int last, double[] u, Vector t1, Vector t2)
    {
        double c00 = 0, c01 = 0, c11 = 0, x0 = 0, x1 = 0;
        Point p0 = d[first], p3 = d[last];
        for (int i = 0; i < u.Length; i++)
        {
            double t = u[i], s = 1 - t, b0 = s * s * s, b1 = 3 * s * s * t, b2 = 3 * s * t * t, b3 = t * t * t;
            Vector a0 = t1 * b1, a1 = t2 * b2;
            c00 += a0 * a0; c01 += a0 * a1; c11 += a1 * a1;
            var tmp = d[first + i] - (new Point(0, 0) + (b0 + b1) * (Vector)p0 + (b2 + b3) * (Vector)p3);
            x0 += a0 * tmp; x1 += a1 * tmp;
        }
        double det = c00 * c11 - c01 * c01, seg = (p3 - p0).Length;
        double al = det == 0 ? 0 : (x0 * c11 - x1 * c01) / det, ar = det == 0 ? 0 : (c00 * x1 - c01 * x0) / det;
        if (al < 1e-6 * seg || ar < 1e-6 * seg) al = ar = seg / 3;
        return [p0, p0 + t1 * al, p3 + t2 * ar, p3];
    }

    static (double Err, int Split) MaxError(Point[] d, int first, int last, Point[] bz, double[] u)
    {
        double max = 0;
        int split = (first + last) / 2;
        for (int i = first + 1; i < last; i++)
            if ((Bez(bz, u[i - first]) - d[i]).Length is var e && e > max) (max, split) = (e, i);
        return (max, Math.Clamp(split, first + 1, last - 1));
    }

    // Satu langkah Newton-Raphson per titik: parameter t yang lebih dekat ke titik datanya.
    static double[] Reparameterize(Point[] d, int first, int last, double[] u, Point[] b)
    {
        var res = new double[u.Length];
        Point[] q1 = [new Point(0, 0) + 3 * (b[1] - b[0]), new Point(0, 0) + 3 * (b[2] - b[1]), new Point(0, 0) + 3 * (b[3] - b[2])];
        Point[] q2 = [new Point(0, 0) + 2 * (q1[1] - q1[0]), new Point(0, 0) + 2 * (q1[2] - q1[1])];
        for (int i = 0; i < u.Length; i++)
        {
            double t = u[i], s = 1 - t;
            Vector q = Bez(b, t) - d[first + i];
            Vector d1 = s * s * (Vector)q1[0] + 2 * s * t * (Vector)q1[1] + t * t * (Vector)q1[2], d2 = s * (Vector)q2[0] + t * (Vector)q2[1];
            double den = d1 * d1 + q * d2;
            res[i] = den == 0 ? t : Math.Clamp(t - q * d1 / den, 0, 1);
        }
        return res;
    }

    static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}
