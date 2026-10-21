using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BitmapToVector;
using PdfSharp.Pdf.IO;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace StickerLayout;

// Satu garis potong: aktif, jarak dari artwork, dan radius penghalus lekukan (mm).
public record Cut(bool On, double Off, double Smooth);

public class Sticker
{
    public string Name { get; set; } = "";
    public int Copies { get; set; } = 1;
    public double WidthMm { get; set; }          // lebar artwork yang diminta operator (mm)
    public string Source = "";
    public int Page;
    public string? Pdf;                          // PDF yang ditempel vektor saat export (PDF/AI asli atau hasil konversi CDR)
    public byte[] Raster = [];                   // gambar yang ditempel saat export bila bukan vektor
    public byte[]? Edited;                       // PNG hasil "Rapikan gambar"; dipakai menggantikan file asli
    public bool CanClean => Pdf == null;         // vektor (PDF/AI/CDR) tidak dirapikan agar warna CMYK tetap utuh
    // PDF/AI/CDR yang tidak bisa ditempel vektor (halaman diputar, tidak terbaca PDFsharp): ditempel sebagai gambar RGB.
    public bool PdfAsImage => Pdf == null && Path.GetExtension(Source).ToLowerInvariant() is ".pdf" or ".ai" or ".cdr";
    public double LoadedW, LoadedBleed;
    public Cut LoadedKiss = null!, LoadedDie = null!;
    public double W, H;                          // kotak (mm), kelipatan Nest.Cell
    public double Off, ArtW, ArtH;               // posisi & ukuran artwork di dalam kotak (mm)
    public Rect Full;                            // seluruh sumber (halaman PDF / gambar penuh) dalam koordinat kotak (mm)
    public byte[]? BleedPng;                     // raster + bleed (warna tepi diperluas) untuk export; null = tanpa bleed
    public BitmapSource? BleedView;              // salinan kecil BleedPng untuk layar
    public Rect BleedRect;                       // posisi BleedPng di kotak (mm)
    public double AreaMm2;                       // luas di dalam garis potong terluar
    public BitmapSource Preview { get; set; } = null!; // artwork yang sudah di-crop, salinan kecil untuk layar
    public int PxW;                                     // lebar artwork dalam piksel asli (untuk dpi)
    public string HText => $"× {ArtH:0.#} mm";        // tinggi artwork di daftar desain
    // Resolusi cetak untuk desain yang ditempel sebagai gambar. Target riset 300 ppi; 150 = batas kasar,
    // sesuaikan setelah melihat hasil cetak Develop.
    public const double MinDpi = 150;
    public double? Dpi => Pdf == null && WidthMm > 0 ? PxW * 25.4 / WidthMm : null;
    public string DpiText => Dpi is { } d ? $"{d:0} dpi" : "";
    public bool LowDpi => Dpi < MinDpi;
    public string Kiss = "", Die = "";           // path SVG garis potong (mm, koordinat kotak)
    public bool[,] Grid = null!;                 // hunian sel nesting dari garis terluar [x,y]
    public int Area;
    Geometry? kg, dg, clip;
    public Geometry? KissGeo => Kiss == "" ? null : kg ??= Freeze(Geometry.Parse(Kiss));
    public Geometry? DieGeo => Die == "" ? null : dg ??= Freeze(Geometry.Parse(Die));
    public Geometry Outer => DieGeo ?? KissGeo!;
    // Seberapa penuh bentuk mengisi kotaknya: persegi ±1, lingkaran ±0,79, bintang/logo bebas lebih kecil.
    public double Fill => AreaMm2 / Math.Max(1e-9, Outer.Bounds.Width * Outer.Bounds.Height);
    // Batas artwork saat digambar: garis terluar, atau dengan bleed: garis kiss (tanpa kiss: die) diperlebar bleed mm.
    public Geometry ArtClip => clip ??= BleedPng == null ? Outer : Freeze(Geometry.Combine(KissGeo ?? DieGeo!,
        (KissGeo ?? DieGeo!).GetWidenedPathGeometry(new Pen(null, 2 * LoadedBleed), 0.01, ToleranceType.Absolute),
        GeometryCombineMode.Union, null, 0.01, ToleranceType.Absolute));
    static Geometry Freeze(Geometry g) { g.Freeze(); return g; }
}

public class Placed
{
    public Sticker S = null!;
    public double X, Y;
    public int Rot;
    public bool Bad;                             // tumpang tindih atau keluar margin
    public double BW => Rot % 180 == 0 ? S.W : S.H;
    public double BH => Rot % 180 == 0 ? S.H : S.W;
    public (double x, double y) T => Rot switch { 90 => (S.H, 0), 180 => (S.W, S.H), 270 => (0, S.W), _ => (0, 0) };
}

public static class Loader
{
    public const double PxMm = 6;    // resolusi mask garis potong: 6 px per mm
    // Potrace: tanpa sudut tajam (pisau tidak berbelok mendadak) dan gabung segmen kurva sampai 1 px (0,17 mm).
    // Diukur 2026-10-16 di pipeline ini; kalibrasi ulang setelah uji potong di Graphtec.
    const double AlphaMax = 1.3334, OptTolerance = 1.0;
    const double MaxInward = 0.3;    // penghalusan tidak boleh membawa garis lebih dekat dari (offset - 0,3 mm) ke artwork
    const double ViewPxMm = 10;      // resolusi render PDF untuk layar & deteksi konten
    const double Pt = 72 / 25.4;     // point per mm

    // PDF multi-halaman -> satu desain per halaman. PDF/AI/CDR memakai ukuran asli; PNG/JPG/SVG memakai defWidthMm.
    public static List<Sticker> LoadAll(string path, double defWidthMm, Cut kiss, Cut die, double bleed = 0)
    {
        var pdf = PdfOf(path);
        int pages = 1;
        if (pdf != null)
            using (var r = new Pdfium(pdf)) pages = r.Pages;
        var res = new List<Sticker>();
        for (int i = 0; i < pages; i++) res.Add(Load(path, i, pdf != null ? 0 : defWidthMm, kiss, die, pages > 1, bleed: bleed));
        return res;
    }

    static string? PdfOf(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".pdf" or ".ai" => Snapshot(path),
        ".cdr" => Corel.ToPdf(path),
        _ => null,
    };

