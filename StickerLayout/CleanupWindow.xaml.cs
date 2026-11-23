using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StickerLayout;

public partial class CleanupWindow : Window
{
    const int ViewMax = 1600; // layar cukup 1600 px; hasil tetap resolusi penuh
    readonly CleanImage img;
    readonly WriteableBitmap wb;
    readonly byte[] view;
    readonly int vw, vh;

    // Hasil (PNG dengan transparansi) bila operator menekan Pakai.
    public byte[]? Result { get; private set; }

    public CleanupWindow(byte[] image, string name)
    {
        InitializeComponent();
        Title = Lang.T("CleanTitle", name);
        img = new CleanImage(image);
        double k = Math.Min(1, (double)ViewMax / Math.Max(img.W, img.H));
        vw = Math.Max(1, (int)(img.W * k));
        vh = Math.Max(1, (int)(img.H * k));
        view = new byte[vw * vh * 4];
        wb = new WriteableBitmap(vw, vh, 96, 96, PixelFormats.Bgra32, null);
        View.Source = wb;
        Refresh();
    }

    int TolValue => (int)Tol.Value;
    int EdgeValue => (int)Edge.Value;

    void Refresh()
    {
        img.ComposeView(EdgeValue, view, vw, vh);
        wb.WritePixels(new Int32Rect(0, 0, vw, vh), view, vw * 4, 0);
        Info.Text = Lang.T("CleanInfo", img.W, img.H, img.ErasedFraction);
        UndoBtn.IsEnabled = ResetBtn.IsEnabled = img.Changed;
        TolText.Text = TolValue.ToString();
        EdgeText.Text = EdgeValue.ToString();
    }

    bool busy;

    // Hitungan berat jalan di background supaya jendela tetap merespons; isi jendela dikunci selama proses.
    async void Run(Func<int> op)
    {
        if (busy) return;
        busy = true;
        var content = (UIElement)Content;
        content.IsEnabled = false;
        Mouse.OverrideCursor = Cursors.Wait;
        try { if (await Task.Run(op) > 0) Refresh(); }
        finally { content.IsEnabled = true; Mouse.OverrideCursor = null; busy = false; }
    }

    // Nilai slider dibaca di UI thread; hitungannya sendiri jalan di background.
    void Background_Click(object s, RoutedEventArgs e) { int tol = TolValue; Run(() => img.EraseFromBorder(tol)); }

    // Crop 4 sisi (daftar tugas 2026-11-20): bingkai mulai seukuran gambar; tiap sisi atau pojok ditarik, lalu Terapkan crop menghapus di luar bingkai.
    Rect crop;              // bingkai crop dalam piksel gambar
    int cropEdges;          // sisi yang sedang ditarik: 1 kiri, 2 kanan, 4 atas, 8 bawah (pojok = gabungan dua sisi)
    const double Grip = 8;  // jarak tangkap sisi bingkai (px layar)

    void Crop_Toggled(object s, RoutedEventArgs e)
    {
        crop = new Rect(0, 0, img.W, img.H);
        CropApply.IsEnabled = false;
        if (CropBox.IsChecked != true) Board.Cursor = Cursors.Cross;
        ShowCrop();
    }

    void ShowCrop()
    {
        bool on = CropBox.IsChecked == true;
        CropRect.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        CropDots.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (!on) return;
        Canvas.SetLeft(CropRect, ox + crop.X * scale);
        Canvas.SetTop(CropRect, oy + crop.Y * scale);
        CropRect.Width = crop.Width * scale;
        CropRect.Height = crop.Height * scale;

        // Titik di empat pojok bingkai crop (sama gaya dengan HandleDots di CutEditWindow).
        double l = ox + crop.Left * scale, r = ox + crop.Right * scale, t = oy + crop.Top * scale, b = oy + crop.Bottom * scale;
        const double dot = 3;
        var dots = new GeometryGroup();
        dots.Children.Add(new EllipseGeometry(new Point(l, t), dot, dot));
        dots.Children.Add(new EllipseGeometry(new Point(r, t), dot, dot));
        dots.Children.Add(new EllipseGeometry(new Point(r, b), dot, dot));
        dots.Children.Add(new EllipseGeometry(new Point(l, b), dot, dot));
        CropDots.Data = dots;
    }

