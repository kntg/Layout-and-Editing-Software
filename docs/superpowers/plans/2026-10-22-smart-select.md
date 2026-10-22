# Smart Select Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Editor garis potong Printech mendapat smart select tanpa AI: klik objek membuat garis kiss/die yang mengikuti bentuknya dengan jarak offset, dan klik di dalam lubang menambah atau menghapus garis dalamnya.

**Architecture:** Mesin baru di `StickerLayout/SmartSelect.cs` sebagai bagian `partial` dari `Loader`, memakai ulang `Pixels`, `Solid`, `Dilate`, `Trace`, dan `ToPolygons`. Objek dipilih dari grid 6 px/mm (alpha atau beda warna dengan latar), dilebarkan sejauh offset, lalu lubang yang aktif "dilubangi" pada jarak yang muat. Hasilnya polygon manual yang sudah ada (`ManualKiss`/`ManualDie`), jadi Simpan, nesting, dan export tidak berubah. `CutEditWindow` mendapat kotak centang Smart select, slider Toleransi, dan `Pick()`.

**Tech Stack:** C# 13 / .NET 10 WPF, BitmapToVector (Potrace). Tes: selfcheck bawaan (`StickerLayout.exe --selfcheck <folder>`), skrip UI Automation (Windows PowerShell 5.1), validasi Python.

**Spec:** [docs/superpowers/specs/2026-10-22-smart-select-design.md](../specs/2026-10-22-smart-select-design.md)

## Global Constraints

- Tanpa AI dan tanpa dependensi baru.
- Garis otomatis (`Loader.Load` tanpa polygon manual) tidak berubah: S1–S12 lulus seperti sebelumnya dan `validasi.json` identik (abaikan kolom `loadMs`). Ambang alpha garis otomatis tetap > 16.
- Toleransi awal 64 untuk gambar bertransparansi, 32 untuk gambar tanpa transparansi. Ukuran beda warna sama dengan Rapikan: selisih terbesar di antara kanal R, G, B terhadap `CleanImage.BorderColor`.
- Lubang: keadaan awal dipotong bila jari-jari ruang R ≥ offset + 0,5 mm. Jarak garis dalam = min(offset, R − 0,5 mm), minimal min(offset, 0,3 mm). Klik di lubang membalik keadaannya. Die harus berada di dalam garis dalam kiss (minimal 1 px); bila tidak muat, lubang hanya di-kiss-cut.
- Hasil disimpan sebagai `Sticker.ManualKiss`/`ManualDie` yang sudah ada; tidak ada format baru.
- Build Release dan Debug 0 warning. Dokumen Bahasa Indonesia, markdownlint 0 masalah (config `MD013: false`).
- Uji UI tidak boleh menggerakkan mouse (PC dipakai user). Klik kanvas = NOT RUN kecuali user mengizinkan.
- Repo publik: file pngtree/customer dan zip operator tidak masuk git; informasi pribadi user tidak pernah ditulis.
- **Commit dan push hanya atas perintah user.** Langkah "Checkpoint" di bawah berarti: jalankan uji, catat hasil, jangan commit.

Path yang dipakai berulang:

- Repo: `C:\Users\skyne\OneDrive\Documents\Prototype Software Layout`
- Scratchpad sesi: `C:\Users\skyne\AppData\Local\Temp\claude\c--Users-skyne-OneDrive-Documents-Prototype-Software-Layout\4fb470fa-9084-4f86-82e9-d99ecda43ec6\scratchpad` (berisi `run-selfcheck.ps1`, `gen_e2e.py`, `e2e-template.md`, `mdl\.markdownlint-cli2.jsonc`, `donut-src\donut.png`)

---

### Task 1: Mesin smart select + selfcheck S13 (mesin)

**Files:**

- Create: `StickerLayout/SmartSelect.cs`
- Modify: `StickerLayout/Core.cs:81` (`public static class Loader` → `public static partial class Loader`)
- Modify: `StickerLayout/Cleanup.cs:52-70` (`BorderColor` jadi delegasi ke versi statis)
- Test: `StickerLayout/SelfCheck.cs` (S13 setelah baris `S(12, ...)`)

**Interfaces:**

- Consumes: `Loader.Pixels(BitmapSource) → (byte[] px, int w, int h)`, `Loader.Solid(bool[,])`, `Loader.Dilate(bool[,], int)`, `Loader.Cells(bool[,])`, `Loader.Trace(bool[,]) → string`, `Loader.ToPolygons(string path, double off, double eps) → List<Point[]>`, `Loader.PxMm = 6`, `Sticker.Preview/ArtW/ArtH/LoadedKiss/LoadedDie`.
- Produces:
  - `public enum Loader.PickKind { Empty, Object, Hole }`
  - `public static int Loader.DefaultTolerance(Sticker s)`
  - `public static Loader.PickKind Loader.Classify(Sticker s, Point mm, int tol)`
  - `public static (List<Point[]> Kiss, List<Point[]> Die, bool TooSmall) Loader.SmartSelect(Sticker s, IReadOnlyList<Point> objects, IReadOnlyList<Point> holes, int tol, double eps)` — titik dan polygon dalam mm relatif pojok kiri-atas artwork.
  - `public static Color? CleanImage.BorderColor(byte[] px, int W, int H, Func<int, bool> skip)`

- [ ] **Step 1: Simpan baseline validasi**

```bash
cp "C:/Users/skyne/OneDrive/Documents/Prototype Software Layout/Printech-TestData/03-exports/validasi/validasi.json" "C:/Users/skyne/AppData/Local/Temp/claude/c--Users-skyne-OneDrive-Documents-Prototype-Software-Layout/4fb470fa-9084-4f86-82e9-d99ecda43ec6/scratchpad/smart-baseline-validasi.json"
```