    // Versi file = waktu ubah + ukuran. File yang diedit lalu ditambah ulang dimuat dari isi barunya, bukan dari cache.
    public static (DateTime, long) Stamp(string path) { var f = new FileInfo(path); return f.Exists ? (f.LastWriteTimeUtc, f.Length) : default; }

    // PDF/AI disalin ke folder temp saat dimuat. Export memakai salinan itu, jadi artwork tetap sama dengan dasar garis
    // potong walau file asli diubah, dipindah, atau dihapus sesudahnya.
    static readonly ConcurrentDictionary<string, (string Copy, (DateTime, long) Stamp)> snaps = new();

    static string Snapshot(string path)
    {
        var v = Stamp(path);
        if (snaps.TryGetValue(path, out var s) && s.Stamp == v && File.Exists(s.Copy)) return s.Copy;
        Directory.CreateDirectory(App.Temp);
        var copy = Path.Combine(App.Temp, Guid.NewGuid() + Path.GetExtension(path));
        File.Copy(path, copy);
        snaps[path] = (copy, v);
        return copy;
    }

    // Semua input -> bitmap penuh -> crop ke konten -> mask -> garis kiss/die (dilasi + closing) -> Potrace.
    // loadedPdf: PDF yang dipakai saat desain pertama dimuat (muat ulang saat Susun): isi desain tidak berubah diam-diam
    // dan CorelDRAW tidak dibuka lagi.
    public static Sticker Load(string path, int page, double widthMm, Cut kiss, Cut die, bool multiPage = false, byte[]? edited = null, double bleed = 0,
        string? loadedPdf = null)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var s = new Sticker { Source = path, Page = page, Name = Path.GetFileName(path) + (multiPage ? $" p{page + 1}" : "") };
        var pdf = edited == null ? loadedPdf ?? PdfOf(path) : null;
        BitmapSource full;
        double nat = double.NaN;                 // mm per px pada ukuran asli (hanya PDF)
        Rect media;                              // area yang digambar saat export, px relatif terhadap bitmap
        if (pdf != null)
        {
            (full, nat, media, bool vector) = RenderPdf(pdf, page);
            if (vector) s.Pdf = pdf;
        }
        else if (edited == null && ext == ".svg") { full = RenderSvg(path, widthMm); s.Raster = Png(full); media = new Rect(0, 0, full.PixelWidth, full.PixelHeight); }
        else if (edited != null || ext is ".png" or ".jpg" or ".jpeg")
        {
            s.Edited = edited;
            s.Raster = edited ?? File.ReadAllBytes(path);
            using var ms = new MemoryStream(s.Raster);
            full = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
            media = new Rect(0, 0, full.PixelWidth, full.PixelHeight);
        }
        else throw new NotSupportedException(Lang.T("Unsupported"));

        var (px, fw, fh) = Pixels(full);
        bool alpha = false;
        for (int i = 3; i < px.Length; i += 4) if (px[i] < 250) { alpha = true; break; }
        // Gambar opak berlatar warna, termasuk warna muda yang tetap tercetak (abu 242, pink pastel 255,240,245): seluruh
        // gambar = artwork (alpha 255 semua). Hanya latar putih yang dibuang (temuan D10).
        alpha |= !WhiteBorder(px, fw, fh);
        var bb = ContentBox(px, fw, fh, alpha) ?? throw new InvalidOperationException(Lang.T("Empty"));
        var crop = new CroppedBitmap(full, bb);
        crop.Freeze();
        s.PxW = bb.Width;
        s.Preview = Proxy(crop);
        if (pdf != null && s.Pdf == null) { s.Raster = Png(crop); media = new Rect(bb.X, bb.Y, bb.Width, bb.Height); } // PDF tak terbaca vektor: tempel render-nya

        if (widthMm <= 0) widthMm = Math.Round(bb.Width * nat, 1);
        double k = widthMm / bb.Width;           // mm per px
        s.WidthMm = s.LoadedW = widthMm;
        s.LoadedKiss = kiss;
        s.LoadedDie = die;
        s.LoadedBleed = bleed;
        bool bled = bleed > 0 && s.Pdf == null; // bleed hanya untuk raster (PNG/JPG/SVG/hasil Rapikan)
        s.ArtW = widthMm;
        s.ArtH = bb.Height * k;

        var small = new TransformedBitmap(crop, new ScaleTransform(s.ArtW * PxMm / bb.Width, s.ArtH * PxMm / bb.Height));
        var mask = Silhouette(small, alpha, out int mw, out int mh);
        static int Reach(Cut c) => c.On ? (int)Math.Ceiling((c.Off + c.Smooth) * PxMm) : 0;
        int pad = Math.Max(Reach(kiss), Reach(die)) + (bled ? (int)Math.Ceiling(bleed * PxMm) : 0) + 3, W = mw + 2 * pad, H = mh + 2 * pad;
        var m = new bool[W, H];
        for (int y = 0; y < mh; y++) for (int x = 0; x < mw; x++) m[x + pad, y + pad] = mask[x, y];

        bool[,]? outer = null, kissMask = null;
        if (kiss.On) { outer = kissMask = Solid(Smooth(Close(m, kiss), m, kiss)); s.Kiss = Trace(outer); }
        if (die.On)
        {
            outer = Smooth(Close(m, die), m, die);
            // Die selalu membungkus kiss, minimal 1 px di luarnya: bila sama persis, kurva Potrace keduanya bisa bersilangan.
            if (kissMask != null) { var near = Dilate(kissMask, 1); foreach (var (x, y) in Cells(outer)) outer[x, y] |= near[x, y]; }
            outer = Solid(outer);
            s.Die = Trace(outer);
        }
        if (outer == null) throw new InvalidOperationException(Lang.T("NeedCut"));

