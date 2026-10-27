using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace StickerLayout;

// Editor garis potong (masukan leader 2026-10-21, contoh Roboflow; node seperti CorelDRAW sejak 2026-10-23): garis kiss/die
// jadi node lurus atau kurva yang bisa digeser, ditambah (klik di garis/kurva), dihapus (Delete), dan diatur handle-nya.
// Preset bentuk (kotak, lingkaran, dll.) mengelilingi artwork. Koordinat node: mm relatif terhadap pojok kiri-atas artwork.
public partial class CutEditWindow : Window
{
    sealed record State(List<Node[]> Kiss, List<Node[]> Die, bool Auto, CutShape Shape, CutShape DieShape, Point[] Objs, Point[] Holes);

    readonly Sticker auto;      // desain dengan garis otomatis pada pengaturan sekarang: sumber Simplify, bentuk, dan Reset otomatis
    double reach;               // node dan handle tidak bisa keluar sejauh ini dari artwork (kotak desain otomatis atau bentuk)
    List<Node[]> kiss, die;
    bool isAuto;                // belum diedit: Simpan mempertahankan garis otomatis (kurva) atau bentuk preset
    CutShape shape;             // dasar garis kiss: Auto = garis otomatis, selain itu preset bentuk
    CutShape dieShape;          // dasar garis die: Auto = mengikuti kiss (garis otomatis atau bentuk yang sama)
    readonly List<State> undo = [], redo = [];
    string? merging;            // geser slider berturut-turut = satu langkah undo
    readonly List<(int C, int I)> sel = []; // node terpilih (Shift+klik menambah)
    (int C, int J, bool Out)? hsel; // handle yang sedang diseret: node pemilik dan sisinya
    (int C, int I)? grabbed;    // node yang digenggam: semua node terpilih bergeser sejauh node ini
    Point pressAt;              // titik klik: seret baru dimulai setelah mouse bergeser lebih dari slop
    Vector grab;                // node/handle yang digenggam dikurangi titik klik, supaya node tidak melompat ke kursor
    double slop;
    bool collapse;              // klik node dalam grup tanpa Shift dan tanpa geser: tinggal node itu yang terpilih
    State? before;              // keadaan sebelum node/handle diseret atau node ditambah
    bool dragging, moved, ready, syncing;
    double scale = 1, ox, oy;
    List<Point> objs = [], holes = [];  // pilihan smart select: klik objek dan klik lubang (mm relatif artwork)
    Cmyk? border;               // warna border desain (null = kertas putih)

    // Warna border siap pakai (CMYK 0..1); selain ini lewat kotak CMYK.
    static readonly (string Key, Cmyk? Color)[] Swatches = [("BorderNone", null), ("BorderRed", new(0, 1, 1, 0)), ("BorderBlack", new(0, 0, 0, 1)),
        ("BorderBlue", new(1, 0.6, 0, 0)), ("BorderYellow", new(0, 0, 1, 0)), ("BorderGreen", new(0.8, 0, 1, 0))];

    // Hasil: node garis yang aktif, atau null = garis otomatis/bentuk. Garis yang sedang mati kembali otomatis.
    public List<Node[]>? ManualKiss => isAuto || !auto.LoadedKiss.On ? null : kiss;
    public List<Node[]>? ManualDie => isAuto || !auto.LoadedDie.On ? null : die;
    public CutShape Shape => isAuto ? shape : CutShape.Auto;
    public CutShape DieShape => isAuto ? dieShape : CutShape.Auto;
    public Cmyk? Border => border;

