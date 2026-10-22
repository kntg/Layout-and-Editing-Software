using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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
            log.Add($"dpi: circle 40 mm = {circle.Dpi:0} dpi, 80 mm = {wide.Dpi:0} dpi (rendah)");

            // Skenario uji garis potong S1-S9 dan S11-S13 (docs/UJI-GARIS-POTONG.md). S10 dan bagian CorelDRAW di S8 diuji operator.
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

            // S12: editor garis potong (masukan leader 2026-10-21). Polygon dari kurva otomatis; garis manual dipakai apa adanya.
            var cPath = Path.Combine(dir, "circle.png");
            var pk = Loader.ToPolygons(circle40.Kiss, circle40.Off, 0.2);
            var pd = Loader.ToPolygons(circle40.Die, circle40.Off, 0.2);
            double dev = Deviation(circle40.Kiss, circle40.Off, pk);
            int nFine = Loader.ToPolygons(circle40.Kiss, circle40.Off, 0.05).Sum(c => c.Length), nMid = pk.Sum(c => c.Length),
                nCoarse = Loader.ToPolygons(circle40.Kiss, circle40.Off, 0.5).Sum(c => c.Length);
            var man = Loader.Load(cPath, 0, 40, kiss, die, manualKiss: pk, manualDie: pd);
            bool straight = !(man.Kiss + man.Die).Contains('C') && man.Kiss.StartsWith('M') && man.Die.EndsWith('Z');
            double areaDiff = Math.Abs(man.AreaMm2 - circle40.AreaMm2) / circle40.AreaMm2;
            // Titik die terkanan digeser 1,5 mm keluar: grid nesting ikut berubah.
            var pd2 = pd.Select(c => (Point[])c.Clone()).ToList();
            pd2[0][Array.FindIndex(pd2[0], q => q.X == pd2[0].Max(r => r.X))].X += 1.5;
            var man2 = Loader.Load(cPath, 0, 40, kiss, die, manualKiss: pk, manualDie: pd2);
            // Lebar 40 -> 80 mm: polygon diskala. Offset kiss 2 -> 1 mm: garis manual tidak berubah.
            var man80 = Loader.Load(cPath, 0, 80, kiss, die, manualKiss: Loader.Scale(pk, 2), manualDie: Loader.Scale(pd, 2));
            var manOff = Loader.Load(cPath, 0, 40, new Cut(true, 1, 1), die, manualKiss: pk, manualDie: pd);
            static Rect Rel(Sticker s) { var b = s.KissGeo!.Bounds; b.Offset(-s.Off, -s.Off); return b; }
            double scaleErr = Math.Abs(Rel(man80).Width - 2 * Rel(man).Width);
            double lockErr = (Rel(manOff).TopLeft - Rel(man).TopLeft).Length + Math.Abs(Rel(manOff).Width - Rel(man).Width);
            // Die manual rapat (di posisi garis kiss) dengan kiss otomatis: kiss dipangkas ke dalam die.
            var tight = Loader.Load(cPath, 0, 40, kiss, die, manualDie: pk);
            double tightOut = Geometry.Combine(tight.KissGeo!, tight.DieGeo!, GeometryCombineMode.Exclude, null).GetArea();
            // Export: garis manual = garis lurus, warna CMYK sama.
            var manPdf = Path.Combine(dir, "export-manual.pdf");
            Check(Export.Pdf(manPdf, [[new Placed { S = man, X = Margin, Y = Margin }]], A4W, [A4H]).Count == 0, "peringatan export garis manual");
            string mc;
            using (var mdoc = PdfReader.Open(manPdf, PdfDocumentOpenMode.Import)) mc = Content(mdoc.Pages[0]);
            int mLines = Regex.Matches(mc, @"(?<=\s)l(?=\s)").Count, mCurves = Regex.Matches(mc, @"(?<=\s)c(?=\s)").Count;
            bool mExport = mLines > 0 && mCurves == 0 && mc.Contains("0 1 0 0 K") && mc.Contains("1 0 0 0 K");

            // Editor tanpa mouse: tambah titik di tengah segmen lalu seret, undo/redo, hapus, kiss keluar die, Simplify, reset.
            var ed = new CutEditWindow(circle40, null, null, "uji");
            var mid = pk[0][0] + (pk[0][1] - pk[0][0]) / 2;
            ed.Press(mid, 0.3);
            ed.DragTo(mid + (new Point(circle40.ArtW / 2, circle40.ArtH / 2) - mid) * 0.05);
            ed.Release();
            int Pts(List<Point[]>? p) => p?.Sum(c => c.Length) ?? -1;
            var edLog = new List<string> { $"tambah {Pts(ed.ManualKiss)}" };
            bool edOk = Pts(ed.ManualKiss) == nMid + 1 && ed.CanSave;
            ed.Undo();
            edOk &= ed.ManualKiss == null;
            ed.Redo();
            edOk &= Pts(ed.ManualKiss) == nMid + 1;
            ed.Press(pk[0][2], 0.3);
            ed.Release();
            ed.DeletePoint();
            edOk &= Pts(ed.ManualKiss) == nMid;
            edLog.Add($"undo/redo/hapus {Pts(ed.ManualKiss)}");
            ed.Press(pk[0][3], 0.3);
            ed.DragTo(new Point(-100, -100));
            ed.Release();
            edOk &= !ed.CanSave && !ed.KissInside;
            ed.Undo();
            edOk &= ed.CanSave;
            ed.Eps.Value = 0.5;
            edOk &= Pts(ed.ManualKiss) == nCoarse;
            edLog.Add($"Simplify 0,5 {Pts(ed.ManualKiss)}");
            ed.Reset();
            edOk &= ed.ManualKiss == null && ed.ManualDie == null && ed.CanSave;
            // Donat: garis dalam yang tidak diinginkan dihapus titik demi titik sampai konturnya hilang.
            var dn = Png(Donut(70), 40, kiss, die);
            var dk = Loader.ToPolygons(dn.Kiss, dn.Off, 0.2);
            var hole = dk.MinBy(c => c.Max(q => q.X) - c.Min(q => q.X))!;
            var ed2 = new CutEditWindow(dn, null, null, "donat");
            foreach (var q in hole.Take(hole.Length - 2)) { ed2.Press(q, 0.01); ed2.Release(); ed2.DeletePoint(); }
            edOk &= ed2.ManualKiss?.Count == 1 && ed2.ManualKiss[0].Length == dk.First(c => c != hole).Length;
            edLog.Add($"donat: kontur kiss {dk.Count} -> {ed2.ManualKiss?.Count}");
            S(12, "editor garis potong: polygon, garis manual, skala, kunci offset, export garis lurus, edit titik",
                dev <= 0.21 && nCoarse < nMid && nMid < nFine && straight && areaDiff < 0.02 && man2.Area > man.Area && scaleErr < 0.05 && lockErr < 0.01
                && tightOut < 0.5 && mExport && edOk,
                $"simpangan {dev:F3} mm (eps 0,2); titik eps 0,05/0,2/0,5 = {nFine}/{nMid}/{nCoarse}; M/L/Z {straight}; luas {areaDiff:P1}; "
                + $"grid {man.Area} -> {man2.Area} sel; skala {scaleErr:F3} mm; kunci offset {lockErr:F3} mm; die rapat: kiss di luar die {tightOut:F2} mm2; "
                + $"export {mLines} garis lurus, {mCurves} kurva; editor {string.Join(", ", edLog)} ({(edOk ? "benar" : "SALAH")})");

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
            S(13, "smart select: objek, takik diikuti, watermark diabaikan, lubang sempit lewat klik, pulau, objek opak", smartOk && edSmart,
                $"kiss kiri {Left(sr1.Kiss):F2} mm (Toleransi 64) / {Left(sr5.Kiss):F2} mm (16); kontur kiss {sr1.Kiss.Count}, +lubang {sr2.Kiss.Count} "
                + $"(garis dalam {ssInnerW:F2} mm, die {sr2.Die.Count}, kiss di luar die {ssOut:F2} mm2), klik lubang 2x {sr3.Kiss.Count}, +pulau {sr4.Kiss.Count}, "
                + $"lubang jarum terlalu kecil {sr6.TooSmall}; takik: smart memuat titik {Holds(sr1.Kiss, notchP)}, otomatis {Holds(ssAuto, notchP)}; "
                + $"opak: Toleransi {Loader.DefaultTolerance(ssOpaque)}, kiri {Left(sro.Kiss):F2} mm"
                + $"; editor {se1}/{se2}/{se3}/{se4}/{se5} kontur, kiri {seL64:F2} -> {seL16:F2} mm, Toleransi awal {(seTol ? 64 : -1)} ({(edSmart ? "benar" : "SALAH")})");

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
                string msg = "tanpa error";
                try { Corel.ToPdf(fakeCdr); } catch (InvalidOperationException ex) { msg = ex.Message; }
                P(1, "CDR tanpa CorelDRAW: pesan jelas, aplikasi tidak crash", msg == Lang.T("NoCorel"), $"{cdrSw.ElapsedMilliseconds} ms: {msg}");
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

            // Indikator bahan: bentuk reguler vs irregular, warna target, preset valid, Print Ticket 1 halaman A4.
            var fillOf = items.ToDictionary(s => s.Name, s => s.Fill);
            Check(fillOf["badge.svg"] > 0.85 && Math.Abs(fillOf["circle.png"] - Math.PI / 4) < 0.03 && fillOf["star.jpg"] < 0.75,
                $"isi bentuk salah: badge {fillOf["badge.svg"]:F2}, circle {fillOf["circle.png"]:F2}, star {fillOf["star.jpg"]:F2}");
            Check(Yield.Level(0.76, true) == 2 && Yield.Level(0.72, true) == 1 && Yield.Level(0.65, true) == 0
                && Yield.Level(0.61, false) == 2 && Yield.Level(0.56, false) == 1 && Yield.Level(0.50, false) == 0, "batas warna bahan salah");
            Check(Presets.Material.Values.All(m => (m.Kiss.On || m.Die.On) && (!(m.Kiss.On && m.Die.On) || m.Die.Off > m.Kiss.Off) && m.Gap >= 0),
                "preset bahan tidak valid");
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
            Check(fails.Count == 0, "skenario gagal: " + string.Join(", ", fails));

            Save(Export.Snapshot(Nest.Sheet(sheets[0], A4W, A4H, Margin, null, 0), A4W, A4H, 4), Path.Combine(dir, "layout-1.png"));

            // Preview JPG untuk customer: satu file per lembar, sisi panjang +-2000 px, bisa dibaca ulang.
            var jpgs = Export.Preview(Path.Combine(dir, "preview.jpg"), sheets, A4W, heights, Margin);
            Check(jpgs.Count == sheets.Count && jpgs.All(File.Exists), "jumlah preview JPG != jumlah lembar");
            var jf = BitmapDecoder.Create(new Uri(jpgs[0]), BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            Check(Math.Abs(Math.Max(jf.PixelWidth, jf.PixelHeight) - 2000) <= 2, $"ukuran preview salah: {jf.PixelWidth}x{jf.PixelHeight}");
            log.Add($"preview: {jpgs.Count} JPG, {jf.PixelWidth}x{jf.PixelHeight} px, {new FileInfo(jpgs[0]).Length / 1024} KB");

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

    // Simpangan maksimal kurva (dipecah 0,01 mm) dari polygon (mm relatif artwork).
    static double Deviation(string path, double off, List<Point[]> polys)
    {
        double worst = 0;
        foreach (var f in Geometry.Parse(path).GetFlattenedPathGeometry(0.01, ToleranceType.Absolute).Figures)
            foreach (var q0 in f.Segments.SelectMany(s => s is PolyLineSegment pl ? pl.Points : s is LineSegment l ? [l.Point] : []).Prepend(f.StartPoint))
            {
                var q = new Point(q0.X - off, q0.Y - off);
                worst = Math.Max(worst, polys.Min(c => c.Select((a, i) => (q - Loader.Nearest(q, a, c[(i + 1) % c.Length])).Length).Min()));
            }
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
