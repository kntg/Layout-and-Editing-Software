using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;

namespace StickerLayout;

// Jalankan: StickerLayout.exe --selfcheck <folder>. Membuat sampel, memuat, menyusun, mengekspor PDF, lalu memeriksa hasilnya.
public static class SelfCheck
{
    const double A4W = 210, A4H = 297, Margin = 10, Gap = 2;

    // Kegagalan apa pun (termasuk folder yang tidak bisa dibuat) = exit code 1.
    public static int Run(string dir)
    {
        var log = new List<string>();
        try
        {
            dir = Path.GetFullPath(dir); // folder relatif ("--selfcheck samples" di README): new Uri butuh path lengkap
            Directory.CreateDirectory(dir);
            Samples(dir);
            Cut kiss = new(true, 2, 1), die = new(true, 4, 2);
            var files = new[] { ("circle.png", 6), ("star.jpg", 6), ("badge.svg", 4), ("logo.pdf", 6), ("logo.ai", 4) };
            // Latar opak tidak dibuang otomatis: JPG berlatar putih dirapikan dulu (Hapus latar), seperti alur operator.
            IEnumerable<Sticker> Open(string f, double w, Cut k, Cut d) => f.EndsWith(".jpg")
                ? [Loader.Load(Path.Combine(dir, f), 0, w, k, d, edited: NoBackground(Path.Combine(dir, f)))]
                : Loader.LoadAll(Path.Combine(dir, f), w, k, d);
            List<Sticker> Load() => files.SelectMany(f => Open(f.Item1, 40, kiss, die).Select(s => { s.Copies = f.Item2; return s; })).ToList();

            // Ukur sel grid 2 mm vs 1 mm pada job ±130 stiker (waktu vs pemakaian bahan).
            double cell0 = Nest.Cell;
            long nestMs = 0; // dipakai P5
            foreach (double cell in new[] { 2.0, 1.0 })
            {
                Nest.Cell = cell;
                var big = Load();
                foreach (var s in big) s.Copies *= 5;
                var sw = Stopwatch.StartNew();
                var r = Nest.Run(big, A4W, A4H, Margin, Gap, out _);
                if (cell == cell0) nestMs = sw.ElapsedMilliseconds;
                log.Add($"cell {cell} mm: {big.Sum(s => s.Copies)} stiker -> {r.Count} lembar A4, pemakaian {Use(r):P1}, {sw.ElapsedMilliseconds} ms");
            }
            Nest.Cell = cell0;

            long mem0 = GC.GetTotalMemory(true);
            var items = Load();
            log.Add($"memori: managed +{(GC.GetTotalMemory(true) - mem0) / 1048576.0:F1} MB untuk {items.Count} desain, total managed {GC.GetTotalMemory(true) / 1048576.0:F0} MB, working set {Environment.WorkingSet / 1048576} MB");
            foreach (var s in items)
            {
                Check(s.Kiss.StartsWith('M') && s.Die.StartsWith('M'), $"{s.Name}: garis potong kosong");
                Check(s.DieGeo!.GetArea() > s.KissGeo!.GetArea(), $"{s.Name}: die-cut tidak membungkus kiss-cut");
                log.Add($"{s.Name}: artwork {s.ArtW:F1}x{s.ArtH:F1} mm, kotak {s.W}x{s.H} mm, vektor={s.Pdf != null}");
            }
            foreach (var f in new[] { "circle.png", "star.jpg", "badge.svg", "logo.pdf", "rough.jpg" })
            {
                var nsw = Stopwatch.StartNew();
                var ns = Open(f, f.EndsWith(".pdf") ? 0 : 40, kiss, die).First();
                log.Add($"node {f}: kiss {Nodes(ns.Kiss)} ({Corners(ns.Kiss)} sudut), die {Nodes(ns.Die)} ({Corners(ns.Die)} sudut), {nsw.ElapsedMilliseconds} ms");
            }

            var pdfItem = items.First(s => s.Name == "logo.pdf");
            Check(pdfItem.Pdf != null, "logo.pdf tidak dipakai sebagai vektor");
            // isi PDF: lebar 140 pt (49,39 mm), tinggi 120 pt (42,33 mm), kotak merah di luar CropBox tidak boleh ikut
            Check(Math.Abs(pdfItem.ArtW - 49.39) < 0.6 && Math.Abs(pdfItem.ArtH - 42.33) < 0.6, $"ukuran asli PDF salah: {pdfItem.ArtW:F2}x{pdfItem.ArtH:F2}");
            Check(items.First(s => s.Name == "logo.ai").Pdf != null, "logo.ai tidak dibaca sebagai PDF");

            // Rapikan gambar: logo oranye + teks hijau di latar biru (JPG, tanpa alpha).
            var blueJpg = Path.Combine(dir, "logo-on-blue.jpg");
            var rawBlue = Loader.Load(blueJpg, 0, 40, kiss, new Cut(false, 0, 0));
            var ci = new CleanImage(File.ReadAllBytes(blueJpg));
            Check(ci.BorderColor() is { } bc && bc.B > 150 && bc.R < 80, $"warna latar salah: {ci.BorderColor()}");
            int full0 = ci.OpaqueCount(0);
            Check(ci.EraseFromBorder(32) > 0, "hapus latar tidak menghapus apa pun");
            int afterBg = ci.OpaqueCount(0);
            Check(ci.EraseAt(150, 286, 32) > 0, "penghapus klik tidak menghapus teks");
            int afterText = ci.OpaqueCount(0);
            Check(ci.Undo() && ci.OpaqueCount(0) == afterBg, "undo tidak mengembalikan teks");
            Check(ci.EraseAt(150, 286, 32) > 0 && ci.OpaqueCount(0) == afterText, "hapus teks ulang gagal");
            Check(ci.OpaqueCount(2) < afterText, "kikis tepi tidak mengurangi piksel");
            var cleaned = Loader.Load(blueJpg, 0, 40, kiss, new Cut(false, 0, 0), edited: ci.ToPng(1));
            Check(cleaned.Edited != null && cleaned.Raster == cleaned.Edited, "hasil rapikan tidak dipakai untuk export");
            Check(Math.Abs(cleaned.ArtW - cleaned.ArtH) < 1, $"teks masih ikut: {cleaned.ArtW:F1}x{cleaned.ArtH:F1} mm");
            Check(cleaned.KissGeo!.GetArea() < rawBlue.KissGeo!.GetArea() * 0.85, "garis potong tidak mengikuti lingkaran");
            log.Add($"rapikan: piksel opak {full0} -> latar {afterBg} -> teks {afterText}; luas kiss {rawBlue.KissGeo!.GetArea():F0} -> {cleaned.KissGeo!.GetArea():F0} mm2");
            var cs = new CleanImage(File.ReadAllBytes(Path.Combine(dir, "star.jpg")));
            cs.EraseFromBorder(32);
            Check(cs.Alpha(0)[155 * cs.W + 150] > 16, "hapus latar ikut menghapus lubang di tengah bintang");

            // A3+ = kertas stiker Develop (AP 55).
            Check(Paper.Sizes.TryGetValue("A3+", out var a3p) && a3p == (320, 480), "preset A3+ 320x480 tidak ada");
            // DPI hanya untuk desain yang ditempel sebagai gambar; vektor tidak punya DPI.
            var circle = items.First(s => s.Name == "circle.png");
            Check(circle.Dpi is { } cd && Math.Abs(cd - circle.PxW * 25.4 / 40) < 0.01 && !circle.LowDpi, $"DPI circle salah: {circle.Dpi}");
            Check(pdfItem.Dpi == null && !pdfItem.LowDpi, "PDF vektor tidak boleh punya DPI");
            var wide = Loader.Load(Path.Combine(dir, "circle.png"), 0, 80, kiss, die);
            Check(wide.LowDpi, $"circle 80 mm ({wide.Dpi:0} dpi) harus ditandai resolusi rendah");
            // D6: angka dpi dan warna merahnya memakai pembulatan yang sama (149,6 dpi tampil "150 dpi", jadi tidak merah).
            Sticker DpiOf(int px) => new() { WidthMm = 254, PxW = px };
            Check(DpiOf(1496).DpiText == "150 dpi" && !DpiOf(1496).LowDpi && DpiOf(1494).DpiText == "149 dpi" && DpiOf(1494).LowDpi,
                $"D6: 149,6 dpi tampil {DpiOf(1496).DpiText}, merah {DpiOf(1496).LowDpi}; 149,4 dpi tampil {DpiOf(1494).DpiText}, merah {DpiOf(1494).LowDpi}");
            log.Add($"dpi: circle 40 mm = {circle.Dpi:0} dpi, 80 mm = {wide.Dpi:0} dpi (rendah); 149,6 dpi = {DpiOf(1496).DpiText} tidak merah");

            // Skenario uji garis potong S1-S9 dan S11-S23 (docs/UJI-GARIS-POTONG.md). S10 dan bagian CorelDRAW di S8 diuji operator.
            // Skenario stabilitas dan performa P1-P5 (docs/UJI-STABILITAS.md). P6-P10 diuji pengembang/operator.
            var fails = new List<string>();
            void Report(string id, string title, bool ok, string detail)
            {
                log.Add($"{id} {(ok ? "LULUS" : "GAGAL")}: {title} | {detail}");
                if (!ok) fails.Add(id);
            }
            void S(int n, string title, bool ok, string detail) => Report($"S{n}", title, ok, detail);
            void P(int n, string title, bool ok, string detail) => Report($"P{n}", title, ok, detail);
            var none = new Cut(false, 0, 0);
            Sticker FromFile(string f, double w, Cut k, Cut d) => Open(f, w, k, d).First();
            Sticker Png(BitmapSource b, double w, Cut k, Cut d) => Loader.Load("uji.png", 0, w, k, d, edited: Loader.Png(b));

            // S1: logo JPG bertepi kasar.
            var rough0 = FromFile("rough.jpg", 40, new Cut(true, 2, 0), new Cut(true, 4, 0));
            var rough1 = FromFile("rough.jpg", 40, kiss, die);
            double cutK = 1 - (double)Nodes(rough1.Kiss) / Nodes(rough0.Kiss), cutD = 1 - (double)Nodes(rough1.Die) / Nodes(rough0.Die);
            S(1, "logo JPG kasar: node turun >= 60%, tanpa sudut tajam", cutK >= 0.6 && cutD >= 0.6 && Corners(rough1.Kiss) + Corners(rough1.Die) == 0,
                $"kiss {Nodes(rough0.Kiss)} -> {Nodes(rough1.Kiss)} ({cutK:P0}), die {Nodes(rough0.Die)} -> {Nodes(rough1.Die)} ({cutD:P0})");

            // S2: lingkaran 40 mm + kiss 2 mm tetap bulat, ukuran benar, node minimal.
            var circle40 = items.First(s => s.Name == "circle.png");
            var cb = circle40.KissGeo!.Bounds;
            double cExp = circle40.ArtW + 2 * kiss.Off;
            S(2, "lingkaran tetap bulat dan node minimal", Nodes(circle40.Kiss) <= 4 && Math.Abs(cb.Width - cb.Height) < 0.3 && Math.Abs(cb.Width - cExp) < 0.5,
                $"{Nodes(circle40.Kiss)} node, {cb.Width:F2} x {cb.Height:F2} mm (harapan {cExp:F1})");

            // S3: teks tebal. Node turun, tanpa sudut, huruf tidak terpotong.
            var text = Draw(dc => dc.DrawText(new FormattedText("PRINTECH", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Arial"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal), 100, Brushes.Black, 1.0), new Point(10, 10)), 620, 140);
            var tArt = Png(text, 60, new Cut(true, 0, 0), none);
            var t0 = Png(text, 60, new Cut(true, 2, 0), none);
            var t1 = Png(text, 60, kiss, none);
            double tOut = Outside(tArt, t1);
            S(3, "teks: node turun, tanpa sudut tajam, huruf tidak terpotong", Nodes(t1.Kiss) < Nodes(t0.Kiss) && Corners(t1.Kiss) == 0 && tOut < 0.5,
                $"halus 0 = {Nodes(t0.Kiss)} node, halus 1 = {Nodes(t1.Kiss)} node, artwork di luar garis {tOut:F2} mm2");

            // S4: offset kecil (0,5 mm) dan nol pada bintang (ujung lancip): garis tidak masuk artwork.
            var sArt = FromFile("star.jpg", 40, new Cut(true, 0, 0), none);
            double o05 = Outside(sArt, FromFile("star.jpg", 40, new Cut(true, 0.5, 1), none)), o0 = Outside(sArt, FromFile("star.jpg", 40, new Cut(true, 0, 1), none));
            S(4, "offset 0,5 mm dan 0 mm: garis tidak masuk artwork", o05 < 0.5 && o0 < 3,
                $"artwork di luar garis: offset 0,5 = {o05:F2} mm2, offset 0 = {o0:F2} mm2");

            // S5: berbagai nilai Halus. Semua jalan, semua lebih sedikit node daripada Halus 0.
            var sweep = new[] { 0, 0.5, 1, 2, 4 }.Select(h => Nodes(FromFile("rough.jpg", 40, new Cut(true, 2, h), none).Kiss)).ToArray();
            S(5, "halus 0 / 0,5 / 1 / 2 / 4 mm: node selalu di bawah halus 0", sweep.Skip(1).All(n => n < sweep[0]), string.Join(" / ", sweep) + " node");

            // S6: kiss selalu di dalam die, termasuk pengaturan tidak biasa (kiss halus 3, die halus 0).
            var odd = new[] { FromFile("rough.jpg", 40, new Cut(true, 2, 3), new Cut(true, 4, 0)), FromFile("star.jpg", 40, new Cut(true, 2, 3), new Cut(true, 4, 0)),
                Png(text, 60, new Cut(true, 2, 3), new Cut(true, 4, 0)) };
            double worst = items.Append(rough1).Concat(odd).Max(s => Geometry.Combine(s.KissGeo, s.DieGeo, GeometryCombineMode.Exclude, null).GetArea());
            S(6, "kiss selalu di dalam die", worst < 1, $"luas kiss di luar die maks {worst:F2} mm2 dari {items.Count + 4} desain");

            // S7: dua bagian artwork berdekatan (celah 3-3,9 mm) tidak pecah jadi dua stiker oleh penghalusan.
            var split = new List<string>();
            foreach (double gap in new[] { 3.0, 3.3, 3.6, 3.9 })
            {
                var duo = Draw(dc => { dc.DrawEllipse(Brushes.Black, null, new Point(100, 100), 100, 100); dc.DrawEllipse(Brushes.Black, null, new Point(300 + gap * 10, 100), 100, 100); }, (int)(400 + gap * 10), 200);
                int c0 = Png(duo, 40 + gap, new Cut(true, 2, 0), none).Kiss.Count(ch => ch == 'M'), c1 = Png(duo, 40 + gap, kiss, none).Kiss.Count(ch => ch == 'M');
                if (c1 > c0) split.Add($"celah {gap} mm: {c0} -> {c1} kontur");
            }
            S(7, "dua bagian berdekatan tidak pecah", split.Count == 0, split.Count == 0 ? "celah 3,0 / 3,3 / 3,6 / 3,9 mm: kontur tidak bertambah" : string.Join("; ", split));

            // S11: hanya kontur luar. Offset menutup celah cincin "C" dan titik melingkar; lubang yang terbentuk tidak boleh ikut dipotong.
            var cRing = Draw(dc =>
            {
                double a = 5.4 * Math.PI / 180;
                var g = new StreamGeometry();
                using (var ctx = g.Open())
                {
                    ctx.BeginFigure(new Point(150 + 120 * Math.Cos(a), 150 + 120 * Math.Sin(a)), false, false);
                    ctx.ArcTo(new Point(150 + 120 * Math.Cos(a), 150 - 120 * Math.Sin(a)), new Size(120, 120), 0, true, SweepDirection.Clockwise, true, false);
                }
                dc.DrawGeometry(null, new Pen(Brushes.Black, 40), g);
            });
            var dots = Draw(dc =>
            {
                for (int i = 0; i < 12; i++) dc.DrawEllipse(Brushes.Black, null, new Point(150 + 110 * Math.Cos(i * Math.PI / 6), 150 + 110 * Math.Sin(i * Math.PI / 6)), 22, 22);
                dc.DrawEllipse(Brushes.Black, null, new Point(150, 150), 40, 40);
            });
            // Donat (masukan leader 2026-10-21): lubang asli yang tetap terbuka setelah offset dapat garis dalam kiss dan die,
            // die tetap membungkus kiss. Lubang kecil yang tertutup offset tidak dipotong.
            BitmapSource Donut(double inner) => Draw(dc => dc.DrawGeometry(Brushes.DarkOrange, null, new CombinedGeometry(GeometryCombineMode.Exclude,
                new EllipseGeometry(new Point(150, 150), 130, 130), new EllipseGeometry(new Point(150, 150), inner, inner))));
            var holes = new List<string>();
            bool solid = true;
            foreach (var (name, img, want) in new[] { ("cincin C", cRing, 1), ("titik melingkar + tengah", dots, 1), ("donat lubang Ø 21,5 mm", Donut(70), 2), ("donat lubang Ø 4,6 mm", Donut(15), 1) })
            {
                var hs = Png(img, 40, kiss, die);
                int kissN = hs.Kiss.Count(ch => ch == 'M'), dieN = hs.Die.Count(ch => ch == 'M');
                double outside = Geometry.Combine(hs.KissGeo!, hs.DieGeo!, GeometryCombineMode.Exclude, null).GetArea();
                solid &= kissN == want && dieN == want && outside < 0.5;
                holes.Add($"{name}: kiss {kissN}, die {dieN} kontur, kiss di luar die {outside:F2} mm2");
            }
            S(11, "lubang: lubang asli yang tetap terbuka dipotong (donat), rongga dari celah tertutup dan lubang kecil tidak", solid, string.Join("; ", holes));

            // S12: editor garis potong (masukan leader 2026-10-21; node kurva seperti CorelDRAW 2026-10-23). Garis manual dipakai apa adanya.
            var cPath = Path.Combine(dir, "circle.png");
            var pk = Curves.Simplify(circle40.Kiss, circle40.Off, 0.2); // garis lurus: uji muat dan export garis lurus
            var pd = Curves.Simplify(circle40.Die, circle40.Off, 0.2);
            // Simplify editor = fit kurva Bezier: simpangan dari kurva otomatis <= eps, node logo kasar turun bila eps naik.
            var ek = Curves.Fit(circle40.Kiss, circle40.Off, 0.2);
            double dev = Math.Max(Deviation(circle40.Kiss, circle40.Off, ek), Deviation(rough1.Kiss, rough1.Off, Curves.Fit(rough1.Kiss, rough1.Off, 0.2)));
            int nFine = Curves.Count(Curves.Fit(rough1.Kiss, rough1.Off, 0.05)), nMid = Curves.Count(Curves.Fit(rough1.Kiss, rough1.Off, 0.2)),
                nCoarse = Curves.Count(Curves.Fit(rough1.Kiss, rough1.Off, 0.5)), nEd = Curves.Count(ek), nEd5 = Curves.Count(Curves.Fit(circle40.Kiss, circle40.Off, 0.5));
            var man = Loader.Load(cPath, 0, 40, kiss, die, manualKiss: pk, manualDie: pd);
            bool straight = !(man.Kiss + man.Die).Contains('C') && man.Kiss.StartsWith('M') && man.Die.EndsWith('Z');
            double areaDiff = Math.Abs(man.AreaMm2 - circle40.AreaMm2) / circle40.AreaMm2;
            // Node die terkanan digeser 1,5 mm keluar: grid nesting ikut berubah.
            var pd2 = pd.Select(c => (Node[])c.Clone()).ToList();
            int far = Array.FindIndex(pd2[0], q => q.P.X == pd2[0].Max(r => r.P.X));
            pd2[0][far] = Node.Corner(pd2[0][far].P + new Vector(1.5, 0));
            var man2 = Loader.Load(cPath, 0, 40, kiss, die, manualKiss: pk, manualDie: pd2);
            // Lebar 40 -> 80 mm: garis diskala. Offset kiss 2 -> 1 mm: garis manual tidak berubah.
            var man80 = Loader.Load(cPath, 0, 80, kiss, die, manualKiss: Curves.Scale(pk, 2), manualDie: Curves.Scale(pd, 2));
            var manOff = Loader.Load(cPath, 0, 40, new Cut(true, 1, 1), die, manualKiss: pk, manualDie: pd);
            static Rect Rel(Sticker s) { var b = s.KissGeo!.Bounds; b.Offset(-s.Off, -s.Off); return b; }
            double scaleErr = Math.Abs(Rel(man80).Width - 2 * Rel(man).Width);
            double lockErr = (Rel(manOff).TopLeft - Rel(man).TopLeft).Length + Math.Abs(Rel(manOff).Width - Rel(man).Width);
            // Die manual rapat (di posisi garis kiss) dengan kiss otomatis: kiss dipangkas ke dalam die.
            var tight = Loader.Load(cPath, 0, 40, kiss, die, manualDie: pk);
            double tightOut = Geometry.Combine(tight.KissGeo!, tight.DieGeo!, GeometryCombineMode.Exclude, null).GetArea();
            // Export: garis manual lurus = garis lurus, garis manual kurva = kurva Bezier, warna CMYK sama.
            var manPdf = Path.Combine(dir, "export-manual.pdf");
            Check(Export.Pdf(manPdf, [[new Placed { S = man, X = Margin, Y = Margin }]], A4W, [A4H]).Count == 0, "peringatan export garis manual");
            string mc;
            using (var mdoc = PdfReader.Open(manPdf, PdfDocumentOpenMode.Import)) mc = Content(mdoc.Pages[0]);
            int mLines = Regex.Matches(mc, @"(?<=\s)l(?=\s)").Count, mCurves = Regex.Matches(mc, @"(?<=\s)c(?=\s)").Count;
            var manC = Loader.Load(cPath, 0, 40, kiss, die, manualKiss: Curves.Parse(circle40.Kiss, circle40.Off), manualDie: Curves.Parse(circle40.Die, circle40.Off));
            int cCurves = ExportOps([new Placed { S = manC, X = Margin, Y = Margin }]).Curves;
            bool mExport = mLines > 0 && mCurves == 0 && mc.Contains("0 1 0 0 K") && mc.Contains("1 0 0 0 K") && cCurves > 0
                && Math.Abs(manC.AreaMm2 - circle40.AreaMm2) / circle40.AreaMm2 < 0.01;
            // Node kurva: membelah kurva tidak mengubah bentuk; jadikan kurva lalu garis kembali ke polygon semula; node halus segaris.
            double splitDev = Deviation(circle40.Kiss, circle40.Off, [Curves.Split(Curves.Parse(circle40.Kiss, circle40.Off)[0], 1, 0.37)]);
            var curved = Curves.ToCurve(pk[0], 1);
            var smooth = Curves.SetSmooth(curved, 1, true)[1];
            bool nodeOps = splitDev < 0.01 && !Curves.Straight(curved[0], curved[1]) && !Curves.Straight(curved[1], curved[2])
                && Curves.PathOf([Curves.ToLine(curved, 1)], 0) == Curves.PathOf([pk[0]], 0)
                && Math.Abs(Vector.CrossProduct(smooth.In - smooth.P, smooth.Out - smooth.P)) < 1e-9 && smooth.Smooth;

            // Editor tanpa mouse: tambah node di kurva lalu seret, undo/redo, hapus, kiss keluar die, handle, tombol node, Simplify, reset.
            var ed = new CutEditWindow(circle40, null, null, CutShape.Auto, "uji");
            var mid = Curves.At(ek[0][0], ek[0][1], 0.5); // titik tepat di kurva antara node 0 dan 1
            ed.Press(mid, 0.3);
            ed.DragTo(mid + (new Point(circle40.ArtW / 2, circle40.ArtH / 2) - mid) * 0.05);
            ed.Release();
            int Pts(List<Node[]>? p) => p?.Sum(c => c.Length) ?? -1;
            var edLog = new List<string> { $"tambah {Pts(ed.ManualKiss)}" };
            bool edOk = Pts(ed.ManualKiss) == nEd + 1 && ed.CanSave;
            ed.Undo();
            edOk &= ed.ManualKiss == null;
            ed.Redo();
            edOk &= Pts(ed.ManualKiss) == nEd + 1;
            ed.Press(ek[0][2].P, 0.3);
            ed.Release();
            ed.DeletePoint();
            edOk &= Pts(ed.ManualKiss) == nEd;
            edLog.Add($"undo/redo/hapus {Pts(ed.ManualKiss)}");
            ed.Press(ek[0][3].P, 0.3);
            ed.DragTo(new Point(-100, -100));
            ed.Release();
            edOk &= !ed.CanSave && !ed.KissInside;
            ed.Undo();
            edOk &= ed.CanSave;
            // Handle Out node 1 diseret: node halus, jadi handle In ikut segaris; jumlah node tetap.
            ed.Press(ek[0][1].P, 0.3);
            ed.Release();
            var h0 = ed.ManualKiss![0].First(n => (n.P - ek[0][1].P).Length < 1e-6);
            ed.Press(h0.Out, 0.3);
            ed.DragTo(h0.Out + new Vector(1.5, 0.5));
            ed.Release();
            var h1 = ed.ManualKiss![0].First(n => (n.P - ek[0][1].P).Length < 1e-6);
            bool handleOk = Pts(ed.ManualKiss) == nEd && (h1.Out - h0.Out).Length > 1 && Math.Abs(Vector.CrossProduct(h1.In - h1.P, h1.Out - h1.P)) < 1e-6;
            ed.ToLine();
            var h2 = ed.ManualKiss![0].First(n => (n.P - ek[0][1].P).Length < 1e-6);
            ed.ToCurve();
            var h3 = ed.ManualKiss![0].First(n => (n.P - ek[0][1].P).Length < 1e-6);
            handleOk &= h2.In == h2.P && h2.Out == h2.P && h3.In != h3.P && h3.Out != h3.P;
            edOk &= handleOk;
            edLog.Add($"handle {(handleOk ? "benar" : "SALAH")}");
            ed.Eps.Value = 0.5;
            edOk &= Pts(ed.ManualKiss) == nEd5;
            edLog.Add($"Simplify 0,5 {Pts(ed.ManualKiss)}");
            ed.Reset();
            edOk &= ed.ManualKiss == null && ed.ManualDie == null && ed.CanSave;
            // Temuan leader 2026-10-26: klik node hanya memilih (getaran kecil tidak menggeser), seret menggeser node sejauh gerakan
            // mouse (tidak lompat ke kursor), Shift+klik memilih beberapa node; seret dan Delete berlaku untuk semua node terpilih.
            var rk = Curves.Fit(rough1.Kiss, rough1.Off, 0.2)[0];
            var ed3 = new CutEditWindow(rough1, null, null, CutShape.Auto, "klik");
            Point n0 = rk[0].P, n2 = rk[2].P;
            Vector off = new(0.05, 0.03), dv = new(1, 0.5);
            static bool Has(CutEditWindow w, Point p) => w.ManualKiss?[0].Any(n => (n.P - p).Length < 1e-6) == true;
            ed3.Press(n0 + off, 0.3);
            ed3.DragTo(n0 + off);
            ed3.Release();
            bool clickOk = ed3.ManualKiss == null;
            ed3.Press(n0 + off, 0.3);
            ed3.DragTo(n0 + off + dv);
            ed3.Release();
            bool grabOk = Has(ed3, n0 + dv);
            ed3.Undo();
            ed3.Press(n0, 0.3);
            ed3.Release();
            ed3.Press(n2, 0.3, add: true);
            ed3.DragTo(n2 + dv);
            ed3.Release();
            bool multiOk = Has(ed3, n0 + dv) && Has(ed3, n2 + dv);
            int nMulti = Pts(ed3.ManualKiss);
            ed3.DeletePoint();
            multiOk &= Pts(ed3.ManualKiss) == nMulti - 2;
            edOk &= clickOk && grabOk && multiOk;
            edLog.Add($"klik {(clickOk ? "tanpa geser" : "MENGGESER")}, seret relatif {(grabOk ? "benar" : "SALAH")}, Shift 2 node {(multiOk ? "benar" : "SALAH")}");
            // Donat: garis dalam yang tidak diinginkan dihapus node demi node sampai konturnya hilang.
            var dn = Png(Donut(70), 40, kiss, die);
            var dk = Curves.Fit(dn.Kiss, dn.Off, 0.2);
            var hole = dk.MinBy(c => c.Max(q => q.P.X) - c.Min(q => q.P.X))!;
            var ed2 = new CutEditWindow(dn, null, null, CutShape.Auto, "donat");
            foreach (var q in hole.Take(Math.Max(1, hole.Length - 2))) { ed2.Press(q.P, 0.01); ed2.Release(); ed2.DeletePoint(); } // <= 3 node: sekali hapus
            edOk &= ed2.ManualKiss?.Count == 1 && ed2.ManualKiss[0].Length == dk.First(c => c != hole).Length;
            edLog.Add($"donat: kontur kiss {dk.Count} -> {ed2.ManualKiss?.Count}");
            S(12, "editor garis potong: node lurus dan kurva, garis manual, skala, kunci offset, export, edit node dan handle",
                dev <= 0.21 && nCoarse < nMid && nMid < nFine && straight && areaDiff < 0.02 && man2.Area > man.Area && scaleErr < 0.05 && lockErr < 0.01
                && tightOut < 0.5 && mExport && nodeOps && edOk,
                $"simpangan {dev:F3} mm (eps 0,2); node logo kasar eps 0,05/0,2/0,5 = {nFine}/{nMid}/{nCoarse}, lingkaran {nEd}; M/L/Z {straight}; luas {areaDiff:P1}; "
                + $"grid {man.Area} -> {man2.Area} sel; skala {scaleErr:F3} mm; kunci offset {lockErr:F3} mm; die rapat: kiss di luar die {tightOut:F2} mm2; "
                + $"export lurus {mLines} garis lurus, {mCurves} kurva, kurva {cCurves} kurva; belah kurva {splitDev:F4} mm; jadikan kurva/garis/halus {nodeOps}; "
                + $"editor {string.Join(", ", edLog)} ({(edOk ? "benar" : "SALAH")})");

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
            static double Left(List<Node[]> p) => Geometry.Parse(Curves.PathOf(p, 0)).Bounds.Left;
            static bool Holds(List<Node[]> p, Point q) => Geometry.Parse(Curves.PathOf(p, 0)).FillContains(q);
            Point ringP = new(26, 10), holeP = new(20, 10), pinP = new(14, 10), islandP = new(37, 10), notchP = new(20, 1.5), emptyP = new(2, 2);
            var sr1 = Loader.SmartSelect(ss, [ringP], [], 64, 0.2);
            var sr2 = Loader.SmartSelect(ss, [ringP], [holeP], 64, 0.2);
            var sr3 = Loader.SmartSelect(ss, [ringP], [holeP, new(20.05, 10.05)], 64, 0.2);
            var sr4 = Loader.SmartSelect(ss, [ringP, islandP], [], 64, 0.2);
            var sr5 = Loader.SmartSelect(ss, [ringP], [], 16, 0.2);
            var sr6 = Loader.SmartSelect(ss, [ringP], [pinP], 64, 0.2);
            var ssAuto = Curves.Simplify(ss.Kiss, ss.Off, 0.2);
            var ssInner = sr2.Kiss.MinBy(c => c.Max(q => q.P.X) - c.Min(q => q.P.X))!;
            double ssInnerW = Geometry.Parse(Curves.PathOf([ssInner], 0)).Bounds.Width;
            double ssOut = Geometry.Combine(Geometry.Parse(Curves.PathOf(sr2.Kiss, 0)), Geometry.Parse(Curves.PathOf(sr2.Die, 0)), GeometryCombineMode.Exclude, null).GetArea();
            var ssOpaque = Png(Draw(dc => { dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 400, 300)); dc.DrawEllipse(Brushes.DarkOrange, null, new Point(200, 150), 100, 100); }, 400, 300), 40, kiss, die);
            var sro = Loader.SmartSelect(ssOpaque, [new(26, 15)], [], Loader.DefaultTolerance(ssOpaque), 0.2);
            bool smartOk = sr1.Kiss.Count == 1 && sr1.Die.Count == 1 && Math.Abs(Left(sr1.Kiss) - 8) < 0.5 && !Holds(sr1.Kiss, notchP) && Holds(ssAuto, notchP)
                && sr2.Kiss.Count == 2 && sr2.Die.Count == 1 && ssInnerW is > 0.5 and < 1.6 && ssOut < 0.5
                && sr3.Kiss.Count == 1 && sr4.Kiss.Count == 2 && Left(sr5.Kiss) < Left(sr1.Kiss) - 5 && sr6.TooSmall && sr6.Kiss.Count == 1
                && Loader.Classify(ss, holeP, 64) == Loader.PickKind.Hole && Loader.Classify(ss, emptyP, 64) == Loader.PickKind.Empty
                && Loader.Classify(ss, ringP, 64) == Loader.PickKind.Object && Loader.DefaultTolerance(ss) == 64
                && Loader.DefaultTolerance(ssOpaque) == 32 && sro.Kiss.Count == 1 && Math.Abs(Left(sro.Kiss) - 8) < 0.5
                && Loader.Classify(ssOpaque, emptyP, 32) == Loader.PickKind.Empty;
            var se = new CutEditWindow(ss, null, null, CutShape.Auto, "smart");
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
            S(13, "smart select: objek, takik diikuti, watermark diabaikan, lubang sempit lewat klik, pulau, objek opak", smartOk && edSmart,
                $"kiss kiri {Left(sr1.Kiss):F2} mm (Toleransi 64) / {Left(sr5.Kiss):F2} mm (16); kontur kiss {sr1.Kiss.Count}, +lubang {sr2.Kiss.Count} "
                + $"(garis dalam {ssInnerW:F2} mm, die {sr2.Die.Count}, kiss di luar die {ssOut:F2} mm2), klik lubang 2x {sr3.Kiss.Count}, +pulau {sr4.Kiss.Count}, "
                + $"lubang jarum terlalu kecil {sr6.TooSmall}; takik: smart memuat titik {Holds(sr1.Kiss, notchP)}, otomatis {Holds(ssAuto, notchP)}; "
                + $"opak: Toleransi {Loader.DefaultTolerance(ssOpaque)}, kiri {Left(sro.Kiss):F2} mm"
                + $"; editor {se1}/{se2}/{se3}/{se4}/{se5} kontur, kiri {seL64:F2} -> {seL16:F2} mm, Toleransi awal {(seTol ? 64 : -1)} ({(edSmart ? "benar" : "SALAH")})");

            // S14: desain kotak (masukan 2026-10-23). Gambar opak dan PNG kotak bermargin transparan mendapat garis kotak bersudut
            // tajam sejauh offset (4 node lurus, Halus tidak dipakai); kotak bersudut bulat (badge.svg) tetap mengikuti bentuk.
            var boxImg = Draw(dc => { dc.DrawRectangle(Brushes.SteelBlue, null, new Rect(0, 0, 400, 200)); dc.DrawEllipse(Brushes.Gold, null, new Point(200, 100), 60, 60); }, 400, 200);
            var bx = Png(boxImg, 40, kiss, die);
            var bxSharp = Png(boxImg, 40, new Cut(true, 2, 0), new Cut(true, 4, 0));
            var bxMargin = Png(Draw(dc => dc.DrawRectangle(Brushes.SteelBlue, null, new Rect(50, 40, 300, 120)), 400, 200), 30, kiss, die);
            var bxDie = Png(boxImg, 40, none, die);
            var bxTight = Png(boxImg, 40, kiss, new Cut(true, 2.05, 0));
            static bool Box4(string p) => p != "" && !p.Contains('C') && Curves.Parse(p, 0) is [{ Length: 4 }];
            // Selisih terbesar kotak garis (kiss atau die) dari kotak artwork ± off.
            static double BoxErr(Sticker s, bool die, double off)
            {
                var b = (die ? s.DieGeo : s.KissGeo)!.Bounds;
                b.Offset(-s.Off, -s.Off);
                return new[] { b.X + off, b.Y + off, b.Width - s.ArtW - 2 * off, b.Height - s.ArtH - 2 * off }.Max(Math.Abs);
            }
            double bxErr = new[] { BoxErr(bx, false, 2), BoxErr(bx, true, 4), BoxErr(bxSharp, false, 2), BoxErr(bxMargin, false, 2), BoxErr(bxMargin, true, 4),
                BoxErr(bxDie, true, 4), BoxErr(bxTight, true, 2 + 1 / Loader.PxMm) }.Max();
            double bxArea = bx.AreaMm2 / ((bx.ArtW + 8) * (bx.ArtH + 8)) - 1;
            var bxEd = new CutEditWindow(bx, null, null, CutShape.Auto, "kotak");
            bool bxOk = Box4(bx.Kiss) && Box4(bx.Die) && Box4(bxSharp.Kiss) && Box4(bxMargin.Kiss) && bxDie.Kiss == "" && Box4(bxDie.Die) && Box4(bxTight.Die)
                && Box4(rawBlue.Kiss) && items.First(s => s.Name == "badge.svg").Kiss.Contains('C') && bxErr < 0.05 && Math.Abs(bxArea) < 0.01
                && bxEd.Info.Text.StartsWith("Kiss 4 ");
            S(14, "desain kotak: garis kotak tajam sejauh offset, 4 node lurus, tanpa Halus; kotak bersudut bulat tetap kurva", bxOk,
                $"kiss {Curves.Count(Curves.Parse(bx.Kiss, 0))} node, die {Curves.Count(Curves.Parse(bx.Die, 0))} node; selisih kotak maks {bxErr:F3} mm "
                + $"(Halus 0/1, margin transparan, die saja, die rapat = kiss + 1 px); luas {bxArea:P1}; JPG opak {Box4(rawBlue.Kiss)}; "
                + $"badge sudut bulat kurva {items.First(s => s.Name == "badge.svg").Kiss.Contains('C')}; editor '{bxEd.Info.Text}'");

            // S15: preset bentuk (masukan 2026-10-23). Bentuk mengelilingi artwork sejauh offset (artwork + offset - 0,3 mm di dalam
            // garis kiss), die membungkus kiss, jumlah node sesuai bentuk, ukuran sesuai bentuk terkecil, ikut offset dan lebar.
            var circArt = Loader.Load(cPath, 0, 40, new Cut(true, 1.7, 0), none);
            var textArt = Png(text, 60, new Cut(true, 1.7, 0), none);
            Sticker ShapeOf(string src, double w, Cut k, Cut d, CutShape shape) =>
                Loader.Load(src, 0, w, k, d, edited: src == "uji.png" ? Loader.Png(text) : null, shape: shape);
            // Ukuran garis kiss bentuk terkecil pada circle.png (artwork Ø 40 mm, offset 2, jadi lingkaran r 22 harus muat): lingkaran
            // Ø 44, segitiga tinggi 3 x 22, segi enam tinggi 44 dan lebar 44 / cos 30°, bintang (lekuk dalam 0,5 x jari-jari luar)
            // lebar 2 x 44 x sin 72°. Batas: -0,1 s.d. +Tol mm. Hull dihitung dari sudut piksel 6 px/mm (sengaja ke luar supaya
            // artwork tidak terpotong), jadi bentuk bulat/miring bisa ±0,6 mm lebih besar dari minimal.
            var shapeWant = new Dictionary<CutShape, (int Nodes, double W, double H, double Tol)>
            {
                [CutShape.Rect] = (4, 44, 44, 0.1), [CutShape.RoundRect] = (8, 44, 44, 0.1), [CutShape.Circle] = (4, 44, 44, 0.7),
                [CutShape.Oval] = (4, 44, 44, 0.8), [CutShape.Triangle] = (3, 66 / Math.Sqrt(3) * 2, 66, 0.7),
                [CutShape.Hexagon] = (6, 44 / Math.Cos(Math.PI / 6), 44, 0.7), [CutShape.Star] = (10, 2 * 44 * Math.Sin(2 * Math.PI / 5), double.NaN, 0.9),
                [CutShape.Heart] = (4, double.NaN, double.NaN, 0),
            };
            static bool Near(double got, double want, double tol) => double.IsNaN(want) || got >= want - 0.1 && got <= want + tol;
            var shapeLog = new List<string>();
            bool shapeOk = true;
            foreach (var (shape, (nodes, ww, wh, tol)) in shapeWant)
                foreach (var (lbl, s, art) in new[] { ("lingkaran", ShapeOf(cPath, 40, kiss, die, shape), circArt), ("teks", ShapeOf("uji.png", 60, kiss, die, shape), textArt) })
                {
                    var kn = Curves.Parse(s.Kiss, 0);
                    bool wantCurve = shape is CutShape.RoundRect or CutShape.Circle or CutShape.Oval or CutShape.Heart;
                    double artOut = Outside(art, s), kissOut = Geometry.Combine(s.KissGeo!, s.DieGeo!, GeometryCombineMode.Exclude, null).GetArea();
                    var kb = s.KissGeo!.Bounds;
                    bool size = lbl != "lingkaran" || Near(kb.Width, ww, tol) && Near(kb.Height, wh, tol);
                    bool ok = kn.Count == 1 && kn[0].Length == nodes && s.Kiss.Contains('C') == wantCurve && artOut < 0.05 && kissOut < 0.01 && size && s.Shape == shape;
                    shapeOk &= ok;
                    if (!ok || lbl == "lingkaran")
                        shapeLog.Add($"{shape} {lbl}: {kn.Sum(c => c.Length)} node, {kb.Width:F1}x{kb.Height:F1} mm, artwork keluar {artOut:F2}, kiss di luar die {kissOut:F3}{(ok ? "" : " SALAH")}");
                }
            // Parametrik: offset kiss 3 dan lebar 60 mm menyesuaikan bentuk; editor: bentuk dipilih, node diedit = garis manual.
            double c3 = ShapeOf(cPath, 40, new Cut(true, 3, 1), new Cut(true, 5, 2), CutShape.Circle).KissGeo!.Bounds.Width;
            double c60 = ShapeOf(cPath, 60, kiss, die, CutShape.Circle).KissGeo!.Bounds.Width;
            var edS = new CutEditWindow(circle40, null, null, CutShape.Auto, "bentuk");
            edS.ApplyShape(CutShape.Circle);
            bool edShape = edS.Shape == CutShape.Circle && edS.ManualKiss == null && edS.Info.Text.StartsWith("Kiss 4 ");
            var sn = Loader.ShapeLines(circle40, CutShape.Circle).Kiss![0][0].P;
            edS.Press(sn, 0.3);
            edS.DragTo(sn + new Vector(1, 0));
            edS.Release();
            edShape &= edS.Shape == CutShape.Auto && edS.ManualKiss != null;
            edS.Undo();
            edShape &= edS.Shape == CutShape.Circle && new CutEditWindow(circle40, null, null, CutShape.Star, "bintang").Shape == CutShape.Star;
            // Temuan leader 2026-10-26: bentuk kiss dan die terpisah (kiss bintang, die kotak). Die bentuk lain membungkus kiss sejauh
            // selisih offset; die Otomatis mengikuti bentuk kiss. Bentuk berlaku untuk garis yang dipilih di "Garis yang diedit".
            var edD = new CutEditWindow(circle40, null, null, CutShape.Auto, "dua bentuk");
            edD.ApplyShape(CutShape.Star);
            edD.EditDie.IsChecked = true;
            edD.ApplyShape(CutShape.Rect);
            bool rectOn = edD.ShapePanel.Children.OfType<System.Windows.Controls.RadioButton>().Single(r => (CutShape)r.Tag == CutShape.Rect).IsChecked == true;
            bool twoOk = edD.Shape == CutShape.Star && edD.DieShape == CutShape.Rect && edD.ManualKiss == null && edD.KissInside && rectOn
                && edD.Info.Text.StartsWith("Kiss 10 titik · die 4 titik");
            // Kiss diperlebar d mm (sambungan bulat = jarak sebenarnya; sambungan miter memanjang di ujung bintang) yang keluar dari die.
            static double OutsideBy(Sticker s, double d) => Geometry.Combine(Geometry.Combine(s.KissGeo!,
                s.KissGeo!.GetWidenedPathGeometry(new Pen(null, 2 * d) { LineJoin = PenLineJoin.Round }), GeometryCombineMode.Union, null),
                s.DieGeo!, GeometryCombineMode.Exclude, null).GetArea();
            // Kiss bintang: die kotak membungkus kontur kiss sejauh selisih offset (2 mm). Kiss otomatis: die kotak sejauh offset die dari
            // artwork (Ø 40 + 2 x 4 = 48 mm), kiss tetap di dalam die.
            var starRect = Loader.Load(cPath, 0, 40, kiss, die, shape: CutShape.Star, dieShape: CutShape.Rect);
            var autoRect = Loader.Load(cPath, 0, 40, kiss, die, dieShape: CutShape.Rect);
            double twoOut = OutsideBy(starRect, 1.9), autoOut = Geometry.Combine(autoRect.KissGeo!, autoRect.DieGeo!, GeometryCombineMode.Exclude, null).GetArea();
            var adb = autoRect.DieGeo!.Bounds;
            twoOk &= Curves.Parse(starRect.Kiss, 0)[0].Length == 10 && Curves.Parse(starRect.Die, 0)[0].Length == 4 && Curves.Parse(autoRect.Die, 0)[0].Length == 4
                && twoOut < 0.05 && autoOut < 0.01 && Near(adb.Width, 48, 0.1) && Near(adb.Height, 48, 0.1)
                && starRect.CutBadge == "Bintang · die Kotak" && autoRect.CutBadge == "die Kotak";
            var (sl, scv) = ExportOps([new Placed { S = ShapeOf(cPath, 40, kiss, die, CutShape.Rect), X = Margin, Y = Margin },
                new Placed { S = ShapeOf(cPath, 40, kiss, die, CutShape.Circle), X = Margin + 60, Y = Margin }]);
            S(15, "preset bentuk: mengelilingi artwork sejauh offset, die membungkus kiss, node sesuai bentuk, ikut offset dan lebar",
                shapeOk && Near(c3, 46, 0.7) && Near(c60, 64, 0.7) && edShape && sl > 0 && scv > 0 && twoOk,
                string.Join("; ", shapeLog) + $"; lingkaran offset 3 = {c3:F2} mm (harapan 46), lebar 60 = {c60:F2} mm (64); editor {(edShape ? "benar" : "SALAH")}; "
                + $"export {sl} garis lurus, {scv} kurva; kiss bintang + die kotak {(twoOk ? "benar" : "SALAH")} (kiss+1,9 mm di luar die {twoOut:F3} mm2; "
                + $"kiss otomatis + die kotak {adb.Width:F2}x{adb.Height:F2} mm, kiss di luar die {autoOut:F3} mm2)");

            // S16: warna border (masukan 2026-10-23). Area di dalam garis kiss di luar artwork dicetak CMYK pilihan, diteruskan
            // 1 mm di luar garis (bleed 0) atau sejauh bleed; artwork tetap; tengah lubang donat tetap putih; tanpa perluasan warna tepi.
            var red = new Cmyk(0, 1, 1, 0);
            Placed At2(Sticker s, double x, double y) => new() { S = s, X = x, Y = y };
            var bPlain = At2(Loader.Load(cPath, 0, 40, kiss, die), Margin, Margin);
            var bCircle = At2(Loader.Load(cPath, 0, 40, kiss, die, border: red), Margin + 55, Margin);
            var bDonut = At2(Loader.Load("uji.png", 0, 40, kiss, die, edited: Loader.Png(Donut(70)), border: red), Margin, Margin + 55);
            var bBleed = At2(Loader.Load(cPath, 0, 40, kiss, die, bleed: 2, border: red), Margin + 55, Margin + 55);
            List<Placed> bSheet = [bPlain, bCircle, bDonut, bBleed];
            var bPdf = Path.Combine(Path.GetTempPath(), $"printech-border-{Guid.NewGuid():N}.pdf");
            var bJpgDir = Path.Combine(Path.GetTempPath(), $"printech-border-{Guid.NewGuid():N}");
            string bContent;
            BitmapSource bRender, bJpg;
            try
            {
                Check(Export.Pdf(bPdf, [bSheet], A4W, [A4H]).Count == 0, "peringatan export warna border");
                using (var bd = PdfReader.Open(bPdf, PdfDocumentOpenMode.Import)) bContent = Content(bd.Pages[0]);
                bRender = RenderPage(bPdf, 0, 10);
                Directory.CreateDirectory(bJpgDir);
                var bFiles = Export.Preview(Path.Combine(bJpgDir, "b.jpg"), [bSheet], A4W, [A4H], Margin);
                using var js = File.OpenRead(bFiles[0]);
                bJpg = BitmapDecoder.Create(js, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            }
            finally { File.Delete(bPdf); if (Directory.Exists(bJpgDir)) Directory.Delete(bJpgDir, true); }
            static double Pt(double mm) => mm * 72 / 25.4;
            Color Pdf(Placed p, double x, double y) => At(bRender, p, Pt(x), Pt(y), 10);
            Color Jpg(Placed p, double x, double y)
            {
                double k = bJpg.PixelHeight / A4H;
                var q = Nest.Xf(p).Transform(new Point(p.S.Off + x, p.S.Off + y));
                var px = new byte[4];
                new FormatConvertedBitmap(bJpg, PixelFormats.Bgra32, null, 0).CopyPixels(new Int32Rect((int)(q.X * k), (int)(q.Y * k), 1, 1), px, 4, 0);
                return Color.FromRgb(px[2], px[1], px[0]);
            }
            static bool IsRed(Color c) => c.R > 190 && c.G < 90 && c.B < 90;
            static bool IsWhite(Color c) => c.R > 235 && c.G > 235 && c.B > 235;
            // circle.png: pusat (20; 20) mm, artwork r 20, kiss r 22, die r 24. Donat: lubang r 10,77, garis kiss di lubang r 8,77.
            bool bOk = bContent.Contains("0 1 1 0 k") && IsRed(Pdf(bCircle, 41, 20)) && IsRed(Pdf(bCircle, 42.5, 20)) && IsWhite(Pdf(bCircle, 43.5, 20))
                && IsWhite(Pdf(bPlain, 41, 20)) && Pdf(bCircle, 37, 20) == Pdf(bPlain, 37, 20)
                && IsRed(Pdf(bDonut, 20 + 9.5, 20)) && IsWhite(Pdf(bDonut, 20, 20)) && IsRed(Pdf(bBleed, 43.5, 20)) && bBleed.S.BleedPng == null
                && IsRed(Jpg(bCircle, 41, 20)) && IsWhite(Jpg(bPlain, 41, 20));
            var bEd = new CutEditWindow(circle40, null, null, CutShape.Auto, "warna", red);
            bool bEdit = bEd.Border == red;
            bEd.BorderCmyk.Text = "0 0 0 100";
            bEd.ApplyCmyk();
            bEdit &= bEd.Border == new Cmyk(0, 0, 0, 1);
            bEd.BorderCmyk.Text = "abc";
            bEd.ApplyCmyk();
            bEdit &= bEd.Border == new Cmyk(0, 0, 0, 1);
            bEd.SetBorder(null);
            bEdit &= bEd.Border == null && bEd.ManualKiss == null;
            // Ujung tajam: border dan bleed menjorok sejauh e ke segala arah (sambungan bulat), tanpa lonjakan miter di ujung
            // (sebelumnya ujung bintang 2,26 mm dan segitiga 2,00 mm untuk e = 1 mm). Batas luar = batas garis kiss + e di keempat sisi.
            static double Over(Geometry outer, Geometry inner)
            {
                Rect o = outer.Bounds, i = inner.Bounds;
                return Math.Max(Math.Max(i.Top - o.Top, i.Left - o.Left), Math.Max(o.Right - i.Right, o.Bottom - i.Bottom));
            }
            var bStar = Loader.Load(cPath, 0, 40, kiss, die, shape: CutShape.Star, border: red);
            var bTri = Loader.Load(cPath, 0, 40, kiss, die, shape: CutShape.Triangle, border: red);
            var bStarBleed = Loader.Load(cPath, 0, 40, kiss, die, bleed: 2, shape: CutShape.Star);
            double oStar = Over(bStar.BorderGeo!, bStar.KissGeo!), oTri = Over(bTri.BorderGeo!, bTri.KissGeo!), oBleed = Over(bStarBleed.ArtClip, bStarBleed.KissGeo!);
            bool tips = Math.Abs(oStar - 1) < 0.02 && Math.Abs(oTri - 1) < 0.02 && bStarBleed.BleedPng != null && Math.Abs(oBleed - 2) < 0.02;
            S(16, "warna border: area offset berwarna CMYK, diteruskan di luar garis, artwork dan lubang tetap, layar sama dengan PDF", bOk && bEdit && tips,
                $"PDF: offset {Pdf(bCircle, 41, 20)}, 0,5 mm di luar kiss {Pdf(bCircle, 42.5, 20)}, 1,5 mm di luar {Pdf(bCircle, 43.5, 20)}, tanpa warna {Pdf(bPlain, 41, 20)}; "
                + $"artwork sama {Pdf(bCircle, 37, 20) == Pdf(bPlain, 37, 20)}; donat: tepi lubang {Pdf(bDonut, 29.5, 20)}, tengah {Pdf(bDonut, 20, 20)}; "
                + $"bleed 2: 1,5 mm di luar {Pdf(bBleed, 43.5, 20)}, perluasan warna tepi {(bBleed.S.BleedPng == null ? "tidak" : "ADA")}; "
                + $"operator 0 1 1 0 k {bContent.Contains("0 1 1 0 k")}; preview JPG offset {Jpg(bCircle, 41, 20)}; editor {(bEdit ? "benar" : "SALAH")}; "
                + $"menjorok di ujung tajam (harapan 1/1/2 mm): border bintang {oStar:F2}, segitiga {oTri:F2}, bleed 2 bintang {oBleed:F2} mm");

            // S9: stiker besar 300 mm (gambar 3000 px), kiss + die.
            var bigImg = Draw(dc => dc.DrawEllipse(Brushes.Black, null, new Point(1500, 1500), 1400, 1400), 3000, 3000);
            var bsw = Stopwatch.StartNew();
            var big300 = Png(bigImg, 300, kiss, die);
            S(9, "stiker besar 300 mm dimuat < 5 detik", bsw.ElapsedMilliseconds < 5000, $"{bsw.ElapsedMilliseconds} ms, kiss {Nodes(big300.Kiss)} node, die {Nodes(big300.Die)} node");

            // P1: CDR tanpa CorelDRAW (lewat proses worker): pesan jelas, aplikasi tidak crash.
            var fakeCdr = Path.Combine(dir, "tidak-ada.cdr");
            var cdrStart = DateTime.Now;
            var cdrSw = Stopwatch.StartNew();
            if (Type.GetTypeFromProgID("CorelDRAW.Application") == null)
            {
                // libcdr dicoba dulu, lalu CorelDRAW sebagai cadangan (keputusan user 2026-11-06): file yang tidak terbaca keduanya memberi satu pesan.
                var badCdr = Path.Combine(dir, "rusak.cdr");
                File.WriteAllText(badCdr, "bukan file CDR");
                string msg = "tanpa error";
                try { Loader.LoadAll(badCdr, 40, kiss, die); } catch (InvalidOperationException ex) { msg = ex.Message; }
                File.Delete(badCdr);
                P(1, "CDR yang tidak terbaca tanpa CorelDRAW: pesan jelas, aplikasi tidak crash", msg == Lang.T("BadCdr"), $"{cdrSw.ElapsedMilliseconds} ms: {msg}");
            }
            else P(1, "CDR tanpa CorelDRAW", true, "dilewati: CorelDRAW terpasang, uji manual P7");

            // P2: worker yang melewati batas waktu dihentikan paksa dan tidak ada yang tersisa.
            bool killed = false;
            cdrSw.Restart();
            try { Corel.ToPdf(fakeCdr, timeoutMs: 1); } catch (TimeoutException) { killed = true; }
            long killMs = cdrSw.ElapsedMilliseconds;
            int left = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(Environment.ProcessPath!))
                .Count(p => p.Id != Environment.ProcessId && Started(p) >= cdrStart);
            P(2, "worker yang melewati batas waktu dihentikan", killed && left == 0 && killMs < 2000, $"dihentikan dalam {killMs} ms, worker tersisa {left}");

            // P3: nesting paralel deterministik: hasil sama setiap kali.
            static string Sig(List<List<Placed>> r) => string.Join(";", r.SelectMany((sh, i) => sh.Select(p => $"{i}:{p.S.Name}:{p.X}:{p.Y}:{p.Rot}")));
            int variants = Enumerable.Range(0, 5).Select(_ => Sig(Nest.Run(items, A4W, A4H, Margin, Gap, out _))).Distinct().Count();
            P(3, "nesting paralel deterministik", variants == 1, $"5 kali susun, {variants} hasil berbeda");

            // P4: kikis tepi paralel identik dengan algoritma berurutan; tampilan 1:1 identik dengan hasil penuh.
            var alpha0 = (byte[])ci.Alpha(0).Clone();
            bool erodeSame = Enumerable.Range(1, 3).All(c => ci.Alpha(c).AsSpan().SequenceEqual(ErodeRef(alpha0, ci.W, ci.H, c)));
            var full = (byte[])ci.Compose(1).Clone();
            var one = new byte[full.Length];
            ci.ComposeView(1, one, ci.W, ci.H);
            bool viewSame = one.AsSpan().SequenceEqual(full);
            P(4, "hapus latar paralel identik dengan versi berurutan", erodeSame && viewSame, $"kikis 1-3 px: {(erodeSame ? "sama" : "BEDA")}, tampilan 1:1: {(viewSame ? "sama" : "BEDA")}");

            // P5: kerja di UI thread tetap singkat: susun 130 stiker, satu langkah Rapikan pada gambar 4000x3000.
            var bigClean = new CleanImage(Loader.Png(Draw(dc =>
            {
                dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(30, 90, 168)), null, new Rect(0, 0, 4000, 3000));
                dc.DrawEllipse(Brushes.DarkOrange, null, new Point(2000, 1500), 1300, 1300);
            }, 4000, 3000)));
            var view = new byte[1600 * 1200 * 4];
            long cleanMs = long.MaxValue; // terbaik dari 3: waktu tidak ikut naik karena aplikasi lain sedang sibuk
            for (int run = 0; run < 3; run++)
            {
                bigClean.Reset();
                var rsw = Stopwatch.StartNew();
                bigClean.EraseFromBorder(32);
                bigClean.ComposeView(1, view, 1600, 1200);
                cleanMs = Math.Min(cleanMs, rsw.ElapsedMilliseconds);
            }
            P(5, "UI thread: susun 130 stiker < 1000 ms, satu langkah Rapikan 4000x3000 < 2000 ms", nestMs < 1000 && cleanMs < 2000,
                $"susun {nestMs} ms, rapikan {cleanMs} ms ({bigClean.ErasedFraction:P0} dihapus)");

            // Auto-bleed raster: warna tepi meluas sampai bleed di luar garis kiss (dicek di render PDF), vektor tidak berubah.
            var thin = new Cut(true, 0.5, 0);
            Color OutsideArt(double bleedMm, double mm) // warna di render PDF, mm di kiri tepi lingkaran pada garis tengah
            {
                var st = Loader.Load(Path.Combine(dir, "circle.png"), 0, 40, thin, none, bleed: bleedMm);
                var sh = Nest.Run([st], A4W, A4H, Margin, Gap, out _);
                var pdf = Path.Combine(dir, "bleed.pdf");
                Export.Pdf(pdf, sh, A4W, [A4H]);
                return At(RenderPage(pdf, 0, 4), sh[0][0], -mm * 72 / 25.4, st.ArtH / 2 * 72 / 25.4, 4);
            }
            static bool White(Color c) => c.R > 240 && c.G > 240 && c.B > 240;
            static bool Orange(Color c) => c.R > 200 && c.G is > 120 and < 210 && c.B < 80;
            Color noBleed = OutsideArt(0, 1.5), bleedNear = OutsideArt(2, 1.5), bleedFar = OutsideArt(2, 4);
            var vecBleed = Loader.Load(Path.Combine(dir, "logo.pdf"), 0, 0, kiss, die, bleed: 2);
            Check(White(noBleed) && Orange(bleedNear) && White(bleedFar) && vecBleed.BleedPng == null,
                $"bleed salah: 1,5 mm di luar tepi tanpa bleed {noBleed}, bleed 2 mm {bleedNear}; 4 mm {bleedFar}; vektor {(vecBleed.BleedPng == null ? "tanpa bleed" : "ikut bleed")}");
            log.Add($"bleed: 1,5 mm di luar tepi lingkaran: tanpa bleed {noBleed}, bleed 2 mm {bleedNear}; 4 mm (di luar bleed) {bleedFar}; logo.pdf vektor tidak di-bleed");

            // Bleed tidak membuat bagian semi-transparan jadi pekat (temuan 2026-10-28): bayangan hitam alpha 40 di sekeliling
            // lingkaran oranye tetap tembus, dan zona bleed berwarna tepi oranye, bukan hitam.
            var shadowPng = Loader.Png(Draw(dc =>
            {
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)), null, new Point(200, 200), 150, 150);
                dc.DrawEllipse(Brushes.DarkOrange, null, new Point(200, 200), 100, 100);
            }, 400, 400));
            Color Shadow(double bleedMm, double x) // warna di render PDF, x mm dari kiri artwork pada garis tengah
            {
                var st = Loader.Load("bayangan.png", 0, 40, thin, none, edited: shadowPng, bleed: bleedMm);
                var sh = Nest.Run([st], A4W, A4H, Margin, Gap, out _);
                var pdf = Path.Combine(dir, "bleed.pdf");
                Export.Pdf(pdf, sh, A4W, [A4H]);
                return At(RenderPage(pdf, 0, 4), sh[0][0], x * 72 / 25.4, st.ArtH / 2 * 72 / 25.4, 4);
            }
            // Artwork 40 mm = bayangan r 150 px: objek (r 50 px) di 26,7 mm, bayangan (r 125 px) di 36,7 mm, zona bleed 41,5 mm
            // (garis kiss 40,5 mm, batas bleed 42,5 mm).
            Color sObj = Shadow(2, 26.7), sRing = Shadow(2, 36.7), sOut = Shadow(2, 41.5), sRing0 = Shadow(0, 36.7);
            Check(Orange(sObj) && sRing.R > 150 && sRing.G < 160 && Orange(sOut) && sRing0.R > 200 && sRing0.G > 200,
                $"bleed membuat bayangan semi-transparan pekat: objek {sObj}, bayangan {sRing} (tanpa bleed {sRing0}), zona bleed {sOut}");
            log.Add($"bleed: bayangan alpha 40 tetap tembus: objek {sObj}, bayangan {sRing} (tanpa bleed {sRing0}), zona bleed {sOut}");

            // Tepi pekat bertekstur (120 juring hitam/putih, ±1 mm per juring di tepi) tidak membentuk garis lurus di zona bleed:
            // lapisan perluasan dihaluskan ±1 mm, jadi zona bleed abu rata, bukan selang-seling hitam/putih.
            var raysPng = Loader.Png(Draw(dc =>
            {
                for (int i = 0; i < 120; i++)
                {
                    var g = new StreamGeometry();
                    using (var sg = g.Open())
                    {
                        sg.BeginFigure(new Point(200, 200), true, true);
                        sg.LineTo(new Point(200 + 150 * Math.Cos(i * Math.PI / 60), 200 + 150 * Math.Sin(i * Math.PI / 60)), false, false);
                        sg.LineTo(new Point(200 + 150 * Math.Cos((i + 1) * Math.PI / 60), 200 + 150 * Math.Sin((i + 1) * Math.PI / 60)), false, false);
                    }
                    dc.DrawGeometry(i % 2 == 0 ? Brushes.Black : Brushes.White, null, g);
                }
            }, 400, 400));
            var rays = Loader.Load("juring.png", 0, 40, thin, none, edited: raysPng, bleed: 2);
            var raySheet = Nest.Run([rays], A4W, A4H, Margin, Gap, out _);
            var rayPdf = Path.Combine(dir, "bleed.pdf");
            Export.Pdf(rayPdf, raySheet, A4W, [A4H]);
            var rayPage = RenderPage(rayPdf, 0, 4);
            // Artwork 40 mm, pusat (20; 20) mm, tepi r 20 mm, garis kiss r 20,5, batas bleed r 22,5: sampel di r 21,5 pada tengah tiap juring.
            var rayLum = Enumerable.Range(0, 120).Select(i =>
            {
                double t = (i + 0.5) * Math.PI / 60;
                var c = At(rayPage, raySheet[0][0], (20 + 21.5 * Math.Cos(t)) * 72 / 25.4, (20 + 21.5 * Math.Sin(t)) * 72 / 25.4, 4);
                return (c.R + c.G + c.B) / 3;
            }).ToList();
            int rayRange = rayLum.Max() - rayLum.Min();
            Check(rayRange < 60, $"bleed bergaris: kecerahan di zona bleed dari {rayLum.Min()} sampai {rayLum.Max()} (harapan rata, selisih < 60)");
            log.Add($"bleed: tepi bertekstur 120 juring hitam/putih, kecerahan zona bleed {rayLum.Min()}-{rayLum.Max()} (selisih {rayRange})");

            // D9: file rusak memberi pesan yang bisa dipahami operator, bukan pesan teknis PDFium, WIC, atau XML.
            string LoadError(string name, string content)
            {
                var f = Path.Combine(dir, name);
                File.WriteAllText(f, content);
                try { Loader.LoadAll(f, 40, kiss, die); return "tanpa error"; }
                catch (Exception ex) { return ex.Message; }
                finally { File.Delete(f); }
            }
            string eAi = LoadError("rusak.ai", "%!PS-Adobe-3.0\n%%EOF\n"), ePdf = LoadError("rusak.pdf", "bukan pdf"),
                ePng = LoadError("rusak.png", "bukan gambar"), eJpg = LoadError("rusak.jpg", "bukan gambar"), eSvg = LoadError("rusak.svg", "bukan svg");
            Check(eAi == Lang.T("BadAi") && ePdf == Lang.T("BadPdf") && ePng == Lang.T("BadImage") && eJpg == Lang.T("BadImage") && eSvg == Lang.T("BadSvg"),
                $"D9: pesan file rusak: ai '{eAi}', pdf '{ePdf}', png '{ePng}', jpg '{eJpg}', svg '{eSvg}'");
            log.Add($"file rusak: ai '{eAi}'; pdf '{ePdf}'; png dan jpg '{ePng}'; svg '{eSvg}'");

            // Latar opak tidak dibuang otomatis (D10, masukan leader 2026-10-21): putih, abu muda, dan pastel tetap ikut,
            // artwork = seluruh gambar 300x200 px = 60x40 mm (bukan kotak biru 60x60 mm).
            foreach (var bg in new[] { Colors.White, Color.FromRgb(242, 242, 242), Color.FromRgb(255, 240, 245) })
            {
                var lt = Png(Draw(dc => { dc.DrawRectangle(new SolidColorBrush(bg), null, new Rect(0, 0, 300, 200)); dc.DrawRectangle(Brushes.SteelBlue, null, new Rect(100, 50, 100, 100)); }, 300, 200), 60, kiss, none);
                Check(Math.Abs(lt.ArtH - 40) < 0.5, $"latar {bg} dibuang: artwork {lt.ArtW:F1}x{lt.ArtH:F1} mm, harapan 60x40");
            }

            var sheets = Nest.Run(items, A4W, A4H, Margin, Gap, out int tooBig);
            Check(tooBig == 0, $"{tooBig} stiker tidak muat");
            Check(sheets.Count >= 2, "tidak meluap ke lembar kedua");
            Check(sheets.Sum(x => x.Count) == items.Sum(s => s.Copies), "jumlah tempatan salah");
            foreach (var sh in sheets) Check(Nest.Check(sh, A4W, A4H, Margin) == 0, "ada tumpang tindih / keluar margin");
            log.Add($"susun: {sheets.Sum(x => x.Count)} stiker -> {sheets.Count} lembar A4, pemakaian {Use(sheets):P1}");

            // Uji deteksi tumpang tindih: geser satu stiker ke atas tetangganya.
            var probe = sheets[0];
            var (ox, oy) = (probe[1].X, probe[1].Y);
            (probe[1].X, probe[1].Y) = (probe[0].X, probe[0].Y);
            Check(Nest.Check(probe, A4W, A4H, Margin) >= 2, "tumpang tindih tidak terdeteksi");
            (probe[1].X, probe[1].Y) = (ox, oy);
            Nest.Check(probe, A4W, A4H, Margin);

            // Snap magnet: stiker yang dijatuhkan di atas tetangganya, atau diputar, pindah ke posisi kosong terdekat.
            var sa = new Placed { S = circle40, X = Margin, Y = Margin };
            var sb = new Placed { S = pdfItem, X = Margin + 3, Y = Margin + 3 };
            var two = new List<Placed> { sa, sb };
            bool snapped = Nest.Snap(two, sb, A4W, A4H, Margin, Gap);
            int afterDrop = Nest.Check(two, A4W, A4H, Margin);
            double jump = Math.Sqrt(Math.Pow(sb.X - Margin - 3, 2) + Math.Pow(sb.Y - Margin - 3, 2));
            sb.Rot = 90;
            bool snappedRot = Nest.Snap(two, sb, A4W, A4H, Margin, Gap);
            int afterRot = Nest.Check(two, A4W, A4H, Margin);
            Check(snapped && afterDrop == 0 && snappedRot && afterRot == 0, $"snap gagal: jatuh {afterDrop} tabrakan, putar {afterRot} tabrakan");
            log.Add($"snap: dijatuhkan di atas tetangga -> pindah {jump:F0} mm, 0 tabrakan; diputar -> 0 tabrakan");

            // S17: tanda registrasi (temuan leader 2026-10-26: stiker kena tanda). Area pindai Graphtec di sekitar tanda (a = 6 mm,
            // manual CE7000 bab 5) tidak boleh terisi stiker: susun, Penuhi lembar, dan snap menjauhinya; Check menandai stiker di dalamnya.
            var regLines = RegistrationMarks.Lines(A4W, A4H, Margin);
            double regClear = RegistrationMarks.Stroke / 2 + RegistrationMarks.ScanClear;
            double ToMarks(IEnumerable<Placed> ps) => ps.Select(p =>
            {
                var g = p.S.Outer.Clone();
                g.Transform = Nest.Xf(p);
                return g.GetFlattenedPathGeometry(0.05, ToleranceType.Absolute).Figures
                    .SelectMany(f => f.Segments.SelectMany(s => s is PolyLineSegment pl ? pl.Points : s is LineSegment l ? [l.Point] : (IEnumerable<Point>)[]).Prepend(f.StartPoint))
                    .Min(q => regLines.Min(l => (q - Loader.Nearest(q, l.A, l.B)).Length));
            }).DefaultIfEmpty(double.MaxValue).Min();
            var regRun = Nest.Run(items, A4W, A4H, Margin, Gap, out _, marks: true);
            var regFill = regRun.Select(sh => sh.ToList()).ToList();
            int regAdded = Nest.Fill(regFill, items, A4W, A4H, Margin, Gap, marks: true);
            var corner = new Placed { S = bx, X = Margin, Y = Margin };
            int badOn = Nest.Check([corner], A4W, A4H, Margin, marks: true), badOff = Nest.Check([corner], A4W, A4H, Margin);
            bool snapOut = Nest.Snap([corner], corner, A4W, A4H, Margin, Gap, marks: true);
            double dRun = regRun.Min(ToMarks), dFill = regFill.Min(ToMarks), dSnap = ToMarks([corner]), dOld = sheets.Min(ToMarks);
            double regStroke = RegistrationMarks.Stroke, regLen = RegistrationMarks.Length; // rentang Graphtec: tebal 0,3-1,0 mm, ukuran 5-20 mm
            bool regOk = regLines.Count == 8 && regStroke is >= 0.3 and <= 1 && regLen is >= 5 and <= 20
                && dRun >= regClear && dFill >= regClear && regAdded > 0 && snapOut && dSnap >= regClear && badOn == 1 && badOff == 0 && dOld < regClear
                && regRun.All(sh => Nest.Check(sh, A4W, A4H, Margin, marks: true) == 0);
            S(17, "tanda registrasi: stiker tidak masuk area pindai 6 mm di sekitar tanda (susun, Penuhi lembar, snap), stiker di pojok ditandai",
                regOk, $"jarak terdekat ke tanda: susun {dRun:F2}, Penuhi lembar {dFill:F2} (+{regAdded} stiker), snap {dSnap:F2} mm (batas {regClear:F2}); "
                + $"tanpa tanda {dOld:F2} mm; pojok ditandai {badOn}/{badOff}; tebal {regStroke} mm, panjang {regLen} mm");

            // Indikator bahan: bentuk reguler vs irregular, warna target, Print Ticket 1 halaman A4.
            var fillOf = items.ToDictionary(s => s.Name, s => s.Fill);
            Check(fillOf["badge.svg"] > 0.85 && Math.Abs(fillOf["circle.png"] - Math.PI / 4) < 0.03 && fillOf["star.jpg"] < 0.75,
                $"isi bentuk salah: badge {fillOf["badge.svg"]:F2}, circle {fillOf["circle.png"]:F2}, star {fillOf["star.jpg"]:F2}");
            Check(Yield.Level(0.76, true) == 2 && Yield.Level(0.72, true) == 1 && Yield.Level(0.65, true) == 0
                && Yield.Level(0.61, false) == 2 && Yield.Level(0.56, false) == 1 && Yield.Level(0.50, false) == 0, "batas warna bahan salah");
            var ticket = Path.Combine(dir, "print-ticket.pdf");
            Export.Ticket(ticket, "A4", sheets, A4W, sheets.Select(_ => A4H).ToList(), Margin, ["uji"]);
            using (var tk = PdfReader.Open(ticket, PdfDocumentOpenMode.Import))
                Check(tk.PageCount == 1 && Math.Abs(tk.Pages[0].Width.Millimeter - 210) < 0.5, "Print Ticket bukan 1 halaman A4");
            log.Add($"bahan: isi bentuk badge {fillOf["badge.svg"]:F2}, circle {fillOf["circle.png"]:F2}, star {fillOf["star.jpg"]:F2}; "
                + $"lembar 1 {Yield.Use(sheets[0], A4W, A4H, Margin):P0} ({(Yield.Regular(sheets[0]) ? "reguler" : "irregular")}); Print Ticket 1 halaman A4");

            var heights = sheets.Select(_ => A4H).ToList();
            var outPdf = Path.Combine(dir, "export.pdf");
            var warn = Export.Pdf(outPdf, sheets, A4W, heights);
            Check(warn.Count == 0, "peringatan export: " + string.Join("; ", warn));

            using (var r = new Pdfium(outPdf))
                Check(r.Pages == sheets.Count, "jumlah halaman != jumlah lembar");

            // Artwork PDF harus tepat di posisinya: titik biru (persegi) dan oranye (lingkaran) pada render export.
            int si = sheets.FindIndex(x => x.Any(p => p.S == pdfItem));
            var pp = sheets[si].First(p => p.S == pdfItem);
            var render = RenderPage(outPdf, si, 4);
            Save(render, Path.Combine(dir, $"export-render-{si + 1}.png"));
            var blue = At(render, pp, 70, 60, 4);
            var orange = At(render, pp, 20, 60, 4);
            Check(blue.B > 150 && blue.R < 100, $"posisi artwork PDF salah (biru = {blue})");
            Check(orange.R > 200 && orange.B < 100, $"posisi artwork PDF salah (oranye = {orange})");

            using (var doc = PdfReader.Open(outPdf, PdfDocumentOpenMode.Import))
            {
                var page = doc.Pages[si];
                var xo = page.Resources.Elements.GetDictionary("/XObject");
                Check(xo != null && xo.Elements.Values.Any(v => (v as PdfReference)?.Value is PdfDictionary d && d.Elements.GetName("/Subtype") == "/Form"),
                    "artwork PDF tidak tertempel sebagai vektor (Form XObject)");
                var content = Content(page);
                Check(content.Contains("0 1 0 0 K") && content.Contains("1 0 0 0 K"), "garis potong bukan CMYK magenta/cyan");
                int lines = Regex.Matches(content, @"(?<=\s)l(?=\s)").Count, curves = Regex.Matches(content, @"(?<=\s)c(?=\s)").Count;
                // DeviceCMYK murni: warna garis hanya lewat operator K, tanpa RGB atau ruang warna lain (ICC/sRGB).
                var strokes = Regex.Matches(content, @"([\d.]+ [\d.]+ [\d.]+ [\d.]+) K").Select(m => m.Groups[1].Value).Distinct().Order().ToList();
                int otherColor = Regex.Matches(content, @"(?<=\s)(RG|rg|CS|cs|SC|SCN|sc|scn)(?=\s)").Count;
                S(8, "PDF export: garis potong hanya kurva Bezier, warna DeviceCMYK murni",
                    lines == 0 && curves > 0 && strokes.SequenceEqual(["0 1 0 0", "1 0 0 0"]) && otherColor == 0,
                    $"halaman {si + 1}: {curves} kurva, {lines} garis lurus, warna garis {string.Join(" / ", strokes)}, operator RGB/ruang warna lain {otherColor}");
            }

            // S18: susun berbaris (permintaan user 2026-11-02), tanpa dan dengan tanda registrasi; lalu desain sedikit mengisi sisa baris
            // rata tengah, desain yang cukup untuk satu baris mulai di baris baru, diputar hanya bila perlu, dan yang terlalu besar dihitung.
            var (rowBad, rowLog) = (new List<string>(), new List<string>());
            foreach (bool marks in new[] { false, true })
            {
                var job = Load();
                int want = job.Sum(s => s.Copies);
                var rs = Nest.Rows(job, A4W, A4H, Margin, Gap, out int rowBig, marks);
                if (rowBig != 0 || rs.Sum(x => x.Count) != want || rs.Count < 2) rowBad.Add($"{rs.Sum(x => x.Count)} dari {want} stiker di {rs.Count} lembar, tidak muat {rowBig}");
                rowBad.AddRange(RowsErrors(rs, marks));
                var rsw = Stopwatch.StartNew();
                var rf = Nest.FillRows(job, A4W, A4H, Margin, Gap, out int rowAdded, marks);
                long fillMs = rsw.ElapsedMilliseconds;
                Nest.FillRows(job, A4W, A4H, Margin, Gap, out int rowAgain, marks);
                if (rowAdded <= 0 || rowAgain != 0 || rf.Count != rs.Count || rf.Sum(x => x.Count) != want + rowAdded)
                    rowBad.Add($"Penuhi lembar: +{rowAdded}, isi ulang +{rowAgain}, lembar {rs.Count} -> {rf.Count}");
                rowBad.AddRange(RowsErrors(rf, marks));
                rowLog.Add($"{(marks ? "dengan" : "tanpa")} tanda registrasi: {want} stiker -> {rs.Count} lembar A4, pemakaian {Use(rs):P1} (padat {(marks ? regRun : sheets).Count} lembar, {Use(marks ? regRun : sheets):P1}), Penuhi lembar +{rowAdded} ({fillMs} ms)");
                var rowBled = Loader.Load(Path.Combine(dir, "circle.png"), 0, 40, kiss, die, bleed: 2);
                rowBled.Copies = 20;
                rowBad.AddRange(RowsErrors(Nest.Rows([rowBled], A4W, A4H, Margin, Gap, out _, marks), marks));
            }
            Sticker Box(int w, int h, int copies = 1)
            {
                var b = Png(Draw(dc => dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, w * 2, h * 2)), w * 2, h * 2), w, kiss, die);
                b.Copies = copies;
                return b;
            }
            var (flat, tall) = (Box(30, 20), Box(30, 60));
            var mix = Nest.Rows([flat, tall], A4W, A4H, Margin, Gap, out _);
            if (mix.Count != 1 || mix[0][0].S != tall || mix[0][1].X <= mix[0][0].X || Math.Abs(mix[0][0].Y + mix[0][0].BH / 2 - mix[0][1].Y - mix[0][1].BH / 2) > 1)
                rowBad.Add("dua desain satuan tidak sebaris, tidak rata tengah, atau desain tertinggi tidak dulu");
            var (rowA, rowB) = (Box(30, 30, 7), Box(30, 20, 7));
            var blocks = Nest.Rows([rowA, rowB], A4W, A4H, Margin, Gap, out _).SelectMany(x => x).ToList();
            var (lastA, firstB) = (blocks.Where(p => p.S == rowA).Max(p => p.Y), blocks.First(p => p.S == rowB));
            if (firstB.Y <= lastA || firstB.X != blocks[0].X) rowBad.Add("desain yang cukup untuk satu baris tidak mulai di baris baru");
            var turned = Nest.Rows([Box(220, 60)], A4W, A4H, Margin, Gap, out int wideBig);
            Nest.Rows([Box(400, 400)], A4W, A4H, Margin, Gap, out int hugeBig);
            if (wideBig != 0 || turned[0][0].Rot % 180 == 0 || hugeBig != 1) rowBad.Add($"putar atau terlalu besar salah: tidak muat {wideBig}, {hugeBig}");
            S(18, "susun berbaris: baris dan kolom lurus, salinan berkumpul, tanpa tabrakan, diputar hanya bila perlu, Penuhi lembar", rowBad.Count == 0,
                string.Join("; ", rowBad.Distinct().Concat(rowLog)));

            // S19: duplikat kanvas dan tempel stiker (permintaan user 2026-11-02). Salinan kanvas sama persis dan terpisah dari aslinya;
            // tempel berjalan ke kanan lalu ke bawah, segaris dengan stiker asli, tanpa tabrakan, sampai tidak ada tempat.
            var (copyBad, copyLog) = (new List<string>(), new List<string>());
            static List<(Sticker, double, double, int)> Spots(List<Placed> ps) => [.. ps.Select(p => (p.S, p.X, p.Y, p.Rot))];
            var dup = sheets.Select(sh => sh.Select(p => new Placed { S = p.S, X = p.X, Y = p.Y, Rot = p.Rot }).ToList()).ToList();
            List<double> dupH = [A4H, .. sheets.Skip(1).Select(_ => 200.0)];
            Nest.CopySheet(dup, dupH, 0);
            bool dupOk = dup.Count == sheets.Count + 1 && Spots(dup[0]).SequenceEqual(Spots(sheets[0])) && Spots(dup[1]).SequenceEqual(Spots(sheets[0]))
                && dup.Skip(2).Zip(sheets.Skip(1)).All(x => Spots(x.First).SequenceEqual(Spots(x.Second)))
                && dupH.SequenceEqual([A4H, A4H, .. sheets.Skip(1).Select(_ => 200.0)]);
            if (dupOk)
            {
                dup[1][0].X += 5;
                dupOk = dup[0][0].X == sheets[0][0].X;
                dup[1][0].X -= 5;
            }
            if (!dupOk) copyBad.Add($"salinan kanvas salah: {sheets.Count} -> {dup.Count} lembar");
            if (dup.Any(sh => Nest.Check(sh, A4W, A4H, Margin) != 0)) copyBad.Add("salinan kanvas bertabrakan atau keluar margin");
            var dupPdf = Path.Combine(Path.GetTempPath(), $"printech-duplikat-{Guid.NewGuid():N}.pdf");
            try
            {
                Export.Pdf(dupPdf, dup, A4W, [.. dup.Select(_ => A4H)]);
                using var dupRead = new Pdfium(dupPdf);
                if (dupRead.Pages != dup.Count) copyBad.Add($"PDF {dupRead.Pages} halaman, kanvas {dup.Count}");
            }
            finally { File.Delete(dupPdf); }
            foreach (bool marks in new[] { false, true })
            {
                Placed? Next(List<Placed> ps, Placed from) => Nest.PasteNext(ps, from.S, from.Rot, from.X, from.Y, A4W, A4H, Margin, Gap, marks);
                var unit = Box(40, 30);
                var page = Nest.Rows([unit], A4W, A4H, Margin, Gap, out _, marks)[0];
                double step = 0;
                if (Next(page, page[0]) is { } right)
                {
                    step = right.X - page[0].X;
                    if (right.Y != page[0].Y || step < 0 || step >= (unit.DieGeo ?? unit.KissGeo)!.Bounds.Width + Gap + 2) copyBad.Add($"salinan pertama tidak tepat di kanan: geser {step:F1} mm");
                }
                else copyBad.Add("tempel pertama tidak menemukan tempat");
                var last = page[0];
                while (page.Count < 200 && Next(page, last) is { } next)
                {
                    page.Add(next);
                    last = next;
                }
                copyBad.AddRange(RowsErrors([page], marks));
                if (!page.SequenceEqual(page.OrderBy(p => p.Y).ThenBy(p => p.X))) copyBad.Add("urutan tempel bukan ke kanan lalu ke bawah");
                unit.Copies = 100;
                int rowsFull = Nest.Rows([unit], A4W, A4H, Margin, Gap, out _, marks)[0].Count;
                if (page.Count != rowsFull) copyBad.Add($"tempel sampai penuh {page.Count} stiker, susun berbaris {rowsFull}");
                copyLog.Add($"{(marks ? "dengan" : "tanpa")} tanda registrasi: kotak 40 x 30 mm ditempel sampai penuh {page.Count} stiker, langkah {step:F0} mm");

                // Mulai dari tengah: baris penuh turun ke kolom paling kiri yang segaris, dan baris di atas stiker asli tidak dipakai.
                var center = new Placed { S = unit, X = Margin + 60, Y = Margin + 40 };
                var midPage = new List<Placed> { center };
                last = center;
                while (midPage.Count < 200 && Next(midPage, last) is { } more && more.Y == center.Y)
                {
                    midPage.Add(more);
                    last = more;
                }
                if (Next(midPage, last) is not { } down || down.Y <= center.Y || down.X >= center.X || Math.Abs(Math.IEEERemainder(center.X - down.X, step)) > 1e-6)
                    copyBad.Add("baris penuh tidak turun ke kolom kiri yang segaris");

                var near = new List<Placed> { new() { S = unit, X = Margin + 10, Y = Margin + 40 }, new() { S = unit, X = Margin + 10 + step, Y = Margin + 40 } };
                if (Next(near, near[0]) is not { } skip || skip.Y != near[0].Y || Math.Abs(skip.X - (near[0].X + 2 * step)) > 1e-6)
                    copyBad.Add("posisi yang terisi tidak dilewati satu kotak penuh");

                var side = new Placed { S = Box(60, 30), Rot = 90, X = Margin + 10, Y = Margin + 40 };
                if (Next([side], side) is not { } sideCopy || sideCopy.Rot != 90 || sideCopy.Y != side.Y || sideCopy.X <= side.X || Nest.Check([side, sideCopy], A4W, A4H, Margin, marks) != 0)
                    copyBad.Add("rotasi tidak ikut, atau salinan yang diputar bertabrakan");

                var outside = new Placed { S = unit, X = Margin - 5, Y = Margin - 5 };
                if (Next([outside], outside) is not { } inside || Nest.Check([inside], A4W, A4H, Margin, marks) != 0)
                    copyBad.Add("stiker asli keluar margin: tempel gagal");
            }
            if (Nest.PasteNext([], Box(400, 400), 0, Margin, Margin, A4W, A4H, Margin, Gap) != null) copyBad.Add("desain lebih besar dari kertas tetap ditempel");
            S(19, "duplikat kanvas dan tempel stiker: salinan kanvas sama persis, tempel ke kanan lalu ke bawah, segaris, tanpa tabrakan", copyBad.Count == 0,
                string.Join("; ", copyBad.Distinct().Concat(copyLog)));

            // S20: atur ukuran di kanvas dan tepi kiri susun Rapi (permintaan user 2026-11-03). Pojok seberang diam, rasio terkunci atau bebas (2026-11-07), jarak garis
            // potong tetap, batas 5 mm dan area cetak, hanya stiker yang diubah (2026-11-07); baris Rapi mulai dari tepi kiri tanpa menambah lembar.
            var (sizeBad, sizeLog) = (new List<string>(), new List<string>());
            static bool About(double a, double b, double tol = 0.11) => Math.Abs(a - b) <= tol;
            var grip = new Placed { S = Box(40, 30), X = 50, Y = 60 };
            double gripX = grip.BW - grip.S.ArtW, gripY = grip.BH - grip.S.ArtH;
            var (rbW, _, rb) = Nest.ResizeTo(grip, 2, grip.X + 80 + gripX, grip.Y, A4W, A4H, Margin);
            if (!About(rbW, 80) || rb.X != grip.X || rb.Y != grip.Y || !About(rb.Width, 80 + gripX) || !About(rb.Height, 60 + gripY)) sizeBad.Add($"tarik kanan bawah: lebar {rbW:F1}, kotak {rb.Width:F1} x {rb.Height:F1}");
            var (ltW, _, ltBox) = Nest.ResizeTo(grip, 0, grip.X + grip.BW - 20 - gripX, grip.Y + grip.BH - 15 - gripY, A4W, A4H, Margin);
            if (!About(ltW, 20) || !About(ltBox.Right, grip.X + grip.BW) || !About(ltBox.Bottom, grip.Y + grip.BH)) sizeBad.Add($"tarik kiri atas: lebar {ltW:F1}, kanan bawah tidak diam");
            var (rtW, _, rt) = Nest.ResizeTo(grip, 1, grip.X + 60 + gripX, grip.Y + grip.BH - 10, A4W, A4H, Margin);
            if (!About(rtW, 60) || rt.X != grip.X || !About(rt.Bottom, grip.Y + grip.BH)) sizeBad.Add($"tarik kanan atas: lebar {rtW:F1}, kiri bawah tidak diam");
            var (lbW, _, lb) = Nest.ResizeTo(grip, 3, grip.X + grip.BW - 60 - gripX, grip.Y + 10, A4W, A4H, Margin);
            if (!About(lbW, 60) || !About(lb.Right, grip.X + grip.BW) || lb.Y != grip.Y) sizeBad.Add($"tarik kiri bawah: lebar {lbW:F1}, kanan atas tidak diam");

            var quarter = new Placed { S = Box(60, 30), Rot = 90, X = 50, Y = 60 };
            double quarterX = quarter.BW - quarter.S.ArtH, quarterY = quarter.BH - quarter.S.ArtW;
            var (qW, _, qBox) = Nest.ResizeTo(quarter, 2, quarter.X, quarter.Y + 120 + quarterY, A4W, A4H, Margin);
            if (!About(qW, 120) || !About(qBox.Width, 60 + quarterX) || !About(qBox.Height, 120 + quarterY)) sizeBad.Add($"stiker diputar: lebar {qW:F1}, kotak {qBox.Width:F1} x {qBox.Height:F1}");

            var (minW, _, _) = Nest.ResizeTo(grip, 2, 0, 0, A4W, A4H, Margin);
            if (!About(minW, 6.7)) sizeBad.Add($"batas kecil: lebar {minW:F1} mm, harapan 6,7 (sisi pendek 5 mm)");
            var tiny = new Placed { S = Box(4, 3), X = 50, Y = 60 };
            var (tinyW, _, _) = Nest.ResizeTo(tiny, 2, 0, 0, A4W, A4H, Margin);
            if (!About(tinyW, tiny.S.ArtW)) sizeBad.Add($"desain yang sudah kecil ikut membesar: {tinyW:F1} mm");
            var (maxW, _, maxBox) = Nest.ResizeTo(grip, 2, 5000, 5000, A4W, A4H, Margin);
            if (maxBox.Width > A4W - 2 * Margin + 0.11 || maxBox.Height > A4H - 2 * Margin + 0.11 || maxW <= 40) sizeBad.Add($"batas area cetak: kotak {maxBox.Width:F1} x {maxBox.Height:F1}");
            var (oddW, _, _) = Nest.ResizeTo(grip, 2, grip.X + 33.33 + gripX, grip.Y, A4W, A4H, Margin);
            if (!About(oddW, 33.3, 1e-9)) sizeBad.Add($"lebar tidak dibulatkan ke 0,1 mm: {oddW}");

            var sizeCircle = Loader.Load(cPath, 0, 40, kiss, die);
            double sizeGap = sizeCircle.Outer.Bounds.Width - sizeCircle.ArtW;
            sizeCircle.WidthMm = 80;
            var sizeBig = Loader.Reload(sizeCircle, sizeCircle.LoadedKiss, sizeCircle.LoadedDie, sizeCircle.LoadedBleed);
            double sizeGap2 = sizeBig.Outer.Bounds.Width - sizeBig.ArtW;
            if (!About(sizeBig.ArtW, 80) || !About(sizeBig.ArtH, 2 * sizeCircle.ArtH, 0.2) || !About(sizeGap2, sizeGap, 0.5)) sizeBad.Add($"muat ulang: lebar {sizeBig.ArtW:F1}, jarak garis {sizeGap:F2} -> {sizeGap2:F2} mm");
            var sizeStar = Loader.Load(cPath, 0, 40, kiss, die, shape: CutShape.Star, border: new Cmyk(0, 1, 1, 0), dieShape: CutShape.Rect);
            sizeStar.WidthMm = 60;
            var sizeStar2 = Loader.Reload(sizeStar, sizeStar.LoadedKiss, sizeStar.LoadedDie, sizeStar.LoadedBleed);
            if (!About(sizeStar2.ArtW, 60) || sizeStar2.Shape != CutShape.Star || sizeStar2.DieShape != CutShape.Rect || sizeStar2.Border != sizeStar.Border) sizeBad.Add("muat ulang: bentuk atau warna border hilang");
            var sizePdf = Loader.Load(Path.Combine(dir, "logo.pdf"), 0, 40, kiss, die);
            sizePdf.WidthMm = 60;
            var sizePdf2 = Loader.Reload(sizePdf, sizePdf.LoadedKiss, sizePdf.LoadedDie, sizePdf.LoadedBleed);
            if (!About(sizePdf2.ArtW, 60) || sizePdf2.Pdf == null) sizeBad.Add("muat ulang: PDF tidak lagi vektor");
            var sizeHand = Loader.Load(cPath, 0, 40, kiss, die, manualKiss: pk, manualDie: pd);
            sizeHand.WidthMm = 80;
            var sizeHand2 = Loader.Reload(sizeHand, sizeHand.LoadedKiss, sizeHand.LoadedDie, sizeHand.LoadedBleed);
            if (!About(sizeHand2.Outer.Bounds.Width, man80.Outer.Bounds.Width, 0.01)) sizeBad.Add($"muat ulang: garis manual {sizeHand2.Outer.Bounds.Width:F2} mm, harapan {man80.Outer.Bounds.Width:F2}");

            var (swapOld, swapNew) = (Box(40, 30), Box(60, 45));
            var swap = new List<List<Placed>> { new() { new() { S = swapOld, X = 20, Y = 20 }, new() { S = grip.S, X = 120, Y = 20 } }, new() { new() { S = swapOld, X = 30, Y = 30 } } };
            Nest.SwapDesign(swap, swapOld, swapNew);
            if (swap.SelectMany(x => x).Count(p => p.S == swapNew) != 2 || swap[0][1].S != grip.S || swap[1][0].X != 30) sizeBad.Add("ganti desain: salinan lain tidak ikut berganti");

            // Atur ukuran satu stiker saja (keputusan user 2026-11-07), seperti EndResize: desain yang juga dipakai stiker lain dipisah menjadi baris baru
            // di bawahnya, stiker lain tidak berubah. Tanpa stiker lain (semua yang memakai desain itu ikut diubah), desainnya sendiri yang diganti.
            var solo = Loader.Load(cPath, 0, 40, kiss, die);
            solo.Copies = 3;
            var (soloA, soloB, soloC) = (new Placed { S = solo, X = 20, Y = 20 }, new Placed { S = solo, X = 80, Y = 20, Rot = 90 }, new Placed { S = solo, X = 20, Y = 20 });
            var soloBook = new List<List<Placed>> { new() { soloA, soloB }, new() { soloC } };
            var soloItems = new List<Sticker> { solo, grip.S };
            var soloBig = Loader.Reload(solo, solo.LoadedKiss, solo.LoadedDie, solo.LoadedBleed, 60);
            if (!Nest.Split(soloItems, soloBook, [soloA], solo, soloBig)) Nest.SwapDesign(soloBook, solo, soloBig);
            if (soloA.S != soloBig || soloB.S != solo || soloC.S != solo || !About(solo.WidthMm, 40) || !About(soloBig.ArtW, 60)
                || !soloItems.SequenceEqual([solo, soloBig, grip.S]) || (solo.Copies, soloBig.Copies) != (2, 1) || soloBig.Name != solo.Name)
                sizeBad.Add($"atur ukuran satu stiker: stiker lain ikut berubah atau daftar desain salah ({soloItems.Count} baris, jumlah {solo.Copies} dan {soloBig.Copies})");
            var soloAll = Loader.Reload(solo, solo.LoadedKiss, solo.LoadedDie, solo.LoadedBleed, 50);
            if (Nest.Split(soloItems, soloBook, [soloB, soloC], solo, soloAll) || soloB.S != solo || soloItems.Count != 3) sizeBad.Add("semua stiker desain diubah: desainnya tetap dipisah");

            // Rasio bebas (keputusan user 2026-11-07): kunci rasio dilepas, lebar dan tinggi artwork diatur sendiri-sendiri. Pojok seberang diam, batas
            // 5 mm dan area cetak per sisi; garis potong, bleed, dan border dihitung ulang dengan jarak mm yang sama; garis manual ikut diregangkan.
            var (fW, fH, fBox) = Nest.ResizeTo(grip, 2, grip.X + 80 + gripX, grip.Y + 45 + gripY, A4W, A4H, Margin, true);
            if (!About(fW, 80) || !About(fH, 45) || fBox.X != grip.X || fBox.Y != grip.Y || !About(fBox.Width, 80 + gripX) || !About(fBox.Height, 45 + gripY))
                sizeBad.Add($"rasio bebas, tarik kanan bawah: {fW:F1} x {fH:F1} mm, kotak {fBox.Width:F1} x {fBox.Height:F1}");
            var (fqW, fqH, fqBox) = Nest.ResizeTo(quarter, 2, quarter.X + 30 + quarterX, quarter.Y + 120 + quarterY, A4W, A4H, Margin, true);
            if (!About(fqW, 120) || !About(fqH, 30) || !About(fqBox.Width, 30 + quarterX) || !About(fqBox.Height, 120 + quarterY))
                sizeBad.Add($"rasio bebas, stiker diputar: {fqW:F1} x {fqH:F1} mm, kotak {fqBox.Width:F1} x {fqBox.Height:F1}");
            var (fminW, fminH, _) = Nest.ResizeTo(grip, 2, 0, 0, A4W, A4H, Margin, true);
            if (!About(fminW, 5) || !About(fminH, 5)) sizeBad.Add($"rasio bebas, batas kecil: {fminW:F1} x {fminH:F1} mm, harapan 5 x 5");
            var (_, _, fmaxBox) = Nest.ResizeTo(grip, 2, 5000, 5000, A4W, A4H, Margin, true);
            if (!About(fmaxBox.Width, A4W - 2 * Margin) || !About(fmaxBox.Height, A4H - 2 * Margin))
                sizeBad.Add($"rasio bebas, batas area cetak: kotak {fmaxBox.Width:F1} x {fmaxBox.Height:F1}, harapan {A4W - 2 * Margin} x {A4H - 2 * Margin}");

            var flatBox = Box(40, 30);
            var tallBox = Loader.Reload(flatBox, flatBox.LoadedKiss, flatBox.LoadedDie, flatBox.LoadedBleed, 40, 60);
            static (double X, double Y) Gap2(Sticker s) => (s.Outer.Bounds.Width - s.ArtW, s.Outer.Bounds.Height - s.ArtH);
            if (!About(tallBox.ArtW, 40, 0.01) || !About(tallBox.ArtH, 60, 0.01) || !About(tallBox.StretchY, 2, 1e-9) || !About(Gap2(tallBox).X, Gap2(flatBox).X, 0.35)
                || !About(Gap2(tallBox).Y, Gap2(flatBox).Y, 0.35) || !About(tallBox.Full.Width, 40, 0.01) || !About(tallBox.Full.Height, 60, 0.01) || !About(tallBox.Dpi ?? 0, 25.4, 0.05))
                sizeBad.Add($"rasio bebas, muat ulang 40 x 60: {tallBox.ArtW:F2} x {tallBox.ArtH:F2} mm, regang {tallBox.StretchY:F2}, jarak garis {Gap2(flatBox).X:F2}/{Gap2(flatBox).Y:F2} -> "
                    + $"{Gap2(tallBox).X:F2}/{Gap2(tallBox).Y:F2} mm, gambar export {tallBox.Full.Width:F2} x {tallBox.Full.Height:F2}, {tallBox.Dpi:F1} dpi");
            if (!About(flatBox.ArtH, 30, 0.01) || flatBox.StretchY != 1) sizeBad.Add("rasio bebas: desain lama ikut berubah");
            tallBox.WidthMm = 80;
            var tallWide = Loader.Reload(tallBox, tallBox.LoadedKiss, tallBox.LoadedDie, tallBox.LoadedBleed);
            if (!About(tallWide.ArtW, 80, 0.01) || !About(tallWide.ArtH, 120, 0.01) || !About(tallWide.StretchY, 2, 1e-9))
                sizeBad.Add($"rasio bebas hilang saat lebar desain diubah (Susun otomatis): {tallWide.ArtW:F2} x {tallWide.ArtH:F2} mm");
            tallBox.WidthMm = 40;
            var lowBox = Loader.Reload(flatBox, flatBox.LoadedKiss, flatBox.LoadedDie, flatBox.LoadedBleed, 40, 15);
            if (!About(lowBox.ArtH, 15, 0.01) || !About(lowBox.StretchY, 0.5, 1e-9) || !About(lowBox.Dpi ?? 0, 50.8, 0.05))
                sizeBad.Add($"rasio bebas, dipendekkan 40 x 15: tinggi {lowBox.ArtH:F2} mm, regang {lowBox.StretchY:F2}, {lowBox.Dpi:F1} dpi");
            var blackPng = Loader.Png(Draw(dc => dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, 80, 60)), 80, 60));
            var bledBox = Loader.Load("uji.png", 0, 40, kiss, die, edited: blackPng, bleed: 2);
            var bledTall = Loader.Reload(bledBox, bledBox.LoadedKiss, bledBox.LoadedDie, bledBox.LoadedBleed, 40, 60);
            if (bledTall.BleedPng == null || !About(bledTall.BleedRect.Width - bledTall.ArtW, bledBox.BleedRect.Width - bledBox.ArtW)
                || !About(bledTall.BleedRect.Height - bledTall.ArtH, bledBox.BleedRect.Height - bledBox.ArtH))
                sizeBad.Add($"rasio bebas, bleed: lebih {bledBox.BleedRect.Width - bledBox.ArtW:F2}/{bledBox.BleedRect.Height - bledBox.ArtH:F2} -> "
                    + $"{bledTall.BleedRect.Width - bledTall.ArtW:F2}/{bledTall.BleedRect.Height - bledTall.ArtH:F2} mm");
            var borderBox = Loader.Load("uji.png", 0, 40, kiss, die, edited: blackPng, border: new Cmyk(0, 1, 1, 0));
            var borderTall = Loader.Reload(borderBox, borderBox.LoadedKiss, borderBox.LoadedDie, borderBox.LoadedBleed, 40, 60);
            static double Rim(Sticker s) => s.BorderGeo!.Bounds.Height - s.KissGeo!.Bounds.Height;
            if (!About(borderTall.ArtH, 60, 0.01) || !About(Rim(borderTall), Rim(borderBox), 0.05)) sizeBad.Add($"rasio bebas, border: {Rim(borderBox):F2} -> {Rim(borderTall):F2} mm di luar garis kiss");
            var pkTall = Curves.Scale(pk, 1, 2)!;
            if (pk.Zip(pkTall).Any(c => c.First.Zip(c.Second).Any(n => n.First.P.X != n.Second.P.X || n.First.P.Y * 2 != n.Second.P.Y || n.First.In.Y * 2 != n.Second.In.Y || n.First.Out.Y * 2 != n.Second.Out.Y)))
                sizeBad.Add("rasio bebas: garis manual tidak diskala per sumbu");
            var handFlat = Loader.Load(cPath, 0, 40, kiss, die, manualKiss: pk, manualDie: pd);
            var handTall = Loader.Reload(handFlat, handFlat.LoadedKiss, handFlat.LoadedDie, handFlat.LoadedBleed, 40, handFlat.ArtH * 2);
            if (!About(handTall.Outer.Bounds.Width, handFlat.Outer.Bounds.Width, 0.01) || !About(handTall.Outer.Bounds.Height, 2 * handFlat.Outer.Bounds.Height, 0.01))
                sizeBad.Add($"rasio bebas, garis manual: {handFlat.Outer.Bounds.Width:F2} x {handFlat.Outer.Bounds.Height:F2} -> {handTall.Outer.Bounds.Width:F2} x {handTall.Outer.Bounds.Height:F2} mm");
            var starTall = Loader.Reload(sizeStar, sizeStar.LoadedKiss, sizeStar.LoadedDie, sizeStar.LoadedBleed, 40, 80);
            if (!About(starTall.ArtH, 80, 0.01) || starTall.Shape != CutShape.Star || starTall.DieShape != CutShape.Rect || starTall.Border != sizeStar.Border)
                sizeBad.Add($"rasio bebas, bentuk preset: tinggi {starTall.ArtH:F2} mm, bentuk {starTall.Shape}/{starTall.DieShape}");
            var soloTall = Loader.Reload(solo, solo.LoadedKiss, solo.LoadedDie, solo.LoadedBleed, solo.ArtW, solo.ArtH * 2);
            if (!Nest.Split(soloItems, soloBook, [soloB], solo, soloTall) || soloB.S != soloTall || soloC.S != solo || !About(soloB.S.ArtH, 2 * solo.ArtH, 0.01))
                sizeBad.Add("rasio bebas, satu stiker diregangkan: stiker lain ikut berubah");
            tallBox.Copies = 4;
            if (Nest.Run([tallBox], A4W, A4H, Margin, Gap, out _) is not [var tallPage] || tallPage.Count != 4 || Nest.Check(tallPage, A4W, A4H, Margin, false, Gap) != 0)
                sizeBad.Add("rasio bebas: susun otomatis desain yang diregangkan bertabrakan");
            // Export PDF: gambar raster dan PDF vektor ikut diregangkan sampai bawah garis potongnya.
            var tallCircle = Loader.Reload(Loader.Load(cPath, 0, 40, thin, none), thin, none, 0, 40, 80);
            var tallSheet = new List<List<Placed>> { new() { new Placed { S = tallCircle, X = Margin, Y = Margin } } };
            var tallPdf = Path.Combine(dir, "regang.pdf");
            Export.Pdf(tallPdf, tallSheet, A4W, [A4H]);
            var tallLow = At(RenderPage(tallPdf, 0, 4), tallSheet[0][0], 20 * 72 / 25.4, 0.95 * tallCircle.ArtH * 72 / 25.4, 4);
            var vecSrc = Path.Combine(dir, "regang-sumber.pdf");
            using (var vdoc = new PdfDocument())
            {
                var vpage = vdoc.AddPage();
                vpage.Width = XUnit.FromMillimeter(40);
                vpage.Height = XUnit.FromMillimeter(20);
                using (var vg = XGraphics.FromPdfPage(vpage)) vg.DrawRectangle(XBrushes.Black, 0, 0, vpage.Width.Point, vpage.Height.Point);
                vdoc.Save(vecSrc);
            }
            var vecTall = Loader.Reload(Loader.Load(vecSrc, 0, 40, thin, none), thin, none, 0, 40, 40);
            var vecSheet = new List<List<Placed>> { new() { new Placed { S = vecTall, X = Margin, Y = Margin } } };
            Export.Pdf(tallPdf, vecSheet, A4W, [A4H]);
            var vecPage = RenderPage(tallPdf, 0, 4);
            var (vecTop, vecLow) = (At(vecPage, vecSheet[0][0], 20 * 72 / 25.4, 5 * 72 / 25.4, 4), At(vecPage, vecSheet[0][0], 20 * 72 / 25.4, 35 * 72 / 25.4, 4));
            static bool Dark(Color c) => c.R < 80 && c.G < 80 && c.B < 80;
            if (!Orange(tallLow) || vecTall.Pdf == null || !Dark(vecTop) || !Dark(vecLow))
                sizeBad.Add($"rasio bebas, export PDF: gambar di bawah {tallLow}, PDF vektor {(vecTall.Pdf == null ? "jadi gambar" : "vektor")} atas {vecTop} bawah {vecLow}");
            File.Delete(tallPdf);
            File.Delete(vecSrc);
            // SVG digambar ulang pada resolusi lain saat diregangkan, jadi kotak isinya bisa bergeser satu piksel: tinggi yang diketik tetap harus tepat.
            var bannerSvg = Path.Combine(dir, "regang-banner.svg");
            File.WriteAllText(bannerSvg, """<svg xmlns="http://www.w3.org/2000/svg" width="50mm" height="5.04mm" viewBox="0 0 50 5.04"><rect width="50" height="5.04" fill="#000"/></svg>""");
            var banner = Loader.Load(bannerSvg, 0, 50, kiss, die);
            var bannerTall = Loader.Reload(banner, banner.LoadedKiss, banner.LoadedDie, banner.LoadedBleed, 50, 20);
            File.Delete(bannerSvg);
            if (!About(bannerTall.ArtH, 20, 0.01)) sizeBad.Add($"rasio bebas, SVG tipis 50 x {banner.ArtH:F3} mm diketik tinggi 20: hasil {bannerTall.ArtH:F3} mm");
            sizeLog.Add($"rasio bebas: kotak 40 x 30 ditarik ke 80 x 45 mm; 40 x 30 -> 40 x 60 mm, jarak garis {Gap2(flatBox).X:F2}/{Gap2(flatBox).Y:F2} -> {Gap2(tallBox).X:F2}/{Gap2(tallBox).Y:F2} mm, {tallBox.Dpi:F1} dpi");

            var evenPage = Nest.Rows([Box(40, 30, 5)], A4W, A4H, Margin, Gap, out _, true)[0];
            var evenRows = evenPage.GroupBy(p => p.Y).OrderBy(g => g.Key).ToList();
            if (evenRows.Count < 2 || evenRows[0].Min(p => p.X) != evenRows[1].Min(p => p.X) || Nest.Check(evenPage, A4W, A4H, Margin, true) != 0)
                sizeBad.Add($"tepi kiri baris Rapi tidak rata: {string.Join(" / ", evenRows.Select(g => g.Min(p => p.X)))}");
            // 17 stiker: isi lembar A4 sebelum aturan tepi kiri (S19). Tepi rata tidak boleh menambah lembar.
            if (Nest.Rows([Box(40, 30, 17)], A4W, A4H, Margin, Gap, out _, true).Count != 1) sizeBad.Add("tepi rata menambah lembar");

            // Hemat (masukan user 2026-11-03, foto 178,5 x 100 mm, 20 salinan, A3+): baris pertama turun melewati area tanda bila itu lebih hemat.
            var photo = Png(Draw(dc => dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, 356, 200)), 356, 200), 178, kiss, new Cut(false, 4, 2));
            photo.Copies = 20;
            var packed = Nest.Run([photo], 320, 480, Margin, Gap, out _, true);
            var upright = packed[0].Where(p => p.Rot % 180 == 0).Select(p => p.X).Distinct().ToList();
            if (packed[0].Count < 6 || packed.Count > 4 || upright.Count != 1 || packed.Any(sh => Nest.Check(sh, 320, 480, Margin, true) != 0))
                sizeBad.Add($"Hemat: lembar pertama {packed[0].Count} pcs, {packed.Count} lembar, tepi kiri {string.Join(" / ", upright)}");
            // 2 lembar: pekerjaan S17 dengan tanda registrasi sebelum percobaan ini. Hemat tidak boleh menambah lembar.
            if (regRun.Count != 2) sizeBad.Add($"Hemat dengan tanda registrasi: {regRun.Count} lembar, harapan 2");
            sizeLog.Add($"kotak 40 x 30 mm: paling kecil {minW:F1} mm, paling besar {maxW:F1} mm; lingkaran 40 -> 80 mm, jarak garis {sizeGap:F2} -> {sizeGap2:F2} mm; tepi kiri baris Rapi {evenRows[0].Min(p => p.X):F0} mm; "
                + $"Hemat foto 178 x 100 mm: lembar pertama {packed[0].Count} pcs, {packed.Count} lembar");
            S(20, "atur ukuran di kanvas dan tepi kiri Rapi: pojok seberang diam, rasio terkunci atau bebas, jarak garis tetap, batas ukuran, stiker lain dengan desain sama tidak ikut, tepi rata tanpa menambah lembar", sizeBad.Count == 0,
                string.Join("; ", sizeBad.Distinct().Concat(sizeLog)));

            // S21: pindah stiker antar-lembar dan pemeriksaan jarak (audit 2026-11-03). Tidak ada tempat: stiker tidak berubah dan tidak tertinggal
            // di luar lembar. Check menandai stiker yang lebih dekat dari jarak antar stiker, dan tidak menandai hasil Snap atau susun otomatis.
            var dropBad = new List<string>();
            var brick = Box(40, 30);
            int brickW = Enumerable.Range(0, brick.Grid.GetLength(0)).Count(x => Enumerable.Range(0, brick.Grid.GetLength(1)).Any(y => brick.Grid[x, y]));
            int brickH = Enumerable.Range(0, brick.Grid.GetLength(1)).Count(y => Enumerable.Range(0, brick.Grid.GetLength(0)).Any(x => brick.Grid[x, y]));
            Placed Lay(double x, double y, int rot = 0) => new() { S = brick, X = x, Y = y, Rot = rot };
            int Apart(double dx, double dy, double gap)
            {
                var two = new List<Placed> { Lay(30, 30), Lay(30 + dx, 30 + dy) };
                return Nest.Check(two, A4W, A4H, Margin, false, gap);
            }
            if (Apart(brickW + Gap, 0, Gap) != 0) dropBad.Add("jarak tepat sebesar setelan ikut ditandai");
            if (Apart(brickW + Gap - 1, 0, Gap) != 2) dropBad.Add("jarak 1 mm di bawah setelan tidak ditandai");
            if (Apart(0, brickH + Gap - 1, Gap) != 2) dropBad.Add("jarak ke bawah di bawah setelan tidak ditandai");
            if (Apart(brickW + Gap - 1, brickH + Gap - 1, Gap) != 2) dropBad.Add("jarak diagonal di bawah setelan tidak ditandai");
            if (Apart(brickW + Gap, brickH + Gap, Gap) != 0) dropBad.Add("jarak diagonal yang cukup ikut ditandai");
            if (Apart(brickW, 0, 0) != 0) dropBad.Add("jarak 0 mm: stiker bersentuhan ikut ditandai");

            // Hasil Snap dan susun otomatis lolos Check dengan setelan yang sama, termasuk margin pecahan.
            foreach (var (m, g, marks) in new[] { (Margin, Gap, false), (Margin, Gap, true), (10.5, 0.0, false), (12.5, 1.0, true) })
            {
                var dice = new Random(7);
                var stack = new List<Placed>();
                for (int i = 0; i < 60; i++)
                {
                    var piece = new Placed { S = i % 2 == 0 ? brick : Box(60, 30), Rot = dice.Next(4) * 90, X = dice.Next(-50, 260), Y = dice.Next(-50, 350) };
                    if (!Nest.Snap(stack, piece, A4W, A4H, m, g, marks)) continue;
                    stack.Add(piece);
                }
                int flagged = Nest.Check(stack, A4W, A4H, m, marks, g);
                if (stack.Count < 2 || flagged != 0) dropBad.Add($"hasil Snap ditandai (margin {m}, jarak {g}, tanda {marks}): {flagged} dari {stack.Count}");

                var job = new List<Sticker> { Box(40, 30, 8), Box(60, 30, 5), Box(30, 30, 9) };
                var dense = Nest.Run(job, A4W, A4H, m, g, out _, marks);
                var neat = Nest.Rows(job, A4W, A4H, m, g, out _, marks);
                var filled = Nest.Run(job, A4W, A4H, m, g, out _, marks);
                Nest.Fill(filled, job, A4W, A4H, m, g, marks);
                var neatFilled = Nest.FillRows(job, A4W, A4H, m, g, out _, marks);
                foreach (var (name, pages) in new[] { ("Hemat", dense), ("Rapi", neat), ("Hemat penuh", filled), ("Rapi penuh", neatFilled) })
                    if (pages.Sum(sh => Nest.Check(sh, A4W, A4H, m, marks, g)) is > 0 and var n)
                        dropBad.Add($"{name} ditandai (margin {m}, jarak {g}, tanda {marks}): {n} stiker");
            }

            var fromSheet = new List<Placed> { Lay(30, 30, 90) };
            var mover = fromSheet[0];
            var toSheet = new List<Placed> { Lay(30, 30) };
            (mover.X, mover.Y) = (35, 35);
            if (!Nest.MoveTo([fromSheet, toSheet], [mover], 1, A4W, A4H, Margin, Gap) || fromSheet.Count != 0 || toSheet.Count != 2 || !toSheet.Contains(mover)
                || mover.Rot != 90 || Nest.Check(toSheet, A4W, A4H, Margin, false, Gap) != 0)
                dropBad.Add("pindah ke lembar yang ada tempat gagal");
            var crowd = Nest.Run([Box(40, 30, 40)], A4W, A4H, Margin, Gap, out _)[0];
            var stay = Lay(30, 30);
            var home = new List<Placed> { stay };
            int crowdCount = crowd.Count;
            if (Nest.MoveTo([home, crowd], [stay], 1, A4W, A4H, Margin, Gap) || home.Count != 1 || crowd.Count != crowdCount || stay.X != 30 || stay.Y != 30)
                dropBad.Add("lembar tujuan penuh: stiker atau lembar ikut berubah");

            // Beberapa stiker terpilih pindah bersama: semua muat, atau tidak ada yang berubah. Anggota yang sudah di lembar tujuan tidak berlipat.
            var (duoA, duoB, there) = (Lay(30, 30), Lay(100, 30), Lay(30, 100));
            var (pageA, pageB) = (new List<Placed> { duoA, duoB, Lay(30, 200) }, new List<Placed> { there });
            if (!Nest.MoveTo([pageA, pageB], [duoA, duoB, there], 1, A4W, A4H, Margin, Gap) || pageA.Count != 1 || pageB.Count != 3 || Nest.Check(pageB, A4W, A4H, Margin, false, Gap) != 0)
                dropBad.Add($"pindah berkelompok: isi lembar {pageA.Count} dan {pageB.Count}");
            var nearlyFull = Nest.Run([Box(40, 30, 40)], A4W, A4H, Margin, Gap, out _)[0];
            nearlyFull.RemoveAt(nearlyFull.Count - 1); // tepat satu tempat kosong
            var (fits, stuck) = (Lay(30, 30), Lay(100, 30));
            var (origin, roomFor) = (new List<Placed> { fits, stuck }, nearlyFull.Count);
            if (Nest.MoveTo([origin, nearlyFull], [fits, stuck], 1, A4W, A4H, Margin, Gap) || origin.Count != 2 || nearlyFull.Count != roomFor
                || (fits.X, fits.Y) != (30, 30) || (stuck.X, stuck.Y) != (100, 30))
                dropBad.Add("pindah berkelompok tanpa tempat: sebagian stiker ikut berubah");

            var bar = new Placed { S = Box(250, 20), Rot = 90, X = 30, Y = 15 };
            if (Nest.Turn([bar], bar, A4W, A4H, Margin, Gap) || bar.Rot != 90 || bar.X != 30 || bar.Y != 15)
                dropBad.Add($"putar tanpa tempat: rotasi {bar.Rot}, posisi {bar.X},{bar.Y}");
            var spin = Lay(30, 30);
            if (!Nest.Turn([spin], spin, A4W, A4H, Margin, Gap) || spin.Rot != 90) dropBad.Add("putar yang ada tempat gagal");

            var over = Lay(190, 280);
            Nest.KeepInside(over, A4W, A4H, Margin);
            var under = Lay(-50, -50);
            Nest.KeepInside(under, A4W, A4H, Margin);
            if (over.X + over.BW > A4W - Margin || over.Y + over.BH > A4H - Margin || under.X != Margin || under.Y != Margin)
                dropBad.Add($"tidak dijaga di dalam area cetak: {over.X},{over.Y} dan {under.X},{under.Y}");
            S(21, "pindah antar-lembar dan jarak antar stiker: tanpa tempat stiker tidak berubah, Check menandai yang terlalu dekat dan tidak menandai hasil Snap atau susun otomatis", dropBad.Count == 0,
                string.Join("; ", dropBad.Distinct()));

            // S22: pilih banyak dan bahan. Beberapa stiker ditempel bersama, semua atau tidak sama sekali. Stiker bertumpuk tidak boleh membuat bahan tampil di atas 100% atau hijau.
            var groupBad = new List<string>();
            var glue = new List<List<Placed>> { new() { Lay(30, 30), Lay(100, 30) }, new() { Lay(30, 30) } };
            (Sticker, int, double, double, int)[] glueClip = [(brick, 0, 30, 30, 0), (brick, 0, 100, 30, 0), (brick, 0, 30, 30, 1)];
            var glued = Nest.PasteAll(glue, [A4H, A4H], glueClip, A4W, Margin, Gap);
            if (glued == null || glued.Count != 3 || glue[0].Count != 4 || glue[1].Count != 2 || glued[0].X >= glued[1].X || glued[0].Y != glued[1].Y
                || glue.Sum(sh => Nest.Check(sh, A4W, A4H, Margin, false, Gap)) != 0)
                groupBad.Add("tempel berkelompok: tiap salinan harus di lembarnya sendiri, urut, dan tidak bertabrakan");
            var roomy = new List<Placed> { Lay(30, 30) };
            int fullCount = crowd.Count;
            (Sticker, int, double, double, int)[] tooMany = [(brick, 0, 30, 30, 0), (brick, 0, 30, 30, 1)];
            if (Nest.PasteAll([roomy, crowd], [A4H, A4H], tooMany, A4W, Margin, Gap) != null || roomy.Count != 1 || crowd.Count != fullCount)
                groupBad.Add("tempel berkelompok tanpa tempat: sebagian salinan tertinggal");
            var heap = new List<List<Placed>> { Enumerable.Range(0, 60).Select(_ => Lay(30, 30)).ToList() };
            double heapUse = Yield.Use(heap[0], A4W, A4H, Margin);
            if (heapUse <= 1) groupBad.Add($"uji butuh bahan di atas 100%, dapat {heapUse:P0}");
            if (Yield.Level(heapUse, true) != 0 || Yield.Level(heapUse, false) != 0) groupBad.Add("bahan di atas 100% tampil sebagai target tercapai");
            if (Yield.Shown(heapUse) != 1 || Yield.Shown(0.6) != 0.6) groupBad.Add($"tampilan bahan {Yield.Shown(heapUse):P0} untuk {heapUse:P0}");
            var heapTicket = Export.TicketLines("A4", heap, A4W, [A4H], Margin, ["uji pengaturan"]).Select(l => l.Text).ToList();
            if (!heapTicket.Contains(Lang.T("TicketUse", 1.0, 0.0)) || !heapTicket.Contains(Lang.T("TicketSheetLine", 1, 60, 1.0)))
                groupBad.Add("Print Ticket menulis bahan di atas 100%");
            S(22, "pilih banyak dan bahan: tempel berkelompok semua atau tidak sama sekali; bahan di atas 100% tampil 100% dan merah, juga di Print Ticket", groupBad.Count == 0, string.Join("; ", groupBad));

            // S23: keadaan sementara saat stiker ditarik. Dokumen tidak berubah sebelum dilepas; pindah, salin, dan tanpa tempat.
            var stateBad = new List<string>();
            List<double> twoH = [A4H, A4H];
            var (tugA, tugB) = (Lay(30, 30), Lay(100, 30, 90));
            List<List<Placed>> tugBook = [[tugA, tugB, Lay(30, 200)], [Lay(30, 30)]];
            var tug = new DragState([tugA, tugB], 0) { Offset = new Vector(0, 120), ToSheet = 1 };
            if ((tugA.X, tugA.Y) != (30, 30) || tugBook[0].Count != 3 || tugBook[1].Count != 1 || !tug.Moved || !tug.Away || tug.FromSheet != 0)
                stateBad.Add("dokumen berubah sebelum stiker dilepas");
            tug.Copy = true;
            var made = tug.Drop(tugBook, twoH, A4W, Margin, Gap);
            if (made == null || made.Count != 2 || tugBook[0].Count != 3 || tugBook[1].Count != 3 || (tugA.X, tugA.Y) != (30, 30) || made.Contains(tugA) || made.Contains(tugB)
                || !made.All(tugBook[1].Contains) || made[1].Rot != 90 || made[0].Y <= 30 || Nest.Check(tugBook[1], A4W, A4H, Margin, false, Gap) != 0)
                stateBad.Add("salin ke lembar lain: asli harus tetap, salinan di lembar tujuan dengan rotasi sama");
            var gapCopy = new DragState([tugA], 0) { Offset = new Vector(150, 300), Copy = true };
            var gapMade = gapCopy.Drop(tugBook, twoH, A4W, Margin, Gap);
            if (gapMade == null || gapMade.Count != 1 || tugBook[0].Count != 4 || !tugBook[0].Contains(gapMade[0]) || Nest.Check(tugBook[0], A4W, A4H, Margin, false, Gap) != 0)
                stateBad.Add("salin di celah antar-lembar: salinan harus di lembar asal");
            var (fullA, fullB) = (Lay(30, 30), Lay(100, 30));
            List<List<Placed>> fullBook = [[fullA, fullB], Nest.Run([Box(40, 30, 40)], A4W, A4H, Margin, Gap, out _)[0]];
            int fullCount2 = fullBook[1].Count;
            var fullCopy = new DragState([fullA, fullB], 0) { Offset = new Vector(5, 5), ToSheet = 1, Copy = true };
            if (fullCopy.Drop(fullBook, twoH, A4W, Margin, Gap) != null || fullBook[0].Count != 2 || fullBook[1].Count != fullCount2 || (fullA.X, fullA.Y) != (30, 30))
                stateBad.Add("salin tanpa tempat: dokumen harus tetap");
            var fullMove = new DragState([fullA, fullB], 0) { Offset = new Vector(5, 5), ToSheet = 1 };
            if (fullMove.Drop(fullBook, twoH, A4W, Margin, Gap) != null || fullBook[0].Count != 2 || fullBook[1].Count != fullCount2 || (fullA.X, fullA.Y) != (30, 30) || (fullB.X, fullB.Y) != (100, 30))
                stateBad.Add("pindah tanpa tempat: stiker harus kembali ke posisi awal");
            var moveA = Lay(30, 30);
            List<List<Placed>> moveBook = [[moveA], [Lay(30, 30)]];
            var moved2 = new DragState([moveA], 0) { Offset = new Vector(0, 120), ToSheet = 1 }.Drop(moveBook, twoH, A4W, Margin, Gap);
            if (moved2 == null || moved2[0] != moveA || moveBook[0].Count != 0 || moveBook[1].Count != 2 || !moveBook[1].Contains(moveA) || moveA.Y <= 30)
                stateBad.Add("pindah ke lembar lain gagal");
            var nearA = Lay(30, 30);
            List<List<Placed>> nearBook = [[nearA], []];
            var nearDrop = new DragState([nearA], 0) { Offset = new Vector(50, 60), ToSheet = 0 }.Drop(nearBook, twoH, A4W, Margin, Gap);
            if (nearDrop == null || nearBook[0].Count != 1 || Math.Abs(nearA.X - 80) > 1 || Math.Abs(nearA.Y - 90) > 1)
                stateBad.Add($"pindah di lembar yang sama: posisi {nearA.X},{nearA.Y}");
            var hugeP = new Placed { S = Box(400, 400), X = 30, Y = 30 };
            List<List<Placed>> hugeBook = [[hugeP], []];
            if (new DragState([hugeP], 0) { Offset = new Vector(9, 9) }.Drop(hugeBook, twoH, A4W, Margin, Gap) != null || (hugeP.X, hugeP.Y) != (30, 30) || hugeBook[0].Count != 1)
                stateBad.Add("pindah di lembar yang sama tanpa tempat: stiker harus kembali ke posisi awal");
            S(23, "keadaan sementara saat stiker ditarik: dokumen tidak berubah sebelum dilepas; pindah dan salin semua atau tidak sama sekali; stiker asal tetap saat disalin", stateBad.Count == 0, string.Join("; ", stateBad));

            Check(fails.Count == 0, "skenario gagal: " + string.Join(", ", fails));

            Save(Export.Snapshot(Nest.Sheet(sheets[0], A4W, A4H, Margin, null, 0), A4W, A4H, 4), Path.Combine(dir, "layout-1.png"));

            // Preview JPG untuk customer: satu file per lembar, sisi panjang +-2000 px, bisa dibaca ulang.
            var jpgs = Export.Preview(Path.Combine(dir, "preview.jpg"), sheets, A4W, heights, Margin);
            Check(jpgs.Count == sheets.Count && jpgs.All(File.Exists), "jumlah preview JPG != jumlah lembar");
            var jf = BitmapDecoder.Create(new Uri(jpgs[0]), BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            Check(Math.Abs(Math.Max(jf.PixelWidth, jf.PixelHeight) - 2000) <= 2, $"ukuran preview salah: {jf.PixelWidth}x{jf.PixelHeight}");
            log.Add($"preview: {jpgs.Count} JPG, {jf.PixelWidth}x{jf.PixelHeight} px, {new FileInfo(jpgs[0]).Length / 1024} KB");

            // Export SVG (fitur leader 2026-10-29): artwork setiap desain ikut tertempel di kotak yang sama dengan export PDF,
            // dan JPG tetap bertanda JPEG.
            var svgBad = SvgErrors(dir, sheets, rawBlue, kiss, die);
            Check(svgBad.Count == 0, "export SVG salah: " + string.Join("; ", svgBad));
            log.Add($"export SVG: artwork {sheets.Sum(x => x.Count)} stiker di kotak yang sama dengan export PDF, di-clip ke garis potong; "
                + "warna border dan bleed ikut; JPG bertanda JPEG");

            // PDF multi-halaman: satu desain per halaman, ukuran asli per halaman, export menempel halaman yang benar.
            var multi = Loader.LoadAll(Path.Combine(dir, "multi.pdf"), 40, kiss, die);
            var msh = Nest.Run(multi, A4W, A4H, Margin, Gap, out _);
            var mpdf = Path.Combine(dir, "multi-export.pdf");
            Export.Pdf(mpdf, msh, A4W, [A4H]);
            var mr = RenderPage(mpdf, 0, 4);
            Color Mid(Sticker st) => At(mr, msh[0].First(q => q.S == st), st.ArtW / 2 * 72 / 25.4, st.ArtH / 2 * 72 / 25.4, 4);
            Color m1 = Mid(multi[0]), m2 = Mid(multi[1]);
            Check(multi.Count == 2 && multi[0].Name == "multi.pdf p1" && multi[1].Name == "multi.pdf p2" && multi.All(m => m.Pdf != null)
                && Math.Abs(multi[0].ArtW - 28.2) < 0.6 && Math.Abs(multi[1].ArtW - 63.5) < 0.6 && Math.Abs(multi[1].ArtH - 14.1) < 0.6
                && m1.R > 200 && m1.B < 100 && m2.B > 150 && m2.R < 100,
                $"PDF multi-halaman salah: {string.Join(" / ", multi.Select(m => $"{m.Name} {m.ArtW:F1}x{m.ArtH:F1} mm"))}, warna tengah {m1} / {m2}");
            log.Add($"multi-halaman: {string.Join(", ", multi.Select(m => $"{m.Name} {m.ArtW:F1}x{m.ArtH:F1} mm"))}; export menempel halaman yang benar");

            // File PDF diganti lalu dihapus setelah dimuat: muat ulang (Susun) dan export tetap memakai isi saat dimuat;
            // tambah ulang file yang berubah memakai isi barunya.
            var src = Path.Combine(dir, "sumber-uji.pdf");
            File.Copy(Path.Combine(dir, "logo.pdf"), src, true);
            var first = Loader.LoadAll(src, 40, kiss, die)[0];
            File.Copy(Path.Combine(dir, "multi.pdf"), src, true);
            File.SetLastWriteTimeUtc(src, DateTime.UtcNow.AddMinutes(1));
            var reload = Loader.Load(src, 0, 0, kiss, die, loadedPdf: first.Pdf);
            var readd = Loader.LoadAll(src, 40, kiss, die)[0];
            File.Delete(src);
            var swarn = Export.Pdf(Path.Combine(dir, "sumber-export.pdf"), [[new Placed { S = first }]], A4W, [A4H]);
            Check(first.Pdf != src && File.Exists(first.Pdf) && swarn.Count == 0 && Math.Abs(reload.ArtW - first.ArtW) < 0.01
                && Math.Abs(readd.ArtW - first.ArtW) > 1,
                $"salinan PDF salah: dimuat {first.ArtW:F1} mm, muat ulang {reload.ArtW:F1} mm, tambah ulang {readd.ArtW:F1} mm, peringatan export {swarn.Count}");
            log.Add($"PDF diganti lalu dihapus setelah dimuat: muat ulang {reload.ArtW:F1} mm dan export tetap dari salinan ({first.ArtW:F1} mm); "
                + $"tambah ulang memakai isi baru ({readd.ArtW:F1} mm)");

            var foundBad = FoundErrors(dir, kiss, die);
            Check(foundBad.Count == 0, "temuan 2026-11-05: " + string.Join("; ", foundBad));
            log.Add("file gambar diganti lalu dihapus: muat ulang tetap memakai gambar saat dimuat; foto bertanda orientasi EXIF 1-8 tegak di layar, file export, dan sesudah muat ulang; pratinjau JPG tanpa bingkai merah; "
                + "kotak lebar menerima koma dan titik; export ke file terkunci memberi pesan untuk operator; "
                + "gambar dan garis margin di dalam jendela tepat di posisi datanya; dilepas di lembar lain tepat di grid pada 4 rotasi");

            var (importBad, importNote) = ImportErrors(dir, kiss, die);
            Check(importBad.Count == 0, "CDR dan AI: " + string.Join("; ", importBad));
            log.Add("AI tanpa PDF-compatible ditolak dengan pesan jelas; CDR dibuka lewat libcdr tanpa CorelDRAW: alat dan DLL termuat, halaman dipecah dan dipotong "
                + "seukuran halaman, ukuran asli, batas waktu dihentikan; " + importNote);

            var moveBad = IntegrityErrors(dir, kiss, die);
            Check(moveBad.Count == 0, "pindah, salin, dan duplikat: " + string.Join("; ", moveBad));
            log.Add("pindah, salin dengan Alt, dan duplikat: data desain tetap (gambar, PDF, bleed, garis potong, garis manual, bentuk, border, offset), "
                + "salinan memakai desain dan rotasi yang sama, penempatan sah di grid, tanda merah mengikuti pemeriksaan, desain yang diganti berlaku untuk semua salinannya");

            // Penuhi lembar (Compact): stiker bertambah bergiliran, jumlah lembar tetap, tidak ada tabrakan atau keluar margin,
            // dan setelah penuh tidak ada lagi yang muat.
            var fs = sheets.Select(sh => sh.Select(p => new Placed { S = p.S, X = p.X, Y = p.Y, Rot = p.Rot }).ToList()).ToList();
            int before = fs.Sum(x => x.Count), added = Nest.Fill(fs, items, A4W, A4H, Margin, Gap), again = Nest.Fill(fs, items, A4W, A4H, Margin, Gap);
            var grew = items.Count(s => fs.Sum(x => x.Count(p => p.S == s)) > sheets.Sum(x => x.Count(p => p.S == s)));
            Check(added > 0 && again == 0 && fs.Count == sheets.Count && fs.All(x => Nest.Check(x, A4W, A4H, Margin) == 0) && grew >= 2,
                $"penuhi lembar salah: +{added} stiker, isi ulang +{again}, lembar {sheets.Count} -> {fs.Count}, desain bertambah {grew}");
            log.Add($"penuhi lembar: {before} -> {before + added} stiker di {fs.Count} lembar A4, {grew} desain bertambah, isi ulang +{again}");

            // Isi Print Ticket: total lembar, total pcs, efisiensi bahan, baris per lembar dan per desain, pengaturan.
            var tl = Export.TicketLines("A4", sheets, A4W, heights, Margin, ["uji pengaturan"]).Select(l => l.Text).ToList();
            double tUse = sheets.Sum(x => x.Sum(p => p.S.AreaMm2)) / (sheets.Count * Yield.Fillable(A4W, A4H, Margin));
            int pcs = sheets.Sum(x => x.Count);
            Check(tl.Contains(Lang.T("TicketSheets", sheets.Count)) && tl.Contains(Lang.T("TicketPcs", pcs)) && tl.Contains(Lang.T("TicketUse", tUse, 1 - tUse))
                && sheets.Select((sh, i) => Lang.T("TicketSheetLine", i + 1, sh.Count, Yield.Use(sh, A4W, A4H, Margin))).All(tl.Contains)
                && sheets.SelectMany(x => x).GroupBy(p => p.S).All(gp => tl.Any(t => t.StartsWith(gp.Key.Name + " — ") && t.EndsWith($" — {gp.Count()} pcs")))
                && tl[^1] == "uji pengaturan",
                "isi Print Ticket salah: " + string.Join(" | ", tl));
            log.Add($"print ticket: {tl.Count(t => t.Length > 0)} baris; {Lang.T("TicketSheets", sheets.Count)}; {Lang.T("TicketPcs", pcs)}; {Lang.T("TicketUse", tUse, 1 - tUse)}");
            log.Add("OK");
            File.WriteAllLines(Path.Combine(dir, "selfcheck.txt"), log);
            return 0;
        }
        catch (Exception ex)
        {
            log.Add("GAGAL: " + ex);
            try { File.WriteAllLines(Path.Combine(dir, "selfcheck.txt"), log); }
            catch (Exception) { } // folder tidak bisa ditulis: log hilang, exit code tetap 1
            return 1;
        }
    }

    // Node seperti di CorelDRAW: satu per segmen (C = kurva, L = garis lurus); sudut tajam Potrace = dua L.
    static DateTime Started(Process p) { try { return p.StartTime; } catch (Exception) { return DateTime.MinValue; } }

    // Referensi berurutan untuk kikis tepi (algoritma sebelum diparalelkan), dipakai P4.
    static byte[] ErodeRef(byte[] src, int w, int h, int contract)
    {
        var a = (byte[])src.Clone();
        for (int pass = 0; pass < contract; pass++)
        {
            var clear = new List<int>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    if (a[i] <= 16) continue;
                    bool edge = false;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int u = x + dx, v = y + dy;
                            if (u >= 0 && v >= 0 && u < w && v < h && a[v * w + u] <= 16) edge = true;
                        }
                    if (edge) clear.Add(i);
                }
            foreach (int i in clear) a[i] = 0;
        }
        return a;
    }

    static int Nodes(string path) => path.Count(ch => ch is 'C' or 'L');
    static int Corners(string path) => path.Count(ch => ch == 'L') / 2;

    static void Check(bool ok, string msg)
    {
        if (!ok) throw new Exception(msg);
    }

    static double Use(List<List<Placed>> sheets) => sheets.Sum(x => x.Sum(p => p.S.AreaMm2)) / (A4W * A4H * sheets.Count);

    // Export satu lembar A4 ke file sementara: jumlah operator garis lurus (l) dan kurva (c) di halaman.
    static (int Lines, int Curves) ExportOps(List<Placed> sheet)
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"printech-uji-{Guid.NewGuid():N}.pdf");
        try
        {
            Check(Export.Pdf(tmp, [sheet], A4W, [A4H]).Count == 0, "peringatan export");
            string c;
            using (var d = PdfReader.Open(tmp, PdfDocumentOpenMode.Import)) c = Content(d.Pages[0]);
            return (Regex.Matches(c, @"(?<=\s)l(?=\s)").Count, Regex.Matches(c, @"(?<=\s)c(?=\s)").Count);
        }
        finally { File.Delete(tmp); }
    }

    // Susun berbaris per lembar: tanpa tabrakan, tidak diputar tanpa perlu, desain sama berkumpul, baris dan kolom tiap desain lurus.
    static List<string> RowsErrors(List<List<Placed>> sheets, bool marks)
    {
        var bad = new List<string>();
        static List<double> Steps(IEnumerable<double> values)
        {
            var v = values.Distinct().Order().ToList();
            return v.Zip(v.Skip(1), (a, b) => b - a).ToList();
        }
        static bool Straight(List<double> steps) => steps.All(d => Math.Abs(Math.IEEERemainder(d, steps.Min())) < 1e-6);
        foreach (var sh in sheets)
        {
            if (Nest.Check(sh, A4W, A4H, Margin, marks) != 0) bad.Add($"{sh.Count(p => p.Bad)} stiker bertabrakan, keluar margin, atau kena tanda");
            var order = sh.OrderBy(p => p.Y).ThenBy(p => p.X).Select(p => p.S).ToList();
            foreach (var g in sh.GroupBy(p => p.S))
            {
                if (g.Any(p => p.Rot != 0)) bad.Add($"{g.Key.Name}: diputar tanpa perlu");
                if (order.LastIndexOf(g.Key) - order.IndexOf(g.Key) + 1 != g.Count()) bad.Add($"{g.Key.Name}: salinan tidak berkumpul");
                var (dx, dy) = (Steps(g.Select(p => p.X)), Steps(g.Select(p => p.Y)));
                if (!Straight(dx) || !Straight(dy)) bad.Add($"{g.Key.Name}: baris atau kolom tidak lurus");
                var outer = (g.Key.DieGeo ?? g.Key.KissGeo)!.Bounds;
                if (dx.Any(d => d < outer.Width + Gap - 1e-6) || dy.Any(d => d < outer.Height + Gap - 1e-6)) bad.Add($"{g.Key.Name}: jarak antar stiker kurang dari {Gap} mm");
            }
        }
        return bad;
    }

    // Export SVG ke folder sementara. Kotak gambar mengikuti export PDF: bleed di BleedRect, gambar sumber di Full (gambar
    // penuh, termasuk margin transparan seperti circle.png), vektor PDF/AI lewat render layar di kotak artwork.
    static List<string> SvgErrors(string dir, List<List<Placed>> sheets, Sticker jpg, Cut kiss, Cut die)
    {
        XNamespace svg = "http://www.w3.org/2000/svg", xlink = "http://www.w3.org/1999/xlink";
        var bad = new List<string>();
        var tmp = Path.Combine(Path.GetTempPath(), $"printech-uji-svg-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmp);
        try
        {
            List<XElement> Images(List<List<Placed>> sh, string name) =>
                ExportSvg.Svg(Path.Combine(tmp, name), sh, A4W, sh.Select(_ => A4H).ToList(), Margin)
                    .SelectMany(f => XDocument.Load(f).Descendants(svg + "image")).ToList();
            List<List<Placed>> One(Sticker s) => [[new Placed { S = s, X = Margin, Y = Margin }]];

            // Per gambar, seperti export PDF: kotak dan isi benar, di-clip ke ArtClip, warna border (bila ada) di bawahnya.
            void CheckImages(List<List<Placed>> sh, string name)
            {
                var placed = sh.SelectMany(x => x).ToList();
                var ims = Images(sh, name);
                if (ims.Count != placed.Count) { bad.Add($"{name}: {ims.Count} gambar untuk {placed.Count} stiker"); return; }
                for (int i = 0; i < ims.Count; i++)
                {
                    var s = placed[i].S;
                    var r = s.BleedPng != null ? s.BleedRect : s.Pdf == null ? s.Full : new Rect(s.Off, s.Off, s.ArtW, s.ArtH);
                    var e = ims[i];
                    double N(string a) => double.Parse(e.Attribute(a)!.Value, CultureInfo.InvariantCulture);
                    var data = e.Attribute(xlink + "href")!.Value.Split(',', 2);
                    if (data.Length < 2 || data[1].Length == 0 || Math.Abs(N("x") - r.X) > 0.01 || Math.Abs(N("y") - r.Y) > 0.01
                        || Math.Abs(N("width") - r.Width) > 0.01 || Math.Abs(N("height") - r.Height) > 0.01)
                        bad.Add($"{s.Name}: gambar {(data.Length < 2 ? 0 : data[1].Length)} karakter di {N("x"):F2};{N("y"):F2} "
                            + $"{N("width"):F2}x{N("height"):F2} mm, harus {r.X:F2};{r.Y:F2} {r.Width:F2}x{r.Height:F2} mm");
                    var id = Regex.Match((string?)e.Parent?.Attribute("clip-path") ?? "", @"^url\(#(.+)\)$").Groups[1].Value;
                    var clip = e.Document!.Descendants(svg + "clipPath").FirstOrDefault(c => id != "" && (string?)c.Attribute("id") == id);
                    if (clip?.Element(svg + "path")?.Attribute("d")?.Value is not { Length: > 0 })
                        bad.Add($"{s.Name}: artwork tidak di-clip ke garis potong");
                    if ((string?)e.Attribute("preserveAspectRatio") != "none") bad.Add($"{s.Name}: gambar tidak ikut diregangkan (preserveAspectRatio bukan none)");
                    var fill = (string?)e.Parent?.Parent?.Element(svg + "path")?.Attribute("fill");
                    var want = s.Border is { } b ? $"#{b.Rgb.R:X2}{b.Rgb.G:X2}{b.Rgb.B:X2}" : null;
                    if (fill != want) bad.Add($"{s.Name}: warna border {fill ?? "tidak ada"}, harus {want ?? "tidak ada"}");
                }
            }

            CheckImages(sheets, "export.svg");
            var bordered = Loader.Load(Path.Combine(dir, "circle.png"), 0, 40, kiss, die, border: new Cmyk(0, 1, 1, 0));
            var bled = Loader.Load(Path.Combine(dir, "circle.png"), 0, 40, kiss, die, bleed: 2);
            CheckImages([[new Placed { S = bordered, X = Margin, Y = Margin }, new Placed { S = bled, X = Margin + 60, Y = Margin }]], "border-bleed.svg");

            var jh = Images(One(jpg), "jpg.svg")[0].Attribute(xlink + "href")!.Value;
            if (!jh.StartsWith("data:image/jpeg;base64,")) bad.Add($"JPG ditandai {jh[..Math.Min(22, jh.Length)]}");
        }
        finally { Directory.Delete(tmp, true); }
        return bad.Distinct().ToList();
    }

    static string Content(PdfPage page)
    {
        var sb = new StringBuilder();
        foreach (var item in page.Contents.Elements)
            if ((item as PdfReference)?.Value is PdfDictionary d && d.Stream != null) sb.Append(Encoding.ASCII.GetString(d.Stream.UnfilteredValue));
        return sb.ToString();
    }

    static BitmapSource RenderPage(string pdf, int page, double pxMm)
    {
        using var r = new Pdfium(pdf);
        var src = r.Render(page, pxMm * 25.4 / 72);
        int w = src.PixelWidth, h = src.PixelHeight;
        var px = new byte[w * h * 4];
        src.CopyPixels(px, w * 4, 0);
        for (int i = 0; i < px.Length; i += 4) // latar transparan -> putih
        {
            double a = px[i + 3] / 255.0;
            for (int c = 0; c < 3; c++) px[i + c] = (byte)(px[i + c] * a + 255 * (1 - a));
            px[i + 3] = 255;
        }
        return BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, px, w * 4);
    }

    // Warna render pada titik (pt dari pojok kiri-atas konten PDF) milik stiker yang ditempatkan.
    static Color At(BitmapSource bmp, Placed p, double xPt, double yPt, double pxMm)
    {
        var page = Nest.Xf(p).Transform(new Point(p.S.Off + xPt * 25.4 / 72, p.S.Off + yPt * 25.4 / 72));
        var c = new byte[4];
        bmp.CopyPixels(new Int32Rect((int)(page.X * pxMm), (int)(page.Y * pxMm), 1, 1), c, 4, 0);
        return Color.FromRgb(c[2], c[1], c[0]);
    }

    // Temuan 2026-11-05. File gambar diganti lalu dihapus setelah dimuat: muat ulang tetap memakai gambar saat dimuat, SVG digambar ulang
    // hanya bila filenya masih sama. Foto bertanda orientasi EXIF tegak di layar, di file export, dan sesudah muat ulang.
    // Pratinjau JPG untuk customer tanpa bingkai merah.
    static List<string> FoundErrors(string dir, Cut kiss, Cut die)
    {
        var bad = new List<string>();
        static Sticker Resized(Sticker st, double w) { st.WidthMm = w; return Loader.Reload(st, st.LoadedKiss, st.LoadedDie, st.LoadedBleed); }
        // Empat warna, supaya putaran dan cermin terlihat: merah, hijau; biru, gelap.
        static BitmapSource Quads(int w, int h)
        {
            var px = new byte[w * 3 * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    (byte B, byte G, byte R) c = (x < w / 2, y < h / 2) switch { (true, true) => (30, 30, 220), (false, true) => (30, 200, 30), (true, false) => (220, 30, 30), _ => (40, 40, 40) };
                    (px[(y * w + x) * 3], px[(y * w + x) * 3 + 1], px[(y * w + x) * 3 + 2]) = c;
                }
            return BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgr24, null, px, w * 3);
        }
        static char Hue(Color c) => c.R > 150 && c.G < 100 && c.B < 100 ? 'R' : c.G > 150 && c.R < 100 ? 'G' : c.B > 150 && c.R < 100 ? 'B' : c.R < 90 && c.G < 90 && c.B < 90 ? 'K' : '?';
        static string Hues(BitmapSource b)
        {
            var f = new FormatConvertedBitmap(b, PixelFormats.Bgra32, null, 0);
            char One(double x, double y)
            {
                var c = new byte[4];
                f.CopyPixels(new Int32Rect((int)(x * f.PixelWidth), (int)(y * f.PixelHeight), 1, 1), c, 4, 0);
                return Hue(Color.FromRgb(c[2], c[1], c[0]));
            }
            return $"{One(.25, .25)}{One(.75, .25)}{One(.25, .75)}{One(.75, .75)}";
        }

        foreach (var name in new[] { "circle.png", "star.jpg", "badge.svg" })
        {
            var f = Path.Combine(dir, "sumber-" + name);
            File.Copy(Path.Combine(dir, name), f, true);
            try
            {
                var loaded = Loader.LoadAll(f, 40, kiss, die)[0];
                double ratio = loaded.ArtH / loaded.ArtW;
                if (name.EndsWith(".svg") && Resized(Loader.LoadAll(f, 40, kiss, die)[0], 80).PxW < 1.5 * loaded.PxW)
                    bad.Add("SVG yang filenya masih sama tidak digambar ulang pada lebar baru");
                if (name.EndsWith(".svg")) File.WriteAllText(f, "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"400\" height=\"100\"><rect width=\"400\" height=\"100\" fill=\"#0a0\"/></svg>");
                else Save(Quads(400, 100), f);
                File.SetLastWriteTimeUtc(f, DateTime.UtcNow.AddMinutes(1));
                var changed = Resized(loaded, 60);
                var readded = Loader.LoadAll(f, 40, kiss, die)[0];
                File.Delete(f);
                var gone = Resized(changed, 70);
                if (Math.Abs(changed.ArtH / changed.ArtW - ratio) > 0.01 || Math.Abs(gone.ArtH / gone.ArtW - ratio) > 0.01)
                    bad.Add($"{name}: perbandingan sisi {ratio:F2}, sesudah file diganti {changed.ArtH / changed.ArtW:F2}, sesudah dihapus {gone.ArtH / gone.ArtW:F2}");
                if (Math.Abs(readded.ArtH / readded.ArtW - 0.25) > 0.01) bad.Add($"{name}: tambah ulang harus memakai isi baru, perbandingan sisi {readded.ArtH / readded.ArtW:F2}");
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { bad.Add($"{name}: {ex.Message}"); }
            finally { File.Delete(f); }
        }

        string[] upright = ["RGBK", "GRKB", "KBGR", "BKRG", "RBGK", "BRKG", "KGBR", "GKRB"];
        for (ushort o = 1; o <= 8; o++)
        {
            var md = new BitmapMetadata("jpg");
            md.SetQuery("/app1/ifd/{ushort=274}", o);
            var enc = new JpegBitmapEncoder { QualityLevel = 95 };
            enc.Frames.Add(BitmapFrame.Create(Quads(240, 120), null, md, null));
            var f = Path.Combine(dir, $"foto-exif-{o}.jpg");
            using (var fs = File.Create(f)) enc.Save(fs);
            var st = Loader.LoadAll(f, 40, kiss, die)[0];
            double wantH = o <= 4 ? 20 : 80;
            string want = upright[o - 1];
            var raster = BitmapDecoder.Create(new MemoryStream(st.Raster), BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            var again = Resized(st, 60);
            File.Delete(f);
            if (Math.Abs(st.ArtH - wantH) > 0.1 || Hues(st.Preview) != want || Hues(raster) != want || Math.Abs(again.ArtH - wantH * 1.5) > 0.1 || Hues(again.Preview) != want)
                bad.Add($"orientasi {o}: {st.ArtW:F0} x {st.ArtH:F0} mm, layar {Hues(st.Preview)}, gambar export {Hues(raster)}, sesudah muat ulang {again.ArtH:F0} mm {Hues(again.Preview)}; harapan tinggi {wantH:F0} mm, {want}");
            if (o == 6)
            {
                var pdf = Path.Combine(dir, "foto-exif.pdf");
                var pl = new Placed { S = st, X = 20, Y = 20 };
                Export.Pdf(pdf, [[pl]], A4W, [A4H]);
                var page = RenderPage(pdf, 0, 4);
                File.Delete(pdf);
                char Cell(double x, double y) => Hue(At(page, pl, st.ArtW * x * 72 / 25.4, st.ArtH * y * 72 / 25.4, 4));
                string got = $"{Cell(.25, .25)}{Cell(.75, .25)}{Cell(.25, .75)}{Cell(.75, .75)}";
                if (got != want) bad.Add($"orientasi 6 di PDF: {got}, harapan {want}");
            }
        }

        var framed = new Placed { S = Loader.Load("uji.png", 0, 40, kiss, die, edited: Loader.Png(Quads(240, 180))), X = 30, Y = 30 };
        int RedPixels(bool isBad)
        {
            framed.Bad = isBad;
            var f = Export.Preview(Path.Combine(dir, "pratinjau-merah.jpg"), [[framed]], A4W, [A4H], Margin)[0];
            var b = new FormatConvertedBitmap(BitmapDecoder.Create(new MemoryStream(File.ReadAllBytes(f)), BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0], PixelFormats.Bgr24, null, 0);
            var px = new byte[b.PixelWidth * 3 * b.PixelHeight];
            b.CopyPixels(px, b.PixelWidth * 3, 0);
            File.Delete(f);
            int n = 0;
            for (int i = 0; i + 2 < px.Length; i += 3) if (px[i + 2] > 220 && px[i + 1] < 50 && px[i] < 50) n++;
            return n;
        }
        int off = RedPixels(false), on = RedPixels(true);
        if (on != off) bad.Add($"pratinjau JPG menampilkan bingkai merah: piksel merah {off} menjadi {on}");

        // Kotak lebar desain: koma dan titik sama-sama desimal; teks yang tidak terbaca dan angka bukan positif tidak mengubah lebar.
        foreach (var (text, want) in new (string, object)[] { ("62.5", 62.5), ("62,5", 62.5), ("62,50", 62.5), ("1,5", 1.5), ("abc", DependencyProperty.UnsetValue), ("", DependencyProperty.UnsetValue), ("0", DependencyProperty.UnsetValue), ("-3", DependencyProperty.UnsetValue) })
        {
            var got = MmText.I.ConvertBack(text, typeof(double), null!, CultureInfo.InvariantCulture);
            if (!Equals(got, want)) bad.Add($"kotak lebar '{text}' terbaca {got}");
        }
        if (new Sticker { ArtH = 62.5 }.HText != "× 62.5 mm") bad.Add($"tinggi di daftar desain memakai tanda desimal lain: {new Sticker { ArtH = 62.5 }.HText}");

        // Export ke file yang masih terbuka di program lain: pesan untuk operator, di semua jenis export.
        var locked = Path.Combine(dir, "terkunci.pdf");
        using (new FileStream(locked, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            foreach (var (kind, save) in new (string, Action)[]
            {
                ("PDF", () => Export.Pdf(locked, [[framed]], A4W, [A4H])),
                ("JPG", () => Export.Preview(locked, [[framed]], A4W, [A4H], Margin)),
                ("SVG", () => ExportSvg.Svg(locked, [[framed]], A4W, [A4H], Margin)),
                ("Print Ticket", () => Export.Ticket(locked, "A4", [[framed]], A4W, [A4H], Margin, [])),
            })
                try { save(); bad.Add($"export {kind} ke file terkunci tidak gagal"); }
                catch (Exception ex) when (ex is not OutOfMemoryException) { if (MainWindow.SaveFail(ex) != Lang.T("SaveFail")) bad.Add($"export {kind} ke file terkunci: pesan teknis '{ex.Message}'"); }
        File.Delete(locked);

        // Lembar di dalam jendela: pembulatan tata letak milik jendela ikut turun ke lembar, yang satuannya mm. Gambar dan garis margin harus tetap
        // di posisi datanya; kalau dibulatkan ke piksel, gambar bergeser dari garis potong sampai ±0,4 mm pada skala layar 125%.
        var onScreen = new Placed { S = framed.S, X = 30, Y = 30 };
        var sheetHost = new System.Windows.Controls.Grid { UseLayoutRounding = true };
        var hosted = Nest.Sheet([onScreen], A4W, A4H, Margin, new HashSet<Placed>(), 0);
        sheetHost.Children.Add(hosted);
        sheetHost.Measure(new Size(A4W, A4H));
        sheetHost.Arrange(new Rect(0, 0, A4W, A4H));
        sheetHost.UpdateLayout();
        static IEnumerable<DependencyObject> Tree(DependencyObject o)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(o); i++)
            {
                var c = VisualTreeHelper.GetChild(o, i);
                yield return c;
                foreach (var d in Tree(c)) yield return d;
            }
        }
        static bool Near(Rect a, Rect b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y) + Math.Abs(a.Width - b.Width) + Math.Abs(a.Height - b.Height) < 0.001;
        var shownArt = Tree(hosted).OfType<System.Windows.Controls.Image>().First();
        var artBox = shownArt.TransformToAncestor(hosted).TransformBounds(new Rect(0, 0, shownArt.ActualWidth, shownArt.ActualHeight));
        var artWant = new Rect(onScreen.X + framed.S.Off, onScreen.Y + framed.S.Off, framed.S.ArtW, framed.S.ArtH);
        if (!Near(artBox, artWant)) bad.Add($"gambar di dalam jendela {artBox}, harapan {artWant}");
        var marginLine = hosted.Children.OfType<System.Windows.Shapes.Rectangle>().First();
        var lineBox = marginLine.TransformToAncestor(hosted).TransformBounds(new Rect(0, 0, marginLine.ActualWidth, marginLine.ActualHeight));
        var lineWant = new Rect(Margin, Margin, A4W - 2 * Margin, A4H - 2 * Margin);
        if (!Near(lineBox, lineWant)) bad.Add($"garis margin di dalam jendela {lineBox}, harapan {lineWant}");

        // Dilepas di lembar lain, tiap rotasi: tepat di grid, di posisi kosong terdekat dari titik lepas, dan digambar di kotak yang sama.
        // Ada stiker lain di titik lepas: posisi kosong terdekat, jarak antar stiker tetap dijaga.
        foreach (int rot in new[] { 0, 90, 180, 270 })
        {
            var moved = new Placed { S = framed.S, X = 20, Y = 30, Rot = rot };
            var book = new List<List<Placed>> { new() { moved }, new() };
            bool dropped = new DragState([moved], 0) { ToSheet = 1, Offset = new Vector(60.3, 80.4) }.Drop(book, [A4H, A4H], A4W, Margin, Gap) != null;
            var box = Nest.Xf(moved).TransformBounds(new Rect(0, 0, moved.S.W, moved.S.H));
            if (!dropped || book[0].Count != 0 || book[1].Count != 1 || Math.Abs(moved.X - 80) > 0.001 || Math.Abs(moved.Y - 110) > 0.001 || !Near(box, new Rect(moved.X, moved.Y, moved.BW, moved.BH)))
                bad.Add($"dilepas di lembar lain, rotasi {rot}: berhasil {dropped}, posisi {moved.X},{moved.Y}, kotak {box}");
            var crowded = new Placed { S = framed.S, X = 20, Y = 30, Rot = rot };
            var crowdBook = new List<List<Placed>> { new() { crowded }, new() { new Placed { S = framed.S, X = 80, Y = 110 } } };
            if (new DragState([crowded], 0) { ToSheet = 1, Offset = new Vector(60.3, 80.4) }.Drop(crowdBook, [A4H, A4H], A4W, Margin, Gap) == null || Nest.Check(crowdBook[1], A4W, A4H, Margin, false, Gap) != 0)
                bad.Add($"dilepas di atas stiker lain, rotasi {rot}: posisi {crowded.X},{crowded.Y}");
        }
        return bad;
    }

    // CDR tanpa CorelDRAW dan AI tanpa PDF-compatible (keputusan user 2026-11-06). Dipanggil sesudah export PDF pertama: font Windows untuk PDFsharp sudah aktif.
    static (List<string> Bad, string Note) ImportErrors(string dir, Cut kiss, Cut die)
    {
        var bad = new List<string>();

        // AI dari Illustrator yang disimpan tanpa PDF-compatible: PDF sah berisi satu halaman pengganti. Harus ditolak, bukan dimuat sebagai desain.
        var pgf = Path.Combine(dir, "pgf.ai");
        using (var doc = new PdfDocument())
        {
            var page = doc.AddPage();
            using (var g = XGraphics.FromPdfPage(page))
            {
                var font = new XFont("Arial", 10);
                g.DrawString("This is an Adobe Illustrator File that was saved without", font, XBrushes.Black, new XPoint(40, 60));
                g.DrawString("PDF Content. To Place or open this file in other applications, it should be re-saved from", font, XBrushes.Black, new XPoint(40, 74));
                g.DrawString("Adobe Illustrator with the \"Create PDF Compatible File\" option turned on.", font, XBrushes.Black, new XPoint(40, 88));
            }
            doc.Save(pgf);
        }
        string got = "dimuat sebagai desain";
        try { Loader.LoadAll(pgf, 40, kiss, die); }
        catch (InvalidDataException ex) { got = ex.Message; }
        File.Delete(pgf);
        if (got != Lang.T("BadAi")) bad.Add($"AI tanpa PDF-compatible: {got}");

        // Alat libcdr ikut di folder aplikasi, dan semua DLL-nya termuat: file yang bukan CDR ditolak dengan exit code 1.
        // Kode lain berarti DLL hilang (-1073741515) atau exe rusak.
        File.WriteAllText(Path.Combine(dir, "bukan.cdr"), "bukan file CDR");
        int code = int.MinValue;
        if (File.Exists(Libcdr.Tool))
        {
            var psi = new ProcessStartInfo(Libcdr.Tool) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = dir };
            psi.ArgumentList.Add("bukan.cdr");
            using var tool = Process.Start(psi)!;
            var (o, e) = (tool.StandardOutput.ReadToEndAsync(), tool.StandardError.ReadToEndAsync());
            if (!tool.WaitForExit(30000)) tool.Kill();
            tool.WaitForExit();
            code = tool.ExitCode;
        }
        if (code != 1) bad.Add(code == int.MinValue ? $"alat libcdr tidak ada: {Libcdr.Tool}" : $"alat libcdr: exit code {code}, harapan 1 (-1073741515 = DLL tidak ada)");

        // File yang tidak terbaca dan batas waktu: null tanpa proses yang tertinggal, supaya CorelDRAW bisa dicoba.
        if (Libcdr.ToSvg(Path.Combine(dir, "bukan.cdr")) != null) bad.Add("file yang bukan CDR tidak menghasilkan null");
        File.Copy(Path.Combine(dir, "bukan.cdr"), Path.Combine(dir, "lambat.cdr"), true);
        var before = Process.GetProcessesByName("cdr2xhtml").Length;
        var slow = Libcdr.ToSvg(Path.Combine(dir, "lambat.cdr"), timeoutMs: 1);
        Thread.Sleep(200);
        int leftOver = Process.GetProcessesByName("cdr2xhtml").Length - before;
        if (slow != null || leftOver > 0) bad.Add($"batas waktu libcdr: hasil {(slow == null ? "null" : "ada")}, proses tertinggal {leftOver}");
        File.Delete(Path.Combine(dir, "bukan.cdr"));
        File.Delete(Path.Combine(dir, "lambat.cdr"));

        // Keluaran cdr2xhtml (format asli, ditulis tangan): halaman dipecah, halaman kosong dilewati, benda di luar halaman tidak ikut,
        // dan ukuran halaman inci dengan viewBox dalam point menjadi ukuran asli dalam mm.
        var bmp = new BmpBitmapEncoder();
        bmp.Frames.Add(BitmapFrame.Create(BitmapSource.Create(4, 4, 96, 96, PixelFormats.Bgr24, null, Enumerable.Repeat((byte)90, 48).ToArray(), 12)));
        var ms = new MemoryStream();
        bmp.Save(ms);
        string Page(string body) => $"<svg:svg version=\"1.1\" xmlns:svg=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" width=\"2in\" height=\"1in\" viewBox=\"0 0 144 72\" >\n{body}\n</svg:svg>\n";
        string Xhtml(params string[] pages) => "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
            + "<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Strict//EN\" \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-strict.dtd\">\n"
            + "<html xmlns=\"http://www.w3.org/1999/xhtml\" xmlns:svg=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\">\n<body>\n"
            + "<?import namespace=\"svg\" urn=\"http://www.w3.org/2000/svg\"?>\n<!-- \n<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>\n -->\n"
            + string.Join("<hr/>\n", pages) + "\n</body>\n</html>\n";
        var fixture = Xhtml(
            Page("<svg:path d=\" \nM0,0\nL144,0\nL144,72\nL0,72\nZ\" \nstyle=\"stroke: none; fill-rule: evenodd; fill: #0000ff; \"/>\n"
                + "<svg:path d=\"M200,10 L260,10 L260,60 L200,60 Z\" style=\"stroke: none; fill: #ff0000; \"/>"),
            Page($"<svg:image x=\"10\" y=\"10\" width=\"60\" height=\"40\" xlink:href=\"data:image/bmp;base64,{Convert.ToBase64String(ms.ToArray())}\"/>"),
            Page(""));
        var split = Libcdr.Split(fixture, Path.Combine(dir, "pecah-uji"));
        if (split is not { Length: 2 }) bad.Add($"pecah halaman: {split?.Length.ToString() ?? "null"} halaman, harapan 2 (halaman kosong dilewati)");
        else
        {
            try
            {
                var first = Loader.Load(split[0], 0, 0, kiss, die);
                if (Math.Abs(first.ArtW - 50.8) > 0.3 || Math.Abs(first.ArtH - 25.4) > 0.3)
                    bad.Add($"ukuran asli halaman 2 x 1 inci: {first.ArtW:F2} x {first.ArtH:F2} mm, harapan 50,8 x 25,4 tanpa benda di luar halaman");
                Loader.Load(split[1], 0, 0, kiss, die);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { bad.Add($"halaman hasil pecah tidak termuat: {ex.Message}"); }
            foreach (var f in split) File.Delete(f);
        }
        if (Libcdr.Split(Xhtml(Page("")), Path.Combine(dir, "pecah-kosong")) != null) bad.Add("dokumen tanpa gambar tidak menghasilkan null");

        // CDR asli (contoh uji LibreOffice): terbuka tanpa CorelDRAW, ditandai sebagai gambar RGB, bisa diubah lebarnya, dan export tanpa peringatan.
        string note = "contoh CDR tidak diuji";
        var sample = Path.Combine(dir, "cdr-libreoffice-fdo65220.cdr");
        if (!File.Exists(sample)) File.Copy(Path.Combine(AppContext.BaseDirectory, "samples", "cdr-libreoffice-fdo65220.cdr"), sample);
        try
        {
            var designs = Loader.LoadAll(sample, 40, kiss, die);
            var d = designs[0];
            var (w0, h0) = (d.ArtW, d.ArtH);
            d.WidthMm = Math.Round(w0 / 2, 1);
            var half = Loader.Reload(d, d.LoadedKiss, d.LoadedDie, d.LoadedBleed);
            var placed = designs.Select((x, i) => new Placed { S = x, X = Margin, Y = Margin }).ToList();
            var pdf = Path.Combine(dir, "cdr-export.pdf");
            var warn = Export.Pdf(pdf, [placed.Take(1).ToList()], 320, [480]);
            File.Delete(pdf);
            if (designs.Count < 1 || !designs.All(x => x.CdrAsImage && x.Pdf == null) || w0 < 100 || w0 > 210 || Math.Abs(half.ArtW - d.WidthMm) > 0.05 || warn.Count != 0)
                bad.Add($"contoh CDR: {designs.Count} desain, gambar RGB {designs.All(x => x.CdrAsImage)}, {w0:F1} x {h0:F1} mm, lebar diubah jadi {half.ArtW:F1} mm, peringatan export {warn.Count}");
            note = $"contoh CDR dari LibreOffice: {designs.Count} desain {w0:F1} x {h0:F1} mm, lebar bisa diubah, export tanpa peringatan";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { bad.Add($"contoh CDR tidak terbuka: {ex.Message}"); }
        return (bad, note);
    }

    // Pindah, salin (Alt + seret), dan duplikat tidak mengubah data desain (2026-11-06): salinan memakai Sticker yang sama, rotasi ikut,
    // posisi sah di grid, tanda merah mengikuti pemeriksaan, dan desain yang diganti (atur ukuran) berlaku untuk semua penempatannya.
    static List<string> IntegrityErrors(string dir, Cut kiss, Cut die)
    {
        var bad = new List<string>();
        string circle = Path.Combine(dir, "circle.png");
        var plain = Loader.LoadAll(circle, 20, kiss, die)[0];
        Sticker[] designs =
        [
            plain,
            Loader.LoadAll(circle, 20, kiss, die, 1)[0],
            Loader.Load(circle, 0, 20, kiss, die, shape: CutShape.Star, border: new Cmyk(0, 1, 1, 0), dieShape: CutShape.Rect),
            Loader.Load(circle, 0, 20, kiss, die, manualKiss: Curves.Simplify(plain.Kiss, plain.Off, 0.2), manualDie: Curves.Simplify(plain.Die, plain.Off, 0.2)),
            Loader.Load(Path.Combine(dir, "logo.pdf"), 0, 20, kiss, die),
        ];
        // Data yang tidak boleh berubah: gambar, PDF, bleed, garis potong, garis manual, bentuk, border, offset, ukuran, dan grid jejak.
        static object[] Print(Sticker s) => [s.Raster, s.Pdf ?? "", s.BleedPng ?? (object)"", s.BleedRect, s.Kiss, s.Die, s.KissGeo ?? (object)"", s.DieGeo ?? (object)"",
            s.ManualKiss ?? (object)"", s.ManualDie ?? (object)"", s.Shape, s.DieShape, s.Border ?? (object)"", s.LoadedBleed, s.LoadedKiss.Off, s.LoadedDie.Off,
            s.ArtW, s.ArtH, s.W, s.H, s.Off, s.Full, s.Preview, s.Grid, s.AreaMm2];
        var prints = designs.Select(Print).ToList();
        if (designs[1].BleedPng == null || designs[2].Border == null || designs[3].ManualKiss == null || designs[4].Pdf == null) bad.Add("desain uji tidak lengkap");
        foreach (var d in designs) d.Copies = 1;
        var book = Nest.Run(designs, A4W, A4H, Margin, Gap, out int big);
        book.Add([]);
        List<double> hs = [A4H, A4H];
        if (big != 0 || book.Count != 2 || book[0].Count != designs.Length) { bad.Add($"susun awal: {book[0].Count} stiker, tidak muat {big}"); return bad; }
        var orig = designs.Select(d => book[0].First(p => p.S == d)).ToList();
        if (!Nest.Turn(book[0], orig[1], A4W, A4H, Margin, Gap) || !Nest.Turn(book[0], orig[3], A4W, A4H, Margin, Gap) || !Nest.Turn(book[0], orig[3], A4W, A4H, Margin, Gap))
            bad.Add("putar sebelum uji tidak punya tempat");
        var rot = orig.Select(p => p.Rot).ToList();

        for (int i = 0; i < designs.Length; i++)
        {
            var p = orig[i];
            var (x, y) = (p.X, p.Y);
            if (new DragState([p], 0) { ToSheet = 1, Offset = new Vector(3, 5) }.Drop(book, hs, A4W, Margin, Gap) == null || !book[1].Contains(p))
                bad.Add($"desain {i + 1}: pindah ke lembar 2 gagal");
            if (new DragState([p], 1) { ToSheet = 0, Offset = new Vector(x - p.X, y - p.Y) }.Drop(book, hs, A4W, Margin, Gap) == null || !book[0].Contains(p) || (p.X, p.Y) != (x, y))
                bad.Add($"desain {i + 1}: pindah kembali ke lembar 1 tidak ke posisi asalnya");
            var toB = new DragState([p], 0) { ToSheet = 1, Copy = true, Offset = new Vector(0, 140) }.Drop(book, hs, A4W, Margin, Gap);
            var inGap = new DragState([p], 0) { Copy = true, Offset = new Vector(0, 140) }.Drop(book, hs, A4W, Margin, Gap);
            var dup = Nest.PasteAll(book, hs, [(p.S, p.Rot, p.X, p.Y, 0)], A4W, Margin, Gap);
            foreach (var (kind, made, sheet) in new[] { ("salin ke lembar 2", toB, 1), ("salin di celah", inGap, 0), ("duplikat", dup, 0) })
                if (made is not [var one] || one == p || one.S != designs[i] || one.Rot != p.Rot || !book[sheet].Contains(one))
                    bad.Add($"desain {i + 1}, {kind}: salinan tidak memakai desain, rotasi, atau lembar yang benar");
            if (p.Rot != rot[i] || !book[0].Contains(p) || (p.X, p.Y) != (x, y)) bad.Add($"desain {i + 1}: stiker asal berubah sesudah disalin");
        }

        for (int i = 0; i < designs.Length; i++)
            if (!Print(designs[i]).SequenceEqual(prints[i])) bad.Add($"desain {i + 1}: data desain berubah");
        var all = book.SelectMany(x => x).ToList();
        if (all.Count != designs.Length * 4 || book[1].Count != designs.Length || all.Any(p => !designs.Contains(p.S))) bad.Add($"jumlah penempatan {all.Count}, harapan {designs.Length * 4}");
        for (int i = 0; i < book.Count; i++)
            if (Nest.Check(book[i], A4W, A4H, Margin, false, Gap) != 0) bad.Add($"lembar {i + 1}: {book[i].Count(p => p.Bad)} stiker bertanda merah");
        foreach (var p in all)
        {
            var box = Nest.Xf(p).TransformBounds(new Rect(0, 0, p.S.W, p.S.H));
            if (p.Rot % 90 != 0 || Math.Abs(box.X - p.X) + Math.Abs(box.Y - p.Y) + Math.Abs(box.Width - p.BW) + Math.Abs(box.Height - p.BH) > 0.001
                || Math.Abs((p.X - Margin) / Nest.Cell - Math.Round((p.X - Margin) / Nest.Cell)) > 1e-6)
                bad.Add($"rotasi {p.Rot}: kotak di {box}, harapan {p.X},{p.Y} {p.BW}x{p.BH} di grid");
        }
        if (!rot.Contains(90) || !rot.Contains(180) && !rot.Contains(270)) bad.Add($"rotasi uji kurang beragam: {string.Join(",", rot)}");

        // Gambar dan PDF tetap terhubung: export tanpa peringatan.
        var pdf = Path.Combine(dir, "integritas.pdf");
        var warn = Export.Pdf(pdf, book, A4W, hs);
        File.Delete(pdf);
        if (warn.Count != 0 || !File.Exists(designs[4].Pdf)) bad.Add($"export sesudah pindah dan salin: {string.Join(" ", warn)}");

        // Tanda merah mengikuti pemeriksaan: keluar margin jadi merah, sesudah ditarik ke tempat sah hilang.
        var stray = orig[0];
        var (sx, sy) = (stray.X, stray.Y);
        stray.Y = -stray.BH - 10; // di atas lembar, jauh dari stiker lain: hanya stiker ini yang keluar margin
        int flagged = Nest.Check(book[0], A4W, A4H, Margin, false, Gap);
        bool red = stray.Bad;
        var back = new DragState([stray], 0) { Offset = new Vector(sx - stray.X, sy - stray.Y) }.Drop(book, hs, A4W, Margin, Gap);
        Nest.Check(book[0], A4W, A4H, Margin, false, Gap);
        if (flagged != 1 || !red || back == null || stray.Bad) bad.Add($"tanda merah: keluar margin {red} ({flagged} stiker), sesudah ditarik kembali {stray.Bad}");

        // Desain diganti (Rapikan, Edit garis potong): semua penempatannya, termasuk salinan, memakai desain baru, dan tanda merah dihitung ulang.
        plain.WidthMm = 26;
        var bigger = Loader.Reload(plain, plain.LoadedKiss, plain.LoadedDie, plain.LoadedBleed);
        Nest.SwapDesign(book, plain, bigger);
        int marked = Nest.Check(book[0], A4W, A4H, Margin, false, Gap) + Nest.Check(book[1], A4W, A4H, Margin, false, Gap);
        all = book.SelectMany(x => x).ToList();
        if (all.Count(p => p.S == bigger) != 4 || all.Any(p => p.S == plain) || all.Count(p => p.Bad) != marked)
            bad.Add($"desain diganti: {all.Count(p => p.S == bigger)} dari 4 penempatan memakai desain baru, tanda merah {all.Count(p => p.Bad)} dari {marked}");
        return bad;
    }

    static void Save(BitmapSource b, string path, BitmapEncoder? enc = null)
    {
        enc ??= new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(b));
        using var fs = File.Create(path);
        enc.Save(fs);
    }

    // Luas (mm2) bagian dalam garis kiss a yang keluar dari garis kiss b. Bingkai disamakan lewat Off (posisi artwork).
    static double Outside(Sticker a, Sticker b)
    {
        static Geometry G(Sticker s) { var g = s.KissGeo!.Clone(); g.Transform = new TranslateTransform(-s.Off, -s.Off); return g; }
        return Geometry.Combine(G(a), G(b), GeometryCombineMode.Exclude, null).GetArea();
    }

    static byte[] NoBackground(string path) { var c = new CleanImage(File.ReadAllBytes(path)); c.EraseFromBorder(32); return c.ToPng(0); }

    // Titik kurva yang dipecah 0,01 mm, per kontur (koordinat path dikurangi off).
    static List<Point[]> Flat(string path, double off) => [.. Geometry.Parse(path).GetFlattenedPathGeometry(0.01, ToleranceType.Absolute).Figures
        .Select(f => f.Segments.SelectMany(s => s is PolyLineSegment pl ? pl.Points : s is LineSegment l ? [l.Point] : []).Prepend(f.StartPoint)
            .Select(q => new Point(q.X - off, q.Y - off)).ToArray())];

    // Simpangan maksimal kurva (dipecah 0,01 mm) dari garis node (mm relatif artwork, juga dipecah 0,01 mm).
    static double Deviation(string path, double off, List<Node[]> lines)
    {
        var target = Flat(Curves.PathOf(lines, 0), 0);
        double worst = 0;
        foreach (var q in Flat(path, off).SelectMany(c => c))
            worst = Math.Max(worst, target.Min(c => c.Select((a, i) => (q - Loader.Nearest(q, a, c[(i + 1) % c.Length])).Length).Min()));
        return worst;
    }

    static BitmapSource Draw(Action<DrawingContext> paint, int w = 300, int h = 300)
    {
        var v = new DrawingVisual();
        using (var dc = v.RenderOpen()) paint(dc);
        var rt = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rt.Render(v);
        return rt;
    }

    static void Samples(string dir)
    {
        // circle.png: transparan
        Save(Draw(dc =>
        {
            dc.DrawEllipse(Brushes.Orange, null, new Point(150, 150), 140, 140);
            dc.DrawEllipse(Brushes.SteelBlue, null, new Point(150, 150), 90, 90);
            dc.DrawEllipse(Brushes.White, null, new Point(150, 150), 40, 40);
        }), Path.Combine(dir, "circle.png"));

        // star.jpg: latar putih (tanpa alpha) + lubang putih di tengah
        Save(Draw(dc =>
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 300, 300));
            var pts = Enumerable.Range(0, 10).Select(i =>
            {
                double a = -Math.PI / 2 + i * Math.PI / 5, r = i % 2 == 0 ? 140 : 60;
                return new Point(150 + r * Math.Cos(a), 155 + r * Math.Sin(a));
            }).ToList();
            var g = new StreamGeometry();
            using (var c = g.Open()) { c.BeginFigure(pts[0], true, true); c.PolyLineTo(pts.Skip(1).ToList(), true, false); }
            dc.DrawGeometry(Brushes.Crimson, null, g);
            dc.DrawEllipse(Brushes.White, null, new Point(150, 155), 25, 25);
        }), Path.Combine(dir, "star.jpg"), new JpegBitmapEncoder { QualityLevel = 90 });

        // rough.jpg: logo bergelombang bertepi kasar (seperti foto JPG dari WhatsApp), untuk uji reduksi node
        var rnd = new Random(7);
        var blob = Enumerable.Range(0, 180).Select(i =>
        {
            double a = i * Math.PI / 90, r = 110 + 18 * Math.Sin(5 * a) + rnd.Next(-3, 4);
            return new Point(150 + r * Math.Cos(a), 150 + r * Math.Sin(a));
        }).ToList();
        Save(Draw(dc =>
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 300, 300));
            var g = new StreamGeometry();
            using (var c = g.Open()) { c.BeginFigure(blob[0], true, true); c.PolyLineTo(blob.Skip(1).ToList(), true, false); }
            dc.DrawGeometry(Brushes.SeaGreen, null, g);
        }), Path.Combine(dir, "rough.jpg"), new JpegBitmapEncoder { QualityLevel = 70 });

        // logo-on-blue.jpg: lingkaran oranye + teks hijau di latar biru, untuk uji Rapikan gambar
        Save(Draw(dc =>
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(30, 90, 168)), null, new Rect(0, 0, 300, 300));
            dc.DrawEllipse(Brushes.DarkOrange, null, new Point(150, 140), 110, 110);
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(20, 120, 60)), null, new Rect(20, 280, 260, 12));
        }), Path.Combine(dir, "logo-on-blue.jpg"), new JpegBitmapEncoder { QualityLevel = 90 });

        File.WriteAllText(Path.Combine(dir, "badge.svg"),
            "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"200\" height=\"120\" viewBox=\"0 0 100 60\">" +
            "<rect x=\"2\" y=\"2\" width=\"96\" height=\"56\" rx=\"12\" fill=\"#2a9d8f\"/>" +
            "<circle cx=\"30\" cy=\"30\" r=\"14\" fill=\"#e9c46a\"/><rect x=\"52\" y=\"20\" width=\"36\" height=\"6\" fill=\"#fff\"/><rect x=\"52\" y=\"34\" width=\"28\" height=\"6\" fill=\"#fff\"/></svg>");

        // logo.pdf: vektor CMYK, CropBox (40,60)-(260,250) lebih kecil dari MediaBox 300x300 pt.
        // Dibuat sekali saja: PDFsharp menulis tanggal/ID baru tiap kali, file sampel di git jadi berubah terus.
        if (!File.Exists(Path.Combine(dir, "logo.pdf")))
        using (var doc = new PdfDocument())
        {
            var page = doc.AddPage();
            page.Width = XUnit.FromPoint(300);
            page.Height = XUnit.FromPoint(300);
            using (var g = XGraphics.FromPdfPage(page))
            {
                g.DrawEllipse(new XSolidBrush(XColor.FromCmyk(0, 0.6, 1, 0)), 90, 90, 120, 120);
                g.DrawRectangle(new XSolidBrush(XColor.FromCmyk(1, 0.4, 0, 0)), 150, 135, 80, 30);
                g.DrawRectangle(new XSolidBrush(XColor.FromCmyk(0, 1, 1, 0)), 5, 5, 20, 20); // di luar CropBox
            }
            page.CropBox = new PdfRectangle(new XPoint(40, 60), new XPoint(260, 250));
            doc.Save(Path.Combine(dir, "logo.pdf"));
        }
        File.Copy(Path.Combine(dir, "logo.pdf"), Path.Combine(dir, "logo.ai"), true); // AI "PDF compatible"

        // multi.pdf: 2 halaman (lingkaran oranye 80 pt, persegi panjang biru 180x40 pt) untuk uji satu desain per halaman.
        if (!File.Exists(Path.Combine(dir, "multi.pdf")))
        using (var doc = new PdfDocument())
        {
            foreach (var (w, h, round) in new[] { (100.0, 100.0, true), (200.0, 60.0, false) })
            {
                var page = doc.AddPage();
                page.Width = XUnit.FromPoint(w);
                page.Height = XUnit.FromPoint(h);
                using var g = XGraphics.FromPdfPage(page);
                var brush = new XSolidBrush(round ? XColor.FromCmyk(0, 0.6, 1, 0) : XColor.FromCmyk(1, 0.4, 0, 0));
                if (round) g.DrawEllipse(brush, 10, 10, w - 20, h - 20);
                else g.DrawRectangle(brush, 10, 10, w - 20, h - 20);
            }
            doc.Save(Path.Combine(dir, "multi.pdf"));
        }
    }
}
