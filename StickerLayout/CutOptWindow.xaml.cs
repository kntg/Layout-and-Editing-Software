using System.Windows;
using System.Windows.Controls;

namespace StickerLayout;

public partial class CutOptWindow : Window
{
    public (Cut Kiss, Cut Die)? Result { get; private set; }

    public CutOptWindow(Sticker st, Cut panelKiss, Cut panelDie)
    {
        InitializeComponent();
        Title = Lang.T("CutOptTitle", st.Name);

        if (st.OwnCut)
        {
            FollowBox.IsChecked = false;
            KissOn.IsChecked = st.LoadedKiss.On;
            KissOff.Text = MmText.Show(st.LoadedKiss.Off);
            KissSm.Text = MmText.Show(st.LoadedKiss.Smooth);
            DieOn.IsChecked = st.LoadedDie.On;
            DieOff.Text = MmText.Show(st.LoadedDie.Off);
            DieSm.Text = MmText.Show(st.LoadedDie.Smooth);
        }
        else
        {
            FollowBox.IsChecked = true;
            KissOn.IsChecked = panelKiss.On;
            KissOff.Text = MmText.Show(panelKiss.Off);
            KissSm.Text = MmText.Show(panelKiss.Smooth);
            DieOn.IsChecked = panelDie.On;
            DieOff.Text = MmText.Show(panelDie.Off);
            DieSm.Text = MmText.Show(panelDie.Smooth);
        }
        UpdateState();
    }

    void Window_SourceInitialized(object s, EventArgs e) => App.DarkTitle(this);

    void Follow_Changed(object s, RoutedEventArgs e) => UpdateState();

    void UpdateState()
    {
        if (CustomBorder != null)
        {
            bool custom = FollowBox.IsChecked != true;
            CustomBorder.IsEnabled = custom;
            CustomBorder.Opacity = custom ? 1.0 : 0.5;
        }
    }

    static double D(TextBox t, double def, double min = 0) => MmText.Parse(t.Text) is { } v && v >= min ? v : def;

    void Ok_Click(object s, RoutedEventArgs e)
    {
        if (FollowBox.IsChecked == true)
        {
            Result = null;
            DialogResult = true;
            Close();
            return;
        }

        Cut kiss = new(KissOn.IsChecked == true, D(KissOff, 2, 0), D(KissSm, 0, 0));
        Cut die = new(DieOn.IsChecked == true, D(DieOff, 4, 0), D(DieSm, 0, 0));

        string? err = Cut.Error(kiss, die);
        if (err != null)
        {
            MessageBox.Show(Lang.T(err), App.Brand, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Result = (kiss, die);
        DialogResult = true;
        Close();
    }
}