        s.Off = pad / PxMm;
        s.W = Math.Ceiling(W / PxMm / Nest.Cell) * Nest.Cell;
        s.H = Math.Ceiling(H / PxMm / Nest.Cell) * Nest.Cell;
        int cp = (int)Math.Round(Nest.Cell * PxMm), n = 0;
        s.Grid = new bool[(int)Math.Round(s.W / Nest.Cell), (int)Math.Round(s.H / Nest.Cell)];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) if (outer[x, y]) { s.Grid[x / cp, y / cp] = true; n++; }
        foreach (bool c in s.Grid) if (c) s.Area++;
        s.AreaMm2 = n / (PxMm * PxMm);
        s.Full = new Rect(s.Off + (media.X - bb.X) * k, s.Off + (media.Y - bb.Y) * k, media.Width * k, media.Height * k);
        if (bled)
        {
            var b = kiss.On ? kiss : die; // bleed diukur dari garis kiss; tanpa kiss dari garis die
            (s.BleedPng, s.BleedView, s.BleedRect) = Bleed(crop, alpha, s.ArtW, s.ArtH, b.Off + b.Smooth + bleed + 1, s.Off);
        }
        return s;
    }

    const double BleedPxMm = 300 / 25.4; // resolusi maksimal gambar bleed: 300 dpi
    const int BleedChoke = 2;            // px tepi luar JPG yang diganti warna dari dalam (buang tepi pucat)

    // Erosi 1 px (8 arah); tepi gambar dianggap latar.
    static bool[,] Shrink(bool[,] m)
    {
        int w = m.GetLength(0), h = m.GetLength(1);
        var r = new bool[w, h];
        for (int y = 1; y < h - 1; y++)
            for (int x = 1; x < w - 1; x++)
                r[x, y] = m[x, y] && m[x - 1, y - 1] && m[x, y - 1] && m[x + 1, y - 1] && m[x - 1, y] && m[x + 1, y]
                    && m[x - 1, y + 1] && m[x, y + 1] && m[x + 1, y + 1];
        return r;
    }

    static int Count(bool[,] m) { int n = 0; foreach (bool v in m) if (v) n++; return n; }

    // Auto-bleed untuk raster: warna tepi artwork diperluas keluar (dilasi warna, 8 arah) sejauh d mm, supaya
    // pergeseran pisau tidak menyisakan tepi putih. Lubang di dalam artwork tetap warna aslinya. Saat digambar,
    // hasilnya di-clip ke garis potong + bleed (Sticker.ArtClip).
    // ponytail: gambar bleed dibuat ulang penuh (maks 300 dpi); stiker raster ±300 mm jadi ±1-2 detik lebih lambat.
    // Ganti dengan lapisan bawah beresolusi rendah bila operator merasakan lambat.
    static (byte[] png, BitmapSource view, Rect rect) Bleed(BitmapSource crop, bool alpha, double artW, double artH, double d, double off)
    {
        double k = Math.Min(crop.PixelWidth / artW, BleedPxMm);
        var src = new TransformedBitmap(crop, new ScaleTransform(artW * k / crop.PixelWidth, artH * k / crop.PixelHeight));
        var fg = Silhouette(src, alpha, out int w, out int h);
        var (px, _, _) = Pixels(src);
        // JPG berlatar putih: tepi luar artwork berupa campuran warna + putih (pucat). Tepi itu diganti warna dari
        // dalam supaya bleed tidak bergaris pucat. PNG transparan tidak perlu: warna tepinya asli.
        var core = fg;
        if (!alpha)
            for (int pass = 0; pass < BleedChoke; pass++) { var c = Shrink(core); if (Count(c) == 0) break; core = c; }
        double kx = w / artW, ky = h / artH;
        int p = (int)Math.Ceiling(d * Math.Max(kx, ky)), W = w + 2 * p, H = h + 2 * p;
        var o = new byte[W * H * 4];
        var front = new List<int>();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!core[x, y]) continue;
                int i = (y + p) * W + x + p, j = (y * w + x) * 4;
                o[i * 4] = px[j]; o[i * 4 + 1] = px[j + 1]; o[i * 4 + 2] = px[j + 2]; o[i * 4 + 3] = 255;
                bool edge = x == 0 || y == 0 || x == w - 1 || y == h - 1 || !core[x - 1, y] || !core[x + 1, y] || !core[x, y - 1] || !core[x, y + 1];
                if (edge) front.Add(i);
            }
        // Lapis demi lapis keluar: piksel baru = rata-rata tetangga yang sudah berwarna (8 arah), jadi satu piksel
        // tepi yang menyimpang tidak membentuk garis panjang.
        for (int layer = 0; layer < p + BleedChoke && front.Count > 0; layer++)
        {
            var next = new List<int>();
            foreach (int i in front)
                for (int v = Math.Max(0, i / W - 1); v <= Math.Min(H - 1, i / W + 1); v++)
                    for (int u = Math.Max(0, i % W - 1); u <= Math.Min(W - 1, i % W + 1); u++)
                        if (o[(v * W + u) * 4 + 3] == 0) { o[(v * W + u) * 4 + 3] = 1; next.Add(v * W + u); } // 1 = antre
            foreach (int j in next)
            {
                int b = 0, g = 0, r = 0, n = 0;
                for (int v = Math.Max(0, j / W - 1); v <= Math.Min(H - 1, j / W + 1); v++)
                    for (int u = Math.Max(0, j % W - 1); u <= Math.Min(W - 1, j % W + 1); u++)
                    {
                        int t = (v * W + u) * 4;
                        if (o[t + 3] == 255) { b += o[t]; g += o[t + 1]; r += o[t + 2]; n++; }
                    }
                o[j * 4] = (byte)(b / n); o[j * 4 + 1] = (byte)(g / n); o[j * 4 + 2] = (byte)(r / n);
            }
            foreach (int j in next) o[j * 4 + 3] = 255;
            front = next;
        }
        var bmp = BitmapSource.Create(W, H, 96, 96, PixelFormats.Bgra32, null, o, W * 4);
        bmp.Freeze();
        return (Png(bmp), Proxy(bmp), new Rect(off - p / kx, off - p / ky, W / kx, H / ky));
    }

    // PDFium merender CropBox; PDFsharp menempel MediaBox. Kembalikan posisi MediaBox relatif terhadap render.
    static (BitmapSource bmp, double nat, Rect media, bool vector) RenderPdf(string pdf, int page)
    {
        BitmapSource bmp;
        double pxMm, scale;
        using (var r = new Pdfium(pdf))
        {
            var (wPt, hPt) = r.SizePt(page);
            pxMm = Math.Min(ViewPxMm, 4000 / (Math.Max(wPt, hPt) / Pt)); // batas 4000 px agar memori tetap kecil
            scale = pxMm / Pt;
            bmp = r.Render(page, scale);
        }
        try
        {
            using var doc = PdfReader.Open(pdf, PdfDocumentOpenMode.Import);
            var pg = doc.Pages[page];
            if (pg.Rotate % 360 == 0)
            {
                // Tempel seperti Export.Pdf. PDF yang bisa dibuka tetapi gagal ditempel (mis. terenkripsi, D8) jadi gambar.
                using (var form = PdfSharp.Drawing.XPdfForm.FromFile(pdf))
                using (var probe = new PdfSharp.Pdf.PdfDocument())
                using (var g = PdfSharp.Drawing.XGraphics.FromPdfPage(probe.AddPage()))
                {
                    form.PageIndex = page;
                    g.DrawImage(form, 0, 0, 1, 1);
                }
                var mb = pg.MediaBox;
                var cb = pg.EffectiveCropBoxReadOnly;
                return (bmp, 1 / pxMm, new Rect((mb.X1 - cb.X1) * scale, (cb.Y2 - mb.Y2) * scale, mb.Width * scale, mb.Height * scale), true);
            }
        }
        catch (Exception) { } // terenkripsi / tidak terbaca PDFsharp: tempel sebagai gambar
        return (bmp, 1 / pxMm, default, false);
    }

    // SVG jarang dipakai: dirender ±300 dpi sesuai lebar target, lalu diperlakukan seperti PNG.
    static BitmapSource RenderSvg(string path, double widthMm)
    {
        var d = new FileSvgReader(new WpfDrawingSettings { IncludeRuntime = false, TextAsGeometry = true }).Read(path);
        var b = d.Bounds;
        double k = Math.Min(widthMm * 12 / b.Width, 8000 / Math.Max(b.Width, b.Height));
        var v = new DrawingVisual();
        using (var dc = v.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(k, k));
            dc.PushTransform(new TranslateTransform(-b.X, -b.Y));
            dc.DrawDrawing(d);
        }
        var rt = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(b.Width * k)), Math.Max(1, (int)Math.Ceiling(b.Height * k)), 96, 96, PixelFormats.Pbgra32);
        rt.Render(v);
        rt.Freeze();
        return rt;
    }

    // Layar memakai salinan kecil (sisi panjang maks 1600 px): RAM dan render tetap ringan untuk gambar besar.
    // Export dan garis potong tetap memakai resolusi asli.
    const int ViewMax = 1600;

    // Selalu salinan piksel mandiri. WriteableBitmap(TransformedBitmap) dan CroppedBitmap ikut memegang bitmap sumber
    // resolusi penuh (JPG 24 MP: ±96 MB native per desain, temuan D4).
    static BitmapSource Proxy(BitmapSource b)
    {
        double k = Math.Min(1, (double)ViewMax / Math.Max(b.PixelWidth, b.PixelHeight));
        var t = new FormatConvertedBitmap(new TransformedBitmap(b, new ScaleTransform(k, k)), PixelFormats.Pbgra32, null, 0);
        var px = new byte[t.PixelWidth * t.PixelHeight * 4];
        t.CopyPixels(px, t.PixelWidth * 4, 0);
        var copy = BitmapSource.Create(t.PixelWidth, t.PixelHeight, t.DpiX, t.DpiY, PixelFormats.Pbgra32, null, px, t.PixelWidth * 4);
        copy.Freeze();
        return copy;
    }

    public static byte[] Png(BitmapSource b)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(b));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }

    static (byte[] px, int w, int h) Pixels(BitmapSource b)
    {
        var f = new FormatConvertedBitmap(b, PixelFormats.Bgra32, null, 0);
        var px = new byte[f.PixelWidth * f.PixelHeight * 4];
        f.CopyPixels(px, f.PixelWidth * 4, 0);
        return (px, f.PixelWidth, f.PixelHeight);
    }

    // Foreground = alpha (jika ada transparansi) atau "bukan putih" (< 240, toleransi noise JPG).
    static bool Fg(byte[] px, int i, bool alpha) =>
        alpha ? px[i * 4 + 3] > 16 : px[i * 4] < 240 || px[i * 4 + 1] < 240 || px[i * 4 + 2] < 240;

    // Latar putih = minimal separuh piksel tepi gambar putih (R, G, B >= 250).
    static bool WhiteBorder(byte[] px, int w, int h)
    {
        int white = 0, n = 0;
        void At(int x, int y) { int i = (y * w + x) * 4; n++; if (px[i] >= 250 && px[i + 1] >= 250 && px[i + 2] >= 250) white++; }
        for (int x = 0; x < w; x++) { At(x, 0); At(x, h - 1); }
        for (int y = 1; y < h - 1; y++) { At(0, y); At(w - 1, y); }
        return 2 * white >= n;
    }

    static Int32Rect? ContentBox(byte[] px, int w, int h, bool alpha)
    {
        int x0 = w, y0 = h, x1 = -1, y1 = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (Fg(px, y * w + x, alpha)) { x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y); }
        return x1 < 0 ? null : new Int32Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }

    static bool[,] Silhouette(BitmapSource b, bool alpha, out int w, out int h)
    {
        var (px, W, H) = Pixels(b);
        var m = new bool[W, H];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) m[x, y] = Fg(px, y * W + x, alpha);
        w = W; h = H;
        return Solid(m);
    }

    // Lubang (latar yang tidak terhubung ke tepi) diisi: hanya kontur luar yang dipotong.
    // Berlaku juga untuk lubang baru yang terbentuk saat offset menutup celah artwork (huruf C besar, teks melingkar).
    static bool[,] Solid(bool[,] m)
    {
        int W = m.GetLength(0), H = m.GetLength(1);
        var bg = new bool[W, H];
        var st = new Stack<(int, int)>();
        void Push(int x, int y) { if (x >= 0 && y >= 0 && x < W && y < H && !bg[x, y] && !m[x, y]) { bg[x, y] = true; st.Push((x, y)); } }
        for (int x = 0; x < W; x++) { Push(x, 0); Push(x, H - 1); }
        for (int y = 0; y < H; y++) { Push(0, y); Push(W - 1, y); }
        while (st.Count > 0) { var (x, y) = st.Pop(); Push(x - 1, y); Push(x + 1, y); Push(x, y - 1); Push(x, y + 1); }
        for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) bg[x, y] = !bg[x, y];
        return bg;
    }

    // Offset + penghalus: dilasi (offset + halus), lalu erosi (halus) = closing yang menutup lekukan sempit.
    static bool[,] Close(bool[,] m, Cut c)
    {
        int r = (int)Math.Round(c.Off * PxMm), sm = (int)Math.Round(c.Smooth * PxMm);
        var d = Dilate(m, r + sm);
        return sm > 0 ? Not(Dilate(Not(d), sm)) : d;
    }

    // Haluskan kontur: blur Gaussian (sigma = Halus) lalu threshold 0,5. Lekukan kecil hilang, node berkurang,
    // pisau bergerak mulus. Hasil di-OR dengan artwork + (offset - 0,3 mm) supaya garis tidak mendekati artwork.
    // Jari-jari penjaga dibulatkan ke atas: dibulatkan ke bawah, penjaga 0,03 mm lebih dekat dari aturan.
    static bool[,] Smooth(bool[,] m, bool[,] art, Cut c)
    {
        double s = c.Smooth * PxMm;
        if (s <= 0) return m;
        int W = m.GetLength(0), H = m.GetLength(1);
        var a = new double[W * H]; // urutan [x * H + y], sama seperti bool[,]
        for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) if (m[x, y]) a[x * H + y] = 1;
        // Gaussian didekati 3 lintasan box blur (O(1) per piksel): varians 3 * (w^2 - 1) / 12 = s^2.
        int r = Math.Max(1, (int)Math.Round((Math.Sqrt(4 * s * s + 1) - 1) / 2));
        var b = new double[a.Length];
        for (int pass = 0; pass < 3; pass++)
        {
            Box(a, b, W, H, r, H, 1);   // sepanjang y
            Box(b, a, H, W, r, 1, H);   // sepanjang x
        }
        var res = Dilate(art, (int)Math.Ceiling((c.Off - MaxInward) * PxMm - 1e-9));
        for (int x = 0; x < W; x++) for (int y = 0; y < H; y++) if (a[x * H + y] >= 0.5) res[x, y] = true;
        return res;
    }

    // Rata-rata berjalan lebar 2r+1 untuk `lines` baris berisi `len` sel; indeks = baris * rowStep + i * step.
    // Di luar gambar dihitung 0, sama seperti mask kosong.
    static void Box(double[] src, double[] dst, int lines, int len, int r, int rowStep, int step)
    {
        double k = 1.0 / (2 * r + 1);
        for (int l = 0; l < lines; l++)
        {
            int o = l * rowStep;
            double sum = 0;
            for (int i = 0; i <= Math.Min(r, len - 1); i++) sum += src[o + i * step];
            for (int i = 0; i < len; i++)
            {
                dst[o + i * step] = sum * k;
                if (i + r + 1 < len) sum += src[o + (i + r + 1) * step];
                if (i - r >= 0) sum -= src[o + (i - r) * step];
            }
        }
    }

    static IEnumerable<(int, int)> Cells(bool[,] m)
    {
        for (int x = 0; x < m.GetLength(0); x++) for (int y = 0; y < m.GetLength(1); y++) yield return (x, y);
    }

    static bool[,] Not(bool[,] m)
    {
        var n = new bool[m.GetLength(0), m.GetLength(1)];
        for (int x = 0; x < m.GetLength(0); x++) for (int y = 0; y < m.GetLength(1); y++) n[x, y] = !m[x, y];
        return n;
    }

    static bool[,] Dilate(bool[,] m, int r)
    {
        int W = m.GetLength(0), H = m.GetLength(1);
        var d = (bool[,])m.Clone();
        if (r <= 0) return d;
        var disc = new List<(int, int)>();
        for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++) if (dx * dx + dy * dy <= r * r) disc.Add((dx, dy));
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                if (!m[x, y]) continue;
                bool edge = x == 0 || y == 0 || x == W - 1 || y == H - 1 || !m[x - 1, y] || !m[x + 1, y] || !m[x, y - 1] || !m[x, y + 1];
                if (!edge) continue;
                foreach (var (dx, dy) in disc)
                {
                    int u = x + dx, v = y + dy;
                    if (u >= 0 && v >= 0 && u < W && v < H) d[u, v] = true;
                }
            }
        return d;
    }

    static string Trace(bool[,] m)
    {
        int W = m.GetLength(0), H = m.GetLength(1);
        using var pb = PotraceBitmap.Create(W, H);
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) if (m[x, y]) pb.SetBlack(x, y);
        var st = Potrace.Trace(new PotraceParam { AlphaMax = AlphaMax, OptTolerance = OptTolerance }, pb);
        var sb = new StringBuilder();
        string P(PotraceDPoint p) => $"{F(p.X / PxMm)} {F(p.Y / PxMm)}";
        for (var p = st.Plist; p != null; p = p.Next)
        {
            var c = p.Curve;
            sb.Append('M').Append(P(c.C[c.N - 1][2]));
            for (int i = 0; i < c.N; i++)
            {
                var a = c.C[i];
                if (c.Tag[i] == PotraceCurve.PotraceCurveTo) sb.Append('C').Append(P(a[0])).Append(' ').Append(P(a[1])).Append(' ').Append(P(a[2]));
                else sb.Append('L').Append(P(a[1])).Append('L').Append(P(a[2]));
            }
            sb.Append('Z');
        }
        return sb.ToString();
    }

    static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
}

