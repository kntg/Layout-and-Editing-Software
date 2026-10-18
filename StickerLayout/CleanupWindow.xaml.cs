using System.Windows;
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

    void View_Down(object s, MouseButtonEventArgs e)
    {
        var p = e.GetPosition(View);
        int x = (int)(p.X / View.ActualWidth * img.W), y = (int)(p.Y / View.ActualHeight * img.H), tol = TolValue;
        Run(() => img.EraseAt(x, y, tol));
    }

    void Undo_Click(object s, RoutedEventArgs e) { if (img.Undo()) Refresh(); }
    void Reset_Click(object s, RoutedEventArgs e) { img.Reset(); Refresh(); }
    void Tol_Changed(object s, RoutedPropertyChangedEventArgs<double> e) { if (TolText != null) TolText.Text = TolValue.ToString(); }
    void Edge_Changed(object s, RoutedPropertyChangedEventArgs<double> e) { if (img != null) Refresh(); }

    void Apply_Click(object s, RoutedEventArgs e)
    {
        if (img.Changed || EdgeValue > 0) Result = img.ToPng(EdgeValue);
        DialogResult = Result != null;
    }

    void Window_KeyDown(object s, KeyEventArgs e)
    {
        if (busy || Keyboard.Modifiers != ModifierKeys.Control || e.Key != Key.Z) return;
        if (img.Undo()) Refresh();
        e.Handled = true;
    }

    void Window_SourceInitialized(object s, EventArgs e) => App.DarkTitle(this);
}
