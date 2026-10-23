using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StickerLayout;

// Smart select di editor garis potong (spesifikasi 2026-10-22): klik objek, garis berjarak offset mengikuti bentuknya
// (lekukan diikuti, sudut luar bulat, tanpa Halus). Lubang yang lebih sempit dari offset dipotong lewat klik di dalam
// lubang. Garis otomatis (Load) tidak memakai kode ini.
public static partial class Loader
{
    // ponytail: titik awal kalibrasi (lingkaran potong terkecil ±1 mm); sesuaikan setelah uji potong Graphtec di operator.
    public const double MinLoopRadius = 0.5;   // ruang tersisa minimal di dalam garis dalam (mm)
    public const double MinHoleGap = 0.3;      // jarak minimal garis dalam ke tepi lubang (mm), atau offset bila lebih kecil

    public enum PickKind { Empty, Object, Hole }

    sealed record ObjGrid(bool[,] Obj, int Pad);
    sealed record Part(List<(int X, int Y)> Cells, bool Value, bool Edge);

    // Toleransi awal: alpha untuk gambar bertransparansi, beda warna (seperti Rapikan) untuk gambar opak.
    public static int DefaultTolerance(Sticker s) => HasAlpha(Pixels(s.Preview).px) ? 64 : 32;

    // Apa yang ada di bawah klik: objek, lubang (latar yang dikelilingi objek), atau area kosong.
    public static PickKind Classify(Sticker s, Point mm, int tol)
    {
        var g = Objects(s, tol);
        if (Cell(g, mm) is not (int x, int y)) return PickKind.Empty;
        if (g.Obj[x, y]) return PickKind.Object;
        var (lab, parts) = Label(g.Obj);
        return parts[lab[x, y] - 1].Edge ? PickKind.Empty : PickKind.Hole;
    }

    // Garis kiss/die (node, mm relatif artwork) dari objek yang diklik. holes = klik di dalam lubang: tiap klik membalik
    // keadaan lubang itu (lubang lebar otomatis dipotong, lubang sempit tidak). TooSmall: lubang yang diminta dipotong
    // terlalu kecil untuk garis dalam.
    // ponytail: grid dilabeli ulang setiap klik (stiker 300 mm ±0,2 detik); simpan cache per Toleransi bila terasa lambat.
    public static (List<Node[]> Kiss, List<Node[]> Die, bool TooSmall) SmartSelect(Sticker s, IReadOnlyList<Point> objects,
        IReadOnlyList<Point> holes, int tol, double eps)
    {
        var g = Objects(s, tol);
        var (lab, parts) = Label(g.Obj);
        var sel = new bool[g.Obj.GetLength(0), g.Obj.GetLength(1)];
        var taken = new HashSet<int>();
        void Take(int id) { if (taken.Add(id)) foreach (var (x, y) in parts[id - 1].Cells) sel[x, y] = true; }
        foreach (var p in objects)
            if (Cell(g, p) is (int x, int y) && g.Obj[x, y]) Take(lab[x, y]);
        foreach (var p in holes) // objek di sekeliling lubang yang diklik ikut terpilih
            if (Cell(g, p) is (int x, int y) && !g.Obj[x, y] && !parts[lab[x, y] - 1].Edge)
                foreach (var (hx, hy) in parts[lab[x, y] - 1].Cells)
                    foreach (var (u, v) in new[] { (hx - 1, hy), (hx + 1, hy), (hx, hy - 1), (hx, hy + 1) })
                        if (g.Obj[u, v]) Take(lab[u, v]);

        // Lubang objek terpilih: jarak tiap sel ke objek, keadaan awal, lalu klik membalik keadaan.
        Cut k = s.LoadedKiss, d = s.LoadedDie;
        double minR = MinLoopRadius * PxMm, minGap = MinHoleGap * PxMm, lineOff = (k.On ? k.Off : d.Off) * PxMm;
        var (slab, sparts) = Label(sel);
        var dist = new Dictionary<int, double[]>();
        var on = new Dictionary<int, bool>();
        for (int i = 0; i < sparts.Count; i++)
            if (!sparts[i].Value && !sparts[i].Edge)
            {
                dist[i] = HoleDistance(sel, sparts[i].Cells);
                on[i] = dist[i].Max() >= lineOff + minR;
            }
        foreach (var p in holes)
            if (Cell(g, p) is (int x, int y) && on.ContainsKey(slab[x, y] - 1)) on[slab[x, y] - 1] ^= true;

        bool tooSmall = false;
        bool[,]? kissMask = null, dieMask = null;
        var kissGap = new Dictionary<int, double>();
        if (k.On)
        {
            kissMask = Solid(Dilate(sel, (int)Math.Round(k.Off * PxMm)));
            foreach (var (i, cut) in on)
            {
                if (!cut) continue;
                double want = k.Off * PxMm, gap = Math.Min(want, dist[i].Max() - minR);
                if (gap < Math.Min(want, minGap)) { tooSmall = true; continue; }
                Carve(kissMask, sparts[i].Cells, dist[i], gap);
                kissGap[i] = gap;
            }
        }
        if (d.On)
        {
            dieMask = Solid(Dilate(sel, (int)Math.Round(d.Off * PxMm)));
            if (kissMask != null) { var near = Dilate(kissMask, 1); foreach (var (x, y) in Cells(dieMask)) dieMask[x, y] |= near[x, y]; }
            foreach (var (i, cut) in on)
            {
                if (!cut || kissMask != null && !kissGap.ContainsKey(i)) continue; // die tidak boleh berlubang di tempat kiss utuh
                double want = d.Off * PxMm, r = dist[i].Max(), gap = Math.Min(want, r - minR);
                if (kissGap.TryGetValue(i, out var kg)) gap = Math.Max(gap, kg + 1); // die di dalam garis dalam kiss
                if (gap < Math.Min(want, minGap) || r - gap < minR) { tooSmall |= kissMask == null; continue; } // lubang hanya di-kiss-cut
                Carve(dieMask, sparts[i].Cells, dist[i], gap);
            }
        }
        double off = g.Pad / PxMm;
        return (kissMask == null ? [] : Curves.Fit(Trace(kissMask), off, eps), dieMask == null ? [] : Curves.Fit(Trace(dieMask), off, eps), tooSmall);
    }

