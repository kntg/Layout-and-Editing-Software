using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace StickerLayout;

public partial class MainWindow : Window
{
    enum Tool { Move, Hand, Zoom }

    static readonly string[] Exts = [".pdf", ".ai", ".cdr", ".png", ".jpg", ".jpeg", ".svg"];
    readonly ObservableCollection<Sticker> items = [];
    readonly ScaleTransform zoom = new();
    readonly string[] startFiles;
    List<List<Placed>> sheets = [];
    List<double> sheetH = [];
    List<Canvas> canvases = [];
    double sheetW, panH, panV;
    Placed? sel;
    DragState? drag; // null = tidak sedang ditarik
    readonly HashSet<Placed> sels = [];   // semua stiker terpilih; sel = yang utama (panel info)
    System.Windows.Shapes.Rectangle? band; // kotak seret pilih (rubber-band)
    Point bandFrom;                        // titik awal rubber-band (koordinat kanvas lembar)
    List<(Sticker S, int Rot, double X, double Y, int Sheet)> clip = []; // stiker yang disalin; kosong dengan clipSheet terisi = kanvas
    int selSheet, tooBig, clipSheet = -1, sizing = -1, wrapCols = 1; // sizing: titik pojok yang sedang ditarik
    double sizeW, sizeH;
    Rect sizeBox;
    Vector sizeGrab;
    Border? sizeTip;
    readonly ScaleTransform unzoom = new(); // membatalkan zoom, supaya titik pojok dan angka ukuran tetap sama besar di layar
    Point dragGrab, panFrom; // dragGrab: titik yang dipegang, di lembar asal
    bool panning, space, altUsed; // altUsed: Alt dipakai saat menarik, jadi melepasnya tidak membuka menu
    Tool tool = Tool.Move;
    string status = "";

    bool syncingList;

    bool syncingSel;

    public MainWindow(string[] files)
    {
        startFiles = files;
        InitializeComponent();
        List.ItemsSource = items;
        PaperBox.ItemsSource = Paper.Sizes.Keys.Append("Custom").ToList();
        PaperBox.SelectedItem = "A3+"; // kertas stiker Develop (AP 55)
    }

    static double D(TextBox t, double def, double min = 0.01) => MmText.Parse(t.Text) is { } v && v >= min ? v : def;

    static string F(double v) => MmText.Show(v);

    double CW => D(CanvasW, 297, 10);
    double CH => D(CanvasH, 420, 10);
    double MarginMm => D(MarginBox, 10, 0);
    Cut Kiss => new(KissOn.IsChecked == true, D(KissOff, 2, 0), D(KissSm, 0, 0));
    Cut Die => new(DieOn.IsChecked == true, D(DieOff, 4, 0), D(DieSm, 0, 0));
    double BleedMm => D(BleedBox, 0, 0);
    double GapMm => D(GapBox, 2, 0);
    bool HasRegMarks => RegMarksToggleMenu.IsChecked;

    // Hijau = target bahan tercapai, kuning = dalam rentang target, merah = di bawah target.
    Brush UseBrush(double use, bool regular) => (Brush)FindResource(Yield.Level(use, regular) switch { 2 => "Good", 1 => "Mid", _ => "Warn" });
    Brush Dim => (Brush)FindResource("Dim");

    void Window_SourceInitialized(object s, EventArgs e) => App.DarkTitle(this);