- [ ] **Step 2: Tulis S13 (mesin) yang gagal**

Di `StickerLayout/SelfCheck.cs`, sisipkan tepat setelah baris yang diakhiri
`+ $"export {mLines} garis lurus, {mCurves} kurva; editor {string.Join(", ", edLog)} ({(edOk ? "benar" : "SALAH")})");`:

```csharp

            // S13: smart select (spesifikasi 2026-10-22). Cincin dengan lubang sempit Ø 2,6 mm, lubang jarum Ø 1,2 mm,
            // takik 5 mm, watermark alpha 38 yang menempel di kiri bawah, dan pulau terpisah. Konten 391 px = 39,1 mm.
            var ssImg = Draw(dc =>
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(38, 255, 255, 255)), null, new Rect(0, 200, 160, 20));
                var ssCuts = new GeometryGroup();
                ssCuts.Children.Add(new EllipseGeometry(new Point(200, 150), 13, 13));
                ssCuts.Children.Add(new EllipseGeometry(new Point(140, 150), 6, 6));
                ssCuts.Children.Add(new RectangleGeometry(new Rect(175, 0, 50, 100)));
                dc.DrawGeometry(Brushes.DarkOrange, null, new CombinedGeometry(GeometryCombineMode.Exclude, new EllipseGeometry(new Point(200, 150), 100, 100), ssCuts));
                dc.DrawEllipse(Brushes.DarkOrange, null, new Point(370, 150), 20, 20);
            }, 400, 300);
            var ss = Png(ssImg, 39.1, kiss, die);
            static double Left(List<Point[]> p) => p.SelectMany(c => c).Min(q => q.X);
            static bool Holds(List<Point[]> p, Point q) => Geometry.Parse(Loader.PathOf(p, 0)).FillContains(q);
            Point ringP = new(26, 10), holeP = new(20, 10), pinP = new(14, 10), islandP = new(37, 10), notchP = new(20, 1.5), emptyP = new(2, 2);
            var sr1 = Loader.SmartSelect(ss, [ringP], [], 64, 0.2);
            var sr2 = Loader.SmartSelect(ss, [ringP], [holeP], 64, 0.2);
            var sr3 = Loader.SmartSelect(ss, [ringP], [holeP, new(20.05, 10.05)], 64, 0.2);
            var sr4 = Loader.SmartSelect(ss, [ringP, islandP], [], 64, 0.2);
            var sr5 = Loader.SmartSelect(ss, [ringP], [], 16, 0.2);
            var sr6 = Loader.SmartSelect(ss, [ringP], [pinP], 64, 0.2);
            var ssAuto = Loader.ToPolygons(ss.Kiss, ss.Off, 0.2);
            var ssInner = sr2.Kiss.MinBy(c => c.Max(q => q.X) - c.Min(q => q.X))!;
            double ssInnerW = ssInner.Max(q => q.X) - ssInner.Min(q => q.X);
            double ssOut = Geometry.Combine(Geometry.Parse(Loader.PathOf(sr2.Kiss, 0)), Geometry.Parse(Loader.PathOf(sr2.Die, 0)), GeometryCombineMode.Exclude, null).GetArea();
            var ssOpaque = Png(Draw(dc => { dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 400, 300)); dc.DrawEllipse(Brushes.DarkOrange, null, new Point(200, 150), 100, 100); }, 400, 300), 40, kiss, die);
            var sro = Loader.SmartSelect(ssOpaque, [new(26, 15)], [], Loader.DefaultTolerance(ssOpaque), 0.2);
            bool smartOk = sr1.Kiss.Count == 1 && sr1.Die.Count == 1 && Math.Abs(Left(sr1.Kiss) - 8) < 0.5 && !Holds(sr1.Kiss, notchP) && Holds(ssAuto, notchP)
                && sr2.Kiss.Count == 2 && sr2.Die.Count == 1 && ssInnerW is > 0.5 and < 1.6 && ssOut < 0.5
                && sr3.Kiss.Count == 1 && sr4.Kiss.Count == 2 && Left(sr5.Kiss) < Left(sr1.Kiss) - 5 && sr6.TooSmall && sr6.Kiss.Count == 1
                && Loader.Classify(ss, holeP, 64) == Loader.PickKind.Hole && Loader.Classify(ss, emptyP, 64) == Loader.PickKind.Empty
                && Loader.Classify(ss, ringP, 64) == Loader.PickKind.Object && Loader.DefaultTolerance(ss) == 64
                && Loader.DefaultTolerance(ssOpaque) == 32 && sro.Kiss.Count == 1 && Math.Abs(Left(sro.Kiss) - 8) < 0.5
                && Loader.Classify(ssOpaque, emptyP, 32) == Loader.PickKind.Empty;
            S(13, "smart select: objek, takik diikuti, watermark diabaikan, lubang sempit lewat klik, pulau, objek opak", smartOk,
                $"kiss kiri {Left(sr1.Kiss):F2} mm (Toleransi 64) / {Left(sr5.Kiss):F2} mm (16); kontur kiss {sr1.Kiss.Count}, +lubang {sr2.Kiss.Count} "
                + $"(garis dalam {ssInnerW:F2} mm, die {sr2.Die.Count}, kiss di luar die {ssOut:F2} mm2), klik lubang 2x {sr3.Kiss.Count}, +pulau {sr4.Kiss.Count}, "
                + $"lubang jarum terlalu kecil {sr6.TooSmall}; takik: smart memuat titik {Holds(sr1.Kiss, notchP)}, otomatis {Holds(ssAuto, notchP)}; "
                + $"opak: Toleransi {Loader.DefaultTolerance(ssOpaque)}, kiri {Left(sro.Kiss):F2} mm");
```

