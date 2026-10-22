using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace StickerLayout;

// Editor garis potong (masukan leader 2026-10-21, contoh Roboflow): garis kiss/die jadi polygon dengan titik yang bisa
// digeser, ditambah (klik di garis), dan dihapus (Delete). Koordinat polygon: mm relatif terhadap pojok kiri-atas artwork.
public partial class CutEditWindow : Window
{
    sealed record State(List<Point[]> Kiss, List<Point[]> Die, bool Auto);

    readonly Sticker auto;      // desain dengan garis otomatis pada pengaturan sekarang: sumber Simplify dan Reset otomatis
    readonly double reach;      // titik tidak bisa keluar dari kotak desain otomatis (grid nesting tetap benar)
    List<Point[]> kiss, die;
    bool isAuto;                // belum diedit: Simpan mempertahankan garis otomatis (kurva)
    readonly List<State> undo = [], redo = [];
    string? merging;            // geser slider berturut-turut = satu langkah undo
    (int C, int I)? sel;
    State? before;              // keadaan sebelum titik diseret atau ditambah
    bool dragging, moved, ready;
    double scale = 1, ox, oy;

    // Hasil: polygon garis yang aktif, atau null = garis otomatis. Garis yang sedang mati kembali otomatis.
    public List<Point[]>? ManualKiss => isAuto || !auto.LoadedKiss.On ? null : kiss;
    public List<Point[]>? ManualDie => isAuto || !auto.LoadedDie.On ? null : die;

    public CutEditWindow(Sticker auto, List<Point[]>? manualKiss, List<Point[]>? manualDie, string name)
    {
        InitializeComponent();
        Title = Lang.T("CutTitle", name);
        this.auto = auto;
        reach = auto.Off;
        isAuto = (manualKiss == null || !auto.LoadedKiss.On) && (manualDie == null || !auto.LoadedDie.On); // hanya garis yang tampil
        kiss = auto.LoadedKiss.On ? Copy(manualKiss ?? Polys(auto.Kiss)) : [];
        die = auto.LoadedDie.On ? Copy(manualDie ?? Polys(auto.Die)) : [];
        Art.Source = auto.Preview;
        EditKiss.IsEnabled = auto.LoadedKiss.On;
        EditDie.IsEnabled = auto.LoadedDie.On;
        (auto.LoadedKiss.On ? EditKiss : EditDie).IsChecked = true;
        ready = true;
        Changed();
    }

    bool OnDie => EditDie.IsChecked == true;
    List<Point[]> Active => OnDie ? die : kiss;
    List<Point[]> Polys(string path) => Loader.ToPolygons(path, auto.Off, Eps.Value);
    static List<Point[]> Copy(List<Point[]> p) => [.. p.Select(c => (Point[])c.Clone())];
    State Snap() => new(Copy(kiss), Copy(die), isAuto);
    Point Clamp(Point p) => new(Math.Clamp(p.X, -reach, auto.ArtW + reach), Math.Clamp(p.Y, -reach, auto.ArtH + reach));
    Point Screen(Point p) => new(ox + (p.X + reach) * scale, oy + (p.Y + reach) * scale);
    Point Mm(Point s) => new((s.X - ox) / scale - reach, (s.Y - oy) / scale - reach);

    void Push(State s)
    {
        undo.Add(s);
        redo.Clear();
        merging = null;
    }

    void Step(List<State> from, List<State> to)
    {
        if (from.Count == 0 || dragging) return;
        to.Add(Snap());
        (kiss, die, isAuto) = from[^1];
        from.RemoveAt(from.Count - 1);
        sel = null;
        merging = null;
        Changed();
    }

    internal void Undo() => Step(undo, redo);
    internal void Redo() => Step(redo, undo);

