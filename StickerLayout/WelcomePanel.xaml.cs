using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace StickerLayout;

public partial class WelcomePanel : UserControl
{
    public WelcomePanel()
    {
        InitializeComponent();
    }

    // ---- Sementara: Tombol sidebar (nanti diimplementasikan) ----
    private void BtnNew_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("New File diklik");
    }

    private void BtnRecent_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Recent File diklik");
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Settings diklik");
    }

    private void BtnHelp_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Help diklik");
    }

    // ---- Tombol Open Folder (di dalam Drop Zone) ----
    private void BtnOpenFolder_Click(object sender, MouseButtonEventArgs e)
    {
        if (Window.GetWindow(this) is MainWindow main)
        {
            main.OpenFileDialog();
        }
    }

        // ---- Drag & Drop KHUSUS di dalam Border ----
    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
            // Opsional: Beri efek visual saat file di-drag ke atas border
            DropZone.BorderBrush = (Brush)FindResource("Accent"); 
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

        // Tambahkan event DragLeave di WelcomePanel.xaml:
    // DragLeave="DropZone_DragLeave"
    
    private void DropZone_DragLeave(object sender, DragEventArgs e)
    {
        DropZone.BorderBrush = (Brush)FindResource("LineHi");
        DropIcon.Foreground = (Brush)FindResource("Dim");
        
        // Teruskan ke MainWindow agar DropHint di sana ikut hilang
        if (Window.GetWindow(this) is MainWindow main)
            main.Window_DragLeave(sender, e);
    }

    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        DropZone.BorderBrush = (Brush)FindResource("LineHi");
        DropIcon.Foreground = (Brush)FindResource("Dim");

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0 && Window.GetWindow(this) is MainWindow main)
            {
                // Panggil AddFiles LANGSUNG
                main.AddFiles(files); 
            }
        }
        e.Handled = true;
    }
}