Ubah komentar skenario di `SelfCheck.cs` (baris `// Skenario uji garis potong S1-S9, S11, dan S12 ...`) menjadi `S1-S9 dan S11-S13`.

- [ ] **Step 3: Pastikan gagal**

Run: `cd StickerLayout && dotnet build -c Debug -nologo -v q 2>&1 | grep -E "error" | head -3`
Expected: error CS0117 / CS0234 karena `Loader.SmartSelect`, `Loader.PickKind`, `Loader.DefaultTolerance`, dan `Loader.Classify` belum ada.

- [ ] **Step 4: `Loader` jadi partial dan `BorderColor` statis**

Di `StickerLayout/Core.cs` ganti `public static class Loader` menjadi `public static partial class Loader`.

Di `StickerLayout/Cleanup.cs` ganti seluruh method `BorderColor()` (komentar di atasnya tetap) dengan:

```csharp
    // Warna latar = warna yang paling sering muncul di tepi gambar (dikelompokkan per 16 tingkat).
    public Color? BorderColor() => BorderColor(px, W, H, Clear);

    // Juga dipakai smart select (Loader) untuk gambar tanpa transparansi. skip: piksel yang tidak dihitung.
    public static Color? BorderColor(byte[] px, int W, int H, Func<int, bool> skip)
    {
        var bins = new Dictionary<int, (int n, long b, long g, long r)>();
        void Add(int x, int y)
        {
            int i = y * W + x;
            if (skip(i)) return;
            int key = (px[i * 4 + 2] >> 4) << 8 | (px[i * 4 + 1] >> 4) << 4 | px[i * 4] >> 4;
            var (n, b, g, r) = bins.GetValueOrDefault(key);
            bins[key] = (n + 1, b + px[i * 4], g + px[i * 4 + 1], r + px[i * 4 + 2]);
        }
        for (int x = 0; x < W; x++) { Add(x, 0); Add(x, H - 1); }
        for (int y = 1; y < H - 1; y++) { Add(0, y); Add(W - 1, y); }
        if (bins.Count == 0) return null;
        var t = bins.Values.MaxBy(v => v.n);
        return Color.FromRgb((byte)(t.r / t.n), (byte)(t.g / t.n), (byte)(t.b / t.n));
    }
```

- [ ] **Step 5: Tulis `StickerLayout/SmartSelect.cs`**

```csharp
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

    // Garis kiss/die (polygon, mm relatif artwork) dari objek yang diklik. holes = klik di dalam lubang: tiap klik membalik
    // keadaan lubang itu (lubang lebar otomatis dipotong, lubang sempit tidak). TooSmall: lubang yang diminta dipotong
    // terlalu kecil untuk garis dalam.
    // ponytail: grid dilabeli ulang setiap klik (stiker 300 mm ±0,2 detik); simpan cache per Toleransi bila terasa lambat.
    public static (List<Point[]> Kiss, List<Point[]> Die, bool TooSmall) SmartSelect(Sticker s, IReadOnlyList<Point> objects,
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
        return (kissMask == null ? [] : ToPolygons(Trace(kissMask), off, eps), dieMask == null ? [] : ToPolygons(Trace(dieMask), off, eps), tooSmall);
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
```

- [ ] **Step 6: Build dan jalankan selfcheck Debug**

Run:

```bash
cd "C:/Users/skyne/OneDrive/Documents/Prototype Software Layout/StickerLayout" && dotnet build -c Debug -nologo -v q 2>&1 | grep -E "error|Warn|Error"
```

Expected: `0 Warning(s)` dan `0 Error(s)`.

Run (PowerShell):

```powershell
$sp = 'C:\Users\skyne\AppData\Local\Temp\claude\c--Users-skyne-OneDrive-Documents-Prototype-Software-Layout\4fb470fa-9084-4f86-82e9-d99ecda43ec6\scratchpad'; $exe = 'C:\Users\skyne\OneDrive\Documents\Prototype Software Layout\StickerLayout\bin\Debug\net10.0-windows\StickerLayout.exe'; $d = "$sp\sc-smart"; Remove-Item $d -Recurse -Force -ErrorAction SilentlyContinue; $p = Start-Process $exe -ArgumentList '--selfcheck', "`"$d`"" -Wait -PassThru; "exit $($p.ExitCode)"; Get-Content "$d\selfcheck.txt" | Select-String '^S1[123] |^GAGAL|^OK' | ForEach-Object { $_.Line }
```

Expected: `S13 LULUS` dengan kontur kiss 1 / +lubang 2 / klik lubang 2x 1 / +pulau 2, `lubang jarum terlalu kecil True`, `takik: smart memuat titik False, otomatis True`, kiss kiri ±8 mm (64) dan < 3 mm (16); `S11` dan `S12` tetap LULUS. Exit 0 bila S9 tidak tersandung beban CPU (lihat catatan S9 di UJI-GARIS-POTONG).

- [ ] **Step 7: Checkpoint** — catat baris S13. Jangan commit.

---

### Task 2: Editor (kotak Smart select, Toleransi, klik) + S13 (editor) + uji UI

**Files:**

- Modify: `StickerLayout/CutEditWindow.xaml` (setelah blok Kompleks/Sederhana)
- Modify: `StickerLayout/CutEditWindow.xaml.cs`
- Modify: `StickerLayout/Strings.cs` (setelah `["CutSaved"]`)
- Modify: `StickerLayout/SelfCheck.cs` (blok S13)
- Modify: `Printech-TestData/ui-editor-garis.ps1`

**Interfaces:**

- Consumes: semua yang diproduksi Task 1.
- Produces: `internal void CutEditWindow.Pick(Point mm, bool add)`; kontrol `SmartBox` (CheckBox), `Tol` (Slider 0–255), `TolText`.

- [ ] **Step 1: Tulis bagian editor S13 yang gagal**

Di `SelfCheck.cs`, sisipkan sebelum `S(13, ...)`:

```csharp
            var se = new CutEditWindow(ss, null, null, "smart");
            bool seTol = se.Tol.Value == 64;
            se.SmartBox.IsChecked = true;
            se.Pick(ringP, false);
            int se1 = se.ManualKiss?.Count ?? -1;
            se.Pick(holeP, false);
            int se2 = se.ManualKiss?.Count ?? -1;
            se.Undo();
            int se3 = se.ManualKiss?.Count ?? -1;
            se.Redo();
            se.Pick(emptyP, false);
            int se4 = se.ManualKiss?.Count ?? -1;
            double seL64 = Left(se.ManualKiss!);
            se.Tol.Value = 16;
            double seL16 = Left(se.ManualKiss!);
            se.Pick(islandP, true);
            int se5 = se.ManualKiss?.Count ?? -1;
            bool edSmart = seTol && se1 == 1 && se2 == 2 && se3 == 1 && se4 == 2 && seL16 < seL64 - 5 && se5 == 3;
