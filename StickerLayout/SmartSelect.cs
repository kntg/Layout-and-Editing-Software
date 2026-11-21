using System.IO;
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

    // Pisah jadi item (permintaan 2026-11-14): satu file yang sudah berisi grid item terpisah dipecah menjadi beberapa desain,
    // supaya tiap item disusun sendiri dan jarak antar item (panel Garis potong > Jarak antar stiker) bisa diubah.
    // Hanya desain gambar (PNG/JPG/SVG/hasil Rapikan, atau PDF/AI yang ditempel sebagai gambar). Vektor tidak dipisah: salinan
    // vektornya menempel seluruh halaman, bukan satu item.
    // Dekat item = objek (piksel tidak transparan, atau piksel yang beda dari warna latar untuk gambar opak) yang bertetangga
    // 8 arah; item yang bersentuhan dihitung satu. Gelembung kecil (noise, titik huruf lepas) di bawah ambang luas dibuang.
    // Hasil: PNG tiap item (dipotong pas batasnya) dan lebar (mm) item itu pada skala desain asal, berurutan baris demi baris.
    public static bool CanSplit(Sticker s) => s.Pdf == null;

    public static List<(byte[] Png, double WidthMm)> Split(Sticker s)
    {
        if (!CanSplit(s)) throw new InvalidOperationException(Lang.T("SplitVector"));
        // Resolusi penuh: desain dipotong dari gambar asli (bukan salinan layar), supaya item hasil tetap tajam.
        byte[] raster = s.Edited ?? s.Raster;
        using var ms = new MemoryStream(raster);
        BitmapSource full = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        var (px, w, h) = Pixels(full);

        // Mask objek: piksel tidak transparan, atau untuk gambar opak piksel yang berbeda dari warna latar (tepi gambar).
        bool alpha = HasAlpha(px);
        int tol = alpha ? 16 : 32;
        Color bg = alpha ? default : CleanImage.BorderColor(px, w, h, _ => false) ?? Colors.White;
        var m = new bool[w, h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = (y * w + x) * 4;
                m[x, y] = alpha ? px[i + 3] > tol
                    : Math.Max(Math.Abs(px[i] - bg.B), Math.Max(Math.Abs(px[i + 1] - bg.G), Math.Abs(px[i + 2] - bg.R))) > tol;
            }

        var comps = Components8(m, w, h);
        if (comps.Count == 0) throw new InvalidOperationException(Lang.T("Empty"));
        // Gelembung kecil dibuang: di bawah 1/400 luas item terbesar (temuan kalibrasi awal; sesuaikan bila noise lolos atau item kecil hilang).
        int maxArea = comps.Max(c => c.Area);
        var keep = comps.Where(c => c.Area * 400 >= maxArea).ToList();
        if (keep.Count < 2) throw new InvalidOperationException(Lang.T("SplitOne"));

        // Urut baris demi baris: kelompokkan per baris bila tumpang tindih tegak, lalu kiri ke kanan.
        keep.Sort((a, b) => a.Y0 == b.Y0 ? a.X0.CompareTo(b.X0) : a.Y0.CompareTo(b.Y0));
        keep.Sort((a, b) =>
        {
            bool sameRow = a.Y0 <= b.Y1 && b.Y0 <= a.Y1 && Math.Min(a.Y1, b.Y1) - Math.Max(a.Y0, b.Y0) >= Math.Min(a.Y1 - a.Y0, b.Y1 - b.Y0) / 2;
            return sameRow ? a.X0.CompareTo(b.X0) : a.Y0.CompareTo(b.Y0);
        });

        // Lebar item pada skala desain asal: lebar kotak isi asli (s.PxW piksel) = s.WidthMm.
        double mmPerPx = s.WidthMm / Math.Max(1, s.PxW);
        var res = new List<(byte[], double)>();
        foreach (var c in keep)
        {
            int cw = c.X1 - c.X0 + 1, ch = c.Y1 - c.Y0 + 1;
            // Hanya piksel milik item ini yang disalin; item tetangga yang masuk kotak potong dibuat transparan,
            // supaya dua item yang berdekatan tidak saling muncul.
            var dst = new byte[cw * ch * 4];
            for (int y = 0; y < ch; y++)
                for (int x = 0; x < cw; x++)
                {
                    int sx = c.X0 + x, sy = c.Y0 + y;
                    if (c.Owner[sx, sy]) { int si = (sy * w + sx) * 4, di = (y * cw + x) * 4; dst[di] = px[si]; dst[di + 1] = px[si + 1]; dst[di + 2] = px[si + 2]; dst[di + 3] = px[si + 3]; }
                }
            var bmp = BitmapSource.Create(cw, ch, 96, 96, PixelFormats.Bgra32, null, dst, cw * 4);
            bmp.Freeze();
            res.Add((Png(bmp), Math.Max(1, cw * mmPerPx)));
        }
        return res;
    }

    // Komponen 8 arah dari mask w x h: kotak batas, luas, dan peta kepemilikan piksel per komponen (agar potongan tidak
    // membawa piksel item tetangga). Latar (false) diabaikan.
    sealed class Comp { public int X0, Y0, X1, Y1, Area; public bool[,] Owner = null!; }

    static List<Comp> Components8(bool[,] m, int w, int h)
    {
        var lab = new int[w, h];
        var comps = new List<Comp>();
        var st = new Stack<(int, int)>();
        for (int y0 = 0; y0 < h; y0++)
            for (int x0 = 0; x0 < w; x0++)
            {
                if (lab[x0, y0] != 0 || !m[x0, y0]) continue;
                int id = comps.Count + 1;
                var c = new Comp { X0 = x0, Y0 = y0, X1 = x0, Y1 = y0, Owner = new bool[w, h] };
                lab[x0, y0] = id;
                st.Push((x0, y0));
                while (st.Count > 0)
                {
                    var (x, y) = st.Pop();
                    c.Owner[x, y] = true;
                    c.Area++;
                    c.X0 = Math.Min(c.X0, x); c.X1 = Math.Max(c.X1, x);
                    c.Y0 = Math.Min(c.Y0, y); c.Y1 = Math.Max(c.Y1, y);
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int u = x + dx, v = y + dy;
                            if (u >= 0 && v >= 0 && u < w && v < h && lab[u, v] == 0 && m[u, v]) { lab[u, v] = id; st.Push((u, v)); }
                        }
                }
                comps.Add(c);
            }
        return comps;
    }
}