// CDR dibuka lewat CorelDRAW yang terpasang di PC operator, lalu di-publish ke PDF.
// COM berjalan di proses worker (exe ini dengan --cdr2pdf). Bila CorelDRAW menggantung (misalnya ada dialog),
// worker dihentikan setelah batas waktu dan Printech tetap jalan. CorelDRAW sendiri tidak dimatikan,
// jadi pekerjaan operator yang belum disimpan tetap aman.
// ponytail: belum diuji dengan CorelDRAW asli (tidak ada di PC pengembangan); batas 30 detik dikalibrasi di PC operator.
public static class Corel
{
    public const int TimeoutMs = 30_000;
    static readonly ConcurrentDictionary<string, (string Pdf, (DateTime, long) Stamp)> cache = new();

    public static string ToPdf(string cdr, int timeoutMs = TimeoutMs)
    {
        var v = Loader.Stamp(cdr); // CDR yang diedit lalu ditambah ulang dikonversi lagi
        if (cache.TryGetValue(cdr, out var done) && done.Stamp == v && File.Exists(done.Pdf)) return done.Pdf;
        var pdf = Path.Combine(App.Temp, Guid.NewGuid() + ".pdf");
        Directory.CreateDirectory(App.Temp);
        var psi = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true };
        foreach (var a in new[] { "--cdr2pdf", cdr, pdf }) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var err = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(timeoutMs))
        {
            p.Kill();
            p.WaitForExit();
            throw new TimeoutException(Lang.T("CorelTimeout", timeoutMs / 1000));
        }
        return p.ExitCode switch
        {
            0 when File.Exists(pdf) => (cache[cdr] = (pdf, v)).Pdf,
            2 => throw new InvalidOperationException(Lang.T("NoCorel")),
            _ => throw new InvalidOperationException(Lang.T("CorelFail", err.Result.Trim())),
        };
    }

    // Dijalankan di proses worker (main thread STA). Exit code: 0 = berhasil, 2 = CorelDRAW tidak terpasang,
    // 1 = error lain (pesan di stderr).
    public static int Worker(string cdr, string pdf)
    {
        var t = Type.GetTypeFromProgID("CorelDRAW.Application");
        if (t == null) return 2;
        try
        {
            dynamic app = Activator.CreateInstance(t)!;
            dynamic doc = app.OpenDocument(cdr);
            try { doc.PublishToPDF(pdf); }
            finally { doc.Close(); }
            return 0;
        }
        catch (Exception ex) { Console.Error.Write(ex.Message); return 1; }
    }
}