    // Klik: titik terdekat (dalam tol mm) dipilih dan bisa diseret; klik di garis menambah titik di situ.
    internal void Press(Point q, double tol)
    {
        before = Snap();
        moved = false;
        sel = null;
        var a = Active;
        double best = tol;
        for (int c = 0; c < a.Count; c++)
            for (int i = 0; i < a[c].Length; i++)
                if ((a[c][i] - q).Length is var d && d <= best) (best, sel) = (d, (c, i));
        if (sel == null)
        {
            (int C, int I, Point P)? seg = null;
            best = tol * 0.7;
            for (int c = 0; c < a.Count; c++)
                for (int i = 0; i < a[c].Length; i++)
                {
                    var n = Loader.Nearest(q, a[c][i], a[c][(i + 1) % a[c].Length]);
                    if ((n - q).Length is var d && d <= best) (best, seg) = (d, (c, i, n));
                }
            if (seg is (int sc, int si, Point sp))
            {
                a[sc] = [.. a[sc][..(si + 1)], Clamp(sp), .. a[sc][(si + 1)..]];
                sel = (sc, si + 1);
                moved = true;
                isAuto = false;
            }
        }
        dragging = sel != null;
        Draw();
    }

    internal void DragTo(Point q)
    {
        if (!dragging || sel is not (int c, int i)) return;
        Active[c][i] = Clamp(q);
        moved = true;
        isAuto = false;
        Draw();
    }

    internal void Release()
    {
        if (!dragging) return;
        dragging = false;
        if (moved) Push(before!);
        Changed();
    }

    // Delete: hapus titik terpilih. Kontur 3 titik ikut terhapus (misalnya garis dalam yang tidak diinginkan),
    // asal garis masih punya kontur lain.
    internal void DeletePoint()
    {
        if (dragging || sel is not (int c, int i)) return;
        var a = Active;
        if (a[c].Length <= 3 && a.Count == 1) return;
        Push(Snap());
        if (a[c].Length <= 3) a.RemoveAt(c);
        else a[c] = [.. a[c].Where((_, j) => j != i)];
        sel = null;
        isAuto = false;
        Changed();
    }

    internal void Reset()
    {
        Push(Snap());
        kiss = auto.LoadedKiss.On ? Polys(auto.Kiss) : [];
        die = auto.LoadedDie.On ? Polys(auto.Die) : [];
        isAuto = true;
        sel = null;
        Changed();
    }