    // Objek pada grid 6 px/mm (sama dengan mask garis otomatis), dengan bantalan selebar offset terbesar + 3 px.
    static ObjGrid Objects(Sticker s, int tol)
    {
        var src = new TransformedBitmap(s.Preview, new ScaleTransform(s.ArtW * PxMm / s.Preview.PixelWidth, s.ArtH * PxMm / s.Preview.PixelHeight));
        var (px, w, h) = Pixels(src);
        bool alpha = HasAlpha(px);
        Color bg = alpha ? default : CleanImage.BorderColor(px, w, h, _ => false) ?? Colors.White;
        double reach = Math.Max(s.LoadedKiss.On ? s.LoadedKiss.Off : 0, s.LoadedDie.On ? s.LoadedDie.Off : 0);
        int pad = (int)Math.Ceiling(reach * PxMm) + 3;
        var m = new bool[w + 2 * pad, h + 2 * pad];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = (y * w + x) * 4;
                m[x + pad, y + pad] = alpha ? px[i + 3] > tol
                    : Math.Max(Math.Abs(px[i] - bg.B), Math.Max(Math.Abs(px[i + 1] - bg.G), Math.Abs(px[i + 2] - bg.R))) > tol;
            }
        return new(m, pad);
    }

    static bool HasAlpha(byte[] px)
    {
        for (int i = 3; i < px.Length; i += 4) if (px[i] < 255) return true;
        return false;
    }

    static (int X, int Y)? Cell(ObjGrid g, Point mm)
    {
        int x = g.Pad + (int)Math.Floor(mm.X * PxMm), y = g.Pad + (int)Math.Floor(mm.Y * PxMm);
        return x >= 0 && y >= 0 && x < g.Obj.GetLength(0) && y < g.Obj.GetLength(1) ? (x, y) : null;
    }

    // Komponen 4 arah: lab[x, y] = nomor komponen (mulai 1); tiap komponen menyimpan sel, nilai, dan apakah menyentuh tepi.
    static (int[,] Lab, List<Part> Parts) Label(bool[,] m)
    {
        int W = m.GetLength(0), H = m.GetLength(1);
        var lab = new int[W, H];
        var parts = new List<Part>();
        var st = new Stack<(int, int)>();
        for (int y0 = 0; y0 < H; y0++)
            for (int x0 = 0; x0 < W; x0++)
            {
                if (lab[x0, y0] != 0) continue;
                bool v = m[x0, y0], edge = false;
                int id = parts.Count + 1;
                var cells = new List<(int X, int Y)>();
                lab[x0, y0] = id;
                st.Push((x0, y0));
                while (st.Count > 0)
                {
                    var (x, y) = st.Pop();
                    cells.Add((x, y));
                    edge |= x == 0 || y == 0 || x == W - 1 || y == H - 1;
                    void Push(int u, int w) { if (u >= 0 && w >= 0 && u < W && w < H && lab[u, w] == 0 && m[u, w] == v) { lab[u, w] = id; st.Push((u, w)); } }
                    Push(x - 1, y); Push(x + 1, y); Push(x, y - 1); Push(x, y + 1);
                }
                parts.Add(new(cells, v, edge));
            }
        return (lab, parts);
    }

    // Jarak (px) tiap sel lubang ke piksel objek terpilih terdekat.
    // ponytail: O(luas × keliling) per lubang (lubang 30 mm ±14 juta langkah); ganti EDT linear bila lubang besar lambat.
    static double[] HoleDistance(bool[,] sel, List<(int X, int Y)> hole)
    {
        var rim = new HashSet<(int X, int Y)>();
        foreach (var (x, y) in hole)
            foreach (var (u, v) in new[] { (x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1) })
                if (sel[u, v]) rim.Add((u, v));
        var edge = rim.ToArray();
        var d = new double[hole.Count];
        for (int i = 0; i < hole.Count; i++)
        {
            double best = double.MaxValue;
            foreach (var (u, v) in edge) best = Math.Min(best, (hole[i].X - u) * (hole[i].X - u) + (hole[i].Y - v) * (hole[i].Y - v));
            d[i] = Math.Sqrt(best);
        }
        return d;
    }

    // Garis dalam: sel lubang yang berjarak > gap px dari objek dikosongkan, jadi garis berjarak gap dari tepi lubang.
    static void Carve(bool[,] cut, List<(int X, int Y)> hole, double[] dist, double gap)
    {
        for (int i = 0; i < hole.Count; i++) if (dist[i] > gap) cut[hole[i].X, hole[i].Y] = false;
    }
}