// Pemakaian bahan dihitung terhadap area yang bisa diisi: kertas dikurangi margin tepi.
public static class Yield
{
    public static double Fillable(double w, double h, double margin) => Math.Max(0, w - 2 * margin) * Math.Max(0, h - 2 * margin);

    public static double Use(IEnumerable<Placed> ps, double w, double h, double margin)
    {
        double f = Fillable(w, h, margin);
        return f > 0 ? ps.Sum(p => p.S.AreaMm2) / f : 0;
    }

    // Reguler = bentuk rata-rata (ditimbang luas) mengisi >= 75% kotaknya (persegi, lingkaran); selain itu irregular.
    public static bool Regular(IEnumerable<Placed> ps)
    {
        double a = 0, f = 0;
        foreach (var p in ps) { a += p.S.AreaMm2; f += p.S.AreaMm2 * p.S.Fill; }
        return a == 0 || f / a >= 0.75;
    }

    // Target spesifikasi: reguler 70-85% (hijau >= 75%), irregular 55-70%. Batas hijau irregular tidak disebut di
    // spesifikasi; dipakai aturan yang sama dengan reguler: batas bawah + 5 poin (60%).
    public static (double Low, double Green, double High) Target(bool regular) => regular ? (0.70, 0.75, 0.85) : (0.55, 0.60, 0.70);