    // Slider Simplify membuat ulang garis yang sedang diedit dari kurva otomatis.
    void Eps_Changed(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready) return;
        string tag = OnDie ? "die" : "kiss";
        if (merging != tag) { Push(Snap()); merging = tag; }
        if (OnDie) die = Polys(auto.Die); else kiss = Polys(auto.Kiss);
        sel = null;
        isAuto = false;
        Changed();
    }

    // Kiss harus di dalam die: pisau kiss tidak boleh memotong di luar stiker.
    internal bool KissInside => kiss.Count == 0 || die.Count == 0
        || Geometry.Combine(Geometry.Parse(Loader.PathOf(kiss, 0)), Geometry.Parse(Loader.PathOf(die, 0)), GeometryCombineMode.Exclude, null).GetArea() < 0.01;

    internal bool CanSave => SaveBtn.IsEnabled;

    void Changed()
    {
        bool inside = KissInside;
        Warn.Visibility = inside ? Visibility.Collapsed : Visibility.Visible;
        SaveBtn.IsEnabled = isAuto || inside;
        UndoBtn.IsEnabled = undo.Count > 0;
        RedoBtn.IsEnabled = redo.Count > 0;
        ResetBtn.IsEnabled = !isAuto;
        EpsText.Text = $"{Eps.Value:0.00} mm";
        Info.Text = Lang.T("CutInfo", kiss.Sum(c => c.Length), die.Sum(c => c.Length), Lang.T(isAuto ? "CutAuto" : "CutManual"));
        Draw();
    }

    // Semua digambar dalam piksel layar: titik tetap berukuran sama berapa pun skala desain.
    void Draw()
    {
        double bw = auto.ArtW + 2 * reach, bh = auto.ArtH + 2 * reach, pad = 28;
        scale = Math.Max(1e-3, Math.Min((Board.ActualWidth - 2 * pad) / bw, (Board.ActualHeight - 2 * pad) / bh));
        ox = (Board.ActualWidth - bw * scale) / 2;
        oy = (Board.ActualHeight - bh * scale) / 2;
        Place(ArtBg, Screen(new Point(0, 0)), auto.ArtW * scale, auto.ArtH * scale);
        Place(Art, Screen(new Point(0, 0)), auto.ArtW * scale, auto.ArtH * scale);
        Place(Box, Screen(new Point(-reach, -reach)), bw * scale, bh * scale);
        KissPath.Data = Geo(kiss, 0);
        DiePath.Data = Geo(die, 0);
        KissPath.Opacity = OnDie ? 0.4 : 1;
        DiePath.Opacity = OnDie ? 1 : 0.4;
        Handles.Stroke = SelHandle.Fill = (Brush)FindResource(OnDie ? "DieBrush" : "KissBrush");
        Handles.Data = Geo(Active, 3.5);
        SelHandle.Data = sel is (int c, int i) ? Geo([[Active[c][i]]], 5) : null;
    }

    static void Place(FrameworkElement e, Point p, double w, double h)
    {
        Canvas.SetLeft(e, p.X);
        Canvas.SetTop(e, p.Y);
        e.Width = Math.Max(0, w);
        e.Height = Math.Max(0, h);
    }

    // r = 0: garis polygon tertutup; r > 0: kotak titik berukuran 2r px.
    Geometry Geo(List<Point[]> polys, double r)
    {
        var g = new StreamGeometry();
        using (var ctx = g.Open())
            foreach (var c in polys)
                if (r > 0)
                    foreach (var p in c.Select(Screen))
                    {
                        ctx.BeginFigure(new Point(p.X - r, p.Y - r), true, true);
                        ctx.PolyLineTo([new(p.X + r, p.Y - r), new(p.X + r, p.Y + r), new(p.X - r, p.Y + r)], true, false);
                    }
                else
                {
                    ctx.BeginFigure(Screen(c[0]), false, true);
                    ctx.PolyLineTo([.. c.Skip(1).Select(Screen)], true, false);
                }
        g.Freeze();
        return g;
    }

    void Board_Size(object s, SizeChangedEventArgs e) => Draw();

    void Board_Down(object s, MouseButtonEventArgs e)
    {
        Press(Mm(e.GetPosition(Board)), 7 / scale);
        if (dragging) Board.CaptureMouse();
    }

    void Board_Move(object s, MouseEventArgs e) => DragTo(Mm(e.GetPosition(Board)));

    void Board_Up(object s, MouseButtonEventArgs e)
    {
        Board.ReleaseMouseCapture();
        Release();
    }

    void Line_Checked(object s, RoutedEventArgs e)
    {
        if (!ready) return;
        sel = null;
        Draw();
    }

    void Undo_Click(object s, RoutedEventArgs e) => Undo();
    void Redo_Click(object s, RoutedEventArgs e) => Redo();
    void Reset_Click(object s, RoutedEventArgs e) => Reset();
    void Save_Click(object s, RoutedEventArgs e) => DialogResult = true;

    void Window_KeyDown(object s, KeyEventArgs e)
    {
        var m = Keyboard.Modifiers;
        if (e.Key == Key.Delete && m == ModifierKeys.None) DeletePoint();
        else if (e.Key == Key.Z && m == ModifierKeys.Control) Undo();
        else if (e.Key == Key.Y && m == ModifierKeys.Control || e.Key == Key.Z && m == (ModifierKeys.Control | ModifierKeys.Shift)) Redo();
        else return;
        e.Handled = true;
    }

    void Window_SourceInitialized(object s, EventArgs e) => App.DarkTitle(this);
}