    void Redraw(string? msg = null)
    {
        // ===== PENGAMAN GLOBAL =====
        sels.RemoveWhere(p => p.S == null);
        if (sel != null && sel.S == null) sel = null;
        // ===========================
        if (msg != null) status = msg;
        Status.Text = status;
        Status.ToolTip = status == "" ? null : status;
        canvases = [];
        var views = new List<UIElement>();
        double w = sheets.Count > 0 ? sheetW : CW;
        var paper = PaperBox.SelectedItem as string ?? "Custom";
        for (int i = 0; i < (items.Count == 0 ? 0 : Math.Max(1, sheets.Count)); i++)
        {
            double h = i < sheetH.Count ? sheetH[i] : CH;
            var ps = i < sheets.Count ? sheets[i] : [];
            var c = Nest.Sheet(ps, w, h, MarginMm, sels, i, HasRegMarks);
            c.LayoutTransform = zoom;
            if (sel != null && ps.Contains(sel) && sels.Count <= 1) AddHandles(c, sel); // di lembar stikernya, bukan lembar yang terakhir diklik
            canvases.Add(c);
            var head = new DockPanel { Margin = new Thickness(1, 0, 1, 7) };
            double su = Yield.Use(ps, w, h, MarginMm);
            var pct = new TextBlock { Text = ps.Count == 0 ? "" : $"{Lang.T("SheetYield", ps.Count, paper)} · {Yield.Shown(su):P0}", Foreground = UseBrush(su, Yield.Regular(ps)), VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(pct, Dock.Right);
            head.Children.Add(pct);
            var title = new TextBlock { Text = Lang.T("SheetN", i + 1), Foreground = Dim, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(title, Dock.Left);
            head.Children.Add(title);
            if (ps.Count > 0)
            {
                int at = i;
                var dup = new Button { Content = Lang.T("DupSheet"), ToolTip = Lang.T("DupSheetTip"), Style = (Style)FindResource("Ghost"), Padding = new Thickness(6, 0, 6, 0), Margin = new Thickness(6, 0, 12, 0), HorizontalAlignment = HorizontalAlignment.Left };
                dup.Click += (_, _) => DuplicateSheet(at);
                head.Children.Add(dup);
            }
            var panel = new StackPanel { Margin = new Thickness(SheetPad), HorizontalAlignment = HorizontalAlignment.Left };
            panel.Children.Add(head);
            panel.Children.Add(c);
            views.Add(panel);
        }
        LimitWrap();
        Sheets.ItemsSource = views;
        Dispatcher.BeginInvoke(new Action(UpdateRulers), DispatcherPriority.Loaded);
        Empty.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        FileCount.Text = items.Count > 0 ? items.Count.ToString(CultureInfo.InvariantCulture) : "";
        TabText.Text = $"{paper}   {F(w)} × {F(CH)} mm";

        // Bahan terpakai terhadap area yang bisa diisi (kertas dikurangi margin), target reguler/irregular dari bentuk.
        var all = sheets.SelectMany(x => x).ToList();
        double fill = sheets.Select((_, i) => Yield.Fillable(sheetW, sheetH[i], MarginMm)).Sum(), use = fill > 0 ? all.Sum(p => p.S.AreaMm2) / fill : 0;
        bool regular = Yield.Regular(all);
        var target = Yield.Target(regular);
        UseBar.Width = 84 * Math.Clamp(use, 0, 1);
        UseBar.Background = UseBrush(use, regular);
        UseText.Text = sheets.Count > 0 ? Lang.T("UseWaste", Yield.Shown(use), 1 - Yield.Shown(use)) : "–";
        UseGroup.ToolTip = Lang.T(regular ? "TargetReg" : "TargetIrr", target.Low, target.High, target.Green);
        var warn = new List<string>();
        int bad = sheets.Sum(x => x.Count(p => p.Bad));
        if (bad > 0) warn.Add(Lang.T("Overlap", bad));
        if (tooBig > 0) warn.Add(Lang.T("TooBig", tooBig));
        int low = items.Count(i => i.LowDpi);
        if (low > 0) warn.Add(Lang.T("LowDpi", low));
        int ras = items.Count(i => i.PdfAsImage);
        if (ras > 0) warn.Add(Lang.T("PdfAsImage", ras));
        int cdr = items.Count(i => i.CdrAsImage);
        if (cdr > 0) warn.Add(Lang.T("CdrAsImage", cdr));
        WarnText.Text = string.Join("   ", warn);
        WarnPill.Visibility = warn.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        SelInfo.Visibility = sel == null ? Visibility.Collapsed : Visibility.Visible;
        
        if (sel != null && SelSizeBox != null && SelCopiesBox != null) // Tambahkan pengecekan
        {
            var (aw, ah) = sel.Rot % 180 == 0 ? (sel.S.ArtW, sel.S.ArtH) : (sel.S.ArtH, sel.S.ArtW);
            SelThumb.Source = sel.S.Preview;
            SelThumb.LayoutTransform = new ScaleTransform(1, sel.S.StretchY); // rasio bebas: gambar kecil ikut diregangkan
            SelName.Text = sels.Count > 1 ? Lang.T("NSelected", sels.Count) : sel.S.Name;

            // BLOKIR event TextChanged saat mengisi nilai
            syncingSel = true;
            SelSizeBox.Text = F(sel.S.WidthMm);
            SelHeightBox.Text = F(sel.S.ArtH);
            SelCopiesBox.Text = sel.S.Copies.ToString();
            syncingSel = false;

            SelRot.Text = $"{sel.Rot}°";
            SelPos.Text = $"X {F(sel.X)}   Y {F(sel.Y)} mm";

            // Sinkronkan ListBox panel kanan dengan pengaman
            if (sels.Count <= 1 && items.Contains(sel.S))
            {
                if (List.SelectedItem != sel.S)
                {
                    syncingList = true;
                    List.SelectedItem = sel.S;
                    List.ScrollIntoView(sel.S);
                    syncingList = false;
                }
            }
            else
            {
                if (List.SelectedItem != null)
                {
                    syncingList = true;
                    List.SelectedItem = null;
                    syncingList = false;
                }
            }
        }
        else
        {
            // Jika tidak ada yang dipilih, kosongkan ListBox
            if (List.SelectedItem != null)
            {
                syncingList = true;
                List.SelectedItem = null;
                syncingList = false;
            }
        }
    }

    string Summary() => Lang.T("Summary", sheets.Sum(x => x.Count), sheets.Count);

    void CheckSheet(int i) => Nest.Check(sheets[i], sheetW, sheetH[i], MarginMm, HasRegMarks, GapMm);
    void CheckAll() { for (int i = 0; i < sheets.Count; i++) CheckSheet(i); }
    void CheckSel() { for (int i = 0; i < sheets.Count; i++) if (sheets[i].Any(sels.Contains)) CheckSheet(i); } // pilihan bisa lintas lembar

    // Jarak antar stiker atau margin berubah: stiker yang jadi terlalu dekat atau keluar margin langsung merah, sampai disusun ulang.
    void Spacing_Changed(object s, TextChangedEventArgs e) { if (!restoring && sheets.Count > 0) { CheckAll(); Redraw(); } }

    // Kotak angka pertama yang tidak terbaca atau di bawah batasnya, beserta batas itu.
    (TextBox? Box, double Min) BadNumber() => new (TextBox? Box, double Min)[]
        { (CanvasW, 10), (CanvasH, 10), (MarginBox, 0), (KissOff, 0), (KissSm, 0), (DieOff, 0), (DieSm, 0), (BleedBox, 0), (GapBox, 0), (WidthBox, 0.01) }
        .FirstOrDefault(n => double.IsNaN(D(n.Box!, double.NaN, n.Min)));

    // Angka yang salah ketik tidak diam-diam diganti nilai bawaan: operator diberi tahu lebih dulu.
    bool NumbersValid()
    {
        var (box, min) = BadNumber();
        if (box == null) return true;
        MessageBox.Show(Lang.T("BadNumber", box.Text, min), App.Brand);
        box.Focus();
        box.SelectAll();
        return false;
    }

    bool CutsValid()
    {
        if (!NumbersValid()) return false;
        if (!Kiss.On && !Die.On) { MessageBox.Show(Lang.T("NeedCut"), App.Brand); return false; }
        if (Kiss.On && Die.On && Die.Off <= Kiss.Off) { MessageBox.Show(Lang.T("DieInside"), App.Brand); return false; }
        // Bleed tidak boleh melewati jarak antar stiker: warna tidak pernah masuk garis potong tetangga.
        if (BleedMm > GapMm) { MessageBox.Show(Lang.T("BleedGap"), App.Brand); return false; }
        // Warna border tanpa bleed diteruskan Sticker.BorderMin mm di luar garis potong: jarak antar stiker harus muat.
        if (BleedMm == 0 && GapMm < Sticker.BorderMin && items.Any(s => s.Border != null)) { MessageBox.Show(Lang.T("BorderGap"), App.Brand); return false; }
        return true;
    }

    void ClearLayout()
    {
        sheets = [];
        sheetH = [];
        ClearSel();
        drag = null;
        tooBig = 0;
        Redraw("");
    }

    // ---- Undo/redo global: satu langkah = keadaan dokumen setelah satu aksi operator ----
    sealed record Doc(List<(Sticker S, int Copies, double WidthMm, string Name)> Items, List<List<(Sticker S, double X, double Y, int Rot)>> Sheets,
        List<double> SheetH, double SheetW, int TooBig, string[] Settings);
    const int UndoMax = 30; // snapshot memegang Sticker lama beserta pratinjaunya; batas ini menjaga RAM
    readonly List<Doc> undo = [], redo = [];
    Doc? saved;
    bool restoring;

    string[] Settings() => [PaperBox.SelectedItem as string ?? "", CanvasW.Text, CanvasH.Text, MarginBox.Text, GapBox.Text,
        $"{KissOn.IsChecked}", KissOff.Text, KissSm.Text, $"{DieOn.IsChecked}", DieOff.Text, DieSm.Text, BleedBox.Text, WidthBox.Text, $"{RowsBox.IsChecked}", $"{HasRegMarks}"];

    Doc Capture() => new([.. items.Select(s => (s, s.Copies, s.WidthMm, s.Name))],
        [.. sheets.Select(sh => sh.Select(p => (p.S, p.X, p.Y, p.Rot)).ToList())], [.. sheetH], sheetW, tooBig, Settings());

    static bool Same(Doc a, Doc b) => a.SheetW == b.SheetW && a.TooBig == b.TooBig && a.Settings.SequenceEqual(b.Settings)
        && a.Items.SequenceEqual(b.Items) && a.SheetH.SequenceEqual(b.SheetH)
        && a.Sheets.Count == b.Sheets.Count && a.Sheets.Zip(b.Sheets).All(x => x.First.SequenceEqual(x.Second));

    // Dipanggil di akhir setiap aksi yang mengubah dokumen. Tidak berubah = bukan langkah baru.
    void Commit()
    {
        var now = Capture();
        if (saved != null && !Same(now, saved))
        {
            undo.Add(saved);
            if (undo.Count > UndoMax) undo.RemoveAt(0);
            redo.Clear();
        }
        saved = now;
    }

    void Step(List<Doc> from, List<Doc> to)
    {
        if (from.Count == 0 || saved == null || Mouse.OverrideCursor != null) return; // tidak saat file sedang dimuat
        to.Add(saved);
        var d = saved = from[^1];
        from.RemoveAt(from.Count - 1);
        restoring = true; // ganti kertas tidak boleh memicu susun ulang
        try
        {
            var v = d.Settings;
            PaperBox.SelectedItem = v[0];
            (CanvasW.Text, CanvasH.Text, MarginBox.Text, GapBox.Text) = (v[1], v[2], v[3], v[4]);
            (KissOn.IsChecked, KissOff.Text, KissSm.Text) = (v[5] == "True", v[6], v[7]);
            (DieOn.IsChecked, DieOff.Text, DieSm.Text) = (v[8] == "True", v[9], v[10]);
            (BleedBox.Text, WidthBox.Text, RowsBox.IsChecked, DenseBox.IsChecked) = (v[11], v[12], v[13] == "True", v[13] != "True");
            RegMarksToggleMenu.IsChecked = v[14] == "True";
        }
        finally { restoring = false; }
        items.Clear();
        foreach (var (s, copies, width, name) in d.Items) { (s.Copies, s.WidthMm, s.Name) = (copies, width, name); items.Add(s); }
        List.Items.Refresh();
        sheets = [.. d.Sheets.Select(sh => sh.Select(p => new Placed { S = p.S, X = p.X, Y = p.Y, Rot = p.Rot }).ToList())];
        (sheetH, sheetW, tooBig) = ([.. d.SheetH], d.SheetW, d.TooBig);
        ClearSel();
        drag = null;
        CheckAll();
        Redraw(sheets.Count > 0 ? Summary() : "");
    }

    void Undo() => Step(undo, redo);
    void Redo() => Step(redo, undo);
    void Undo_Click(object s, RoutedEventArgs e) => Undo();
    void Redo_Click(object s, RoutedEventArgs e) => Redo();

    void DropEmpty()
    {
        for (int i = sheets.Count - 1; i >= 0; i--)
        {
            if (sheets[i].Count > 0) continue;
            sheets.RemoveAt(i);
            sheetH.RemoveAt(i);
            if (clip.Count == 0 && clipSheet == i) clipSheet = -1; // kanvas yang disalin sudah tidak ada
            Renumber(x => Math.Min(x > i ? x - 1 : x, sheets.Count - 1));
        }
    }

    // Lembar bertambah atau berkurang: nomor lembar yang disimpan (salinan, klik terakhir) ikut bergeser.
    void Renumber(Func<int, int> to)
    {
        clip = [.. clip.Select(c => (c.S, c.Rot, c.X, c.Y, to(c.Sheet)))];
        clipSheet = to(clipSheet);
        selSheet = to(selSheet);
    }

    void Add_Click(object s, RoutedEventArgs e) => OpenFileDialog();

    // Method public yang bisa dipanggil dari WelcomePanel
    public void OpenFileDialog()
    {
        var dlg = new OpenFileDialog { Filter = "PDF, AI, CDR, PNG, JPG, SVG|*.pdf;*.ai;*.cdr;*.png;*.jpg;*.jpeg;*.svg", Multiselect = true };
        if (dlg.ShowDialog() == true) AddFiles(dlg.FileNames);
    }

    // File baru langsung disusun: satu klik lebih sedikit untuk operator.
    bool loading; // AddFiles bisa menunggu CorelDRAW (await); cegah muat kedua berjalan bersamaan

    public async void AddFiles(IEnumerable<string> files)
    {
        if (loading || !CutsValid()) return;
        loading = true;
        int before = items.Count;
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            foreach (var f in files)
                try
                {
                    // CDR: libcdr dulu, CorelDRAW hanya cadangan. Keduanya bekerja di proses terpisah, jadi tampilan tetap jalan selama menunggu,
                    // dan LoadAll sesudahnya memakai hasil yang sudah disimpan.
                    if (Path.GetExtension(f).Equals(".cdr", StringComparison.OrdinalIgnoreCase))
                    {
                        Status.Text = Lang.T("CdrOpen", Path.GetFileName(f));
                        if (await Task.Run(() => Libcdr.ToSvg(f)) == null)
                        {
                            Status.Text = Lang.T("CorelOpen", Path.GetFileName(f));
                            await Task.Run(() => Corel.ToPdf(f));
                        }
                    }
                    foreach (var st in Loader.LoadAll(f, D(WidthBox, 50), Kiss, Die, BleedMm)) items.Add(st);
                }
                catch (Exception ex) { MessageBox.Show($"{Path.GetFileName(f)}: {ex.Message}", Lang.T("LoadFail")); }
        }
        finally { Mouse.OverrideCursor = null; loading = false; }
        if (items.Count == before) { Status.Text = status; return; }
        bool first = sheets.Count == 0;
        ShowHome(false); // lebih dulu: selama halaman sambutan tampil, tampilan lembar belum punya ukuran untuk Pas layar
        Arrange();
        if (first) { View.UpdateLayout(); Fit(); }
    }

    void RemoveItem_Click(object s, RoutedEventArgs e)
    {
        if ((s as FrameworkElement)?.DataContext is not Sticker st) return;
        // ponytail: dihitung dari selisih jumlah dan yang termuat. Jumlah yang baru diketik tanpa Susun ikut terhitung, sampai Susun berikutnya menghitung ulang.
        tooBig -= Math.Clamp(st.Copies - sheets.Sum(sh => sh.Count(p => p.S == st)), 0, tooBig); // salinan desain ini yang tidak termuat ikut hilang dari peringatan
        items.Remove(st);
        foreach (var sh in sheets) sh.RemoveAll(p => p.S == st);
        DropEmpty();
        ClearSel();
        Redraw(sheets.Count > 0 ? Summary() : "");
        Commit();
    }

    // Rapikan gambar (pengganti langkah Photoshop). Hanya desain yang ditempel sebagai gambar.
    void CleanItem_Click(object s, RoutedEventArgs e)
    {
        if ((s as FrameworkElement)?.DataContext is Sticker st) Clean(st);
    }

    void ResetItem_Click(object s, RoutedEventArgs e)
    {
        if ((s as FrameworkElement)?.DataContext is Sticker st)
        {
            if (st.Copies == 1) return;   // tidak ada yang berubah
            st.Copies = 1;
            List.Items.Refresh();
            Arrange();    
        }
    }

    void Thumb_Down(object s, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && (s as FrameworkElement)?.DataContext is Sticker st) Clean(st);
    }