    public CutEditWindow(Sticker auto, List<Node[]>? manualKiss, List<Node[]>? manualDie, CutShape shape, string name, Cmyk? border = null,
        CutShape dieShape = CutShape.Auto)
    {
        InitializeComponent();
        Title = Lang.T("CutTitle", name);
        this.auto = auto;
        isAuto = (manualKiss == null || !auto.LoadedKiss.On) && (manualDie == null || !auto.LoadedDie.On); // hanya garis yang tampil
        this.shape = isAuto ? shape : CutShape.Auto;
        this.dieShape = isAuto ? dieShape : CutShape.Auto;
        var (k, d) = Base(this.shape, this.dieShape);
        kiss = auto.LoadedKiss.On ? Copy(isAuto ? k : manualKiss ?? k) : [];
        die = auto.LoadedDie.On ? Copy(isAuto ? d : manualDie ?? d) : [];
        reach = Reach();
        Art.Source = auto.Preview;
        EditKiss.IsEnabled = auto.LoadedKiss.On;
        EditDie.IsEnabled = auto.LoadedDie.On;
        (auto.LoadedKiss.On ? EditKiss : EditDie).IsChecked = true;
        Tol.Value = Loader.DefaultTolerance(auto);
        foreach (var s in Enum.GetValues<CutShape>())
        {
            var icon = new System.Windows.Shapes.Path { Data = Shapes.Icon(s), Stretch = Stretch.Uniform, Width = 18, Height = 18, StrokeThickness = 1.4 };
            var rb = new RadioButton { Style = (Style)FindResource("Tool"), GroupName = "Shape", Tag = s, Content = icon, ToolTip = Lang.T("Shape" + s) };
            icon.SetBinding(System.Windows.Shapes.Shape.StrokeProperty, new Binding(nameof(Foreground)) { Source = rb });
            AutomationProperties.SetName(rb, Lang.T("Shape" + s));
            rb.Checked += Shape_Checked;
            ShapePanel.Children.Add(rb);
        }
        foreach (var (key, color) in Swatches)
        {
            var chip = new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(3), BorderThickness = new Thickness(1), BorderBrush = Brushes.Gray,
                Background = color is { } c ? new SolidColorBrush(c.Rgb) : (Brush)FindResource("Checker") };
            var rb = new RadioButton { Style = (Style)FindResource("Tool"), GroupName = "Border", Tag = color, Content = chip, ToolTip = Lang.T(key) };
            AutomationProperties.SetName(rb, Lang.T(key));
            rb.Checked += Swatch_Checked;
            BorderPanel.Children.Add(rb);
        }
        this.border = border;
        ready = true;
        Changed();
    }

    bool OnDie => EditDie.IsChecked == true;
    List<Node[]> Active => OnDie ? die : kiss;
    // Garis yang diedit masih garis otomatis (bukan bentuk): Simplify bisa membuatnya ulang.
    bool ActiveAuto => shape == CutShape.Auto && (!OnDie || dieShape == CutShape.Auto);
    List<Node[]> Polys(string path) => Curves.Fit(path, auto.Off, Eps.Value);
    static List<Node[]> Copy(List<Node[]> p) => [.. p.Select(c => (Node[])c.Clone())];
    State Snap() => new(Copy(kiss), Copy(die), isAuto, shape, dieShape, [.. objs], [.. holes]);
    Point Clamp(Point p) => new(Math.Clamp(p.X, -reach, auto.ArtW + reach), Math.Clamp(p.Y, -reach, auto.ArtH + reach));
    Point Screen(Point p) => new(ox + (p.X + reach) * scale, oy + (p.Y + reach) * scale);
    Point Mm(Point s) => new((s.X - ox) / scale - reach, (s.Y - oy) / scale - reach);

    // Kotak kerja: kotak desain otomatis, atau lebih besar bila bentuk/garis keluar darinya (+1 mm ruang seret).
    double Reach()
    {
        double over = Curves.Overhang(kiss.Concat(die), auto.ArtW, auto.ArtH);
        return over > auto.Off ? over + 1 : auto.Off;
    }

    // Garis dasar: garis otomatis (Simplify) atau bentuk preset; bentuk kiss dan die bisa berbeda, die Auto mengikuti kiss.
    (List<Node[]> Kiss, List<Node[]> Die) Base(CutShape s, CutShape ds)
    {
        var (k, d) = s == CutShape.Auto && ds == CutShape.Auto ? (null, null) : Loader.ShapeLines(auto, s, ds);
        return (auto.LoadedKiss.On ? k ?? Polys(auto.Kiss) : [], auto.LoadedDie.On ? d ?? Polys(auto.Die) : []);
    }

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
        (kiss, die, isAuto, shape, dieShape, var o, var h) = from[^1];
        objs = [.. o];
        holes = [.. h];
        from.RemoveAt(from.Count - 1);
        sel.Clear();
        merging = null;
        reach = Reach();
        Changed();
    }

    internal void Undo() => Step(undo, redo);
    internal void Redo() => Step(redo, undo);

    // Handle yang tampil saat node i terpilih: In dan Out milik node itu, Out node sebelumnya, In node sesudahnya
    // (semua titik kendali dua segmen di sisinya). Handle yang sama dengan node-nya (sisi lurus) tidak tampil.
    static IEnumerable<(int J, bool Out, Point H)> HandlesOf(Node[] c, int i)
    {
        int h = (i - 1 + c.Length) % c.Length, j = (i + 1) % c.Length;
        if (c[h].Out != c[h].P) yield return (h, true, c[h].Out);
        if (c[i].In != c[i].P) yield return (i, false, c[i].In);
        if (c[i].Out != c[i].P) yield return (i, true, c[i].Out);
        if (c[j].In != c[j].P) yield return (j, false, c[j].In);
    }

    // Klik (masukan leader 2026-10-26): handle node terpilih atau node terdekat (dalam tol mm) dipilih tanpa bergeser; Shift+klik
    // menambah/melepas node dari pilihan. Klik di garis atau kurva menambah node di situ tanpa mengubah bentuk.
    internal void Press(Point q, double tol, bool add = false)
    {
        before = Snap();
        moved = collapse = false;
        hsel = null;
        grabbed = null;
        pressAt = q;
        slop = tol / 2;
        var a = Active;
        if (sel.Count == 1 && sel[0] is var (c0, i0) && c0 < a.Count && i0 < a[c0].Length)
            foreach (var (j, o, h) in HandlesOf(a[c0], i0))
                if ((h - q).Length <= tol) { hsel = (c0, j, o); grab = h - q; break; }
        if (hsel == null)
        {
            (int C, int I)? hit = null;
            double best = tol;
            for (int c = 0; c < a.Count; c++)
                for (int i = 0; i < a[c].Length; i++)
                    if ((a[c][i].P - q).Length is var d && d <= best) (best, hit) = (d, (c, i));
            if (hit is { } n)
            {
                if (add) { if (!sel.Remove(n)) sel.Add(n); }
                else if (!sel.Contains(n)) { sel.Clear(); sel.Add(n); }
                else collapse = sel.Count > 1; // klik di grup: tetap grup untuk diseret, tanpa geser tinggal node ini
                if (sel.Contains(n)) (grabbed, grab) = (n, a[n.C][n.I].P - q);
            }
            else
            {
                (int C, int I, double T)? seg = null;
                best = tol * 0.7;
                for (int c = 0; c < a.Count; c++)
                    if (Curves.NearestSegment(a[c], q) is var (i, t, d) && d <= best) (best, seg) = (d, (c, i, t));
                if (seg is (int sc, int si, double st))
                {
                    a[sc] = Curves.Split(a[sc], si, st);
                    sel.Clear();
                    sel.Add((sc, si + 1));
                    (grabbed, grab) = ((sc, si + 1), a[sc][si + 1].P - q);
                    moved = true;
                    isAuto = false;
                }
                else if (!add) sel.Clear();
            }
        }
        dragging = grabbed != null || hsel != null;
        Draw();
    }

    // Seret: node terpilih bergeser sejauh gerakan mouse (tidak melompat ke kursor), bersama handle-nya. Gerakan sampai
    // slop dari titik klik bukan seret, jadi klik saja tidak mengubah garis.
    internal void DragTo(Point q)
    {
        if (!dragging || !moved && (q - pressAt).Length <= slop) return;
        var a = Active;
        var target = Clamp(q + grab);
        if (hsel is (int c, int j, bool o)) a[c][j] = Curves.MoveHandle(a[c][j], o, target);
        else if (grabbed is (int gc, int gi))
        {
            var d = target - a[gc][gi].P;
            foreach (var (c2, i) in sel)
            {
                var n = a[c2][i];
                var dn = Clamp(n.P + d) - n.P;
                a[c2][i] = n with { P = n.P + dn, In = n.In + dn, Out = n.Out + dn };
            }
        }
        else return;
        moved = true;
        isAuto = false;
        Draw();
    }

    internal void Release()
    {
        if (!dragging) return;
        dragging = false;
        hsel = null;
        if (moved) Push(before!);
        else if (collapse && grabbed is { } g) { sel.Clear(); sel.Add(g); }
        grabbed = null;
        Changed();
    }

    // Delete: hapus semua node terpilih. Kontur yang tersisa kurang dari 3 node ikut terhapus (misalnya garis dalam yang
    // tidak diinginkan), asal garis masih punya kontur lain.
    internal void DeletePoint()
    {
        if (dragging || sel.Count == 0) return;
        List<Node[]> rest = [.. Active.Select((c, ci) => c.Where((_, i) => !sel.Contains((ci, i))).ToArray()).Where(c => c.Length >= 3)];
        if (rest.Count == 0) return;
        Push(Snap());
        if (OnDie) die = rest; else kiss = rest;
        sel.Clear();
        isAuto = false;
        Changed();
    }

    // Tombol node: Jadikan kurva/garis untuk kedua segmen di sisi node terpilih, node halus/sudut; berlaku untuk semua node terpilih.
    void EditNode(Func<Node[], int, Node[]> f)
    {
        if (dragging || sel.Count == 0) return;
        Push(Snap());
        foreach (var (c, i) in sel) Active[c] = f(Active[c], i);
        isAuto = false;
        Changed();
    }

    internal void ToCurve() => EditNode(Curves.ToCurve);
    internal void ToLine() => EditNode(Curves.ToLine);
    internal void SetSmooth(bool on) => EditNode((c, i) => Curves.SetSmooth(c, i, on));

    // Preset bentuk (Auto = garis otomatis) untuk garis yang dipilih di "Garis yang diedit" (masukan leader 2026-10-26: kiss dan
    // die bisa berbeda, misalnya kiss bintang dan die kotak). Kedua garis dibuat ulang dari bentuknya; die Auto mengikuti kiss.
    internal void ApplyShape(CutShape s) => SetShapes(OnDie ? shape : s, OnDie ? s : dieShape);

    internal void Reset() => SetShapes(CutShape.Auto, CutShape.Auto);

    void SetShapes(CutShape k, CutShape d)
    {
        Push(Snap());
        (shape, dieShape) = (k, d);
        (kiss, die) = Base(k, d);
        isAuto = true;
        objs = [];
        holes = [];
        sel.Clear();
        reach = Reach();
        Changed();
    }

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
        objs = o;
        holes = h;
        if (auto.LoadedKiss.On) kiss = k;
        if (auto.LoadedDie.On) die = d;
        shape = dieShape = CutShape.Auto;
        sel.Clear();
        isAuto = false;
        reach = Reach();
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

    // Slider Simplify membuat ulang garis yang sedang diedit dari kurva otomatis (bentuk preset tidak disederhanakan).
    void Eps_Changed(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready || !ActiveAuto) return;
        string tag = OnDie ? "die" : "kiss";
        if (merging != tag) { Push(Snap()); merging = tag; }
        if (objs.Count + holes.Count > 0) { Select(objs, holes); return; } // pilihan smart select disederhanakan ulang
        if (OnDie) die = Polys(auto.Die); else kiss = Polys(auto.Kiss);
        sel.Clear();
        isAuto = false;
        Changed();
    }

    // Kiss harus di dalam die: pisau kiss tidak boleh memotong di luar stiker.
    internal bool KissInside => kiss.Count == 0 || die.Count == 0
        || Geometry.Combine(Geometry.Parse(Curves.PathOf(kiss, 0)), Geometry.Parse(Curves.PathOf(die, 0)), GeometryCombineMode.Exclude, null).GetArea() < 0.01;

    internal bool CanSave => SaveBtn.IsEnabled;

    void Changed()
    {
        bool inside = KissInside;
        Warn.Visibility = inside ? Visibility.Collapsed : Visibility.Visible;
        SaveBtn.IsEnabled = isAuto || inside;
        UndoBtn.IsEnabled = undo.Count > 0;
        RedoBtn.IsEnabled = redo.Count > 0;
        ResetBtn.IsEnabled = !isAuto || shape != CutShape.Auto || dieShape != CutShape.Auto;
        Eps.IsEnabled = ActiveAuto;
        EpsText.Text = $"{Eps.Value:0.00} mm";
        TolText.Text = $"{Tolerance}";
        Info.Text = Lang.T("CutInfo", Curves.Count(kiss), Curves.Count(die),
            !isAuto ? Lang.T("CutManual") : Shapes.Label(shape, dieShape) is { Length: > 0 } l ? l : Lang.T("CutAuto"));
        syncing = true;
        foreach (RadioButton rb in ShapePanel.Children) rb.IsChecked = isAuto && (CutShape)rb.Tag == (OnDie ? dieShape : shape);
        foreach (RadioButton rb in BorderPanel.Children) rb.IsChecked = Equals(rb.Tag, border);
        BorderCmyk.Text = border is { } b ? string.Join(" ", new[] { b.C, b.M, b.Y, b.K }.Select(v => (v * 100).ToString("0.#", CultureInfo.InvariantCulture))) : "";
        syncing = false;
        Draw();
    }

    // Semua digambar dalam piksel layar: node dan handle tetap berukuran sama berapa pun skala desain.
    void Draw()
    {
        double bw = auto.ArtW + 2 * reach, bh = auto.ArtH + 2 * reach, pad = 28;
        scale = Math.Max(1e-3, Math.Min((Board.ActualWidth - 2 * pad) / bw, (Board.ActualHeight - 2 * pad) / bh));
        ox = (Board.ActualWidth - bw * scale) / 2;
        oy = (Board.ActualHeight - bh * scale) / 2;
        Place(ArtBg, Screen(new Point(0, 0)), auto.ArtW * scale, auto.ArtH * scale);
        // Pratinjau warna border: isi garis kiss (tanpa kiss: die); papan catur disembunyikan supaya bagian transparan terlihat berwarna.
        BorderPath.Data = border == null ? null : Geo(kiss.Count > 0 ? kiss : die, 0);
        BorderPath.Fill = border is { } bc ? new SolidColorBrush(bc.Rgb) : null;
        ArtBg.Visibility = border == null ? Visibility.Visible : Visibility.Hidden;
        Place(Art, Screen(new Point(0, 0)), auto.ArtW * scale, auto.ArtH * scale);
        Place(Box, Screen(new Point(-reach, -reach)), bw * scale, bh * scale);
        KissPath.Data = Geo(kiss, 0);
        DiePath.Data = Geo(die, 0);
        KissPath.Opacity = OnDie ? 0.4 : 1;
        DiePath.Opacity = OnDie ? 1 : 0.4;
        Handles.Stroke = SelHandle.Fill = (Brush)FindResource(OnDie ? "DieBrush" : "KissBrush");
        Handles.Data = Geo(Active, 3.5);
        var act = Active;
        var picked = sel.Where(s => s.C < act.Count && s.I < act[s.C].Length).ToList();
        SelHandle.Data = picked.Count > 0 ? Geo([[.. picked.Select(s => act[s.C][s.I])]], 5) : null;
        var lines = new StreamGeometry();
        var dots = new StreamGeometry();
        if (picked.Count == 1) // handle hanya untuk satu node terpilih
        {
            var a = act[picked[0].C];
            using var lc = lines.Open();
            using var dc = dots.Open();
            foreach (var (j, _, h) in HandlesOf(a, picked[0].I))
            {
                Point s = Screen(h), n = Screen(a[j].P);
                lc.BeginFigure(n, false, false);
                lc.LineTo(s, true, false);
                dc.BeginFigure(new Point(s.X + 3.5, s.Y), true, true);
                dc.ArcTo(new Point(s.X - 3.5, s.Y), new Size(3.5, 3.5), 0, false, SweepDirection.Clockwise, true, false);
                dc.ArcTo(new Point(s.X + 3.5, s.Y), new Size(3.5, 3.5), 0, false, SweepDirection.Clockwise, true, false);
            }
        }
        lines.Freeze();
        dots.Freeze();
        HandleLines.Data = lines;
        HandleDots.Data = dots;
        ToCurveBtn.IsEnabled = ToLineBtn.IsEnabled = SmoothBtn.IsEnabled = CuspBtn.IsEnabled = picked.Count > 0;
    }

    static void Place(FrameworkElement e, Point p, double w, double h)
    {
        Canvas.SetLeft(e, p.X);
        Canvas.SetTop(e, p.Y);
        e.Width = Math.Max(0, w);
        e.Height = Math.Max(0, h);
    }

    // r = 0: garis tertutup (lurus dan kurva) di layar; r > 0: kotak node berukuran 2r px.
    Geometry Geo(List<Node[]> lines, double r)
    {
        if (r == 0)
        {
            if (lines.Count == 0) return Geometry.Empty;
            var line = Geometry.Parse(Curves.PathOf(lines, reach)).Clone();
            line.Transform = new MatrixTransform(scale, 0, 0, scale, ox, oy); // sama dengan Screen()
            line.Freeze();
            return line;
        }
        var g = new StreamGeometry();
        using (var ctx = g.Open())
            foreach (var p in lines.SelectMany(c => c).Select(n => Screen(n.P)))
            {
                ctx.BeginFigure(new Point(p.X - r, p.Y - r), true, true);
                ctx.PolyLineTo([new(p.X + r, p.Y - r), new(p.X + r, p.Y + r), new(p.X - r, p.Y + r)], true, false);
            }
        g.Freeze();
        return g;
    }

    void Board_Size(object s, SizeChangedEventArgs e) => Draw();

    void Board_Down(object s, MouseButtonEventArgs e)
    {
        if (SmartBox.IsChecked == true) { Pick(Mm(e.GetPosition(Board)), Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)); return; }
        Press(Mm(e.GetPosition(Board)), 7 / scale, Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
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
        sel.Clear();
        Changed(); // tombol bentuk dan Simplify mengikuti garis yang dipilih
    }

    // Warna border: tombol warna atau kotak CMYK (empat angka 0-100, dipisah spasi/koma). Kosong = tanpa warna.
    internal void SetBorder(Cmyk? c)
    {
        border = c;
        Changed();
    }

    void Swatch_Checked(object s, RoutedEventArgs e)
    {
        if (ready && !syncing && s is RadioButton rb) SetBorder(rb.Tag as Cmyk?);
    }

    void BorderCmyk_LostFocus(object s, RoutedEventArgs e) => ApplyCmyk();

    void BorderCmyk_KeyDown(object s, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) ApplyCmyk();
    }

    internal void ApplyCmyk()
    {
        var v = BorderCmyk.Text.Split([' ', ',', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(t => double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ? x : double.NaN).ToArray();
        if (v.Length == 0) SetBorder(null);
        else if (v.Length == 4 && v.All(x => x is >= 0 and <= 100)) SetBorder(new Cmyk(v[0] / 100, v[1] / 100, v[2] / 100, v[3] / 100));
        else Changed(); // isian salah: kembali ke warna sekarang
    }

    void Shape_Checked(object s, RoutedEventArgs e)
    {
        if (ready && !syncing && s is RadioButton { Tag: CutShape v }) ApplyShape(v);
    }

    void Undo_Click(object s, RoutedEventArgs e) => Undo();
    void Redo_Click(object s, RoutedEventArgs e) => Redo();
    void Reset_Click(object s, RoutedEventArgs e) => Reset();
    void ToCurve_Click(object s, RoutedEventArgs e) => ToCurve();
    void ToLine_Click(object s, RoutedEventArgs e) => ToLine();
    void Smooth_Click(object s, RoutedEventArgs e) => SetSmooth(true);
    void Cusp_Click(object s, RoutedEventArgs e) => SetSmooth(false);
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