    // 2 = hijau (mencapai target), 1 = kuning (dalam rentang target), 0 = merah (di bawah target).
    public static int Level(double use, bool regular)
    {
        var t = Target(regular);
        return use >= t.Green ? 2 : use >= t.Low ? 1 : 0;
    }
}

// Preset per jenis bahan. Nilai awal dari panduan cetak stiker publik (lihat RESEARCH.md bagian 13);
// belum dikonfirmasi operator AP 55. Margin 10 mm = ruang reg mark Cutting Master 5.
public static class Presets
{
    public static readonly Dictionary<string, (Cut Kiss, Cut Die, double Margin, double Gap)> Material = new()
    {
        ["Vinyl kiss-cut"] = (new(true, 2, 1), new(false, 4, 2), 10, 2),
        ["Chromo die-cut"] = (new(false, 2, 1), new(true, 3, 1), 10, 3),
        ["Akrilik/tebal"] = (new(false, 2, 1), new(true, 3, 3), 10, 4),
    };
}

public static class Paper
{
    public static readonly Dictionary<string, (double W, double H)> Sizes = new()
    {
        ["A0"] = (841, 1189), ["A1"] = (594, 841), ["A2"] = (420, 594), ["A3"] = (297, 420), ["A3+"] = (320, 480), ["A4"] = (210, 297),
        ["A5"] = (148, 210), ["A6"] = (105, 148), ["SRA3"] = (320, 450), ["SRA4"] = (225, 320),
    };
}

public static class Nest
{
    // mm per sel grid nesting. 1 mm: jarak nyata antar stiker lebih dekat ke jarak yang diminta (median ±3,2 mm untuk
    // jarak 2 mm, sel 2 mm ±4,1 mm), susun ±2× lebih lama. Diukur 2026-10-17.
    public static double Cell = 1;
    static readonly int[] Rots = [0, 90, 180, 270];

    // Jejak garis potong terluar di grid: sel terisi, sel tepi, dan batasnya. Bleed (dalam sel) menjaga warna tidak
    // masuk margin (ruang reg mark). Margin dicek terhadap jejak ini, bukan kotak stiker.
    sealed record Foot(List<(int X, int Y)> Cells, List<(int X, int Y)> Edge, int MinX, int MinY, int MaxX, int MaxY, int Bleed);