    void Clean(Sticker it)
    {
        if (!it.CanClean) { Redraw(Lang.T("CleanVector")); return; }
        if (!CutsValid()) return;
        var w = new CleanupWindow(it.Edited ?? it.Raster, it.Name) { Owner = this };

        var workArea = SystemParameters.WorkArea;
        w.MaxWidth = workArea.Width - 40;
        w.MaxHeight = workArea.Height - 40;
        if (w.Width > w.MaxWidth) w.Width = w.MaxWidth;
        if (w.Height > w.MaxHeight) w.Height = w.MaxHeight;
        w.WindowStartupLocation = WindowStartupLocation.CenterOwner;

        bool ok = w.ShowDialog() == true && w.Result != null;
        var result = w.Result;
        w = null;
        GC.Collect(); // jendela Rapikan memakai buffer gambar besar; kembalikan memorinya segera
        if (!ok) return;
        try
        {
            Replace(it, Loader.Load(it.Source, it.Page, it.WidthMm, Kiss, Die, edited: result, bleed: BleedMm, shape: it.Shape, border: it.Border, dieShape: it.DieShape,
                stretchY: it.StretchY));
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, App.Brand); return; }
        Arrange();
        Redraw(Lang.T("Cleaned", it.Name));
    }

    // Editor garis potong: bentuk preset, node lurus/kurva yang bisa digeser, ditambah, dihapus. Berlaku untuk semua salinan desain.
    void CutItem_Click(object s, RoutedEventArgs e)
    {
        if ((s as FrameworkElement)?.DataContext is not Sticker it || !CutsValid()) return;
        double k = it.WidthMm / it.LoadedW;
        CutEditWindow? w;
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            // Garis otomatis pada pengaturan sekarang: dasar garis baru, bentuk preset, slider Simplify, dan Reset otomatis.
            var auto = Loader.Load(it.Source, it.Page, it.WidthMm, Kiss, Die, edited: it.Frozen, loadedPdf: it.Pdf, stretchY: it.StretchY);
            w = new CutEditWindow(auto, Curves.Scale(it.ManualKiss, k), Curves.Scale(it.ManualDie, k), it.Shape, it.Name, it.Border, it.DieShape) { Owner = this };

            var workArea = SystemParameters.WorkArea;
            w.MaxWidth = workArea.Width - 40;
            w.MaxHeight = workArea.Height - 40;
            if (w.Width > w.MaxWidth) w.Width = w.MaxWidth;
            if (w.Height > w.MaxHeight) w.Height = w.MaxHeight;
            w.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, App.Brand); return; }
        finally { Mouse.OverrideCursor = null; }
        bool ok = w.ShowDialog() == true;
        var (mk, md, shape, dieShape, border) = (w.ManualKiss, w.ManualDie, w.Shape, w.DieShape, w.Border);
        w = null;
        FreeMemory(); // desain otomatis untuk editor memuat ulang gambar penuh
        if (!ok) return;
        try
        {
            Replace(it, Loader.Load(it.Source, it.Page, it.WidthMm, Kiss, Die, edited: it.Frozen, bleed: BleedMm, loadedPdf: it.Pdf,
                manualKiss: mk, manualDie: md, shape: shape, border: border, dieShape: dieShape, stretchY: it.StretchY));
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, App.Brand); return; }
        Arrange();
        Redraw(Lang.T("CutSaved", it.Name));
    }

    // Ganti desain dengan hasil muat ulangnya; jumlah salinan dan nama tetap.
    void Replace(Sticker old, Sticker n)
    {
        if (old == null || n == null) return; // Pagar pengaman

        n.Copies = old.Copies;
        n.Name = old.Name;
        
        int idx = items.IndexOf(old);
        if (idx >= 0) 
        {
            items[idx] = n; // Hanya ganti jika old masih ada di items
        }
        
        clip = [.. clip.Select(c => c.S == old ? (n, c.Rot, c.X, c.Y, c.Sheet) : c)];
    }

    void Nest_Click(object s, RoutedEventArgs e) => Arrange();

    void Rows_Changed(object s, RoutedEventArgs e) { if (!restoring && items.Count > 0) Arrange(); }

    void Arrange()
    {
        CancelDrag(); // file CDR bisa selesai dimuat saat stiker sedang ditarik
        // Ctrl+L atau menu saat kursor masih di kotak lebar desain: angka yang baru diketik ikut dipakai.
        CommitTyping();
        wrapCols = 1;
        if (items.Count == 0) { Redraw(Lang.T("NestFirst")); return; }
        if (!CutsValid()) return;
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            Cut kiss = Kiss, die = Die;
            double bleed = BleedMm;
            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                if (it.WidthMm > 0 && (Math.Abs(it.LoadedW - it.WidthMm) > 1e-6 || it.LoadedKiss != kiss || it.LoadedDie != die
                    || (it.Pdf == null && it.LoadedBleed != bleed)))
                    Replace(it, Loader.Reload(it, kiss, die, bleed));
            }
            sheetW = CW;
            sheets = RowsBox.IsChecked == true ? Nest.Rows(items, sheetW, CH, MarginMm, GapMm, out tooBig, HasRegMarks) : Nest.Run(items, sheetW, CH, MarginMm, GapMm, out tooBig, HasRegMarks);
            sheetH = sheets.Select(_ => CH).ToList();
            CheckAll();
            ClearSel();
            clip = []; // posisi yang disalin tidak berlaku lagi
            clipSheet = -1;
            Redraw(Summary());
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, App.Brand); }
        finally { Mouse.OverrideCursor = null; }
        Commit();
        FreeMemory();
    }

    // Bitmap resolusi penuh dari Loader.Load sudah jadi sampah, tetapi GC tidak jalan sendiri saat aplikasi diam
    // (JPG 24 MP: RAM tetap ±220 MB). Bitmap WPF dilepas lewat finalizer; Aggressive mengembalikan memori heap besar
    // ke Windows saat itu juga. 19 desain uji: 395 -> 70 MB, ±40 ms (D4).
    static void FreeMemory() { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(2, GCCollectionMode.Aggressive, true, true); }

    // Penuhi lembar: jumlah per desain ikut hasil, supaya daftar desain, Print Ticket, dan susun ulang memakai angka yang sama.
    void Compact_Click(object s, RoutedEventArgs e)
    {
        if (sheets.Count == 0) return;
        int added;
        if (RowsBox.IsChecked == true)
        {
            sheets = Nest.FillRows(items, sheetW, CH, MarginMm, GapMm, out added, HasRegMarks);
            sheetH = sheets.Select(_ => CH).ToList();
            ClearSel();
        }
        else added = Nest.Fill(sheets, items, sheetW, CH, MarginMm, GapMm, HasRegMarks);
        AfterAdd(Lang.T("Filled", added));
    }

    // Akhir aksi yang menambah stiker: jumlah per desain ikut hasil, lalu periksa, gambar ulang, dan simpan langkah urungkan.
    void AfterAdd(string msg)
    {
        CountCopies();
        CheckAll();
        Redraw(msg + "  " + Summary());
        Commit();
    }

    void CountCopies()
    {
        foreach (var it in items) if (sheets.Sum(sh => sh.Count(p => p.S == it)) is > 0 and var n) it.Copies = n; // yang tidak muat sama sekali tetap
        List.Items.Refresh();
    }

    // Pilih satu stiker (buang pilihan lain). sel = primer untuk panel info.
    void SelectOne(Placed? p)
    {
        sels.Clear();
        sel = p;
        if (p != null) sels.Add(p);
    }

    // Tambah/buang satu stiker dari pilihan (Ctrl+klik). sel = yang terakhir disentuh.
    void Toggle(Placed p)
    {
        if (!sels.Add(p)) { sels.Remove(p); sel = sels.FirstOrDefault(); }
        else sel = p;
    }

    void ClearSel() { sels.Clear(); sel = null; }

    string SelWho => sels.Count > 1 ? Lang.T("NStickers", sels.Count) : sel!.S.Name;

    // Lembar yang berisi stiker terpilih: pilihan bisa lintas lembar.
    IEnumerable<Canvas> SelCanvases() => canvases.Where((_, i) => i < sheets.Count && sheets[i].Any(sels.Contains));

    // Duplikat kanvas: hasil edit ikut tersalin, beda dengan menaikkan jumlah lalu Susun. WrapPanel menaruh lembar baru di kanan lembar asal,
    // atau di bawah bila baris penuh.
    void DuplicateSheet(int i)
    {
        if (i < 0 || i >= sheets.Count || sheets[i].Count == 0) return;
        Nest.CopySheet(sheets, sheetH, i);
        Renumber(x => x > i ? x + 1 : x);
        ClearSel();
        AfterAdd(Lang.T("SheetDuped", i + 1, i + 2));
        var added = canvases[i + 1];
        Dispatcher.BeginInvoke(new Action(added.BringIntoView), DispatcherPriority.Loaded);
    }

    // Ctrl+C: semua stiker yang dipilih, urut dari kiri atas; bila tidak ada yang dipilih, kanvas yang terakhir diklik.
    void CopySel()
    {
        if (sheets.Count == 0) return;
        clipSheet = Math.Clamp(selSheet, 0, sheets.Count - 1);
        clip = SelItems();
        Redraw(sel == null ? Lang.T("SheetCopied", clipSheet + 1) : Lang.T("StickerCopied", SelWho));
    }

    // Ctrl+V: kanvas yang disalin diduplikat; stiker ditempel di kanan aslinya. Salinannya terpilih; untuk satu stiker, Ctrl+V berikutnya berlanjut dari sana.
    // Beberapa stiker: semua atau tidak sama sekali.
    void Paste()
    {
        if (clipSheet < 0 || clipSheet >= sheets.Count || clip.Any(c => c.Sheet >= sheets.Count || !items.Contains(c.S))) { Redraw(Lang.T("PasteEmpty")); return; }
        if (clip.Count == 0) { DuplicateSheet(clipSheet); return; }
        List<(Sticker S, int Rot, double X, double Y, int Sheet)> src = clip is [var one] && sel?.S == one.S ? [(one.S, one.Rot, sel.X, sel.Y, sheets.FindIndex(sh => sh.Contains(sel!)))] : clip;
        PasteItems(src, "PasteFull", "Pasted");
    }

    // Semua stiker terpilih, urut dari kiri atas, beserta lembarnya.
    List<(Sticker S, int Rot, double X, double Y, int Sheet)> SelItems() =>
        [.. sels.OrderBy(p => p.Y).ThenBy(p => p.X).Select(p => (p.S, p.Rot, p.X, p.Y, sheets.FindIndex(sh => sh.Contains(p))))];

    // Tiap salinan di kanan aslinya; satu saja tidak muat = tidak ada yang ditambahkan. Salinannya jadi pilihan baru, satu langkah urungkan.
    void PasteItems(List<(Sticker S, int Rot, double X, double Y, int Sheet)> src, string full, string done)
    {
        if (Nest.PasteAll(sheets, sheetH, src, sheetW, MarginMm, GapMm, HasRegMarks) is not { } added) { Redraw(Lang.T(full)); return; }
        sels.Clear();
        sels.UnionWith(added);
        sel = added[^1];
        selSheet = sheets.FindIndex(sh => sh.Contains(added[^1]));
        AfterAdd(Lang.T(done, SelWho));
    }

    // Ctrl+D: duplikat semua stiker terpilih, tiap salinan di lembar aslinya. Salinan pakai Sticker yang sama (artwork & geometri tidak digandakan). Asli tidak berubah.
    void DuplicateSel() { if (sels.Count > 0) PasteItems(SelItems(), "DupFull", "Duplicated"); }

    void Copy_Click(object s, RoutedEventArgs e) => CopySel();
    void Paste_Click(object s, RoutedEventArgs e) => Paste();

    void EditSel(Action<Placed> act)
    {
        if (sels.Count == 0) return;
        foreach (var p in sels) act(p);
        CheckSel();
        Redraw(Summary());
        Commit();
    }

    void DeleteSel()
    {
        if (sels.Count == 0) return;
        var designs = sels.Select(p => p.S).ToHashSet();
        foreach (var p in sels) foreach (var sh in sheets) sh.Remove(p); // pilihan bisa lintas lembar
        // Jumlah di daftar ikut, supaya Susun otomatis tidak mengembalikan stiker yang baru dihapus. Bisa menjadi 0; desainnya tetap di daftar.
        foreach (var d in designs) d.Copies = sheets.Sum(sh => sh.Count(p => p.S == d));
        List.Items.Refresh();
        ClearSel();
        DropEmpty();
        CheckAll();
        Redraw(sheets.Count > 0 ? Summary() : "");
        Commit();
    }

    // Putar 90°, lalu snap ke posisi kosong terdekat (tidak menabrak, jarak antar stiker tetap) di lembar tempat stiker berada: pilihan bisa lintas lembar.
    // Yang tidak punya tempat tetap seperti semula.
    void RotateSel()
    {
        Placed? stuck = null;
        foreach (var p in sels)
        {
            int i = sheets.FindIndex(sh => sh.Contains(p));
            if (!Nest.Turn(sheets[i], p, sheetW, sheetH[i], MarginMm, GapMm, HasRegMarks)) stuck = p;
        }
        EditSel(_ => { });
        if (stuck != null) Redraw(Lang.T("NoRoomTurn", stuck.S.Name));
    }
    void Rotate_Click(object s, RoutedEventArgs e) => RotateSel();

    // Print Ticket: ringkasan 1 halaman A4 (lembar, pcs, efisiensi bahan) untuk lampiran nota kasir/SPK.
    void Ticket_Click(object s, RoutedEventArgs e)
    {
        CommitTyping();
        if (sheets.Count == 0) { MessageBox.Show(Lang.T("NestFirst"), App.Brand); return; }
        if (!NumbersValid()) return;
        var dlg = new SaveFileDialog { Filter = "PDF|*.pdf", FileName = "print-ticket.pdf" };
        if (dlg.ShowDialog() != true) return;
        if (sheets.SelectMany(sh => sh).FirstOrDefault() is not { } placed) { MessageBox.Show(Lang.T("NestFirst"), App.Brand); return; }
        try
        {
            var st = placed.S; // pengaturan yang dipakai untuk layout ini
            var lines = new List<string>();
            if (st.LoadedKiss.On) lines.Add(Lang.T("TicketCut", Lang.T("Kiss"), st.LoadedKiss.Off, st.LoadedKiss.Smooth));
            if (st.LoadedDie.On) lines.Add(Lang.T("TicketCut", Lang.T("Die"), st.LoadedDie.Off, st.LoadedDie.Smooth));
            lines.Add(Lang.T("TicketGap", GapMm, st.LoadedBleed));
            Export.Ticket(dlg.FileName, PaperBox.SelectedItem as string ?? "Custom", sheets, sheetW, sheetH, MarginMm, lines);
            Redraw(Lang.T("Saved", dlg.FileName));
        }
        catch (Exception ex) { MessageBox.Show(SaveFail(ex), App.Brand); }
    }
    void DeletePlaced_Click(object s, RoutedEventArgs e) => DeleteSel();
    void Exit_Click(object s, RoutedEventArgs e) => Close();

    // Ctrl+L, Ctrl+E, atau menu saat kursor masih di kotak isian: angka yang baru diketik ikut dipakai. Kotak lebar di daftar desain memakai binding;
    // kotak lebar dan tinggi di panel Pilihan baru berlaku saat ditinggalkan, jadi diterapkan di sini (temuan tinjauan rasio bebas 2026-11-07).
    void CommitTyping()
    {
        var box = FocusManager.GetFocusedElement(this) as TextBox;
        box?.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        if (box == SelSizeBox) SelSize_LostFocus(box, new RoutedEventArgs());
        else if (box == SelHeightBox) SelHeight_LostFocus(box, new RoutedEventArgs());
    }

    // Sebelum file keluar: sudah ada susunan, angka terbaca, dan operator tahu bila ada stiker bertanda merah.
    bool ReadyToExport()
    {
        CommitTyping();
        if (sheets.Count == 0) { MessageBox.Show(Lang.T("NestFirst"), App.Brand); return false; }
        return NumbersValid() && (!sheets.Any(x => x.Any(p => p.Bad))
            || MessageBox.Show(Lang.T("ExportBad"), App.Brand, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes);
    }

    // File tujuan terkunci (masih terbuka di program lain), disk penuh, atau folder tanpa izin: pesan untuk operator, bukan pesan teknis.
    internal static string SaveFail(Exception ex) => ex is IOException or UnauthorizedAccessException ? Lang.T("SaveFail") : ex.Message;

    void Export_Click(object s, RoutedEventArgs e)
    {
        if (!ReadyToExport()) return;
        var dlg = new SaveFileDialog { Filter = "PDF|*.pdf", FileName = "layout.pdf" };
        if (dlg.ShowDialog() != true) return;
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            var warn = Export.Pdf(dlg.FileName, sheets, sheetW, sheetH, HasRegMarks, MarginMm);
            Redraw(string.Join("  ", warn.Prepend(Lang.T("Saved", dlg.FileName))));
        }
        catch (Exception ex) { MessageBox.Show(SaveFail(ex), App.Brand); }
        finally { Mouse.OverrideCursor = null; }
    }

    // JPG ringan untuk konfirmasi ke customer (WhatsApp) sebelum cetak.
    void Preview_Click(object s, RoutedEventArgs e)
    {
        if (!ReadyToExport()) return;
        var dlg = new SaveFileDialog { Filter = "JPG|*.jpg", FileName = "preview.jpg" };
        if (dlg.ShowDialog() != true) return;
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            var files = Export.Preview(dlg.FileName, sheets, sheetW, sheetH, MarginMm, HasRegMarks);
            Redraw(Lang.T("PreviewSaved", files.Count, Path.GetDirectoryName(dlg.FileName)!));
        }
        catch (Exception ex) { MessageBox.Show(SaveFail(ex), App.Brand); }
        finally { Mouse.OverrideCursor = null; }
    }

        // SVG per layer (artwork / kiss-cut / die-cut / registration-marks): dibaca CorelDRAW sebagai layer.
    void ExportSvg_Click(object s, RoutedEventArgs e)
    {
        if (!ReadyToExport()) return;
        var dlg = new SaveFileDialog { Filter = "SVG|*.svg", FileName = "layout.svg" };
        if (dlg.ShowDialog() != true) return;
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            var files = ExportSvg.Svg(dlg.FileName, sheets, sheetW, sheetH, MarginMm, HasRegMarks);
            Redraw(Lang.T("PreviewSaved", files.Count, Path.GetDirectoryName(dlg.FileName)!));
        }
        catch (Exception ex) { MessageBox.Show(SaveFail(ex), App.Brand); }
        finally { Mouse.OverrideCursor = null; }
    }

    void Paper_Changed(object s, SelectionChangedEventArgs e)
    {
        if (restoring) return;
        if (PaperBox.SelectedItem is string k && Paper.Sizes.TryGetValue(k, out var sz)) { CanvasW.Text = F(sz.W); CanvasH.Text = F(sz.H); }
        ClearLayout();
        if (IsLoaded && items.Count > 0) Arrange();
    }

    void PaperRot_Click(object s, RoutedEventArgs e)
    {
        (CanvasW.Text, CanvasH.Text) = (CanvasH.Text, CanvasW.Text);
        ClearLayout();
        if (items.Count > 0) Arrange();
    }

    void Lang_Click(object s, RoutedEventArgs e)
    {
        var code = (string)((MenuItem)s).Tag;
        LangId.IsChecked = code == "id";
        LangEn.IsChecked = code == "en";
        Lang.Apply(code);
        List.Items.Refresh(); // penanda garis di daftar desain ditulis sekali saat barisnya dibuat
        Redraw(sheets.Count > 0 ? Summary() : "");
    }

    void RegMarksMenu_Click(object s, RoutedEventArgs e)
    {
        // IsCheckable: WPF sudah mengubah IsChecked saat diklik. Stiker di area pindai tanda jadi merah; Susun otomatis memindahkannya.
        CheckAll();
        Redraw(sheets.Count > 0 ? Summary() : "");
        Commit();
    }

    void Window_Loaded(object s, RoutedEventArgs e)
    {
        Redraw();
        Fit();
        Commit(); // keadaan awal (kosong) untuk undo

        // Tampilkan Home Page jika aplikasi dibuka tanpa file (startFiles kosong)
        ShowHome(startFiles.Length == 0);

        if (startFiles.Length > 0) AddFiles(startFiles);
    }

    // ---- File: drag & drop ----
    static string[] Dropped(DragEventArgs e) =>
        e.Data.GetData(DataFormats.FileDrop) is string[] f ? f.Where(x => Exts.Contains(Path.GetExtension(x).ToLowerInvariant())).ToArray() : [];

    public void Window_DragOver(object s, DragEventArgs e)
    {
        // Jika WelcomePanel sedang tampil, jangan tampilkan efek drop di luar border
        if (HomePage.Visibility == Visibility.Visible)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        bool ok = Dropped(e).Length > 0;
        e.Effects = ok ? DragDropEffects.Copy : DragDropEffects.None;
        DropHint.Visibility = ok ? Visibility.Visible : Visibility.Collapsed;
        e.Handled = true;
    }

    public void Window_DragLeave(object s, DragEventArgs e) => DropHint.Visibility = Visibility.Collapsed;

    public void Window_Drop(object s, DragEventArgs e)
    {
        DropHint.Visibility = Visibility.Collapsed;
        
        // TAMBAHKAN INI: Jika WelcomePanel sedang tampil, abaikan drop di luar border
        if (HomePage.Visibility == Visibility.Visible)
        {
            e.Handled = true;
            return;
        }

        var files = Dropped(e);
        if (files.Length > 0) AddFiles(files);
        e.Handled = true;
    }

    // ---- Tampilan: zoom & geser ----
    void Fit_Click(object s, RoutedEventArgs e) => Fit();
    void ZoomIn_Click(object s, RoutedEventArgs e) => ZoomAt(ZoomSlider.Value * 1.25);
    void ZoomOut_Click(object s, RoutedEventArgs e) => ZoomAt(ZoomSlider.Value / 1.25);

    void Fit()
    {
        double h = sheetH.Count > 0 ? sheetH.Max() : CH, w = sheets.Count > 0 ? sheetW : CW;
        double k = Math.Min((View.ViewportHeight - 110) / h, (View.ViewportWidth - 80) / w); // DIP per mm
        wrapCols = 1;
        if (k > 0) ZoomSlider.Value = Math.Clamp(k / (96 / 25.4) * 100, ZoomSlider.Minimum, ZoomSlider.Maximum);
        LimitWrap();
    }

    // Zoom dengan titik lembar di bawah kursor tetap di tempat. Titiknya diukur pada lembar, bukan dihitung dari offset gulir, karena saat zoom
    // lembar bisa pindah baris (WrapPanel) dan isi yang lebih kecil dari tampilan berada di tengah.
    void ZoomAt(double pct, Point? at = null)
    {
        pct = Math.Clamp(pct, ZoomSlider.Minimum, ZoomSlider.Maximum);
        var p = at ?? new Point(View.ViewportWidth / 2, View.ViewportHeight / 2);
        var c = SheetNear(p);
        var mm = c == null ? p : View.TransformToDescendant(c).Transform(p);
        ZoomSlider.Value = pct;
        View.UpdateLayout();
        if (c == null) return;
        var now = c.TransformToAncestor(View).Transform(mm);
        View.ScrollToHorizontalOffset(View.HorizontalOffset + now.X - p.X);
        View.ScrollToVerticalOffset(View.VerticalOffset + now.Y - p.Y);
    }

    // Lembar yang paling dekat dengan titik di tampilan. Lembar yang baru digambar dan belum masuk pohon tampilan dilewati.
    Canvas? SheetNear(Point p) => canvases.Where(c => c.IsLoaded).MinBy(c =>
    {
        var box = c.TransformToAncestor(View).TransformBounds(new Rect(0, 0, c.Width, c.Height));
        double dx = Math.Max(0, Math.Max(box.Left - p.X, p.X - box.Right)), dy = Math.Max(0, Math.Max(box.Top - p.Y, p.Y - box.Bottom));
        return dx * dx + dy * dy;
    });

    void Zoom_Changed(object s, RoutedPropertyChangedEventArgs<double> e)
    {
        zoom.ScaleX = zoom.ScaleY = e.NewValue / 100 * 96 / 25.4; // 100% = ukuran asli
        unzoom.ScaleX = unzoom.ScaleY = 1 / zoom.ScaleX;
        LimitWrap();
        if (ZoomText != null) ZoomText.Text = $"{e.NewValue:0}%";
        if (HorizontalRuler != null) Dispatcher.BeginInvoke(new Action(UpdateRulers), DispatcherPriority.Loaded);
    }

    void Rulers_Click(object s, RoutedEventArgs e)
    {
        bool visible = RulerToggle.IsChecked == true;
        RulerRow.Height = visible ? new GridLength(22) : new GridLength(0);
        RulerColumn.Width = visible ? new GridLength(34) : new GridLength(0);
        RulerCorner.Visibility = HorizontalRuler.Visibility = VerticalRuler.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        Dispatcher.BeginInvoke(new Action(UpdateRulers), DispatcherPriority.Loaded);
    }

    void View_ScrollChanged(object s, ScrollChangedEventArgs e)
    {
        LimitWrap();
        UpdateRulers();
        if (drag is { Moved: true }) DragToCursor(); // digulir atau di-zoom selagi menarik: kursor diam, tetapi lembarnya bergeser. Klik tanpa gerakan tetap bukan tarikan
    }

    // WrapPanel di dalam ScrollViewer mendapat lebar tak terbatas, jadi lembar tidak pernah turun ke bawah. Batasi selebar tampilan.
    // Jumlah lembar per baris hanya bertambah (saat zoom out dan muat), supaya zoom in tidak memindahkan lembar ke baris lain: lembar yang
    // melebihi tampilan digeser. Pas layar, Susun, dan perubahan lebar tampilan menghitungnya dari awal.
    // ViewportWidth baru terbarui setelah ini: LimitWrap dipanggil oleh ScrollChanged.
    void View_SizeChanged(object s, SizeChangedEventArgs e) { if (e.WidthChanged) wrapCols = 1; }

    const double SheetPad = 18;
    double headW;
    void LimitWrap()
    {
        if (Sheets == null || canvases.Count == 0) return;
        // Saat zoom kecil judul lembar lebih lebar dari lembarnya. Judul yang belum diukur (baru digambar) memakai ukuran terakhir.
        if (canvases.Max(c => ((Panel)c.Parent).Children[0].DesiredSize.Width) is > 0 and var head) headW = head;
        double room = View.ViewportWidth - Sheets.Margin.Left - Sheets.Margin.Right, cell = Math.Max(canvases[0].Width * zoom.ScaleX, headW) + 2 * SheetPad + 1; // + 1: lebar tepat bisa kalah oleh pembulatan piksel, lalu lembar turun baris
        wrapCols = Math.Max(wrapCols, (int)(room / cell));
        Sheets.MaxWidth = Math.Max(room, wrapCols * cell);
    }

    void UpdateRulers()
    {
        if (HorizontalRuler == null || VerticalRuler == null) return;
        HorizontalRuler.Scale = VerticalRuler.Scale = zoom.ScaleX;
        if (canvases.Count == 0 || !canvases[0].IsLoaded)
        {
            HorizontalRuler.Origin = VerticalRuler.Origin = 0;
            HorizontalRuler.InvalidateVisual();
            VerticalRuler.InvalidateVisual();
            return;
        }
        try
        {
            var origin = canvases[0].TransformToAncestor(View).Transform(new Point(0, 0));
            HorizontalRuler.Origin = origin.X;
            VerticalRuler.Origin = origin.Y;
        }
        catch (InvalidOperationException) { return; }
        HorizontalRuler.InvalidateVisual();
        VerticalRuler.InvalidateVisual();
    }

    void View_Wheel(object s, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return;
        ZoomAt(ZoomSlider.Value * (e.Delta > 0 ? 1.1 : 1 / 1.1), e.GetPosition(View));
        e.Handled = true;
    }

    void Tool_Checked(object s, RoutedEventArgs e)
    {
        tool = s == ToolHand ? Tool.Hand : s == ToolZoom ? Tool.Zoom : Tool.Move;
        if (View != null) UpdateCursor();
    }

    void UpdateCursor() =>
        View.Cursor = panning || space || tool == Tool.Hand ? Cursors.SizeAll : tool == Tool.Zoom ? Cursors.Cross : null;

    static bool OnScrollBar(object src)
    {
        for (var d = src as DependencyObject; d is Visual; d = VisualTreeHelper.GetParent(d))
            if (d is ScrollBar) return true;
        return false;
    }

    // Geser: alat Hand, tahan Spasi, atau tombol tengah mouse. Zoom: alat Zoom (Alt = perkecil).
    void View_PreviewDown(object s, MouseButtonEventArgs e)
    {
        if (OnScrollBar(e.OriginalSource)) return;
        var pos = e.GetPosition(View);
        if (e.ChangedButton == MouseButton.Middle || (e.ChangedButton == MouseButton.Left && (space || tool == Tool.Hand)))
        {
            panning = true;
            panFrom = pos;
            (panH, panV) = (View.HorizontalOffset, View.VerticalOffset);
            View.CaptureMouse();
            UpdateCursor();
            e.Handled = true;
        }
        else if (e.ChangedButton == MouseButton.Left && tool == Tool.Zoom)
        {
            ZoomAt(ZoomSlider.Value * (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) ? 1 / 1.25 : 1.25), pos);
            e.Handled = true;
        }
    }

    void View_PreviewMove(object s, MouseEventArgs e)
    {
        if (!panning) return;
        var d = e.GetPosition(View) - panFrom;
        View.ScrollToHorizontalOffset(panH - d.X);
        View.ScrollToVerticalOffset(panV - d.Y);
    }

    void View_PreviewUp(object s, MouseButtonEventArgs e)
    {
        if (!panning) return;
        panning = false;
        View.ReleaseMouseCapture();
        UpdateCursor();
        e.Handled = true;
    }

    // Capture hilang saat menggeser (Alt+Tab, dialog): tanpa ini tampilan terus mengikuti kursor walau tombol sudah dilepas.
    // Hanya capture milik View sendiri: tarikan stiker yang kalah oleh tombol tengah juga melapor lewat sini, dan saat itu geser baru mulai.
    void View_Lost(object s, MouseEventArgs e)
    {
        if (!panning || e.OriginalSource != View) return;
        panning = false;
        UpdateCursor();
    }

    // Alt+Tab saat Spasi atau Alt ditahan: lepas tombolnya tidak pernah sampai ke jendela ini. Mode geser dilepas di sini, dan Alt berikutnya
    // tidak lagi ditahan, jadi menu tetap terbuka dengan Alt seperti biasa.
    void Window_Deactivated(object s, EventArgs e)
    {
        altUsed = false;
        if (!space) return;
        space = false;
        UpdateCursor();
    }

    // ---- Keyboard, gaya Photoshop ----
    void Window_KeyDown(object s, KeyEventArgs e)
    {
        if (drag != null || sizing >= 0 || band != null) // mouse masih ditahan: Ctrl+Z, Delete, dan lainnya menunggu stiker dilepas; Esc membatalkan
        {
            if (e.Key == Key.Escape && CancelDrag()) Redraw();
            TrackDrag();
            e.Handled = true;
            return;
        }
        bool typing = Keyboard.FocusedElement is TextBox; // Ctrl+Z/Y di kotak teks = undo teks bawaan
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
        {
            if (e.Key == Key.E) Preview_Click(s, e);
            else if (e.Key == Key.Z && !typing) Redo();
            else return;
            e.Handled = true;
            return;
        }
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.Z when !typing: Undo(); break;
                case Key.Y when !typing: Redo(); break;
                case Key.C when !typing: CopySel(); break;
                case Key.V when !typing: Paste(); break;
                case Key.D when !typing: DuplicateSel(); break;
                case Key.O: case Key.I: Add_Click(s, e); break; // Ctrl+I = Import di CorelDRAW
                case Key.E: Export_Click(s, e); break;
                case Key.L: Arrange(); break;
                case Key.D0: case Key.NumPad0: Fit(); break;
                case Key.OemPlus: case Key.Add: ZoomAt(ZoomSlider.Value * 1.25); break;
                case Key.OemMinus: case Key.Subtract: ZoomAt(ZoomSlider.Value / 1.25); break;
                default: return;
            }
            e.Handled = true;
            return;
        }
        if (Keyboard.FocusedElement is TextBox) return;
        double step = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
        switch (e.Key)
        {
            case Key.Space: if (!space) { space = true; UpdateCursor(); } break;
            case Key.V: ToolMove.IsChecked = true; break;
            case Key.H: ToolHand.IsChecked = true; break;
            case Key.Z: ToolZoom.IsChecked = true; break;
            case Key.F4 when Keyboard.FocusedElement is not ComboBox: Fit(); break; // F4 / Shift+F4 seperti CorelDRAW
            case Key.Delete when sel != null: DeleteSel(); break;
            case Key.R when sel != null: RotateSel(); break;
            case Key.Left when sel != null: EditSel(p => p.X -= step); break;
            case Key.Right when sel != null: EditSel(p => p.X += step); break;
            case Key.Up when sel != null: EditSel(p => p.Y -= step); break;
            case Key.Down when sel != null: EditSel(p => p.Y += step); break;
            default: return;
        }
        e.Handled = true;
    }

    void Window_KeyUp(object s, KeyEventArgs e)
    {
        if (altUsed && e.SystemKey is Key.LeftAlt or Key.RightAlt)
        {
            altUsed = false;
            TrackDrag();
            e.Handled = true;
            return;
        }
        if (e.Key != Key.Space || !space) return;
        space = false;
        UpdateCursor();
        e.Handled = true;
    }

    // ---- Pilih, seret, dan atur ukuran stiker (alat Move) ----
    const double HandlePx = 9;

    static Point Corner(Placed p, int corner) => new(corner is 1 or 2 ? p.X + p.BW : p.X, corner is 2 or 3 ? p.Y + p.BH : p.Y);

    void AddHandles(Canvas c, Placed p)
    {
        for (int i = 0; i < 4; i++)
        {
            var at = Corner(p, i);
            var h = new System.Windows.Shapes.Rectangle
            {
                Width = HandlePx, Height = HandlePx, Fill = Brushes.White, Stroke = (Brush)FindResource("Accent"), StrokeThickness = 1.5, Tag = i,
                Cursor = i % 2 == 0 ? Cursors.SizeNWSE : Cursors.SizeNESW, RenderTransform = unzoom, RenderTransformOrigin = new Point(0.5, 0.5)
            };
            Canvas.SetLeft(h, at.X - HandlePx / 2);
            Canvas.SetTop(h, at.Y - HandlePx / 2);
            c.Children.Add(h);
        }
    }

    void HideHandles()
    {
        foreach (var h in canvases[selSheet].Children.OfType<System.Windows.Shapes.Rectangle>()) if (h.Tag is int) h.Visibility = Visibility.Collapsed;
    }

    // Pratinjau selagi titik pojok ditarik: hanya stiker ini yang diskala. Garis potong dihitung ulang saat mouse dilepas (EndResize).
    void SizeTo(Point pt)
    {
        var (c, p) = (canvases[selSheet], sel!);
        if (sizeTip == null)
        {
            if ((pt - Corner(p, sizing)).Length * zoom.ScaleX < 2) return; // klik tanpa menarik tidak mengubah ukuran
            HideHandles();
            sizeTip = new Border
            {
                Background = (Brush)FindResource("Ink0"), CornerRadius = new CornerRadius(3), Padding = new Thickness(6, 2, 6, 2), IsHitTestVisible = false,
                RenderTransform = unzoom, Child = new TextBlock { Foreground = (Brush)FindResource("Text") }
            };
            c.Children.Add(sizeTip);
        }
        (sizeW, sizeH, sizeBox) = Nest.ResizeTo(p, sizing, pt.X, pt.Y, sheetW, sheetH[selSheet], MarginMm, LockRatio.IsChecked != true);
        foreach (var el in c.Children.OfType<FrameworkElement>()) if (el.Tag == p) el.RenderTransform = Nest.Xf(p, sizeBox.Width / p.BW, sizeBox.Height / p.BH, sizeBox.X, sizeBox.Y);
        var (aw, ah) = p.Rot % 180 == 0 ? (sizeW, sizeH) : (sizeH, sizeW);
        ((TextBlock)sizeTip.Child).Text = $"{F(aw)} × {F(ah)} mm";
        sizeTip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(sizeTip, Math.Clamp(pt.X + 12 * unzoom.ScaleX, 0, Math.Max(0, c.Width - sizeTip.DesiredSize.Width * unzoom.ScaleX)));
        Canvas.SetTop(sizeTip, Math.Clamp(pt.Y + 12 * unzoom.ScaleX, 0, Math.Max(0, c.Height - sizeTip.DesiredSize.Height * unzoom.ScaleX)));
    }

    // Mouse dilepas: ukuran hanya berlaku untuk stiker ini (ResizeSel).
    void EndResize()
    {
        bool changed = sizeTip != null && sel?.S != null && (Math.Abs(sizeW - sel.S.ArtW) >= 0.05 || Math.Abs(sizeH - sel.S.ArtH) >= 0.05);
        (sizing, sizeTip) = (-1, null);
        Sheets.ReleaseMouseCapture();
        if (!changed) { Redraw(); return; }
        var (box, w, h) = (sizeBox, sizeW, sizeH);
        ResizeSel([sel!], _ => (w, h), _ => box.TopLeft);
    }

    // Ubah lebar artwork stiker terpilih saja (keputusan user 2026-11-07): stiker lain dengan desain sama tidak ikut, karena desainnya dipisah menjadi baris
    // baru (Nest.Split). Garis potong dihitung ulang dengan offset, Halus, dan bleed yang dimuat, walau panel sudah diubah. Tiap stiker ditaruh di at(p), lalu
    // pindah ke posisi kosong terdekat seperti saat digeser. Tanpa posisi kosong ukuran baru tetap berlaku, stiker dijaga di dalam area cetak dan ditandai merah.
    // size: lebar dan tinggi artwork baru per desain; tinggi yang mengikuti proporsi sekarang berarti rasio tidak berubah.
    void ResizeSel(List<Placed> picked, Func<Sticker, (double W, double H)> size, Func<Placed, Point> at)
    {
        bool split = false, fits = true, done = false;
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            foreach (var g in picked.GroupBy(p => p.S).ToList())
            {
                var old = g.Key;
                var (w, h) = size(old);
                var n = Loader.Reload(old, old.LoadedKiss, old.LoadedDie, old.LoadedBleed, w, h);
                if (Nest.Split(items, sheets, [.. g], old, n)) split = true;
                else { Replace(old, n); Nest.SwapDesign(sheets, old, n); }
                foreach (var p in g)
                {
                    var pt = at(p);
                    (p.X, p.Y) = (pt.X, pt.Y);
                    int i = sheets.FindIndex(sh => sh.Contains(p));
                    if (i < 0 || Nest.Snap(sheets[i], p, sheetW, sheetH[i], MarginMm, GapMm, HasRegMarks)) continue;
                    Nest.KeepInside(p, sheetW, sheetH[i], MarginMm);
                    fits = false;
                }
            }
            done = true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { MessageBox.Show(ex.Message, App.Brand); }
        finally { Mouse.OverrideCursor = null; }
        List.Items.Refresh();
        CheckAll();
        var p0 = picked[0];
        var (aw, ah) = p0.Rot % 180 == 0 ? (p0.S.ArtW, p0.S.ArtH) : (p0.S.ArtH, p0.S.ArtW);
        Redraw((done ? Lang.T(fits ? "Resized" : "ResizedNoRoom", p0.S.Name, F(aw), F(ah)) + (split ? "  " + Lang.T("ResizedSplit") : "") + "  " : "") + Summary());
        Commit();
        FreeMemory();
    }

    void Sheets_Down(object s, MouseButtonEventArgs e)
    {
        Placed? hit = null;
        Canvas? sheet = null;
        int corner = -1;
        for (var d = e.OriginalSource as DependencyObject; d is Visual; d = VisualTreeHelper.GetParent(d))
        {
            if (d is System.Windows.Shapes.Rectangle { Tag: int k }) corner = k;
            if (hit == null && d is FrameworkElement { Tag: Placed p }) hit = p;
            if (d is Canvas { Tag: int }) { sheet = (Canvas)d; break; }
        }
        View.Focus();
        // Fokus pindah dari kotak isian yang menggambar ulang lembar (lebar di panel Pilihan): klik ini hanya menerapkan isiannya.
        if (sheet != null && !canvases.Contains(sheet)) return;
        bool ctrl = (Keyboard.Modifiers & ModifierKeys.Control) != 0;

        if (sheet == null) { ClearSel(); Redraw(); return; }
        selSheet = (int)sheet.Tag;

        // Klik titik pojok stiker terpilih: mulai ubah ukuran (fitur Welcome-UI).
        if (corner >= 0 && sel != null && sels.Count <= 1)
        {
            sizing = corner;
            sizeGrab = e.GetPosition(sheet) - Corner(sel, corner); // relatif terhadap pojok: stiker tidak melompat saat titiknya baru diklik
            Sheets.CaptureMouse();
            return;
        }

                if (hit != null)
        {
            if (ctrl) Toggle(hit);
            else if (!sels.Contains(hit)) SelectOne(hit);
            else sel = hit;

            if (sels.Count > 0)
            {
                dragGrab = e.GetPosition(sheet);
                drag = new DragState(sels, selSheet);
                Sheets.CaptureMouse();
            }
            
            // Pastikan Redraw dipanggil setelah sel di-set
            Redraw(); 
            return;
        }

                // Klik ruang kosong: mulai kotak seret-pilih (rubber-band) pada lembar ini.
        bool had = sels.Count > 0;
        
        bandFrom = e.GetPosition(sheet);

        if (!ctrl) ClearSel();
        
        // Panggil Redraw() untuk membersihkan seleksi lama (handles & frame)
        if (had) Redraw();

        // Kita tangkap referensi canvas yang BARU dibuat oleh Redraw()
        if (selSheet < 0 || selSheet >= canvases.Count) return;
        var activeCanvas = canvases[selSheet];

        band = new System.Windows.Shapes.Rectangle
        {
            Width = 0, Height = 0,
            Stroke = Nest.SelBrush, StrokeThickness = 0.4, StrokeDashArray = [3, 2],
            Fill = new SolidColorBrush(Color.FromArgb(32, 0, 120, 215)), IsHitTestVisible = false
        };
        Canvas.SetLeft(band, bandFrom.X);
        Canvas.SetTop(band, bandFrom.Y);
        
        // Kita gunakan Dispatcher untuk menunda penangkapan mouse (CaptureMouse)
        // sampai WPF selesai melakukan layout pada Canvas yang baru dibuat oleh Redraw().
        Dispatcher.BeginInvoke(new Action(() => 
        {
            if (band != null) Sheets.CaptureMouse();
        }), DispatcherPriority.Loaded);
    }

    void Sheets_Move(object s, MouseEventArgs e)
    {
        // Pengecekan validasi sebelum mengakses canvases
        if (sizing >= 0)
        {
            if (selSheet >= 0 && selSheet < canvases.Count)
                SizeTo(e.GetPosition(canvases[selSheet]) - sizeGrab);
            return;
        }
        
        if (band == null && drag == null) return;
        if (selSheet < 0 || selSheet >= canvases.Count) return;
        
        var c = canvases[selSheet];

        if (band != null)
        {
            var pt = e.GetPosition(c);
            
            double dx = Math.Abs(pt.X - bandFrom.X);
            double dy = Math.Abs(pt.Y - bandFrom.Y);

            // Threshold: Jangan buat kotak jika pergeseran kurang dari 3 mm
            if (band.Parent == null && dx < 3 && dy < 3) return; 

            double x = Math.Min(pt.X, bandFrom.X), y = Math.Min(pt.Y, bandFrom.Y);
            Canvas.SetLeft(band, x);
            Canvas.SetTop(band, y);
            band.Width = dx;
            band.Height = dy;
            
            // Masukkan band ke canvas yang aktif
            if (band.Parent == null && !c.Children.Contains(band)) c.Children.Add(band);
            return;
        }

        DragToCursor();
    }

    // Stiker mengikuti kursor. Satuannya mm dari lembar asal, jadi zoom dan gulir ikut terhitung; lewat lembar lain pun posisinya tetap di bawah kursor.
    void DragToCursor()
    {
        if (drag == null) return;
        var off = Mouse.GetPosition(canvases[drag.FromSheet]) - dragGrab;
        if (!drag.Moved && off != default)
        {
            HideHandles();
            // Selama ditarik, stiker terlihat di luar lembarnya dan di atas lembar lain. Redraw mengembalikan keduanya.
            foreach (var cv in SelCanvases()) { cv.ClipToBounds = false; Panel.SetZIndex((UIElement)cv.Parent, 1); }
        }
        drag.Offset = off;
        TrackDrag();
        foreach (var cv in SelCanvases()) // hanya tampilan yang bergeser: X dan Y stiker baru berubah saat dilepas
            foreach (var el in cv.Children.OfType<FrameworkElement>())
                if (el.Tag is Placed p && sels.Contains(p)) el.RenderTransform = Nest.Xf(p, 1, 1, p.X + off.X, p.Y + off.Y);
    }

    void Sheets_Up(object s, MouseButtonEventArgs e)
    {
        if (sizing >= 0) { EndResize(); return; }
        if (band != null)
        {
            var box = new Rect(Canvas.GetLeft(band), Canvas.GetTop(band), band.Width, band.Height);
            
            // Hapus band dari canvas yang aktif (jika ada)
            if (selSheet >= 0 && selSheet < canvases.Count) 
                canvases[selSheet].Children.Remove(band);
                
            band = null;
            Sheets.ReleaseMouseCapture();
            
            if (selSheet < 0 || selSheet >= sheets.Count) { Redraw(); return; }
            
            // Pilih stiker yang kotak batasnya beririsan dengan kotak seret.
            foreach (var p in sheets[selSheet])
                if (box.IntersectsWith(new Rect(p.X, p.Y, p.BW, p.BH))) sels.Add(p);
                
            sel = sels.LastOrDefault();
            Redraw();
            return;
        }

        if (drag == null) return;
        TrackDrag();
        var note = drag.Moved ? DropSelection(drag, e) : null;
        drag = null;
        Sheets.ReleaseMouseCapture();
        CheckSel();
        Redraw(note == null ? Summary() : note + "  " + Summary());
        Commit();
    }

    // Lembar tujuan dan mode mengikuti kursor dan tombol Alt; baris status memberi tahu operator.
    void TrackDrag()
    {
        if (drag == null) return;
        (drag.ToSheet, drag.Copy) = (SheetAt(), (Keyboard.Modifiers & ModifierKeys.Alt) != 0);
        altUsed |= drag.Copy;
        if (drag.Moved) Status.Text = Lang.T(drag.Copy ? "DragCopy" : "DragMove", (drag.ToSheet >= 0 ? drag.ToSheet : drag.FromSheet) + 1);
    }

    // Snap magnet: stiker yang dilepas pindah ke posisi kosong terdekat (tidak menabrak, jarak tetap), di lembar lain bila dilepas di atasnya.
    // Alt: salinannya yang ditaruh di sana, stiker asal tetap. Satu saja tidak punya tempat: tidak ada yang berubah. Mengembalikan pesan untuk operator.
    string? DropSelection(DragState d, MouseEventArgs e)
    {
        d.Offset = e.GetPosition(canvases[d.ToSheet >= 0 ? d.ToSheet : d.FromSheet]) - dragGrab; // dari kursor saat dilepas, di lembar tujuan
        string who = SelWho;
        int to = d.ToSheet >= 0 ? d.ToSheet : d.FromSheet;
        if (d.Drop(sheets, sheetH, sheetW, MarginMm, GapMm, HasRegMarks) is not { } placed)
            return d.Copy ? Lang.T("CopyFull", to + 1, who) : d.Away ? Lang.T("SheetFull", to + 1, who) : Lang.T("NoRoom", who);
        if (d.Copy)
        {
            sels.Clear();
            sels.UnionWith(placed);
            sel = placed[^1];
            selSheet = to;
            CountCopies();
            return Lang.T("CopiedTo", who, to + 1);
        }
        if (!d.Away) return null;
        CheckAll(); // lembar asal ikut berubah
        DropEmpty();
        selSheet = sheets.FindIndex(sh => sh.Contains(sel!));
        return Lang.T("MovedTo", who, selSheet + 1);
    }

    // Lembar di bawah kursor; -1 di celah antar-lembar atau di luar tampilan (saat zoom besar lembar bisa berlanjut di bawah panel kanan).
    int SheetAt()
    {
        var v = Mouse.GetPosition(View);
        if (v.X < 0 || v.Y < 0 || v.X > View.ViewportWidth || v.Y > View.ViewportHeight) return -1;
        for (int i = 0; i < sheets.Count; i++)
        {
            var pt = Mouse.GetPosition(canvases[i]);
            if (pt.X >= 0 && pt.Y >= 0 && pt.X <= canvases[i].Width && pt.Y <= canvases[i].Height) return i;
        }
        return -1;
    }

    // Tarikan dibatalkan: dokumen tidak berubah, karena posisi sementara hanya ada di tampilan. Pemanggil menggambar ulang. false = tidak ada yang sedang ditarik.
    bool CancelDrag()
    {
        if (drag == null && sizing < 0 && band == null) return false;
        (band?.Parent as Canvas)?.Children.Remove(band);
        (drag, band, sizing, sizeTip) = (null, null, -1, null);
        Sheets.ReleaseMouseCapture();
        return true;
    }

    // Capture hilang di tengah tarikan (Alt+Tab, dialog, tombol tengah mouse): tanpa ini stiker mengikuti kursor walau tombol sudah dilepas.
    void Sheets_Lost(object s, MouseEventArgs e) { if (CancelDrag()) Redraw(); }

    void ShowHome(bool home)
    {
        HomePage.Visibility = home ? Visibility.Visible : Visibility.Collapsed;
        EditorDock.Visibility = home ? Visibility.Collapsed : Visibility.Visible;

        // Matikan AllowDrop di Window saat di Home, nyalakan saat di Editor
        AllowDrop = !home;
    }

    void MoreItem_Click(object s, RoutedEventArgs e)
    {
        if (s is not Button btn) return;
        if (btn.Resources["MorePopup"] is not Popup popup) return;
        popup.PlacementTarget = btn;
        popup.Placement = PlacementMode.Right;
        if (popup.Child is FrameworkElement child)
        {
            child.DataContext = btn.DataContext;
            if (child is Border border && border.Child is StackPanel stack)
            {
                foreach (var item in stack.Children.OfType<MenuItem>())
                {
                    item.DataContext = btn.DataContext;
                    // Vektor (PDF/AI/CDR) tidak dirapikan: menunya mati dan tooltipnya menjelaskan alasannya.
                    if ((string)item.Tag == "Clean" && btn.DataContext is Sticker st)
                    {
                        item.IsEnabled = st.CanClean;
                        item.ToolTip = Lang.T(st.CanClean ? "CleanTip" : "CleanVector");
                    }
                    // HAPUS dulu event listener yang mungkin menumpuk dari klik sebelumnya
                    item.Click -= MenuAction_Click;
                    // Pasang event listener yang baru
                    item.Click += MenuAction_Click;
                }
            }
        }
        popup.IsOpen = true;
    }

    // Fungsi handler yang bersih (tanpa perlu mencari Parent/Popup)
    void MenuAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item) return;
        if (item.DataContext is not Sticker st) return;
        // Tutup menu dulu: Rapikan dan editor garis membuka jendela dialog, dan menu tidak boleh tertinggal di atasnya.
        if (item.Parent is StackPanel { Parent: Border { Parent: Popup popup } }) popup.IsOpen = false;
        string action = item.Tag?.ToString() ?? "";
        if (action == "Clean")
        {
            Clean(st); // Panggil fungsi Clean asli
        }
        else if (action == "Cut")
        {
            // Kita buat Button dummy karena CutItem_Click butuh sender ber-DataContext
            var dummyBtn = new Button { DataContext = st };
            CutItem_Click(dummyBtn, e);
        }
    }
    void CleanSel_Click(object s, RoutedEventArgs e)
    {
        if (sel?.S is Sticker st) Clean(st);
    }

    void CutSel_Click(object s, RoutedEventArgs e)
    {
        if (sel?.S is Sticker st)
        {
            // Panggil CutItem_Click dengan dummy button agar membaca DataContext
            var dummyBtn = new Button { DataContext = st };
            CutItem_Click(dummyBtn, e);
        }
    }

    void SelCopies_Changed(object s, TextChangedEventArgs e)
    {
        if (restoring || syncingList || syncingSel || sel == null) return; // Tambah syncingSel
        if (int.TryParse(SelCopiesBox.Text, out int newCopies) && newCopies > 0 && sel.S.Copies != newCopies)
        {
            sel.S.Copies = newCopies;
            syncingList = true;
            List.Items.Refresh();
            Redraw();
            syncingList = false;
        }
    }
    
    void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (syncingList || restoring) return; // Abaikan jika sedang sinkronisasi

        if (List.SelectedItem is Sticker st)
        {
            // Jika desain ini sudah ada di dalam sels (multi-select), jangan ganggu.
            if (sels.Any(p => p.S == st)) return;

            // Cari salinan pertama dari desain ini di kanvas
            var target = sheets.SelectMany(x => x).FirstOrDefault(p => p.S == st);
            
            if (target != null)
            {
                SelectOne(target);
                selSheet = sheets.FindIndex(sh => sh.Contains(target));
                Redraw();
            }
            else
            {
                ClearSel();
                Redraw();
            }
        }
        else if (List.SelectedItem == null)
        {
            if (sels.Count > 0) { ClearSel(); Redraw(); }
        }
    }

     void ListCopies_LostFocus(object s, RoutedEventArgs e)
    {
        if (sel != null) Redraw(); 
    }

    // Lebar dan tinggi di panel Pilihan: hanya stiker terpilih yang berubah, seperti titik pojok (keputusan user 2026-11-07). Pojok kiri-atas tetap.
    // Kunci rasio menyala: sisi lainnya ikut sebanding. Dilepas (rasio bebas): sisi lainnya tetap.
    void SelSize_LostFocus(object s, RoutedEventArgs e)
    {
        if (sel == null || MmText.Parse(SelSizeBox.Text) is not { } w || w <= 0 || Math.Abs(w - sel.S.WidthMm) < 0.05) return;
        bool free = LockRatio.IsChecked != true;
        var picked = sels.Where(p => Math.Abs(p.S.ArtW - w) >= 0.05).ToList();
        if (picked.Count > 0) ResizeSel(picked, d => (w, free ? d.ArtH : d.ArtH * w / d.ArtW), p => new Point(p.X, p.Y));
    }

    void SelHeight_LostFocus(object s, RoutedEventArgs e)
    {
        if (sel == null || SelHeightBox.Text == F(sel.S.ArtH) || MmText.Parse(SelHeightBox.Text) is not { } h || h <= 0 || Math.Abs(h - sel.S.ArtH) < 0.05) return; // teks tidak diubah: tinggi tampil dibulatkan
        bool free = LockRatio.IsChecked != true;
        var picked = sels.Where(p => Math.Abs(p.S.ArtH - h) >= 0.05).ToList();
        if (picked.Count > 0) ResizeSel(picked, d => (free ? d.ArtW : d.ArtW * h / d.ArtH, h), p => new Point(p.X, p.Y));
    }
}