    // Sisi bingkai di dekat titik layar p; di pojok dua sisi sekaligus.
    int EdgesAt(Point p)
    {
        double l = ox + crop.Left * scale, r = ox + crop.Right * scale, t = oy + crop.Top * scale, b = oy + crop.Bottom * scale;
        bool alongY = p.Y > t - Grip && p.Y < b + Grip, alongX = p.X > l - Grip && p.X < r + Grip;
        int edges = 0;
        if (alongY && Math.Abs(p.X - l) <= Grip) edges |= 1;
        else if (alongY && Math.Abs(p.X - r) <= Grip) edges |= 2;
        if (alongX && Math.Abs(p.Y - t) <= Grip) edges |= 4;
        else if (alongX && Math.Abs(p.Y - b) <= Grip) edges |= 8;
        return edges;
    }

    void CropApply_Click(object s, RoutedEventArgs e)
    {
        var r = crop;
        CropBox.IsChecked = false; // Crop_Toggled menyembunyikan bingkai
        CropPixels(r);
    }

    void View_Down(object s, MouseButtonEventArgs e)
    {
        var p = e.GetPosition(Board);
        if (CropBox.IsChecked == true)
        {
            cropEdges = EdgesAt(p);
            if (cropEdges != 0) Board.CaptureMouse();
            return;
        }
        int x = (int)((p.X - ox) / scale), y = (int)((p.Y - oy) / scale), tol = TolValue;
        if (x < 0 || y < 0 || x >= img.W || y >= img.H) return; // klik di luar gambar
        Run(() => img.EraseAt(x, y, tol));
    }

    void View_Up(object s, MouseButtonEventArgs e)
    {
        if (cropEdges == 0) return;
        cropEdges = 0;
        Board.ReleaseMouseCapture();
    }

    internal void CropPixels(Rect px)
    {
        var k = new Int32Rect((int)Math.Floor(px.X), (int)Math.Floor(px.Y), 0, 0);
        k.Width = (int)Math.Ceiling(px.Right) - k.X;
        k.Height = (int)Math.Ceiling(px.Bottom) - k.Y;
        Run(() => img.CropTo(k));
    }

    void Undo_Click(object s, RoutedEventArgs e) { if (img.Undo()) Refresh(); }

    // Esc (Batal) atau X sesudah menghapus latar: tanya dulu, supaya satu Esc nyasar tidak membuang hasil Rapikan.
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (IsVisible && DialogResult != true && img.Changed
            && MessageBox.Show(this, Lang.T("DiscardEdits"), App.Brand, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) e.Cancel = true;
        base.OnClosing(e);
    }
    void Reset_Click(object s, RoutedEventArgs e) { img.Reset(); Refresh(); }
    void Tol_Changed(object s, RoutedPropertyChangedEventArgs<double> e) { if (TolText != null) TolText.Text = TolValue.ToString(); }
    void Edge_Changed(object s, RoutedPropertyChangedEventArgs<double> e) { if (img != null) Refresh(); }

    void Apply_Click(object s, RoutedEventArgs e)
    {
        if (img.Changed || EdgeValue > 0) Result = img.ToPng(EdgeValue);
        DialogResult = Result != null;
    }

    // Tata letak gambar seperti editor garis potong: pas-papan × zoom, dipusatkan + geseran panX/panY. Piksel gambar = (layar - o) / scale.
    double userZoom = 1, scale = 1, ox, oy, panX, panY;
    Point panFrom;
    bool panning;

    void Board_Size(object s, SizeChangedEventArgs e) => Layout();