```

Lalu ubah `S(13, ...)`: kondisi `smartOk` menjadi `smartOk && edSmart`, dan tambahkan di akhir string detail:

```csharp
                + $"; editor {se1}/{se2}/{se3}/{se4}/{se5} kontur, kiri {seL64:F2} -> {seL16:F2} mm, Toleransi awal {(seTol ? 64 : -1)} ({(edSmart ? "benar" : "SALAH")})");
```

(baris `+ $"opak: ..."` sebelumnya kehilangan `);` di akhir dan diakhiri `"` saja.)

- [ ] **Step 2: Pastikan gagal**

Run: `cd StickerLayout && dotnet build -c Debug -nologo -v q 2>&1 | grep -E "error" | head -3`
Expected: error CS1061 karena `CutEditWindow.Tol`, `SmartBox`, dan `Pick` belum ada.

- [ ] **Step 3: String**

Di `StickerLayout/Strings.cs`, setelah baris `["CutSaved"] = ...`:

```csharp
        ["Smart"] = ("Smart select (klik objek)", "Smart select (click object)"),
        ["SmartHint"] = ("Klik objek: garis mengikuti bentuknya. Shift+klik: tambah objek. Klik di dalam lubang: tambah atau hapus garis dalamnya. Toleransi tinggi mengabaikan piksel tipis seperti watermark.",
                         "Click an object: the lines follow its shape. Shift+click: add an object. Click inside a hole: add or remove its inner line. A high tolerance ignores faint pixels such as watermarks."),
        ["SmartTol"] = ("Toleransi", "Tolerance"),
        ["SmartEmpty"] = ("Tidak ada objek di titik ini.", "No object at this point."),
        ["SmartSmall"] = ("Lubang terlalu kecil untuk dipotong.", "The hole is too small to cut."),
```

- [ ] **Step 4: XAML**

Di `StickerLayout/CutEditWindow.xaml`, sisipkan setelah `</DockPanel>` milik baris Kompleks/Sederhana (sebelum `<TextBlock Text="{DynamicResource CutHint}" ...`):

```xml

                <CheckBox x:Name="SmartBox" Content="{DynamicResource Smart}" Margin="0,20,0,0" AutomationProperties.Name="{DynamicResource Smart}"/>
                <TextBlock Text="{DynamicResource SmartHint}" Style="{StaticResource Small}" TextWrapping="Wrap" Margin="0,6,0,0"/>
                <DockPanel Margin="0,12,0,0">
                    <TextBlock x:Name="TolText" DockPanel.Dock="Right" TextAlignment="Right" Typography.NumeralAlignment="Tabular"/>
                    <TextBlock Text="{DynamicResource SmartTol}" Style="{StaticResource Label}"/>
                </DockPanel>
                <Slider x:Name="Tol" Minimum="0" Maximum="255" Value="64" SmallChange="1" LargeChange="8" Margin="0,6,0,0"
                        ValueChanged="Tol_Changed" AutomationProperties.Name="{DynamicResource SmartTol}"/>
```

- [ ] **Step 5: Code-behind**

Di `StickerLayout/CutEditWindow.xaml.cs`:

- (1) Ganti deklarasi `State`:

```csharp
    sealed record State(List<Point[]> Kiss, List<Point[]> Die, bool Auto, Point[] Objs, Point[] Holes);
```

- (2) Setelah baris `double scale = 1, ox, oy;` tambahkan:

```csharp
    List<Point> objs = [], holes = [];  // pilihan smart select: klik objek dan klik lubang (mm relatif artwork)
```

- (3) Di konstruktor, sebelum `ready = true;`:

```csharp
        Tol.Value = Loader.DefaultTolerance(auto);
```

- (4) Ganti `Snap()`:

```csharp
    State Snap() => new(Copy(kiss), Copy(die), isAuto, [.. objs], [.. holes]);
```

- (5) Di `Step(...)`, ganti `(kiss, die, isAuto) = from[^1];` dengan:

```csharp
        (kiss, die, isAuto, var o, var h) = from[^1];
        (objs, holes) = ([.. o], [.. h]);
```

- (6) Di `Reset()`, setelah `Push(Snap());` tambahkan `objs = []; holes = [];`.

- (7) Di `Eps_Changed`, setelah baris `if (merging != tag) { Push(Snap()); merging = tag; }` tambahkan:

```csharp
        if (objs.Count + holes.Count > 0) { Select(objs, holes); return; } // pilihan smart select disederhanakan ulang
```

- (8) Di `Changed()`, setelah baris `EpsText.Text = ...` tambahkan `TolText.Text = $"{Tolerance}";`.

