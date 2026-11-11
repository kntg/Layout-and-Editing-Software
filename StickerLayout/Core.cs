using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Xml;
using System.Xml.Linq;
using BitmapToVector;
using PdfSharp.Pdf.IO;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace StickerLayout;

// Satu garis potong: aktif, jarak dari artwork, dan radius penghalus lekukan (mm).
public record Cut(bool On, double Off, double Smooth)
{
    // Aturan pasangan kiss dan die, untuk panel Garis potong dan opsi per desain: kunci pesan (Lang.T), atau null bila boleh dipakai.
    public static string? Error(Cut kiss, Cut die) => !kiss.On && !die.On ? "NeedCut" : kiss.On && die.On && die.Off <= kiss.Off ? "DieInside" : null;
}

// Angka milimeter di kotak isian: koma dan titik sama-sama tanda desimal (62,5 = 62.5), sama di semua kotak.
public sealed class MmText : IValueConverter
{
    public static readonly MmText I = new();
    public static double? Parse(string? text) => double.TryParse(text?.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;
    public static string Show(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);
    public object Convert(object value, Type target, object parameter, CultureInfo culture) => Show((double)value);
    // Tidak terbaca atau bukan angka positif: nilai lama tetap dan kotaknya ditandai merah.
    public object ConvertBack(object value, Type target, object parameter, CultureInfo culture) => Parse(value as string) is > 0 and var v ? v : DependencyProperty.UnsetValue;
}

public class Sticker
{
    public string Name { get; set; } = "";
    public int Copies { get; set; } = 1;
    public bool Hidden { get; set; }             // tombol mata di daftar desain (2026-11-10): tetap di daftar dengan jumlahnya, tetapi tidak disusun dan tidak ikut export
    public bool OwnCut;                          // opsi garis potong per desain (2026-11-10): LoadedKiss dan LoadedDie milik desain ini, tidak mengikuti panel Garis potong
    public double WidthMm { get; set; }          // lebar artwork yang diminta operator (mm)
    public string Source = "";
    public int Page;
    public string? Pdf;                          // PDF yang ditempel vektor saat export (PDF/AI asli atau hasil konversi CDR)
    public byte[] Raster = [];                   // gambar yang ditempel saat export bila bukan vektor
    public byte[]? Edited;                       // gambar pengganti file asli: hasil "Rapikan gambar", atau gambar saat dimuat sesudah muat ulang
    public (DateTime, long)? SvgStamp;           // versi file SVG saat digambar; null = bukan SVG yang dibaca dari file
    // Gambar untuk muat ulang. File asli tidak dibaca lagi, karena bisa sudah diubah, dipindah, atau dihapus sesudah dimuat.
    // SVG digambar ulang selama filenya masih sama, supaya tetap tajam pada lebar baru; gambar yang ditautkan SVG hanya terbaca dari folder aslinya.
    // null = baca dari sumbernya (PDF lewat salinan saat dimuat).
    public byte[]? Frozen => Edited ?? (Pdf != null || SvgStamp is { } v && v == Loader.Stamp(Source) ? null : Raster);
    public bool CanClean => Pdf == null;         // vektor (PDF/AI/CDR) tidak dirapikan agar warna CMYK tetap utuh
    // PDF/AI yang tidak bisa ditempel vektor (halaman diputar, tidak terbaca PDFsharp): ditempel sebagai gambar RGB.
    public bool PdfAsImage => Pdf == null && Path.GetExtension(Source).ToLowerInvariant() is ".pdf" or ".ai";
    // CDR yang dibuka tanpa CorelDRAW (libcdr), atau halaman dari CorelDRAW yang tidak bisa ditempel vektor: ditempel sebagai gambar RGB.
    public bool CdrAsImage => Pdf == null && Path.GetExtension(Source).Equals(".cdr", StringComparison.OrdinalIgnoreCase);
    public double LoadedW, LoadedBleed;
    public Cut LoadedKiss = null!, LoadedDie = null!;
    public double W, H;                          // kotak (mm), kelipatan Nest.Cell
    public double Off, ArtW, ArtH;               // posisi & ukuran artwork di dalam kotak (mm)
    public double StretchY { get; set; } = 1;    // rasio bebas (2026-11-07): tinggi artwork dibagi tinggi menurut proporsi file; 1 = asli. Hanya diisi Loader.Load
    public Rect Full;                            // seluruh sumber (halaman PDF / gambar penuh) dalam koordinat kotak (mm)
    public byte[]? BleedPng;                     // raster + bleed (warna tepi diperluas) untuk export; null = tanpa bleed
    public BitmapSource? BleedView;              // salinan kecil BleedPng untuk layar
    public Rect BleedRect;                       // posisi BleedPng di kotak (mm)
    public double AreaMm2;                       // luas di dalam garis potong terluar
    public BitmapSource Preview { get; set; } = null!; // artwork yang sudah di-crop, salinan kecil untuk layar
    public int PxW;                                     // lebar artwork dalam piksel asli (untuk dpi)
    public string HText => $"× {MmText.Show(ArtH)} mm"; // tinggi artwork di daftar desain
    // Resolusi cetak untuk desain yang ditempel sebagai gambar. Target riset 300 ppi; 150 = batas kasar,
    // sesuaikan setelah melihat hasil cetak Develop.
    public const double MinDpi = 150;
    public double? Dpi => Pdf == null && WidthMm > 0 ? PxW * 25.4 / WidthMm / Math.Max(1, StretchY) : null; // ditinggikan: dpi tegak lebih kecil
    public string DpiText => Dpi is { } d ? $"{d:0} dpi" : "";
    // Pembulatan sama dengan DpiText (D6): angka yang tampil dan warna merahnya selalu cocok.
    public bool LowDpi => Dpi is { } d && Math.Round(d, MidpointRounding.AwayFromZero) < MinDpi;
    public string Kiss = "", Die = "";           // path SVG garis potong (mm, koordinat kotak)
    // Garis potong hasil editor (node, mm relatif terhadap pojok kiri-atas artwork); null = garis otomatis.
    public List<Node[]>? ManualKiss, ManualDie;
    public SmartPick? Smart;                     // klik smart select yang membuat garis manual ini; null = garis manual biasa (tidak ikut offset)
    public bool Manual => ManualKiss != null || ManualDie != null;
    public CutShape Shape;                       // bentuk preset dari editor (Auto = mengikuti artwork); garis manual menang
    public CutShape DieShape;                    // bentuk die (Auto = mengikuti kiss)
    public string CutBadge => string.Join(" · ", new[] { Manual ? Lang.T("CutManual") : Shapes.Label(Shape, DieShape), OwnCut ? Lang.T("CutOwn") : "" }.Where(t => t != ""));
    public bool[,] Grid = null!;                 // hunian sel nesting dari garis terluar [x,y]
    public int Area;
    Geometry? kg, dg, clip;
    public Geometry? KissGeo => Kiss == "" ? null : kg ??= Freeze(Geometry.Parse(Kiss));
    public Geometry? DieGeo => Die == "" ? null : dg ??= Freeze(Geometry.Parse(Die));
    public Geometry Outer => DieGeo ?? KissGeo!;
    // Seberapa penuh bentuk mengisi kotaknya: persegi ±1, lingkaran ±0,79, bintang/logo bebas lebih kecil.
    public double Fill => AreaMm2 / Math.Max(1e-9, Outer.Bounds.Width * Outer.Bounds.Height);
    // Batas artwork saat digambar: garis terluar, atau dengan bleed: garis kiss (tanpa kiss: die) diperlebar bleed mm.
    public Geometry ArtClip => clip ??= BleedPng == null ? Outer : Grow(KissGeo ?? DieGeo!, LoadedBleed);
    // Warna border (masukan 2026-10-23): area di dalam garis kiss (tanpa kiss: die) di luar artwork dicetak dengan warna ini,
    // diteruskan sejauh bleed (bleed 0: BorderMin mm) di luar garis supaya pergeseran pisau tidak menyisakan tepi putih.
    // null = kertas putih. Lubang ikut diperlebar ke dalam sejauh yang sama.
    public Cmyk? Border;
    public const double BorderMin = 1;
    public Geometry? BorderGeo => Border == null ? null : bg ??= Grow(KissGeo ?? DieGeo!, LoadedBleed > 0 ? LoadedBleed : BorderMin);
    Geometry? bg;
    // Sambungan bulat: ujung tajam ikut menjorok tepat e. Miter bawaan Pen membuat lonjakan (ujung bintang 2,26e, segitiga 2e)
    // yang bisa tercetak di stiker tetangga.
    static Geometry Grow(Geometry g, double e) => Freeze(Geometry.Combine(g, g.GetWidenedPathGeometry(new Pen(null, 2 * e) { LineJoin = PenLineJoin.Round }, 0.01, ToleranceType.Absolute),
        GeometryCombineMode.Union, null, 0.01, ToleranceType.Absolute));
    static Geometry Freeze(Geometry g) { g.Freeze(); return g; }
}

// Pilihan smart select di editor garis potong (keputusan user 2026-11-08): titik klik objek dan lubang (mm relatif artwork), toleransi, dan Simplify.
// Disimpan bersama desain supaya garisnya dihitung ulang saat offset atau Halus berubah, selama node-nya tidak diedit sesudah smart select.
public sealed record SmartPick(Point[] Objs, Point[] Holes, int Tol, double Eps)
{
    // Ukuran desain berubah: titik klik ikut berskala bersama artwork, seperti garis manual.
    public SmartPick Scale(double kx, double ky) =>
        this with { Objs = [.. Objs.Select(p => new Point(p.X * kx, p.Y * ky))], Holes = [.. Holes.Select(p => new Point(p.X * kx, p.Y * ky))] };
}

// Warna CMYK 0..1 untuk PDF. Rgb hanya perkiraan untuk layar dan preview.
public readonly record struct Cmyk(double C, double M, double Y, double K)
{
    public Color Rgb => Color.FromRgb(B(C), B(M), B(Y));
    // Warna layar ke CMYK (pipet dan dialog warna di editor garis potong, 2026-11-08): kebalikan rumus Rgb, dibulatkan ke persen utuh.
    // ponytail: tanpa profil warna, jadi hasil cetak bisa beda dari layar; pakai profil ICC bila warna border harus persis.
    public static Cmyk FromRgb(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0, k = 1 - Math.Max(r, Math.Max(g, b));
        if (k >= 1) return new(0, 0, 0, 1);
        static double P(double x) => Math.Round(x * 100) / 100;
        return new(P((1 - r - k) / (1 - k)), P((1 - g - k) / (1 - k)), P((1 - b - k) / (1 - k)), P(k));
    }
    byte B(double x) => (byte)Math.Round(255 * (1 - x) * (1 - K));
}

public class Placed
{
    public Sticker S = null!;
    public double X, Y;
    public int Rot;
    public bool Bad;                             // tumpang tindih, terlalu dekat, atau keluar margin
    public double BW => Rot % 180 == 0 ? S.W : S.H;
    public double BH => Rot % 180 == 0 ? S.H : S.W;
    public (double x, double y) T => Rot switch { 90 => (S.H, 0), 180 => (S.W, S.H), 270 => (0, S.W), _ => (0, 0) };
}

public static partial class Loader
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
        // CDR yang dibaca libcdr: satu SVG per halaman (PdfOf memakai CorelDRAW hanya bila libcdr gagal).
        var cdrPages = pdf == null && Path.GetExtension(path).Equals(".cdr", StringComparison.OrdinalIgnoreCase) ? Libcdr.ToSvg(path) : null;
        int pages = cdrPages?.Length ?? 1;
        if (pdf != null)
            using (var r = new Pdfium(pdf))
            {
                pages = r.Pages;
                // AI dari Illustrator yang disimpan tanpa PDF-compatible: PDF-nya hanya satu halaman pengganti, isi aslinya data khusus Illustrator
                // yang tidak bisa dibaca. Ditolak supaya tidak dimuat sebagai desain bertuliskan kalimat itu.
                // ponytail: hanya mengenali kalimat bahasa Inggris dari Illustrator; tambah kalimat lain bila ada Illustrator berbahasa lain.
                if (Path.GetExtension(path).Equals(".ai", StringComparison.OrdinalIgnoreCase) && pages > 0
                    && string.Join(' ', r.Text(0).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Contains("saved without PDF Content", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(Lang.T("BadAi"));
            }
        var res = new List<Sticker>();
        for (int i = 0; i < pages; i++) res.Add(Load(path, i, pdf != null || cdrPages != null ? 0 : defWidthMm, kiss, die, pages > 1, bleed: bleed));
        return res;
    }

    static string? PdfOf(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".pdf" or ".ai" => Snapshot(path),
        ".cdr" => Libcdr.ToSvg(path) == null ? Corel.ToPdf(path) : null, // libcdr dulu; CorelDRAW hanya cadangan (keputusan user 2026-11-06)
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
    // dan CorelDRAW tidak dibuka lagi. manualKiss/manualDie: garis dari editor garis potong, dipakai apa adanya
    // (offset dan Halus tidak mengubahnya).
    // Muat ulang desain pada lebar it.WidthMm, atau widthMm bila diberikan (it tidak diubah). heightMm (rasio bebas, 2026-11-07): tinggi artwork baru; tanpa
    // tinggi, regangan yang sudah ada tetap dipakai. Garis manual tidak ikut offset/Halus; ukuran berubah: garis diskala bersama artwork per sumbu.
    // Bentuk preset dihitung ulang.
    public static Sticker Reload(Sticker it, Cut kiss, Cut die, double bleed, double? widthMm = null, double? heightMm = null)
    {
        double w = widthMm ?? it.WidthMm, k = w / it.LoadedW, ky = heightMm is { } h ? h / it.ArtH : k, sy = it.StretchY * ky / k;
        var (mk, md, smart) = (Curves.Scale(it.ManualKiss, k, ky), Curves.Scale(it.ManualDie, k, ky), it.Smart?.Scale(k, ky));
        if (smart != null)
        {
            // Garis smart select dihitung ulang dari kliknya pada offset dan Halus yang baru (keputusan user 2026-11-08). Bila kliknya tidak lagi
            // mengenai objek, garis lama (diskala) tetap dipakai.
            var plain = Load(it.Source, it.Page, w, kiss, die, edited: it.Frozen, bleed: bleed, loadedPdf: it.Pdf, border: it.Border, stretchY: sy, heightMm: heightMm);
            var (sk, sd, _) = SmartSelect(plain, smart.Objs, smart.Holes, smart.Tol, smart.Eps);
            if (kiss.On && sk.Count > 0) mk = sk;
            if (die.On && sd.Count > 0) md = sd;
        }
        return Load(it.Source, it.Page, w, kiss, die, edited: it.Frozen, bleed: bleed, loadedPdf: it.Pdf,
            manualKiss: mk, manualDie: md, shape: it.Shape, border: it.Border, dieShape: it.DieShape, stretchY: sy, heightMm: heightMm, smart: smart);
    }

    public static Sticker Load(string path, int page, double widthMm, Cut kiss, Cut die, bool multiPage = false, byte[]? edited = null, double bleed = 0,
        string? loadedPdf = null, List<Node[]>? manualKiss = null, List<Node[]>? manualDie = null, CutShape shape = CutShape.Auto, Cmyk? border = null,
        CutShape dieShape = CutShape.Auto, double stretchY = 1, double? heightMm = null, SmartPick? smart = null)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var s = new Sticker { Source = path, Page = page, Name = Path.GetFileName(path) + (multiPage ? $" p{page + 1}" : "") };
        var pdf = edited == null ? loadedPdf ?? PdfOf(path) : null;
        BitmapSource full;
        double nat = double.NaN;                 // mm per px pada ukuran asli (PDF dan halaman CDR)
        Rect media;                              // area yang digambar saat export, px relatif terhadap bitmap
        if (pdf != null)
        {
            (full, nat, media, bool vector) = RenderPdf(pdf, page);
            if (vector) s.Pdf = pdf;
        }
        else if (edited == null && ext is ".svg" or ".cdr")
        {
            // CDR yang sampai di sini sudah dibaca libcdr, karena PdfOf memakai CorelDRAW bila libcdr gagal.
            var svg = ext == ".svg" ? path : Libcdr.ToSvg(path)?.ElementAtOrDefault(page) ?? throw new InvalidDataException(Lang.T("BadCdr"));
            s.SvgStamp = Stamp(path);
            // File rusak: pesan untuk operator (D9), pesan teknis tetap di InnerException.
            try { full = RenderSvg(svg, widthMm * Math.Max(1, stretchY), out nat); } // tinggi diregangkan: digambar lebih rapat supaya tetap tajam
            catch (Exception ex) when (ex is not OutOfMemoryException) { throw new InvalidDataException(Lang.T(ext == ".cdr" ? "BadCdr" : "BadSvg"), ex); }
            s.Raster = Png(full);
            media = new Rect(0, 0, full.PixelWidth, full.PixelHeight);
        }
        else if (edited != null || ext is ".png" or ".jpg" or ".jpeg")
        {
            s.Edited = edited;
            s.Raster = edited ?? File.ReadAllBytes(path);
            using var ms = new MemoryStream(s.Raster);
            try
            {
                full = BitmapDecoder.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
                // Foto HP menyimpan piksel miring ditambah tanda orientasi. Export dan Rapikan memakai Raster, jadi yang disimpan gambar tegaknya.
                // ponytail: gambar tegak disimpan PNG, jadi PDF foto seperti ini lebih besar; simpan sebagai JPG bila ukurannya mengganggu.
                if (edited == null && ExifTurn(full) is { } turn)
                {
                    full = new CachedBitmap(new TransformedBitmap(full, turn), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    s.Raster = Png(full);
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException) { throw new InvalidDataException(Lang.T("BadImage"), ex); }
            media = new Rect(0, 0, full.PixelWidth, full.PixelHeight);
        }
        else throw new NotSupportedException(Lang.T("Unsupported"));

        var (px, fw, fh) = Pixels(full);
        var bb = ContentBox(px, fw, fh) ?? throw new InvalidOperationException(Lang.T("Empty"));
        var crop = new CroppedBitmap(full, bb);
        crop.Freeze();
        s.PxW = bb.Width;
        s.Preview = Proxy(crop);
        if (pdf != null && s.Pdf == null) { s.Raster = Png(crop); media = new Rect(bb.X, bb.Y, bb.Width, bb.Height); } // PDF tak terbaca vektor: tempel render-nya

        if (widthMm <= 0) widthMm = Math.Round(bb.Width * nat, 1);
        double k = widthMm / bb.Width;           // mm per px
        // Tinggi diminta (rasio bebas): regangan dari kotak isi gambar ini. SVG digambar ulang pada resolusi lain, jadi kotak isinya bisa bergeser satu piksel.
        if (heightMm is { } hm) stretchY = hm / (bb.Height * k);
        s.WidthMm = s.LoadedW = widthMm;
        s.LoadedKiss = kiss;
        s.LoadedDie = die;
        s.LoadedBleed = bleed;
        // Bleed perluasan warna tepi hanya untuk raster (PNG/JPG/SVG/hasil Rapikan), dan tidak bila ada warna border: perluasan
        // itu akan menutupi border; bleed-nya memakai warna border (Sticker.BorderGeo).
        s.Border = border;
        bool bled = bleed > 0 && s.Pdf == null && border == null;
        s.ArtW = widthMm;
        s.ArtH = bb.Height * k * stretchY;
        s.StretchY = stretchY;

        var small = new TransformedBitmap(crop, new ScaleTransform(s.ArtW * PxMm / bb.Width, s.ArtH * PxMm / bb.Height));
        var mask = Silhouette(small, out int mw, out int mh);

        // Garis manual milik garis yang aktif. Titik yang lebih jauh dari jangkauan otomatis memperbesar kotak dan bleed.
        s.ManualKiss = manualKiss;
        s.ManualDie = manualDie;
        s.Smart = smart;
        var mk = kiss.On ? manualKiss : null;
        var md = die.On ? manualDie : null;
        // Bentuk preset, atau desain kotak (masukan 2026-10-23): garis bentuk mengelilingi artwork sejauh offset, tanpa Halus.
        // Die memakai bentuk yang sama, minimal 1 px di luar kiss, atau bentuk die sendiri (masukan 2026-10-26) yang membungkus kiss.
        s.Shape = shape;
        s.DieShape = dieShape;
        if (manualKiss == null && manualDie == null)
        {
            var use = shape != CutShape.Auto ? shape : Shapes.IsBox(mask) ? CutShape.Rect : CutShape.Auto;
            if (use != CutShape.Auto || dieShape != CutShape.Auto) (mk, md) = Shapes.Lines(mask, use, s.ArtW, s.ArtH, kiss, die, dieShape);
        }
        double over = Curves.Overhang((mk ?? []).Concat(md ?? []), s.ArtW, s.ArtH);
        static int Reach(Cut c) => c.On ? (int)Math.Ceiling((c.Off + c.Smooth) * PxMm) : 0;
        int pad = Math.Max(Math.Max(Reach(kiss), Reach(die)), (int)Math.Ceiling(over * PxMm)) + (bled ? (int)Math.Ceiling(bleed * PxMm) : 0) + 3,
            W = mw + 2 * pad, H = mh + 2 * pad;
        var m = new bool[W, H];
        for (int y = 0; y < mh; y++) for (int x = 0; x < mw; x++) m[x + pad, y + pad] = mask[x, y];

        // Lubang asli artwork (latar yang tidak terhubung ke tepi gambar), misalnya tengah stiker donat.
        var holes = Solid(m);
        foreach (var (x, y) in Cells(holes)) holes[x, y] &= !m[x, y];
        bool[,]? outer = null, kissMask = null;
        var dieMan = md == null ? null : Raster(md, pad / PxMm, W, H);
        if (kiss.On)
        {
            if (mk != null) { kissMask = Raster(mk, pad / PxMm, W, H); s.Kiss = Curves.PathOf(mk, pad / PxMm); }
            else
            {
                kissMask = KeepHoles(Smooth(Close(m, kiss), m, kiss), holes);
                // Die manual dengan kiss otomatis: kiss dipangkas ke dalam die (minimal 1 px), supaya die tetap membungkus kiss.
                if (dieMan != null) { var inside = Not(Dilate(Not(dieMan), 1)); foreach (var (x, y) in Cells(kissMask)) kissMask[x, y] &= inside[x, y]; }
                s.Kiss = Trace(kissMask);
            }
            outer = kissMask;
        }
        if (die.On)
        {
            if (dieMan != null) { outer = dieMan; s.Die = Curves.PathOf(md!, pad / PxMm); }
            else
            {
                outer = Smooth(Close(m, die), m, die);
                // Die selalu membungkus kiss, minimal 1 px di luarnya: bila sama persis, kurva Potrace keduanya bisa bersilangan.
                if (kissMask != null) { var near = Dilate(kissMask, 1); foreach (var (x, y) in Cells(outer)) outer[x, y] |= near[x, y]; }
                outer = KeepHoles(outer, holes);
                s.Die = Trace(outer);
            }
        }
        if (outer != null) outer = Solid(outer); // grid nesting memakai bentuk padat: tidak ada stiker di dalam lubang
        if (outer == null) throw new InvalidOperationException(Lang.T("NeedCut"));

        s.Off = pad / PxMm;
        s.W = Math.Ceiling(W / PxMm / Nest.Cell) * Nest.Cell;
        s.H = Math.Ceiling(H / PxMm / Nest.Cell) * Nest.Cell;
        int cp = (int)Math.Round(Nest.Cell * PxMm), n = 0;
        s.Grid = new bool[(int)Math.Round(s.W / Nest.Cell), (int)Math.Round(s.H / Nest.Cell)];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) if (outer[x, y]) { s.Grid[x / cp, y / cp] = true; n++; }
        foreach (bool c in s.Grid) if (c) s.Area++;
        s.AreaMm2 = n / (PxMm * PxMm);
        double ky = k * stretchY; // arah tegak, ikut regangan
        s.Full = new Rect(s.Off + (media.X - bb.X) * k, s.Off + (media.Y - bb.Y) * ky, media.Width * k, media.Height * ky);
        if (bled)
        {
            var b = kiss.On ? kiss : die; // bleed diukur dari garis kiss; tanpa kiss dari garis die
            (s.BleedPng, s.BleedView, s.BleedRect) = Bleed(crop, s.ArtW, s.ArtH, Math.Max(b.Off + b.Smooth, over) + bleed + 1, s.Off);
        }
        return s;
    }

    const double BleedPxMm = 300 / 25.4; // resolusi maksimal gambar bleed: 300 dpi
    const double ExtendPxMm = 4;         // resolusi grid lapisan perluasan (pull-push): ±0,25 mm per sel

    // Auto-bleed untuk raster: warna tepi artwork diperluas keluar sejauh d mm, supaya pergeseran pisau tidak menyisakan
    // tepi putih. Sumber warnanya hanya piksel pekat (alpha 255); artwork asli lalu ditumpuk di atas lapisan perluasan.
    // Jadi bayangan dan tepi feather tetap tembus, dan warna latar yang tersembunyi di bawah piksel semi-transparan tidak
    // muncul (temuan 2026-10-28). Saat digambar, hasilnya di-clip ke garis potong + bleed (Sticker.ArtClip).
    // ponytail: gambar bleed dibuat ulang penuh (maks 300 dpi); stiker raster ±300 mm jadi ±1-2 detik lebih lambat.
    // Ganti dengan lapisan bawah beresolusi rendah bila operator merasakan lambat.
    static (byte[] png, BitmapSource view, Rect rect) Bleed(BitmapSource crop, double artW, double artH, double d, double off)
    {
        double k = Math.Min(crop.PixelWidth / artW, BleedPxMm);
        var src = new TransformedBitmap(crop, new ScaleTransform(artW * k / crop.PixelWidth, artH * k / crop.PixelHeight));
        var (px, w, h) = Pixels(src);
        double kx = w / artW, ky = h / artH;
        int p = (int)Math.Ceiling(d * Math.Max(kx, ky)), W = w + 2 * p, H = h + 2 * p;
        var o = Extend(px, w, h, p, W, H, Math.Max(1, (int)Math.Round(Math.Max(kx, ky) / ExtendPxMm)));
        // Artwork ditumpuk di atas lapisan perluasan. Tanpa lapisan (gambar tanpa piksel pekat): apa adanya.
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int j = (y * w + x) * 4, a = px[j + 3], i = ((y + p) * W + x + p) * 4;
                if (a == 0) continue;
                if (o[i + 3] == 0) { o[i] = px[j]; o[i + 1] = px[j + 1]; o[i + 2] = px[j + 2]; o[i + 3] = (byte)a; continue; }
                for (int c = 0; c < 3; c++) o[i + c] = (byte)((px[j + c] * a + o[i + c] * (255 - a) + 127) / 255);
            }
        var bmp = BitmapSource.Create(W, H, 96, 96, PixelFormats.Bgra32, null, o, W * 4);
        bmp.Freeze();
        return (Png(bmp), Proxy(bmp), new Rect(off - p / kx, off - p / ky, W / kx, H / ky));
    }

    // Lapisan perluasan tanpa garis (pull-push): warna piksel pekat dirata-rata ke luar pada beberapa skala. Dekat tepi
    // dipakai skala halus (warna tepi setempat), makin jauh makin kasar. Dilasi 8 arah sebelumnya meneruskan tepi yang
    // bertekstur (kain, bulu, logam) sebagai garis dan baji lurus (temuan 2026-10-28). Dihitung di grid f x f piksel per
    // sel, lalu diperbesar bilinear ke W x H. Hasil BGRA; semua alpha 0 bila tidak ada piksel pekat.
    static byte[] Extend(byte[] px, int w, int h, int p, int W, int H, int f)
    {
        var o = new byte[W * H * 4];
        int gw = (W + f - 1) / f, gh = (H + f - 1) / f;
        var c = new float[gw * gh * 3];
        var m = new float[gw * gh];
        float a = 1f / (f * f);
        bool any = false;
        bool Solid(int x, int y) => px[(y * w + x) * 4 + 3] == 255;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                // Sumber warna = piksel pekat di tepi (warna tepi), bukan warna bagian dalam artwork.
                if (!Solid(x, y) || x > 0 && y > 0 && x < w - 1 && y < h - 1 && Solid(x - 1, y) && Solid(x + 1, y) && Solid(x, y - 1) && Solid(x, y + 1)) continue;
                int j = (y * w + x) * 4, g = (y + p) / f * gw + (x + p) / f;
                m[g] += a;
                for (int ch = 0; ch < 3; ch++) c[g * 3 + ch] += px[j + ch] * a;
                any = true;
            }
        if (!any) return o;
        // Pull: piramida rata-rata 2x2; warna sudah dikali bobot, bobot <= 1.
        var lv = new List<(float[] C, float[] M, int W, int H)> { (c, m, gw, gh) };
        while (lv[^1].W > 1 || lv[^1].H > 1)
        {
            var (pc, pm, pw, ph) = lv[^1];
            int nw = (pw + 1) / 2, nh = (ph + 1) / 2;
            var nc = new float[nw * nh * 3];
            var nm = new float[nw * nh];
            for (int y = 0; y < ph; y++)
                for (int x = 0; x < pw; x++)
                {
                    int s = y * pw + x, t = y / 2 * nw + x / 2;
                    nm[t] += pm[s] / 4;
                    for (int ch = 0; ch < 3; ch++) nc[t * 3 + ch] += pc[s * 3 + ch] / 4;
                }
            lv.Add((nc, nm, nw, nh));
        }
        // Push, dari skala kasar ke halus: E = C + (1 - M) x E skala di atasnya. Skala terkasar = rata-rata warna pekat.
        var (tc, tm, _, _) = lv[^1];
        float[] e = [tc[0] / tm[0], tc[1] / tm[0], tc[2] / tm[0]];
        int ew = 1, eh = 1;
        for (int l = lv.Count - 2; l >= 0; l--)
        {
            var (lc, lm, lw, lh) = lv[l];
            var ne = new float[lw * lh * 3];
            for (int y = 0; y < lh; y++)
                for (int x = 0; x < lw; x++)
                {
                    int i = y * lw + x;
                    for (int ch = 0; ch < 3; ch++)
                        ne[i * 3 + ch] = lc[i * 3 + ch] + (1 - lm[i]) * Sample(e, ew, eh, (x + 0.5) / 2 - 0.5, (y + 0.5) / 2 - 0.5, ch);
                }
            (e, ew, eh) = (ne, lw, lh);
        }
        Parallel.For(0, H, y =>
        {
            for (int x = 0; x < W; x++)
            {
                int i = (y * W + x) * 4;
                for (int ch = 0; ch < 3; ch++) o[i + ch] = (byte)Math.Clamp(Sample(e, ew, eh, (x + 0.5) / f - 0.5, (y + 0.5) / f - 0.5, ch) + 0.5, 0, 255);
                o[i + 3] = 255;
            }
        });
        return o;
    }

    // Nilai bilinear kanal ch dari grid w x h (3 kanal) di titik (x, y) dalam satuan sel; di luar grid = sel tepi.
    static float Sample(float[] g, int w, int h, double x, double y, int ch)
    {
        x = Math.Clamp(x, 0, w - 1);
        y = Math.Clamp(y, 0, h - 1);
        int x0 = (int)x, y0 = (int)y, x1 = Math.Min(x0 + 1, w - 1), y1 = Math.Min(y0 + 1, h - 1);
        double fx = x - x0, fy = y - y0;
        double top = g[(y0 * w + x0) * 3 + ch] * (1 - fx) + g[(y0 * w + x1) * 3 + ch] * fx;
        double bot = g[(y1 * w + x0) * 3 + ch] * (1 - fx) + g[(y1 * w + x1) * 3 + ch] * fx;
        return (float)(top * (1 - fy) + bot * fy);
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

    // SVG dirender ±300 dpi sesuai lebar target, lalu diperlakukan seperti PNG. widthMm 0 = ukuran asli (halaman CDR dari libcdr).
    // nat = mm per px hasil render.
    static BitmapSource RenderSvg(string path, double widthMm, out double nat)
    {
        var d = new FileSvgReader(new WpfDrawingSettings { IncludeRuntime = false, TextAsGeometry = true }).Read(path);
        var b = d.Bounds;
        const double DipMm = 25.4 / 96; // mm per satuan gambar WPF
        double k = Math.Min((widthMm > 0 ? widthMm : b.Width * DipMm) * 12 / b.Width, 8000 / Math.Max(b.Width, b.Height));
        nat = DipMm / k;
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

    // Transformasi yang menegakkan foto menurut tanda orientasi EXIF. null = sudah tegak atau tanpa tanda; file tanpa EXIF dan metadata rusak termasuk di sini.
    static Transform? ExifTurn(BitmapSource b)
    {
        object? tag;
        try { tag = (b.Metadata as BitmapMetadata)?.GetQuery("/app1/ifd/{ushort=274}"); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return null; }
        return tag switch
        {
            (ushort)2 => new MatrixTransform(-1, 0, 0, 1, 0, 0),
            (ushort)3 => new MatrixTransform(-1, 0, 0, -1, 0, 0),
            (ushort)4 => new MatrixTransform(1, 0, 0, -1, 0, 0),
            (ushort)5 => new MatrixTransform(0, 1, 1, 0, 0, 0),
            (ushort)6 => new MatrixTransform(0, 1, -1, 0, 0, 0),
            (ushort)7 => new MatrixTransform(0, -1, -1, 0, 0, 0),
            (ushort)8 => new MatrixTransform(0, -1, 1, 0, 0, 0),
            _ => null,
        };
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

    // Artwork = piksel tidak transparan. Latar opak (termasuk putih) tidak dibuang otomatis: gambar opak dipakai utuh
    // dan garis mengelilingi kotak gambar. Latar dibuang operator lewat Rapikan atau editor garis potong (masukan leader 2026-10-21).
    static bool Fg(byte[] px, int i) => px[i * 4 + 3] > 16;

    static Int32Rect? ContentBox(byte[] px, int w, int h)
    {
        int x0 = w, y0 = h, x1 = -1, y1 = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (Fg(px, y * w + x)) { x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y); }
        return x1 < 0 ? null : new Int32Rect(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
    }

    static bool[,] Silhouette(BitmapSource b, out int w, out int h)
    {
        var (px, W, H) = Pixels(b);
        var m = new bool[W, H];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) m[x, y] = Fg(px, y * W + x);
        w = W; h = H;
        return m;
    }

    // Rongga garis potong dipertahankan (garis dalam, stiker donat) hanya bila seluruhnya berada di lubang asli artwork.
    // Rongga dari celah yang tertutup offset (huruf C besar, teks melingkar) diisi: di situ hanya kontur luar yang dipotong.
    static bool[,] KeepHoles(bool[,] cut, bool[,] holes)
    {
        var solid = Solid(cut);
        int W = cut.GetLength(0), H = cut.GetLength(1);
        var seen = new bool[W, H];
        var st = new Stack<(int, int)>();
        var comp = new List<(int, int)>();
        for (int y0 = 0; y0 < H; y0++)
            for (int x0 = 0; x0 < W; x0++)
            {
                if (seen[x0, y0] || cut[x0, y0] || !solid[x0, y0]) continue;
                bool inHole = true;
                comp.Clear();
                seen[x0, y0] = true;
                st.Push((x0, y0));
                while (st.Count > 0)
                {
                    var (x, y) = st.Pop();
                    comp.Add((x, y));
                    inHole &= holes[x, y];
                    void Push(int u, int v) { if (u >= 0 && v >= 0 && u < W && v < H && !seen[u, v] && !cut[u, v] && solid[u, v]) { seen[u, v] = true; st.Push((u, v)); } }
                    Push(x - 1, y); Push(x + 1, y); Push(x, y - 1); Push(x, y + 1);
                }
                if (inHole) foreach (var (x, y) in comp) solid[x, y] = false;
            }
        return solid;
    }

    // Isi semua rongga (latar yang tidak terhubung ke tepi).
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

    // Titik terdekat ke q pada segmen a-b.
    public static Point Nearest(Point q, Point a, Point b)
    {
        var ab = b - a;
        return ab.LengthSquared == 0 ? a : a + Math.Clamp((q - a) * ab / ab.LengthSquared, 0, 1) * ab;
    }

    // Garis manual -> mask 6 px/mm (grid nesting, luas, batas kiss/die), seperti mask garis otomatis.
    static bool[,] Raster(List<Node[]> lines, double off, int W, int H)
    {
        var v = new DrawingVisual();
        using (var dc = v.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(PxMm, PxMm));
            dc.DrawGeometry(Brushes.Black, null, Geometry.Parse(Curves.PathOf(lines, off)));
        }
        var rt = new RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32);
        rt.Render(v);
        var (px, _, _) = Pixels(rt);
        var m = new bool[W, H];
        for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) m[x, y] = px[(y * W + x) * 4 + 3] >= 128;
        return m;
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
            2 => throw new InvalidOperationException(Lang.T("BadCdr")), // CorelDRAW tidak terpasang, dan libcdr sudah gagal lebih dulu
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

// CDR tanpa CorelDRAW (keputusan user 2026-11-06, disetujui leader): alat libcdr dari MSYS2 (folder libcdr\ di samping exe; asal dan lisensi di
// libcdr\SUMBER.txt) menulis satu SVG per halaman. Dijalankan di proses terpisah seperti CorelDRAW, jadi file yang membuat alatnya macet atau crash
// tidak ikut menggantung Printech. libcdr dicoba lebih dulu; CorelDRAW hanya cadangan bila libcdr gagal.
// ponytail: libcdr mengubah semua warna menjadi RGB, dan artworknya ditempel sebagai gambar ±300 dpi lewat jalur SVG. Tambah pengubah gambar WPF
// ke PDF vektor bila hasil cetak kurang tajam.
public static class Libcdr
{
    static readonly ConcurrentDictionary<string, (string[]? Pages, (DateTime, long) Stamp)> cache = new();
    internal static string Tool => Path.Combine(AppContext.BaseDirectory, "libcdr", "cdr2xhtml.exe");

    [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern uint SetErrorMode(uint mode);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern uint GetErrorMode();

    // DLL yang hilang atau alat yang crash langsung gagal tanpa kotak pesan Windows. Mode ini menurun ke proses alatnya.
    static Libcdr() => SetErrorMode(GetErrorMode() | 0x0001 | 0x0002); // SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX

    // File SVG per halaman, atau null bila libcdr tidak bisa membacanya: alat tidak ada, gagal, lewat batas waktu, keluarannya rusak, atau kosong.
    // Hasil yang gagal juga disimpan, supaya pemanggilan berikutnya (LoadAll di thread tampilan) tidak menunggu lagi. CDR yang diedit dibaca ulang.
    public static string[]? ToSvg(string cdr, int timeoutMs = Corel.TimeoutMs)
    {
        var v = Loader.Stamp(cdr);
        if (cache.TryGetValue(cdr, out var done) && done.Stamp == v && (done.Pages?.All(File.Exists) ?? true)) return done.Pages;
        string[]? pages = null;
        if (File.Exists(Tool) && File.Exists(cdr))
        {
            Directory.CreateDirectory(App.Temp);
            var name = Guid.NewGuid().ToString("N");
            var copy = Path.Combine(App.Temp, name + ".cdr");
            try
            {
                File.Copy(cdr, copy);
                // Nama pendek di folder kerja: libcdr membuka nama file sebagai ANSI, jadi huruf di luar ASCII pada nama aslinya tidak terbaca.
                var psi = new ProcessStartInfo(Tool)
                {
                    UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = App.Temp,
                    RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8,
                };
                psi.ArgumentList.Add(name + ".cdr");
                using var p = Process.Start(psi)!;
                var output = p.StandardOutput.ReadToEndAsync();
                _ = p.StandardError.ReadToEndAsync(); // pesan alat tidak dipakai; tetap dibaca supaya prosesnya tidak tertahan
                if (!p.WaitForExit(timeoutMs)) { p.Kill(); p.WaitForExit(); }
                else if (p.ExitCode == 0) pages = Split(output.Result, Path.Combine(App.Temp, name));
            }
            catch (Exception e) when (e is not OutOfMemoryException) { pages = null; } // alat diblokir, keluaran rusak, dan sejenisnya: coba CorelDRAW
            finally { try { File.Delete(copy); } catch (IOException) { } }
        }
        cache[cdr] = (pages, v);
        return pages;
    }

    // Keluaran cdr2xhtml: XHTML berisi satu svg:svg per halaman, ukuran dalam inci dengan viewBox dalam point. Tiap halaman yang berisi gambar
    // disimpan sebagai file SVG sendiri dan dipotong seukuran halaman: benda di luar halaman tidak ikut, sama seperti PublishToPDF di CorelDRAW.
    // ponytail: keluaran dibaca utuh ke memori; CDR yang penuh gambar bisa besar.
    internal static string[]? Split(string xhtml, string prefix)
    {
        XNamespace svg = "http://www.w3.org/2000/svg";
        XDocument doc;
        using (var r = XmlReader.Create(new StringReader(xhtml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null }))
            doc = XDocument.Load(r);
        string[] drawn = ["path", "rect", "circle", "ellipse", "line", "polyline", "polygon", "text", "image"];
        var files = new List<string>();
        foreach (var page in doc.Descendants(svg + "svg").Where(e => !e.Ancestors(svg + "svg").Any()).ToList())
        {
            if (!page.Descendants().Any(e => e.Name.Namespace == svg && drawn.Contains(e.Name.LocalName))) continue;
            if (((string?)page.Attribute("viewBox"))?.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries) is [var x, var y, var w, var h])
            {
                var body = page.Nodes().ToList();
                body.ForEach(n => n.Remove());
                page.Add(new XElement(svg + "defs", new XElement(svg + "clipPath", new XAttribute("id", "halaman"),
                        new XElement(svg + "rect", new XAttribute("x", x), new XAttribute("y", y), new XAttribute("width", w), new XAttribute("height", h)))),
                    new XElement(svg + "g", new XAttribute("clip-path", "url(#halaman)"), body));
            }
            var file = $"{prefix}-p{files.Count + 1}.svg";
            new XDocument(page).Save(file);
            files.Add(file);
        }
        return files.Count > 0 ? [.. files] : null;
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

    // Luas stiker melebihi area hanya bila stiker bertumpuk (misalnya setelah ukurannya diubah), jadi angka untuk operator dibatasi 100%. Warnanya lihat Level.
    public static double Shown(double use) => Math.Min(use, 1);

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
        if (use > 1) return 0; // stiker bertumpuk, bukan target tercapai
        return use >= t.Green ? 2 : use >= t.Low ? 1 : 0;
    }
}

public static class Paper
{
    public static readonly Dictionary<string, (double W, double H)> Sizes = new()
    {
        ["A0"] = (841, 1189), ["A1"] = (594, 841), ["A2"] = (420, 594), ["A3"] = (297, 420), ["A3+"] = (320, 480), ["A4"] = (210, 297),
        ["A5"] = (148, 210), ["A6"] = (105, 148), ["SRA3"] = (320, 450), ["SRA4"] = (225, 320),
    };
}

// Keadaan sementara selama stiker ditarik: stiker, lembar asal dan tujuan, mode, dan geseran.
// Dokumen (isi lembar, X, Y, Rot) baru berubah di Drop, dan hanya bila berhasil. Batal cukup membuang objek ini.
public sealed class DragState(IEnumerable<Placed> group, int fromSheet)
{
    readonly List<(Placed P, double X, double Y, int Rot)> start = [.. group.Select(p => (p, p.X, p.Y, p.Rot))];
    public int FromSheet => fromSheet;
    public int ToSheet = -1; // lembar di bawah kursor; -1 = celah antar-lembar atau di luar tampilan
    public bool Copy;        // false = pindah
    public Vector Offset;    // geseran dari posisi awal (mm)
    public bool Moved => Offset != default;
    public bool Away => ToSheet >= 0 && ToSheet != fromSheet;

    // Stiker, atau salinannya, ke posisi kosong terdekat dari posisi barunya. Satu saja tidak muat = null, dokumen tidak berubah.
    // Salinan masuk ke satu lembar: ToSheet, atau lembar asal bila dilepas di celah. Pindah tanpa lembar lain: tiap stiker di lembarnya sendiri.
    public List<Placed>? Drop(List<List<Placed>> sheets, IList<double> sheetH, double cw, double margin, double gap, bool marks = false)
    {
        if (Copy)
        {
            int to = ToSheet >= 0 ? ToSheet : fromSheet;
            var copies = start.Select(s => new Placed { S = s.P.S, Rot = s.Rot, X = s.X + Offset.X, Y = s.Y + Offset.Y }).ToList();
            return Nest.MoveTo(sheets, copies, to, cw, sheetH[to], margin, gap, marks) ? copies : null;
        }
        var moved = start.Select(s => s.P).ToList();
        foreach (var (p, x, y, _) in start) (p.X, p.Y) = (x + Offset.X, y + Offset.Y);
        bool ok = Away ? Nest.MoveTo(sheets, moved, ToSheet, cw, sheetH[ToSheet], margin, gap, marks)
            : moved.All(p => sheets.FindIndex(sh => sh.Contains(p)) is var i && Nest.Snap(sheets[i], p, cw, sheetH[i], margin, gap, marks));
        if (ok) return moved;
        foreach (var (p, x, y, rot) in start) (p.X, p.Y, p.Rot) = (x, y, rot);
        return null;
    }
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
        int bleed = s.BleedPng == null ? 0 : CellsOf(s.LoadedBleed);
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

    // Jarak (mm) dalam sel grid, dibulatkan ke atas.
    static int CellsOf(double mm) => (int)Math.Ceiling(mm / Cell - 1e-9);

    // Grid hunian area cetak (di dalam margin), dengan stiker ps dan area larangan keep (mm lembar) sudah ditandai.
    static bool[,] Occupancy(IEnumerable<Placed> ps, double cw, double ch, double margin, int gc, List<Rect>? keep = null)
    {
        var occ = new bool[Math.Max(0, (int)((cw - 2 * margin) / Cell)), Math.Max(0, (int)((ch - 2 * margin) / Cell))];
        foreach (var q in ps) Stamp(occ, FootOf(q.S, q.Rot), (int)Math.Round((q.X - margin) / Cell), (int)Math.Round((q.Y - margin) / Cell), gc);
        foreach (var r in keep ?? [])
        {
            int x0 = Math.Max(0, (int)Math.Floor((r.Left - margin) / Cell)), x1 = Math.Min(occ.GetLength(0), (int)Math.Ceiling((r.Right - margin) / Cell));
            int y0 = Math.Max(0, (int)Math.Floor((r.Top - margin) / Cell)), y1 = Math.Min(occ.GetLength(1), (int)Math.Ceiling((r.Bottom - margin) / Cell));
            for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) occ[x, y] = true;
        }
        return occ;
    }

    // Area pindai tanda registrasi (masukan leader 2026-10-26: stiker kena tanda), diperlebar e mm untuk bagian stiker yang ikut
    // tercetak di luar garis potong. Kosong bila tanda mati.
    static List<Rect> KeepOut(double cw, double ch, double margin, bool marks, double e) =>
        marks ? [.. RegistrationMarks.Zones(cw, ch, margin).Select(r => { r.Inflate(e, e); return r; })] : [];

    // Bagian yang tercetak di luar garis potong terluar: bleed raster, atau warna border (sejauh bleed, atau BorderMin).
    static double Printed(Sticker s) => s.BleedPng != null ? s.LoadedBleed : s.Border != null ? (s.LoadedBleed > 0 ? s.LoadedBleed : Sticker.BorderMin) : 0;

    // Bertumpuk dengan luas > 0 (bersentuhan di tepi tidak dihitung, sama dengan sel grid hunian).
    static bool Overlap(Rect a, Rect b) => a.Left < b.Right - 1e-9 && b.Left < a.Right - 1e-9 && a.Top < b.Bottom - 1e-9 && b.Top < a.Bottom - 1e-9;

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
    public static List<List<Placed>> Run(IEnumerable<Sticker> stickers, double cw, double ch, double margin, double gap, out int tooBig, bool marks = false)
    {
        var items = stickers.Where(s => !s.Hidden).SelectMany(s => Enumerable.Repeat(s, Math.Max(0, s.Copies))).ToList();
        if (items.Count == 0) { tooBig = 0; return []; }
        Func<Sticker, double>[] keys = [s => s.Area, s => Math.Max(s.W, s.H), s => s.W + s.H];
        // 3 percobaan dihitung paralel (kerja CPU). Once hanya membaca Sticker dan memakai cache sendiri.
        var trials = new (List<List<Placed>> r, int big)[keys.Length];
        var keep = KeepOut(cw, ch, margin, marks, items.Select(Printed).DefaultIfEmpty(0).Max());
        Parallel.For(0, keys.Length, i => trials[i] = (Once(items.OrderByDescending(keys[i]).ToList(), cw, ch, margin, gap, keep, out int big), big));
        // Dipilih menurut urutan percobaan, bukan urutan selesai: hasil sama setiap kali.
        static double Score(List<List<Placed>> r) => r.Count * 1e6 + (r.Count == 0 ? 0 : r[^1].Max(p => p.Y + p.BH));
        List<List<Placed>> best = [];
        double bestScore = double.MaxValue;
        int bestKey = 0;
        tooBig = 0;
        for (int i = 0; i < trials.Length; i++)
            if (Score(trials[i].r) < bestScore) (best, bestScore, bestKey, tooBig) = (trials[i].r, Score(trials[i].r), i, trials[i].big);
        // Baris pertama terpaksa mulai setelah area tanda di pojok, jadi tepi kirinya menjorok dan kolom sebelahnya bisa kurang muat (masukan user
        // 2026-11-03). Coba sekali lagi dengan pita atas setinggi area tanda dikosongkan; dipakai hanya bila lebih hemat, karena Hemat mengutamakan bahan.
        if (keep.Count > 0)
        {
            List<Rect> band = [.. keep, new Rect(margin, margin, Math.Max(0, cw - 2 * margin), keep.Where(z => z.Top < ch / 2).Max(z => z.Bottom) - margin)];
            var flush = Once(items.OrderByDescending(keys[bestKey]).ToList(), cw, ch, margin, gap, band, out int flushBig);
            if (flushBig <= tooBig && Score(flush) < bestScore) (best, tooBig) = (flush, flushBig);
        }
        return best;
    }

    static List<List<Placed>> Once(List<Sticker> items, double cw, double ch, double margin, double gap, List<Rect> keep, out int tooBig)
    {
        int gc = CellsOf(gap);
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
            var occ = Occupancy([], cw, ch, margin, gc, keep);
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
    public static bool Snap(List<Placed> ps, Placed p, double cw, double ch, double margin, double gap, bool marks = false)
    {
        if (p == null || p.S == null) return false; // <--- TAMBAHKAN INI
        var occ = Occupancy(ps.Where(q => q != p), cw, ch, margin, CellsOf(gap), KeepOut(cw, ch, margin, marks, Printed(p.S)));
        int cols = occ.GetLength(0), rows = occ.GetLength(1);
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

    // Pindah ke lembar lain: tiap stiker ke posisi kosong terdekat dari posisinya sekarang. Satu tidak muat = semua batal: false, stiker dan semua lembar tidak berubah.
    public static bool MoveTo(List<List<Placed>> sheets, IReadOnlyCollection<Placed> group, int to, double cw, double ch, double margin, double gap, bool marks = false)
    {
        var was = group.Select(p => (p, p.X, p.Y)).ToList();
        var taken = sheets[to].Except(group).ToList();
        foreach (var p in group)
        {
            if (!Snap(taken, p, cw, ch, margin, gap, marks))
            {
                foreach (var (q, x, y) in was) (q.X, q.Y) = (x, y);
                return false;
            }
            taken.Add(p);
        }
        foreach (var sh in sheets) sh.RemoveAll(group.Contains);
        sheets[to].AddRange(group);
        return true;
    }

    // Putar 90°, lalu posisi kosong terdekat. false = tidak ada tempat; rotasi dan posisi tidak berubah.
    public static bool Turn(List<Placed> ps, Placed p, double cw, double ch, double margin, double gap, bool marks = false)
    {
        int rot = p.Rot;
        p.Rot = (rot + 90) % 360;
        if (Snap(ps, p, cw, ch, margin, gap, marks)) return true;
        p.Rot = rot;
        return false;
    }

    // Tidak ada posisi kosong: kotak stiker tetap di dalam area cetak supaya terlihat dan bisa ditarik lagi. Check menandainya merah.
    public static void KeepInside(Placed p, double cw, double ch, double margin)
    {
        p.X = Math.Clamp(p.X, margin, Math.Max(margin, cw - margin - p.BW));
        p.Y = Math.Clamp(p.Y, margin, Math.Max(margin, ch - margin - p.BH));
    }

    // Ubah ukuran tanpa menumpuk (permintaan user 2026-11-09). Di tiap lembar, stiker pertama dari picked (yang ditarik) diam di tempatnya, hanya
    // dirapatkan ke grid dan dijaga di dalam area cetak. Sisa picked di lembar itu, dan stiker lain yang ditimpanya, pindah ke posisi kosong terdekat di
    // lembar yang sama; yang tidak kebagian tempat pindah ke lembar baru tepat sesudah lembar asalnya (seukuran lembar asal, dipakai bersama), dengan
    // ukurannya sendiri. Stiker pertama yang lebih besar dari area cetak tetap di lembarnya dan tidak menggeser yang lain (TooBig; Check menandainya).
    // Moved = jumlah stiker yang pindah ke lembar baru; NewSheets = indeks lembar baru.
    public static (int Moved, List<int> NewSheets, bool TooBig) MakeRoom(List<List<Placed>> sheets, List<double> heights, IReadOnlyList<Placed> picked,
        double cw, double margin, double gap, bool marks = false)
    {
        int moved = 0;
        bool tooBig = false;
        var made = new List<List<Placed>>();
        // Dikelompokkan per lembar (bukan per indeks): lembar baru yang disisipkan menggeser indeks lembar sesudahnya.
        foreach (var grp in picked.GroupBy(p => sheets.Find(sh => sh.Contains(p))).Where(g => g.Key != null).ToList())
        {
            var sheet = grp.Key!;
            double ch = heights[sheets.IndexOf(sheet)];
            var anchor = grp.First();
            bool fits = Snap([], anchor, cw, ch, margin, gap, marks);
            if (!fits) { KeepInside(anchor, cw, ch, margin); tooBig = true; }
            var occ = Occupancy([anchor], cw, ch, margin, CellsOf(gap));
            bool Hit(Placed q)
            {
                int ox = (int)Math.Round((q.X - margin) / Cell), oy = (int)Math.Round((q.Y - margin) / Cell);
                return FootOf(q.S, q.Rot).Cells.Any(c => ox + c.X >= 0 && oy + c.Y >= 0 && ox + c.X < occ.GetLength(0) && oy + c.Y < occ.GetLength(1) && occ[ox + c.X, oy + c.Y]);
            }
            var move = grp.Skip(1).Concat(fits ? sheet.Where(q => q != anchor && !grp.Contains(q) && Hit(q)) : []).ToList();
            sheet.RemoveAll(move.Contains);
            List<Placed>? spill = null;
            foreach (var q in move)
            {
                if (Snap(sheet, q, cw, ch, margin, gap, marks)) { sheet.Add(q); continue; }
                var was = (q.X, q.Y);
                (q.X, q.Y) = (margin, margin);
                if (spill == null || !Snap(spill, q, cw, ch, margin, gap, marks))
                {
                    if (!Snap([], q, cw, ch, margin, gap, marks)) { (q.X, q.Y) = was; KeepInside(q, cw, ch, margin); sheet.Add(q); tooBig = true; continue; } // lebih besar dari lembar
                    int at = spill == null ? sheets.IndexOf(sheet) + 1 : sheets.IndexOf(spill) + 1;
                    spill = [];
                    sheets.Insert(at, spill);
                    heights.Insert(at, ch);
                    made.Add(spill);
                }
                spill.Add(q);
                moved++;
            }
        }
        return (moved, [.. made.Select(m => sheets.IndexOf(m))], tooBig);
    }

    // Tempel beberapa stiker: tiap salinan di kanan aslinya, di lembarnya sendiri (PasteNext). Satu tidak muat = tidak ada yang ditambahkan (null).
    public static List<Placed>? PasteAll(List<List<Placed>> sheets, IList<double> sheetH, IEnumerable<(Sticker S, int Rot, double X, double Y, int Sheet)> clip, double cw, double margin, double gap, bool marks = false)
    {
        var added = new List<Placed>();
        foreach (var c in clip)
        {
            if (PasteNext(sheets[c.Sheet], c.S, c.Rot, c.X, c.Y, cw, sheetH[c.Sheet], margin, gap, marks) is not { } n)
            {
                foreach (var sh in sheets) sh.RemoveAll(added.Contains);
                return null;
            }
            sheets[c.Sheet].Add(n);
            added.Add(n);
        }
        return added;
    }

    // Penuhi lembar (tombol Compact): customer membayar per lembar, jadi ruang kosong di semua lembar diisi salinan desain
    // (jumlah > 0) bergiliran sampai tidak ada yang muat. Grid hunian dan TryPlace sama dengan susun otomatis.
    // Mengembalikan jumlah stiker yang ditambahkan.
    public static int Fill(List<List<Placed>> sheets, IEnumerable<Sticker> designs, double cw, double ch, double margin, double gap, bool marks = false)
    {
        int gc = CellsOf(gap), added = 0;
        var cache = new Dictionary<(Sticker, int), Foot>();
        var keep = KeepOut(cw, ch, margin, marks, designs.Select(Printed).DefaultIfEmpty(0).Max());
        foreach (var ps in sheets)
        {
            var occ = Occupancy(ps, cw, ch, margin, gc, keep);
            var turn = designs.Where(s => !s.Hidden && s.Copies > 0).ToList();
            for (int i = 0; turn.Count > 0; i %= Math.Max(1, turn.Count))
                if (TryPlace(occ, turn[i], cache, gc, margin, ps)) { added++; i++; }
                else turn.RemoveAt(i); // desain ini tidak muat lagi di lembar ini
        }
        return added;
    }

    // Susun berbaris (permintaan user 2026-11-02): desain sama berkumpul, desain tertinggi dulu, baris demi baris dari kiri atas.
    // ponytail: tiap stiker memakai kotak penuh jejaknya dan hanya satu urutan dicoba, jadi lebih rapi tetapi lebih boros dari Run
    // pada bentuk tidak beraturan. Coba beberapa urutan seperti Run bila lembar sering bertambah.
    public static List<List<Placed>> Rows(IEnumerable<Sticker> stickers, double cw, double ch, double margin, double gap, out int tooBig, bool marks = false)
    {
        // Tepi kiri rata: baris selalu mulai dari tepi kiri, dan turun bila pojoknya kena area tanda registrasi. Bila cara ini menambah
        // lembar, baris boleh mulai setelah area tanda, supaya bahan tidak bertambah.
        var even = RowsOnce(stickers, cw, ch, margin, gap, out tooBig, marks, true);
        if (!marks) return even;
        var loose = RowsOnce(stickers, cw, ch, margin, gap, out int big, marks, false);
        if (even.Count <= loose.Count && tooBig <= big) return even;
        tooBig = big;
        return loose;
    }

    static List<List<Placed>> RowsOnce(IEnumerable<Sticker> stickers, double cw, double ch, double margin, double gap, out int tooBig, bool marks, bool flush)
    {
        var designs = stickers.Where(s => !s.Hidden && s.Copies > 0).ToList();
        if (designs.Count == 0) { tooBig = 0; return []; }
        // Tanpa stiker: occ hanya berisi area tanda registrasi, jadi sama untuk semua lembar. Baris tidak bisa saling menabrak.
        var occ = Occupancy([], cw, ch, margin, 0, KeepOut(cw, ch, margin, marks, designs.Select(Printed).DefaultIfEmpty(0).Max()));
        int cols = occ.GetLength(0), rows = occ.GetLength(1), gc = CellsOf(gap), cx = 0, cy = 0, rowH = 0;
        var sheets = new List<List<Placed>> { new() };

        bool Put(Sticker s, int rot, Foot f)
        {
            int fw = f.MaxX - f.MinX + 1, fh = f.MaxY - f.MinY + 1;
            while (true)
            {
                int top = Math.Max(cy, f.Bleed), h = Math.Max(rowH, fh), y = top + (h - fh) / 2;
                if (y + fh > rows - f.Bleed) return false;
                // Lompat satu kotak penuh, supaya kolom tetap lurus saat melewati area tanda registrasi.
                for (int x = Math.Max(cx, f.Bleed); x + fw <= cols - f.Bleed; x += fw + gc)
                {
                    if (Fits(occ, f.Cells, x - f.MinX, y - f.MinY))
                    {
                        sheets[^1].Add(new Placed { S = s, Rot = rot, X = margin + (x - f.MinX) * Cell, Y = margin + (y - f.MinY) * Cell });
                        (cx, cy, rowH) = (x + fw + gc, top, h);
                        return true;
                    }
                    if (flush && cx == 0) break;
                }
                (cy, cx, rowH) = (rowH > 0 ? cy + rowH + gc : cy + 1, 0, 0); // baris kosong berarti terhalang area tanda: turun satu sel
            }
        }

        tooBig = 0;
        var jobs = new List<(Sticker S, int Rot, Foot F)>();
        foreach (var s in designs)
        {
            // Diputar hanya bila tidak muat tegak.
            var (up, side) = (FootOf(s, 0), FootOf(s, 90));
            bool In(Foot f) => f.MaxX - f.MinX + 1 + 2 * f.Bleed <= cols && f.MaxY - f.MinY + 1 + 2 * f.Bleed <= rows;
            if (In(up)) jobs.Add((s, 0, up));
            else if (In(side)) jobs.Add((s, 90, side));
            else tooBig += s.Copies;
        }
        foreach (var (s, rot, f) in jobs.OrderByDescending(j => j.F.MaxY - j.F.MinY))
        {
            // Desain yang cukup untuk satu baris penuh mulai di baris baru; yang sedikit mengisi sisa baris.
            if (cx > 0 && s.Copies >= (cols - 2 * f.Bleed + gc) / (f.MaxX - f.MinX + 1 + gc)) (cy, cx, rowH) = (cy + rowH + gc, 0, 0);
            for (int i = 0; i < s.Copies; i++)
            {
                if (Put(s, rot, f)) continue;
                if (sheets[^1].Count > 0)
                {
                    sheets.Add([]);
                    (cx, cy, rowH) = (0, 0, 0);
                    if (Put(s, rot, f)) continue;
                }
                tooBig += s.Copies - i;
                break;
            }
        }
        sheets.RemoveAll(x => x.Count == 0);
        return sheets;
    }

    // Penuhi lembar saat berbaris: salinan ditambah bergiliran lalu disusun ulang, selama jumlah lembar dan yang tidak muat tidak
    // bertambah. Copies diubah sesuai hasil. Mengembalikan susunan baru; geseran manual hilang (bisa diurungkan).
    public static List<List<Placed>> FillRows(IReadOnlyList<Sticker> designs, double cw, double ch, double margin, double gap, out int added, bool marks = false,
        IReadOnlyCollection<Sticker>? only = null)
    {
        var best = Rows(designs, cw, ch, margin, gap, out int big, marks);
        added = 0;
        // only: desain yang boleh ditambah (stiker terpilih, keputusan user 2026-11-08); null = semua desain bergiliran.
        var turn = designs.Where(s => !s.Hidden && s.Copies > 0 && (only == null || only.Contains(s))).ToList();
        for (int i = 0; turn.Count > 0; i %= Math.Max(1, turn.Count))
        {
            turn[i].Copies++;
            var r = Rows(designs, cw, ch, margin, gap, out int b, marks);
            if (r.Count <= best.Count && b <= big) { best = r; added++; i++; }
            else { turn[i].Copies--; turn.RemoveAt(i); }
        }
        return best;
    }

    const double MinSide = 5; // sisi artwork terkecil saat ukuran diatur di kanvas (mm)

    // Atur ukuran di kanvas: titik pojok (0 kiri atas, 1 kanan atas, 2 kanan bawah, 3 kiri bawah) ditarik ke (mx, my). Pojok seberangnya diam; jarak garis
    // potong ke artwork tidak ikut berskala. Rasio terkunci: satu faktor untuk kedua sisi. free (kunci rasio dilepas, 2026-11-07): sisi mendatar dan tegak
    // mengikuti tarikannya sendiri-sendiri. Sisi terkecil MinSide, kecuali memang sudah lebih kecil; kotak paling besar area cetak, per sisi.
    // Mengembalikan lebar dan tinggi artwork baru (sumbu artwork, 0,1 mm) dan kotak stiker di lembar.
    public static (double Width, double Height, Rect Box) ResizeTo(Placed p, int corner, double mx, double my, double cw, double ch, double margin, bool free = false)
    {
        bool turned = p.Rot % 180 != 0;
        var (aw, ah) = turned ? (p.S.ArtH, p.S.ArtW) : (p.S.ArtW, p.S.ArtH); // sisi artwork mendatar dan tegak di lembar
        double padX = p.BW - aw, padY = p.BH - ah, room = 2 * (margin + Printed(p.S));
        bool right = corner is 1 or 2, down = corner is 2 or 3;
        double ax = right ? p.X : p.X + p.BW, ay = down ? p.Y : p.Y + p.BH;
        double kx = ((right ? mx - ax : ax - mx) - padX) / aw, ky = ((down ? my - ay : ay - my) - padY) / ah;
        static double Fit(double k, double side, double most) => Math.Max(Math.Min(1, MinSide / side), Math.Min(most / side, k));
        if (free) (kx, ky) = (Fit(kx, aw, cw - room - padX), Fit(ky, ah, ch - room - padY));
        else kx = ky = Math.Max(Math.Min(1, MinSide / Math.Min(aw, ah)), Math.Min(Math.Min((cw - room - padX) / aw, (ch - room - padY) / ah), Math.Max(kx, ky)));
        var (kw, kh) = turned ? (ky, kx) : (kx, ky); // faktor lebar dan tinggi artwork
        double width = Math.Round(p.S.ArtW * kw, 1), height = free ? Math.Round(p.S.ArtH * kh, 1) : p.S.ArtH * width / p.S.ArtW;
        double bw = (turned ? height : width) + padX, bh = (turned ? width : height) + padY;
        return (width, height, new Rect(right ? ax : ax - bw, down ? ay : ay - bh, bw, bh));
    }

    // Shift sambil menyeret stiker (daftar tugas leader 2026-11-08): hanya mendatar atau tegak, mengikuti arah gerak yang lebih besar.
    public static Vector Straight(Vector v) => Math.Abs(v.X) >= Math.Abs(v.Y) ? new(v.X, 0) : new(0, v.Y);

    // Stiker yang ditarik (temuan 2026-11-09): titik pegangannya kursor, atau dengan Shift dikunci mendatar atau tegak di koordinat lembar asal,
    // sama dengan pratinjau. Lembar tujuan = lembar yang memuat titik itu (-1 = celah antarlembar atau di luar tampilan), dan geseran dalam mm
    // lembar tujuan (lembar asal bila di celah). cursor, grab, sheets, dan view dalam koordinat lembar asal.
    public static (int To, Vector Offset) Aim(Point cursor, Point grab, bool straight, IReadOnlyList<Rect> sheets, Rect view, int from)
    {
        var at = straight ? grab + Straight(cursor - grab) : cursor;
        int to = view.Contains(at) ? sheets.Select((r, i) => (r, i)).FirstOrDefault(x => x.r.Contains(at), (default, -1)).i : -1;
        return (to, at - grab - (sheets[to >= 0 ? to : from].TopLeft - sheets[from].TopLeft));
    }

    // Desain diganti (Rapikan, Edit garis potong, atau atur ukuran tanpa stiker lain yang memakai desainnya): semua salinannya ikut. Posisinya tetap;
    // yang jadi bertabrakan ditandai Check.
    public static void SwapDesign(IEnumerable<List<Placed>> sheets, Sticker old, Sticker n)
    {
        if (old == null || n == null) return;
        foreach (var p in sheets.SelectMany(x => x)) 
            if (p.S == old) p.S = n;
    }

    // Atur ukuran stiker terpilih saja (keputusan user 2026-11-07): bila desainnya juga dipakai stiker lain, stiker terpilih pindah ke desain n, yang menjadi
    // baris baru tepat di bawah desain lama (nama sama, jumlah = stiker yang diubah). Stiker lain tidak berubah. false = tidak ada stiker lain; pemanggil
    // mengganti desainnya (SwapDesign).
    // ponytail: baris yang kembali ke lebar yang sama tidak digabung lagi; gabungkan bila daftar desain jadi ramai.
    public static bool Split(IList<Sticker> items, IEnumerable<List<Placed>> sheets, ICollection<Placed> chosen, Sticker old, Sticker n)
    {
        var uses = sheets.SelectMany(x => x).Where(p => p.S == old).ToList();
        var mine = uses.Where(chosen.Contains).ToList();
        if (mine.Count == uses.Count) return false;
        (n.Name, n.Copies, n.OwnCut, old.Copies) = (old.Name, mine.Count, old.OwnCut, Math.Max(0, old.Copies - mine.Count)); // opsi garis potong sendiri ikut ke baris baru
        items.Insert(items.IndexOf(old) + 1, n);
        foreach (var p in mine) p.S = n;
        return true;
    }

    // Duplikat kanvas: isi, posisi, dan rotasi sama persis, termasuk hasil edit. Tepat setelah lembar asal, jadi tampil di sampingnya.
    public static void CopySheet(List<List<Placed>> sheets, List<double> heights, int i)
    {
        sheets.Insert(i + 1, [.. sheets[i].Select(p => new Placed { S = p.S, X = p.X, Y = p.Y, Rot = p.Rot })]);
        heights.Insert(i + 1, heights[i]);
    }

    // Tempel stiker: posisi kosong pertama di kanan (fromX, fromY), lalu di baris-baris bawahnya dari kolom paling kiri. Langkahnya sebesar
    // jejak + jarak seperti Rows, jadi salinan rata dengan stiker asli. null = tidak ada tempat; lembar baru tidak dibuat, supaya bahan tidak
    // bertambah tanpa disadari.
    // ponytail: hanya posisi segaris yang dicoba, jadi ruang kosong di atas stiker asli atau di antara kolom dilewati. Tambah pencarian bebas
    // seperti Snap bila operator sering kehabisan tempat.
    public static Placed? PasteNext(List<Placed> ps, Sticker s, int rot, double fromX, double fromY, double cw, double ch, double margin, double gap, bool marks = false)
    {
        int gc = CellsOf(gap);
        var occ = Occupancy(ps, cw, ch, margin, gc, KeepOut(cw, ch, margin, marks, Printed(s)));
        var f = FootOf(s, rot);
        int cols = occ.GetLength(0), rows = occ.GetLength(1), ax = (int)Math.Round((fromX - margin) / Cell), ay = (int)Math.Round((fromY - margin) / Cell);
        int sx = f.MaxX - f.MinX + 1 + gc, sy = f.MaxY - f.MinY + 1 + gc;
        static int First(int a, int low, int step) => low + ((a - low) % step + step) % step; // posisi terkecil >= low yang segaris dengan a
        for (int y = First(ay, f.Bleed - f.MinY, sy); y + f.MaxY < rows - f.Bleed; y += sy)
            for (int x = First(ax, f.Bleed - f.MinX, sx); x + f.MaxX < cols - f.Bleed; x += sx)
                if ((y > ay || (y == ay && x > ax)) && Fits(occ, f.Cells, x, y))
                    return new Placed { S = s, Rot = rot, X = margin + x * Cell, Y = margin + y * Cell };
        return null;
    }

    static bool Fits(bool[,] occ, List<(int X, int Y)> cells, int x, int y)
    {
        foreach (var (cx, cy) in cells) if (occ[x + cx, y + cy]) return false;
        return true;
    }

    // Tandai stiker yang tumpang tindih (jejak garis terluar), lebih dekat dari jarak antar stiker (gap), yang jejaknya (ditambah bleed)
    // keluar margin, atau yang masuk area pindai tanda registrasi (marks). Kembalikan jumlahnya.
    public static int Check(List<Placed> ps, double cw, double ch, double margin, bool marks = false, double gap = 0)
    {
        var feet = ps.Select(p => FootOf(p.S, p.Rot)).ToList();
        var keep = KeepOut(cw, ch, margin, marks, 0);
        for (int i = 0; i < ps.Count; i++)
        {
            var (p, f) = (ps[i], feet[i]);
            double m = margin + f.Bleed * Cell - 0.01, e = Printed(p.S);
            p.Bad = p.X + f.MinX * Cell < m || p.Y + f.MinY * Cell < m || p.X + (f.MaxX + 1) * Cell > cw - m || p.Y + (f.MaxY + 1) * Cell > ch - m;
            // Jejak padat dan area larangan menempel di luar area cetak: cukup sel tepi jejak yang dicek.
            Rect Box(int x0, int y0, int x1, int y1) => new(p.X + x0 * Cell - e, p.Y + y0 * Cell - e, (x1 - x0 + 1) * Cell + 2 * e, (y1 - y0 + 1) * Cell + 2 * e);
            p.Bad |= keep.Any(r => Overlap(r, Box(f.MinX, f.MinY, f.MaxX, f.MaxY)) && f.Edge.Any(c => Overlap(r, Box(c.X, c.Y, c.X, c.Y))));
        }
        // Sel dan jarak sama dengan grid hunian (Occupancy, Stamp), jadi hasil Snap dan susun otomatis tidak pernah ditandai.
        (int X, int Y) Origin(Placed p) => ((int)Math.Round((p.X - margin) / Cell), (int)Math.Round((p.Y - margin) / Cell));
        var owner = new Dictionary<(int, int), Placed>();
        for (int i = 0; i < ps.Count; i++)
        {
            var p = ps[i];
            var (ox, oy) = Origin(p);
            foreach (var (x, y) in feet[i].Cells)
            {
                if (owner.TryGetValue((ox + x, oy + y), out var o) && o != p) o.Bad = p.Bad = true;
                else owner[(ox + x, oy + y)] = p;
            }
        }
        int gc = CellsOf(gap);
        if (gc > 0)
            for (int i = 0; i < ps.Count; i++)
            {
                var p = ps[i];
                var (ox, oy) = Origin(p);
                foreach (var (x, y) in feet[i].Edge)
                    for (int dy = -gc; dy <= gc; dy++)
                        for (int dx = -gc; dx <= gc; dx++)
                            if (owner.TryGetValue((ox + x + dx, oy + y + dy), out var o) && o != p) o.Bad = p.Bad = true;
            }
        return ps.Count(p => p.Bad);
    }

    public static Transform Xf(Placed p)
    {
        var (tx, ty) = p.T;
        return new TransformGroup { Children = { new RotateTransform(p.Rot), new TranslateTransform(p.X + tx, p.Y + ty) } };
    }

    // Pratinjau saat ukuran diatur: stiker diskala kx mendatar dan ky tegak (arah di lembar), dengan pojok kiri atas kotaknya di (x, y).
    public static Transform Xf(Placed p, double kx, double ky, double x, double y)
    {
        var (tx, ty) = p.T;
        var (sx, sy) = p.Rot % 180 == 0 ? (kx, ky) : (ky, kx); // skala di sumbu kotak stiker, sebelum diputar
        return new TransformGroup { Children = { new ScaleTransform(sx, sy), new RotateTransform(p.Rot), new TranslateTransform(x + tx * kx, y + ty * ky) } };
    }

    static readonly Brush KissBrush = Frozen("#EC008C"), DieBrush = Frozen("#00AEEF");
    internal static readonly Brush SelBrush = Frozen("#4A90FF");
    static Brush Frozen(string c) { var b = (Brush)new BrushConverter().ConvertFromString(c)!; b.Freeze(); return b; }
    static Brush Frozen(Color c) { var b = new SolidColorBrush(c); b.Freeze(); return b; }

    // Satuan kanvas = mm. Tag kanvas = indeks lembar, Tag stiker = Placed. sels null = tanpa pilihan (mis. preview/export).
    public static Canvas Sheet(IEnumerable<Placed> ps, double cw, double ch, double margin, IReadOnlyCollection<Placed>? sels, int index, bool regMarks = false)
    {
        // Tanpa pembulatan tata letak: pembulatan milik jendela menghitung per piksel, padahal satuan lembar mm, jadi gambar bergeser dari garis potong.
        var root = new Canvas { Width = cw, Height = ch, Background = Brushes.White, ClipToBounds = true, Tag = index, UseLayoutRounding = false };
        root.Children.Add(new System.Windows.Shapes.Rectangle
        {
            Width = Math.Max(0, cw - 2 * margin), Height = Math.Max(0, ch - 2 * margin), Stroke = Brushes.LightGray, StrokeThickness = 0.3,
            StrokeDashArray = [4, 4], Margin = new Thickness(margin, margin, 0, 0), IsHitTestVisible = false
        });
        foreach (var p in ps)
        {
            var s = p.S;
            var c = new Canvas { Width = s.W, Height = s.H, Background = Brushes.Transparent, Tag = p, RenderTransform = Xf(p) };
            if (s.BorderGeo is { } bgeo) c.Children.Add(new System.Windows.Shapes.Path { Data = bgeo, Fill = Frozen(s.Border!.Value.Rgb), IsHitTestVisible = false });
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
            if (sels != null && (p.Bad || sels.Contains(p))) // bingkai merah dan pilihan hanya untuk layar, tidak ikut pratinjau untuk customer
                c.Children.Add(new System.Windows.Shapes.Rectangle
                {
                    Width = s.W, Height = s.H, Stroke = p.Bad ? Brushes.Red : SelBrush, StrokeThickness = p.Bad ? 0.8 : 0.6,
                    StrokeDashArray = p.Bad ? null : [3, 2], IsHitTestVisible = false
                });
            root.Children.Add(c);
        }
        if (regMarks) RegistrationMarks.Draw(root, cw, ch, margin);
        return root;
    }
}

public static class RegistrationMarks
{
    // ponytail: nilai dari Graphtec CE7000 User's Manual (CE7000-UM-151) bab 5: tebal garis 0,3-1,0 mm, ukuran tanda 5-20 mm,
    // area pindai a = 6 mm di sekitar tanda tidak boleh dicetak (masukan leader 2026-10-26: stiker kena tanda). Sesuaikan dengan
    // mesin dan pengaturan Cutting Master 5 operator.
    public const double Length = 10;
    public const double Clearance = 1;
    public const double Stroke = 1;
    public const double ScanClear = 6;

    // Garis tanda (mm lembar): siku L di 4 pojok, di luar area cetak sejauh Clearance. Kosong bila margin atau lembar terlalu kecil.
    public static List<(Point A, Point B)> Lines(double width, double height, double margin)
    {
        if (margin <= Clearance || width <= 2 * (margin - Clearance + Length) || height <= 2 * (margin - Clearance + Length)) return [];
        double l = margin - Clearance, t = margin - Clearance, r = width - margin + Clearance, b = height - margin + Clearance;
        double half = Stroke / 2.0; // perpanjangan sudut agar garis saling bertemu
        return [
            (new(l, t),        new(l + Length, t)),          // TL horizontal
            (new(l, t - half), new(l, t + Length)),          // TL vertikal ← diperpanjang ke atas
            (new(r, t),        new(r - Length, t)),          // TR horizontal
            (new(r, t - half), new(r, t + Length)),          // TR vertikal ← diperpanjang ke atas
            (new(l, b),        new(l + Length, b)),          // BL horizontal
            (new(l, b + half), new(l, b - Length)),          // BL vertikal ← diperpanjang ke bawah
            (new(r, b),        new(r - Length, b)),          // BR horizontal
            (new(r, b + half), new(r, b - Length))           // BR vertikal ← diperpanjang ke bawah
        ];
    }

    // Satu sudut sebagai satu path: ujung horizontal (A), titik sudut (C), ujung vertikal (B).
    // Dipakai export PDF agar tiap siku jadi satu subpath (vektor benar-benar menyambung, bukan dua garis berpotongan).
    public static List<(Point A, Point C, Point B)> Corners(double width, double height, double margin)
    {
        if (margin <= Clearance || width <= 2 * (margin - Clearance + Length) || height <= 2 * (margin - Clearance + Length)) return [];
        double l = margin - Clearance, t = margin - Clearance, r = width - margin + Clearance, b = height - margin + Clearance;
        return [
            (new(l + Length, t), new(l, t), new(l, t + Length)),   // TL
            (new(r - Length, t), new(r, t), new(r, t + Length)),   // TR
            (new(l + Length, b), new(l, b), new(l, b - Length)),   // BL
            (new(r - Length, b), new(r, b), new(r, b - Length))    // BR
        ];
    }

    // Area pindai: tiap garis tanda diperlebar setengah tebal garis + ScanClear. Stiker tidak boleh masuk ke sini.
    public static IEnumerable<Rect> Zones(double width, double height, double margin) => Lines(width, height, margin).Select(s =>
    {
        var r = new Rect(s.A, s.B);
        r.Inflate(Stroke / 2 + ScanClear, Stroke / 2 + ScanClear);
        return r;
    });

    public static void Draw(Canvas canvas, double width, double height, double margin)
    {
        foreach (var (a, b) in Lines(width, height, margin))
            canvas.Children.Add(new System.Windows.Shapes.Line
            {
                X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = Brushes.Black, StrokeThickness = Stroke, IsHitTestVisible = false
            });
    }
}
