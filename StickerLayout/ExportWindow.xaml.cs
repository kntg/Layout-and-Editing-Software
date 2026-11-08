using System.Windows;
using System.Windows.Controls;

namespace StickerLayout;

public enum ExportKind { Pdf, Svg, Jpg, Ticket }

// Dialog Export: hanya memilih. File ditulis oleh MainWindow.RunExport sesudah lokasi simpan dipilih.
public partial class ExportWindow : Window
{
    readonly List<CheckBox> pages = [];

    // counts: jumlah stiker per lembar, untuk label "Lembar 1 (12 pcs)".
    public ExportWindow(IReadOnlyList<int> counts, ExportKind start)
    {
        InitializeComponent();
        for (int i = 0; i < counts.Count; i++)
        {
            var c = new CheckBox { Content = Lang.T("ExportSheet", i + 1, counts[i]), IsChecked = true, Margin = new Thickness(0, 2, 14, 2) };
            c.Click += (_, _) => { AllPages.IsChecked = pages.All(x => x.IsChecked == true); Update(); };
            pages.Add(c);
            PageList.Children.Add(c);
        }
        (start switch { ExportKind.Svg => FmtSvg, ExportKind.Jpg => FmtJpg, ExportKind.Ticket => FmtTicket, _ => FmtPdf }).IsChecked = true;
        Update();
    }

    public ExportKind Kind => FmtSvg.IsChecked == true ? ExportKind.Svg : FmtJpg.IsChecked == true ? ExportKind.Jpg
        : FmtTicket.IsChecked == true ? ExportKind.Ticket : ExportKind.Pdf;
    public bool Art => Kind is not (ExportKind.Pdf or ExportKind.Svg) || CutOnly.IsChecked != true;   // false = file cutting
    public bool Cuts => Kind is not (ExportKind.Pdf or ExportKind.Svg) || PrintOnly.IsChecked != true; // false = file print
    public bool RegMarks => Marks.IsChecked == true;
    public List<int> Sheets => [.. pages.Select((c, i) => (c, i)).Where(x => x.c.IsChecked == true).Select(x => x.i)]; // indeks lembar

    void Changed(object s, RoutedEventArgs e) => Update();

    void All_Click(object s, RoutedEventArgs e)
    {
        foreach (var c in pages) c.IsChecked = AllPages.IsChecked == true;
        Update();
    }

    // Isi file hanya untuk PDF dan SVG; Print Ticket selalu meringkas semua lembar, tanpa tanda registrasi.
    void Update()
    {
        if (!IsInitialized) return; // Checked dari XAML terpicu sebelum semua kontrol dibuat; konstruktor memanggil Update sesudahnya
        ContentPart.IsEnabled = Kind is ExportKind.Pdf or ExportKind.Svg;
        SheetPart.IsEnabled = Kind != ExportKind.Ticket;
        OkBtn.IsEnabled = Kind == ExportKind.Ticket || pages.Any(c => c.IsChecked == true);
    }

    void Ok_Click(object s, RoutedEventArgs e) => DialogResult = true;

    void Window_SourceInitialized(object s, EventArgs e) => App.DarkTitle(this);
}