- (9) Ganti `Board_Down`:

```csharp
    void Board_Down(object s, MouseButtonEventArgs e)
    {
        if (SmartBox.IsChecked == true) { Pick(Mm(e.GetPosition(Board)), Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)); return; }
        Press(Mm(e.GetPosition(Board)), 7 / scale);
        if (dragging) Board.CaptureMouse();
    }
```

- (10) Tambahkan setelah `Reset()`:

```csharp
    int Tolerance => (int)Tol.Value;

    // Smart select: klik objek mengganti pilihan (add = Shift+klik, menambah objek); klik di lubang membalik garis dalamnya.
    internal void Pick(Point q, bool add)
    {
        var kind = Loader.Classify(auto, q, Tolerance);
        if (kind == Loader.PickKind.Empty) { Info.Text = Lang.T("SmartEmpty"); return; }
        bool fresh = kind == Loader.PickKind.Object && !add;
        List<Point> o = fresh ? [] : [.. objs], h = fresh ? [] : [.. holes];
        (kind == Loader.PickKind.Object ? o : h).Add(q);
        var before = Snap();
        if (Select(o, h)) Push(before);
    }

    // Garis dari objek dan lubang terpilih; false (keadaan lama tetap) bila pilihan tidak menghasilkan garis.
    bool Select(List<Point> o, List<Point> h)
    {
        var (k, d, tooSmall) = Loader.SmartSelect(auto, o, h, Tolerance, Eps.Value);
        if (auto.LoadedKiss.On && k.Count == 0 || auto.LoadedDie.On && d.Count == 0) { Info.Text = Lang.T("SmartEmpty"); return false; }
        (objs, holes) = (o, h);
        if (auto.LoadedKiss.On) kiss = k;
        if (auto.LoadedDie.On) die = d;
        sel = null;
        isAuto = false;
        Changed();
        if (tooSmall) Info.Text = Lang.T("SmartSmall");
        return true;
    }

    // Toleransi menghitung ulang pilihan smart select; geser berturut-turut = satu langkah undo.
    void Tol_Changed(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready) return;
        TolText.Text = $"{Tolerance}";
        if (objs.Count + holes.Count == 0) return;
        var before = Snap();
        if (Select(objs, holes) && merging != "tol") { Push(before); merging = "tol"; }
    }
```

- [ ] **Step 6: Build dan selfcheck Debug**

Run: perintah build dan selfcheck dari Task 1 Step 6.
Expected: 0 warning, `S13 LULUS` dengan `editor 1/2/1/2/3 kontur`, `kiri ±8 -> < 3 mm`, `Toleransi awal 64 (benar)`.

- [ ] **Step 7: Uji UI tanpa mouse**

Di `Printech-TestData/ui-editor-garis.ps1` (simpan tetap UTF-8 dengan BOM), sisipkan sebelum baris `Invoke (Find $AE::NameProperty 'Simpan' $ed)`:

```powershell
$smart = Find $AE::AutomationIdProperty 'SmartBox' $ed
$tg = $smart.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
$tg.Toggle(); Start-Sleep -Milliseconds 200
$state = "$($tg.Current.ToggleState)"
$tolv = (Find $AE::AutomationIdProperty 'Tol' $ed).GetCurrentPattern([System.Windows.Automation.RangeValuePattern]::Pattern).Current.Value
$tg.Toggle()
Res 'EDIT-04' $(if ($state -eq 'On' -and $tolv -eq 64) { 'LULUS' } else { 'GAGAL' }) "Smart select: kotak centang $state, Toleransi awal $tolv (harapan On, 64 untuk PNG transparan); klik di kanvas butuh mouse (NOT RUN)"

```

Ganti label `'EDIT-04'` lama (Simpan) menjadi `'EDIT-05'` dan `'EDIT-05'` lama (Edit > Urungkan) menjadi `'EDIT-06'`; perbarui baris komentar di kepala skrip ("... Urungkan/Ulangi editor, kotak Smart select dan Toleransi, Simpan ...").

Run (Release dulu dibangun: `dotnet build -c Release -nologo -v q`):

```powershell
$sp = 'C:\Users\skyne\AppData\Local\Temp\claude\c--Users-skyne-OneDrive-Documents-Prototype-Software-Layout\4fb470fa-9084-4f86-82e9-d99ecda43ec6\scratchpad'; $r = 'C:\Users\skyne\OneDrive\Documents\Prototype Software Layout'; New-Item -ItemType Directory -Force "$sp\ui-smart" | Out-Null; powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$r\Printech-TestData\ui-editor-garis.ps1" -Exe "$r\StickerLayout\bin\Release\net10.0-windows\StickerLayout.exe" -In "$r\Printech-TestData\01-input" -Shots "$sp\ui-smart" 2>&1 | Tee-Object -FilePath "$sp\ui-smart\ui-editor-garis.txt"
```

Expected: `RINGKAS: 6 uji, 0 GAGAL`. Lihat `editor-garis-potong.png` di folder shots: kotak Smart select dan slider Toleransi tampil rapi di panel kiri.

- [ ] **Step 8: Checkpoint** — catat hasil. Jangan commit.

---

### Task 3: Regresi dan file donat user

**Files:**

- Create: `C:\Users\skyne\AppData\Local\Temp\claude\c--Users-skyne-OneDrive-Documents-Prototype-Software-Layout\4fb470fa-9084-4f86-82e9-d99ecda43ec6\scratchpad\donut-ana\smart.ps1` (di luar repo)
- Create: `Printech-TestData/04-evidence/2026-10-22-smart-select.txt`

**Interfaces:**

- Consumes: build Release dari Task 2; `Loader.SmartSelect`, `Loader.Classify`.
- Produces: bukti regresi dan bukti file nyata.

