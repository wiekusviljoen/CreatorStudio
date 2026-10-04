using System.Windows;
using Microsoft.Win32;

namespace CreatorStudio.App;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import media",
            Filter = "Media files|*.mp4;*.mov;*.mkv;*.avi;*.webm;*.jpg;*.jpeg;*.png;*.webp|All files|*.*",
            Multiselect = true
        };
        if (dialog.ShowDialog() == true)
            MessageBox.Show($"Imported {dialog.FileNames.Length} media file(s).\n\nTimeline/media engine is the next module.", "CreatorStudio");
    }

    private void Media_Click(object sender, RoutedEventArgs e) => Import_Click(sender, e);

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Export pipeline is coming next. The first target will be MP4 for TikTok/Reels/Shorts.", "CreatorStudio");
    }
}