    static Foot FootOf(Sticker s, int rot)
    {
        var g = Rotate(s.Grid, rot);
        int w = g.GetLength(0), h = g.GetLength(1), x0 = int.MaxValue, y0 = int.MaxValue, x1 = 0, y1 = 0;
        List<(int, int)> inner = [], edge = [];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!g[x, y]) continue;
                bool e = x == 0 || y == 0 || x == w - 1 || y == h - 1 || !g[x - 1, y] || !g[x + 1, y] || !g[x, y - 1] || !g[x, y + 1];
                (e ? edge : inner).Add((x, y));
                x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x); y1 = Math.Max(y1, y);
            }
        int bleed = s.BleedPng == null ? 0 : (int)Math.Ceiling(s.LoadedBleed / Cell - 1e-9);
        // Sel tepi dicek dulu: tabrakan hampir selalu terjadi di tepi, jadi posisi yang salah cepat ditolak.
        return new([.. edge, .. inner], edge, x0, y0, x1, y1, bleed);
    }

    // Tandai sel stiker sebagai terisi, ditambah jarak antar stiker (gc sel) di sekeliling tepinya.
    // Jejak selalu padat (lubang diisi), jadi cukup tepinya yang diperlebar.
    static void Stamp(bool[,] occ, Foot f, int ox, int oy, int gc)
    {
        int cols = occ.GetLength(0), rows = occ.GetLength(1);
        foreach (var (cx, cy) in f.Cells)
            if (ox + cx >= 0 && oy + cy >= 0 && ox + cx < cols && oy + cy < rows) occ[ox + cx, oy + cy] = true;
        foreach (var (cx, cy) in f.Edge)
            for (int dy = -gc; dy <= gc; dy++)
                for (int dx = -gc; dx <= gc; dx++)
                {
                    int x = ox + cx + dx, y = oy + cy + dy;
                    if (x >= 0 && y >= 0 && x < cols && y < rows) occ[x, y] = true;
                }
    }

    public static bool[,] Rotate(bool[,] g, int rot)
    {
        if (rot == 0) return g;
        int w = g.GetLength(0), h = g.GetLength(1);
        var r = rot == 180 ? new bool[w, h] : new bool[h, w];
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (g[x, y])
                    switch (rot)
                    {
                        case 90: r[h - 1 - y, x] = true; break;
                        case 180: r[w - 1 - x, h - 1 - y] = true; break;
                        default: r[y, w - 1 - x] = true; break;
                    }
        return r;
    }

    // ponytail: greedy first-fit di grid sel, 4 rotasi, lintas lembar, 3 urutan dicoba. Bukan optimal;
    // ganti dengan NFP/SVGnest bila pemakaian bahan kurang. Masih di UI thread (±50 ms untuk 100 stiker);
    // pindah ke background bila operator merasakan jeda.
    public static List<List<Placed>> Run(IEnumerable<Sticker> stickers, double cw, double ch, double margin, double gap, out int tooBig)
    {
        var items = stickers.SelectMany(s => Enumerable.Repeat(s, Math.Max(0, s.Copies))).ToList();
        Func<Sticker, double>[] keys = [s => s.Area, s => Math.Max(s.W, s.H), s => s.W + s.H];
        // 3 percobaan dihitung paralel (kerja CPU). Once hanya membaca Sticker dan memakai cache sendiri.
        var trials = new (List<List<Placed>> r, int big)[keys.Length];
        Parallel.For(0, keys.Length, i => trials[i] = (Once(items.OrderByDescending(keys[i]).ToList(), cw, ch, margin, gap, out int big), big));
        // Dipilih menurut urutan percobaan, bukan urutan selesai: hasil sama setiap kali.
        List<List<Placed>> best = [];
        double bestScore = double.MaxValue;
        tooBig = 0;
        foreach (var (r, big) in trials)
        {
            double score = r.Count * 1e6 + (r.Count == 0 ? 0 : r[^1].Max(p => p.Y + p.BH));
            if (score < bestScore) { best = r; bestScore = score; tooBig = big; }
        }
        return best;
    }

    static List<List<Placed>> Once(List<Sticker> items, double cw, double ch, double margin, double gap, out int tooBig)
    {
        int cols = Math.Max(0, (int)((cw - 2 * margin) / Cell)), rows = Math.Max(0, (int)((ch - 2 * margin) / Cell));
        int gc = (int)Math.Ceiling(gap / Cell - 1e-9);
        var sheets = new List<(bool[,] occ, List<Placed> ps, HashSet<Sticker> full)>();
        var cache = new Dictionary<(Sticker, int), Foot>();
        tooBig = 0;
        foreach (var s in items)
        {
            bool done = false;
            foreach (var sh in sheets)
            {
                if (sh.full.Contains(s)) continue; // salinan sebelumnya sudah tidak muat di lembar ini
                if (TryPlace(sh.occ, s, cache, gc, margin, sh.ps)) { done = true; break; }
                sh.full.Add(s);
            }
            if (done) continue;
            var occ = new bool[cols, rows];
            var ps = new List<Placed>();
            if (TryPlace(occ, s, cache, gc, margin, ps)) sheets.Add((occ, ps, []));
            else tooBig++;
        }
        return sheets.Select(x => x.ps).ToList();
    }

    static bool TryPlace(bool[,] occ, Sticker s, Dictionary<(Sticker, int), Foot> cache, int gc, double margin, List<Placed> ps)
    {
        int cols = occ.GetLength(0), rows = occ.GetLength(1);
        (int x, int y, int rot, int bottom)? best = null;
        foreach (int rot in Rots)
        {
            if (!cache.TryGetValue((s, rot), out var f)) cache[(s, rot)] = f = FootOf(s, rot);
            // Posisi dibatasi jejak garis potong (bukan kotak stiker) di dalam area cetak.
            for (int y = f.Bleed - f.MinY; y + f.MaxY < rows - f.Bleed; y++)
            {
                if (best is { } b && y + f.MaxY >= b.bottom) break;
                for (int x = f.Bleed - f.MinX; x + f.MaxX < cols - f.Bleed; x++)
                    if (Fits(occ, f.Cells, x, y)) { best = (x, y, rot, y + f.MaxY); goto next; }
            }
        next:;
        }
        if (best is not { } p) return false;
        Stamp(occ, cache[(s, p.rot)], p.x, p.y, gc);
        ps.Add(new Placed { S = s, Rot = p.rot, X = margin + p.x * Cell, Y = margin + p.y * Cell });
        return true;
    }

    // Snap magnet: pindahkan p ke posisi grid terdekat yang tidak menabrak stiker lain (jarak antar stiker tetap
    // dijaga) dan tetap di dalam margin. Memakai grid hunian yang sama dengan susun otomatis. false = tidak ada tempat.
    public static bool Snap(List<Placed> ps, Placed p, double cw, double ch, double margin, double gap)
    {
        int cols = Math.Max(0, (int)((cw - 2 * margin) / Cell)), rows = Math.Max(0, (int)((ch - 2 * margin) / Cell));
        int gc = (int)Math.Ceiling(gap / Cell - 1e-9);
        var occ = new bool[cols, rows];
        foreach (var q in ps)
            if (q != p) Stamp(occ, FootOf(q.S, q.Rot), (int)Math.Round((q.X - margin) / Cell), (int)Math.Round((q.Y - margin) / Cell), gc);
        var f = FootOf(p.S, p.Rot);
        double px = (p.X - margin) / Cell, py = (p.Y - margin) / Cell, best = double.MaxValue;
        (int x, int y)? at = null;
        for (int y = f.Bleed - f.MinY; y + f.MaxY < rows - f.Bleed; y++)
            for (int x = f.Bleed - f.MinX; x + f.MaxX < cols - f.Bleed; x++)
            {
                double d = (x - px) * (x - px) + (y - py) * (y - py);
                if (d < best && Fits(occ, f.Cells, x, y)) { best = d; at = (x, y); }
            }
        if (at is not { } a) return false;
        p.X = margin + a.x * Cell;
        p.Y = margin + a.y * Cell;
        return true;
    }

    // Tinggi lembar setelah dipadatkan: jejak garis potong terbawah (bleed ikut) + margin, tidak pernah lebih
    // tinggi dari lembar sekarang. Memakai jejak, bukan kotak: kotak boleh masuk margin sejak margin dicek per jejak.
    public static double Compact(List<Placed> ps, double ch, double margin) =>
        ps.Count == 0 ? ch : Math.Min(ch, Math.Ceiling(ps.Max(p => { var f = FootOf(p.S, p.Rot); return p.Y + (f.MaxY + 1 + f.Bleed) * Cell; }) + margin));

    static bool Fits(bool[,] occ, List<(int X, int Y)> cells, int x, int y)
    {
        foreach (var (cx, cy) in cells) if (occ[x + cx, y + cy]) return false;
        return true;
    }

    // Tandai stiker yang tumpang tindih (jejak garis terluar) atau yang jejaknya (ditambah bleed) keluar margin.
    // Kembalikan jumlahnya.
    public static int Check(List<Placed> ps, double cw, double ch, double margin)
    {
        var feet = ps.Select(p => FootOf(p.S, p.Rot)).ToList();
        for (int i = 0; i < ps.Count; i++)
        {
            var (p, f) = (ps[i], feet[i]);
            double m = margin + f.Bleed * Cell - 0.01;
            p.Bad = p.X + f.MinX * Cell < m || p.Y + f.MinY * Cell < m || p.X + (f.MaxX + 1) * Cell > cw - m || p.Y + (f.MaxY + 1) * Cell > ch - m;
        }
        var owner = new Dictionary<(int, int), Placed>();
        for (int i = 0; i < ps.Count; i++)
        {
            var p = ps[i];
            int ox = (int)Math.Round(p.X / Cell), oy = (int)Math.Round(p.Y / Cell);
            foreach (var (x, y) in feet[i].Cells)
            {
                if (owner.TryGetValue((ox + x, oy + y), out var o) && o != p) o.Bad = p.Bad = true;
                else owner[(ox + x, oy + y)] = p;
            }
        }
        return ps.Count(p => p.Bad);
    }

    public static Transform Xf(Placed p)
    {
        var (tx, ty) = p.T;
        return new TransformGroup { Children = { new RotateTransform(p.Rot), new TranslateTransform(p.X + tx, p.Y + ty) } };
    }

    static readonly Brush KissBrush = Frozen("#EC008C"), DieBrush = Frozen("#00AEEF"), SelBrush = Frozen("#4A90FF");
    static Brush Frozen(string c) { var b = (Brush)new BrushConverter().ConvertFromString(c)!; b.Freeze(); return b; }

    // Satuan kanvas = mm. Tag kanvas = indeks lembar, Tag stiker = Placed.
    public static Canvas Sheet(IEnumerable<Placed> ps, double cw, double ch, double margin, Placed? sel, int index)
    {
        var root = new Canvas { Width = cw, Height = ch, Background = Brushes.White, ClipToBounds = true, Tag = index };
        root.Children.Add(new System.Windows.Shapes.Rectangle
        {
            Width = Math.Max(0, cw - 2 * margin), Height = Math.Max(0, ch - 2 * margin), Stroke = Brushes.LightGray, StrokeThickness = 0.3,
            StrokeDashArray = [4, 4], Margin = new Thickness(margin, margin, 0, 0), IsHitTestVisible = false
        });
        foreach (var p in ps)
        {
            var s = p.S;
            var c = new Canvas { Width = s.W, Height = s.H, Background = Brushes.Transparent, Tag = p, RenderTransform = Xf(p) };
            // artwork di-clip ke garis potongnya sendiri agar tidak menimpa stiker tetangga
            var art = new Canvas { Width = s.W, Height = s.H, Clip = s.ArtClip, IsHitTestVisible = false };
            var (src, r) = s.BleedView is { } bv ? (bv, s.BleedRect) : (s.Preview, new Rect(s.Off, s.Off, s.ArtW, s.ArtH));
            var img = new Image { Source = src, Width = r.Width, Height = r.Height, Stretch = Stretch.Fill };
            Canvas.SetLeft(img, r.X);
            Canvas.SetTop(img, r.Y);
            art.Children.Add(img);
            c.Children.Add(art);
            if (s.KissGeo is { } k) c.Children.Add(new System.Windows.Shapes.Path { Data = k, Stroke = KissBrush, StrokeThickness = 0.4, IsHitTestVisible = false });
            if (s.DieGeo is { } d) c.Children.Add(new System.Windows.Shapes.Path { Data = d, Stroke = DieBrush, StrokeThickness = 0.4, IsHitTestVisible = false });
            if (p.Bad || p == sel)
                c.Children.Add(new System.Windows.Shapes.Rectangle
                {
                    Width = s.W, Height = s.H, Stroke = p.Bad ? Brushes.Red : SelBrush, StrokeThickness = p.Bad ? 0.8 : 0.6,
                    StrokeDashArray = p.Bad ? null : [3, 2], IsHitTestVisible = false
                });
            root.Children.Add(c);
        }
        return root;
    }
}