- [ ] **Step 1: Selfcheck runner Release dan Debug**

```powershell
$sp = 'C:\Users\skyne\AppData\Local\Temp\claude\c--Users-skyne-OneDrive-Documents-Prototype-Software-Layout\4fb470fa-9084-4f86-82e9-d99ecda43ec6\scratchpad'; $r = 'C:\Users\skyne\OneDrive\Documents\Prototype Software Layout'; foreach ($c in 'Release', 'Debug') { & "$sp\run-selfcheck.ps1" -Repo $r -Config $c -Work "$sp\sc-$c" 2>&1 | Select-String 'BENAR|SALAH|RINGKAS|pesan gagal' }
```

Expected: Release `RINGKAS: 4/4` dengan 17/17 skenario. Debug: 17/17 kecuali S9 bisa tersandung beban CPU; bila S9 gagal, catat waktunya dan beban CPU (`(Get-CimInstance Win32_Processor).LoadPercentage`), jangan ubah batasnya.

- [ ] **Step 2: Validasi identik**

```powershell
Set-Location 'C:\Users\skyne\OneDrive\Documents\Prototype Software Layout\Printech-TestData'; pwsh -NoProfile -File .\validasi.ps1 2>&1 | Select-Object -Last 2; python validasi.py 2>&1 | Select-String 'GAGAL:|RINGKAS'
```

```bash
python -I -c "
import json,sys
def strip(o):
    if isinstance(o,dict): return {k:strip(v) for k,v in o.items() if k!='loadMs'}
    if isinstance(o,list): return [strip(v) for v in o]
    return o
a,b=(strip(json.load(open(p,encoding='utf-8'))) for p in sys.argv[1:])
print('IDENTIK' if a==b else 'BERBEDA')" "C:/Users/skyne/AppData/Local/Temp/claude/c--Users-skyne-OneDrive-Documents-Prototype-Software-Layout/4fb470fa-9084-4f86-82e9-d99ecda43ec6/scratchpad/smart-baseline-validasi.json" "C:/Users/skyne/OneDrive/Documents/Prototype Software Layout/Printech-TestData/03-exports/validasi/validasi.json"
```