    void Layout()
    {
        if (img == null) return;
        double pad = 28, fit = Math.Max(1e-3, Math.Min((Board.ActualWidth - 2 * pad) / img.W, (Board.ActualHeight - 2 * pad) / img.H));
        scale = fit * userZoom;
        // Batasi geseran: gambar tidak boleh ditarik keluar dari papan.
        panX = Clamp1(panX, img.W * scale, Board.ActualWidth);
        panY = Clamp1(panY, img.H * scale, Board.ActualHeight);
        ox = (Board.ActualWidth - img.W * scale) / 2 + panX;
        oy = (Board.ActualHeight - img.H * scale) / 2 + panY;
        Place(ViewBg); Place(View);
        ShowCrop(); // bingkai crop ikut zoom dan geser
    }

    void Place(FrameworkElement e)
    {
        Canvas.SetLeft(e, ox);
        Canvas.SetTop(e, oy);
        e.Width = Math.Max(0, img.W * scale);
        e.Height = Math.Max(0, img.H * scale);
    }

    // Geseran maksimum satu sumbu: setengah selisih ukuran gambar dan papan, plus margin supaya tepi gambar berhenti sebelum tepi papan.
    static double Clamp1(double pan, double content, double board)
    {
        double max = Math.Max(0, (content - board) / 2) + 28;
        return Math.Clamp(pan, -max, max);
    }

    // Klik kanan tahan + seret: geser gambar (kiri tetap untuk hapus).
    void View_PanDown(object s, MouseButtonEventArgs e)
    {
        panFrom = e.GetPosition(Board);
        panning = Board.CaptureMouse();
    }

    void View_PanMove(object s, MouseEventArgs e)
    {
        if (CropBox.IsChecked == true && !panning)
        {
            var q = e.GetPosition(Board);
            if (cropEdges != 0 && e.LeftButton != MouseButtonState.Pressed) { cropEdges = 0; Board.ReleaseMouseCapture(); } // tombol dilepas di luar jendela
            int edges = cropEdges != 0 ? cropEdges : EdgesAt(q);
            Board.Cursor = edges switch { 1 or 2 => Cursors.SizeWE, 4 or 8 => Cursors.SizeNS, 5 or 10 => Cursors.SizeNWSE, 6 or 9 => Cursors.SizeNESW, _ => Cursors.Arrow };
            if (cropEdges == 0) return;
            double x = Math.Clamp((q.X - ox) / scale, 0, img.W), y = Math.Clamp((q.Y - oy) / scale, 0, img.H);
            double l = crop.Left, r = crop.Right, t = crop.Top, b = crop.Bottom;
            if ((cropEdges & 1) != 0) l = Math.Min(x, r - 1);
            if ((cropEdges & 2) != 0) r = Math.Max(x, l + 1);
            if ((cropEdges & 4) != 0) t = Math.Min(y, b - 1);
            if ((cropEdges & 8) != 0) b = Math.Max(y, t + 1);
            crop = new Rect(new Point(l, t), new Point(r, b));
            CropApply.IsEnabled = crop.Width < img.W || crop.Height < img.H;
            ShowCrop();
            return;
        }
        if (!panning) return;
        var p = e.GetPosition(Board);
        panX += p.X - panFrom.X;
        panY += p.Y - panFrom.Y;
        panFrom = p;
        Layout();
    }

    void View_PanUp(object s, MouseButtonEventArgs e)
    {
        if (!panning) return;
        Board.ReleaseMouseCapture();
        panning = false;
    }

    // Ctrl+scroll: zoom 1x–8x, titik di bawah kursor tetap.
    void View_Wheel(object s, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        var p = e.GetPosition(Board);
        double px = (p.X - ox) / scale, py = (p.Y - oy) / scale; // piksel gambar di bawah kursor
        userZoom = Math.Clamp(userZoom * (e.Delta > 0 ? 1.1 : 1 / 1.1), 1, 8);
        Layout();
        panX += p.X - (ox + px * scale); // jaga titik itu tetap di bawah kursor
        panY += p.Y - (oy + py * scale);
        Layout();
        e.Handled = true;
    }

    void Window_KeyDown(object s, KeyEventArgs e)
    {
        if (busy || Keyboard.Modifiers != ModifierKeys.Control || e.Key != Key.Z) return;
        if (img.Undo()) Refresh();
        e.Handled = true;
    }

    void Window_SourceInitialized(object s, EventArgs e) => App.DarkTitle(this);
}