Expected: `IDENTIK`; `validasi.py` hanya A2b, A4, A5 gagal (sama seperti sebelumnya). `validasi.py` menulis `04-evidence/2026-10-22-validasi-*`; karena garis otomatis identik dengan bukti 2026-10-21, pindahkan file-file 2026-10-22 itu ke `...\scratchpad\val-smart\` (bukan bukti baru) dan catat `IDENTIK` di bukti smart select (Step 4).

- [ ] **Step 3: File donat user (tidak masuk repo)**

Tulis `...\scratchpad\donut-ana\smart.ps1`:

```powershell
param([string]$Bin, [string]$Png)
# Smart select pada donat pngtree milik user (lebar 50 mm, kiss 2/1, die mati), dibandingkan garis otomatis.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase
[System.Runtime.InteropServices.NativeLibrary]::Load("$Bin\runtimes\win-x64\native\pdfium.dll") | Out-Null
foreach ($d in 'BitmapToVector', 'PdfSharp', 'SharpVectors.Rendering.Wpf', 'StickerLayout') { [Reflection.Assembly]::LoadFrom("$Bin\$d.dll") | Out-Null }
$k = [StickerLayout.Cut]::new($true, 2, 1); $none = [StickerLayout.Cut]::new($false, 0, 0)
$s = [StickerLayout.Loader]::Load($Png, 0, 50, $k, $none, $false, $null, 0)
$mm = 50 / 1141   # mm per px gambar asli (konten x 21-1161)
$hole = [System.Windows.Point]::new((612 - 21) * $mm, (620 - 22) * $mm)
$body = [System.Windows.Point]::new($hole.X - 8, $hole.Y)
$tol = [StickerLayout.Loader]::DefaultTolerance($s)
"Toleransi awal $tol; klik badan: $([StickerLayout.Loader]::Classify($s, $body, $tol)); klik tengah: $([StickerLayout.Loader]::Classify($s, $hole, $tol))"
$auto = [StickerLayout.Loader]::ToPolygons($s.Kiss, $s.Off, 0.2)
function W($p) { $xs = $p | ForEach-Object { $_ } | ForEach-Object { $_.X }; [math]::Round(($xs | Measure-Object -Maximum).Maximum - ($xs | Measure-Object -Minimum).Minimum, 2) }
$r1 = [StickerLayout.Loader]::SmartSelect($s, [System.Windows.Point[]]@($body), [System.Windows.Point[]]@(), $tol, 0.2)
$r2 = [StickerLayout.Loader]::SmartSelect($s, [System.Windows.Point[]]@($body), [System.Windows.Point[]]@($hole), $tol, 0.2)
"otomatis: $($auto.Count) kontur, lebar $(W $auto) mm"
"smart klik badan: $($r1.Item1.Count) kontur, lebar $(W $r1.Item1) mm"
"smart + klik tengah: $($r2.Item1.Count) kontur, terlalu kecil $($r2.Item3); lebar garis dalam $(W ($r2.Item1 | Sort-Object { ($_ | Measure-Object X -Maximum).Maximum - ($_ | Measure-Object X -Minimum).Minimum } | Select-Object -First 1)) mm"
```

Run:

```bash
SP="C:/Users/skyne/AppData/Local/Temp/claude/c--Users-skyne-OneDrive-Documents-Prototype-Software-Layout/4fb470fa-9084-4f86-82e9-d99ecda43ec6/scratchpad"; cd "$SP/donut-ana" && pwsh -NoProfile -File smart.ps1 -Bin "C:/Users/skyne/OneDrive/Documents/Prototype Software Layout/StickerLayout/bin/Release/net10.0-windows" -Png "$SP/donut-src/donut.png"
```

Expected: Toleransi awal 64; badan `Object`, tengah `Hole`; otomatis 1 kontur ±49,5 mm; smart klik badan 1 kontur, lebih sempit dari otomatis (watermark tidak ikut); smart + klik tengah 2 kontur, `terlalu kecil False`, garis dalam ±1 mm. Bila `Classify` badan bukan `Object`, geser titik badan (misalnya `-6` mm) dan catat titik yang dipakai.

- [ ] **Step 4: Bukti**

Tulis `Printech-TestData/04-evidence/2026-10-22-smart-select.txt` (UTF-8, LF): kepala (tanggal, build, mesin tanpa mouse), baris `S13` dari selfcheck Release, ringkasan runner Release/Debug, hasil `IDENTIK` validasi, keluaran `smart.ps1` (sebut file pngtree tidak masuk repo), dan keluaran `ui-editor-garis.ps1`. Salin `editor-garis-potong.png` dari Step 7 Task 2 ke `Printech-TestData/04-evidence/2026-10-22-editor-smart-select.png`.

- [ ] **Step 5: Checkpoint** — jangan commit.

---

### Task 4: Dokumen

**Files:**

- Modify: `docs/PRD.md`, `docs/UJI-GARIS-POTONG.md`, `docs/superpowers/specs/2026-10-22-smart-select-design.md`, `Printech-TestData/README.md`, `Printech-TestData/02-expected/expected-results.md`
- Modify (sumber di scratchpad): `e2e-template.md`, `gen_e2e.py` → regenerate `docs/testing/e2e-validation.md` dan `docs/testing/e2e-test-results-template.csv`

**Interfaces:**

- Consumes: angka dari Task 1–3.
- Produces: dokumen yang cocok dengan kode.

- [ ] **Step 1: PRD**

- Tabel fitur, setelah baris F23: `| F24 | Smart select di editor garis potong (kotak centang di panel kiri): klik objek, garis kiss dan die mengikuti bentuknya dengan jarak offset (lekukan diikuti, sudut luar bulat); Shift+klik menambah objek; klik di dalam lubang menambah atau menghapus garis dalam, termasuk lubang yang lebih sempit dari offset; slider Toleransi mengabaikan piksel tipis seperti watermark. Tanpa AI | Ada (2026-10-22) |`
- Keputusan, setelah blok bullet "Editor garis potong: polygon dengan titik" (beserta sub-bullet-nya), sisipkan:

```markdown
- **Smart select tanpa AI** (2026-10-22, [spesifikasi](superpowers/specs/2026-10-22-smart-select-design.md)). Klik objek di editor garis potong: objek = piksel dengan alpha > Toleransi (awal 64) pada gambar bertransparansi, atau piksel yang beda warnanya dengan warna tepi gambar > Toleransi (awal 32, ukuran sama dengan Rapikan) pada gambar opak, yang tersambung dengan titik klik. Garis = objek dilebarkan sejauh offset tanpa Halus, jadi lekukan diikuti dan sudut luar membulat. Lubang objek yang lebar (jari-jari ruang ≥ offset + 0,5 mm) langsung dapat garis dalam; lubang yang lebih sempit dipotong lewat klik di dalam lubang dengan jarak min(offset, jari-jari ruang − 0,5 mm), minimal min(offset, 0,3 mm). Die harus berada di dalam garis dalam kiss; bila tidak muat, lubang hanya di-kiss-cut. Hasilnya polygon manual biasa. Garis otomatis sengaja tidak berubah: lubang huruf berukuran mirip lubang donat (P dan R pada teks setinggi 8,8 mm: jari-jari ruang 1,04–1,14 mm; donat uji 1,29 mm), jadi memotong semua lubang otomatis akan membuat lingkaran potong ±1 mm di setiap huruf. Angka 0,5 dan 0,3 mm dikalibrasi di Graphtec operator.
```

- Batasan: `Smart select membaca transparansi atau beda warna dengan latar; objek di foto berlatar ramai belum bisa dipilih. Klik di kanvas belum diuji dengan mouse sungguhan (logikanya diuji selfcheck S13).`
- Checklist operator: sisipkan sebelum butir terakhir `Buka Edit garis potong pada stiker donat berwatermark, centang Smart select, klik donat lalu klik lubang tengahnya. Apakah garis luar mengabaikan watermark, dan lubang tengah terpotong rapi di Graphtec?`, lalu nomori ulang butir terakhir.

- [ ] **Step 2: UJI-GARIS-POTONG.md** — tambah baris `S13` setelah `S12` (skenario dari Task 1 Step 2, kriteria dari `smartOk && edSmart`, cara Otomatis, hasil angka baris S13 Release).

- [ ] **Step 3: Spesifikasi** — baris Status: `... disetujui user per bagian dan sebagai dokumen 2026-10-22; implementasi selesai 2026-10-22 (selfcheck S13, uji UI EDIT-04).`

- [ ] **Step 4: Test data** — `Printech-TestData/README.md`: deskripsi `ui-editor-garis.ps1` menyebut kotak Smart select dan slider Toleransi. `expected-results.md`: baris hasil `Smart select (2026-10-22)` dengan keluaran `smart.ps1` (file pngtree tidak masuk repo) dan tautan bukti.

- [ ] **Step 5: e2e** — di `gen_e2e.py` (scratchpad), tambah tuple berikut. Kolom catatan CUT-15 diisi angka baris S13 Release yang sebenarnya.

Sebelum baris `("NEST-01",` (baris tuple diawali 4 spasi):

```python
    ("CUT-15", "Garis potong", "Otomatis", "Cincin berlubang sempit Ø 2,6 mm, lubang jarum Ø 1,2 mm, takik 5 mm, watermark alpha 38, pulau; gambar opak berlatar putih", "Kiss 2/1, die 4/2", "selfcheck S13: SmartSelect/Classify langsung dan lewat editor tanpa mouse", "Watermark tidak ikut pada Toleransi 64; takik diikuti; lubang sempit tertutup sampai diklik; klik kedua menghapus; lubang jarum ditolak; die membungkus kiss; Shift+klik menambah pulau; gambar opak memilih objek", "selfcheck S13", "Mayor", "Spesifikasi smart select 2026-10-22; PRD F24", "PASS", "<isi dengan detail baris S13 Release>"),
```

Sebelum baris `("EXP-01",` (baris tuple diawali 4 spasi):

```python
    ("EDIT-15", "Edit layout", "Manual UI (otomatis UIA)", "png-transparent.png", "Release; die diaktifkan", "ui-editor-garis.ps1: centang Smart select, baca Toleransi awal", "Kotak centang bisa dinyalakan; Toleransi awal 64 untuk PNG transparan", "04-evidence/2026-10-22-smart-select.txt", "Minor", "Spesifikasi smart select 2026-10-22", "PASS", "EDIT-04 di skrip UI"),
    ("EDIT-16", "Edit layout", "Manual UI", "Stiker donat berwatermark", "Editor terbuka, Smart select dicentang", "Klik donat, klik lubang tengah, klik lagi, Shift+klik objek lain, geser Toleransi", "Garis mengikuti donat tanpa watermark; garis dalam muncul lalu hilang; objek kedua ikut; Toleransi menghitung ulang", "Tangkapan layar", "Mayor", "Spesifikasi smart select 2026-10-22", "NOT RUN", "Butuh mouse; logika sama lulus di selfcheck S13 (CUT-15)"),
```

Sebelum baris `("PERF-01",` (baris tuple diawali 4 spasi):

```python
    ("HW-16", "Operator/hardware", "Operator/hardware", "Stiker donat dengan lubang sempit hasil smart select", "PC operator, Graphtec", "Smart select donat + klik lubang, export, potong", "Lubang tengah terpotong rapi; lingkaran potong terkecil masih bisa dibuang", "Foto hasil potong", "Mayor", "Dok: PRD §10 (butir smart select)", "BLOCKED", "Kalibrasi 0,5 mm dan 0,3 mm di sini"),
```

Lalu ganti teks `"<isi dengan detail baris S13 Release>"` dengan detail S13 yang sebenarnya sebelum regenerate. Di `e2e-template.md`: ganti `skenario S1–S9, S11, S12, P1–P5` menjadi `skenario S1–S9, S11–S13, P1–P5`; sisipkan paragraf berikut setelah paragraf "Masukan leader tahap 2–4" (angka dari Task 3):

```markdown
**Smart select (2026-10-22):** klik objek tanpa AI di editor garis potong ([spesifikasi](../superpowers/specs/2026-10-22-smart-select-design.md)). Selfcheck S13 lulus (CUT-15); kotak centang dan Toleransi lewat UI Automation lulus (EDIT-15); klik kanvas dengan mouse NOT RUN (EDIT-16). Garis otomatis tidak berubah: `validasi.json` identik dengan sebelum perubahan. File donat user (pngtree, tidak masuk repo): lebar garis luar turun dari ±49,5 mm (watermark ikut) ke angka di bukti, dan lubang tengah dapat garis dalam dengan 1 klik ([bukti](../../Printech-TestData/04-evidence/2026-10-22-smart-select.txt)).
```

Pada kalimat "Status per 2026-10-19; ... per 2026-10-21." tambahkan `; CUT-15, EDIT-15, EDIT-16, dan HW-16 per 2026-10-22` sebelum titik akhir. Regenerate:

```bash
SP="C:/Users/skyne/AppData/Local/Temp/claude/c--Users-skyne-OneDrive-Documents-Prototype-Software-Layout/4fb470fa-9084-4f86-82e9-d99ecda43ec6/scratchpad"; R="C:/Users/skyne/OneDrive/Documents/Prototype Software Layout"; cd "$SP" && python -I gen_e2e.py e2e-template.md "$R/docs/testing/e2e-validation.md" "$R/docs/testing/e2e-test-results-template.csv"
```

Expected: `90 kasus; {'PASS': 65, 'FAIL': 3, 'BLOCKED': 19, 'NOT RUN': 3}`.

- [ ] **Step 6: Lint dan pemeriksaan akhir**

```bash
cd "C:/Users/skyne/OneDrive/Documents/Prototype Software Layout" && npx --yes markdownlint-cli2@0.23.2 --config "C:/Users/skyne/AppData/Local/Temp/claude/c--Users-skyne-OneDrive-Documents-Prototype-Software-Layout/4fb470fa-9084-4f86-82e9-d99ecda43ec6/scratchpad/mdl/.markdownlint-cli2.jsonc" "docs/**/*.md" "Printech-TestData/**/*.md" "*.md" 2>&1 | tail -1; git status --short; { git diff; git ls-files --others --exclude-standard | grep -E '\.(md|cs|xaml|ps1|py|txt|csv)$' | while read f; do cat "$f"; done; } | grep -n -i -E "$POLA_PRIVAT" | cut -c1-120; echo selesai
```

Expected: `0 issues`; tidak ada file customer/pngtree/zip di status; `$POLA_PRIVAT` (pola informasi pribadi user, dicatat di memori sesi, tidak ditulis di repo) tidak menemukan apa pun. Jalankan `pwsh -File Printech-TestData/cek-original.ps1` → exit 0.

- [ ] **Step 7: Laporan ke user** — Changed / Verified / Remaining (format CLAUDE.md global), lalu tanya apakah di-commit dan di-push. Jangan commit sebelum dijawab.
