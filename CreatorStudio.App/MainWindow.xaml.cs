using System;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace CreatorStudio.App;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private bool _seeking;

    public MainWindow()
    {
        InitializeComponent();
        _timer.Tick += (_, _) => UpdatePlaybackUi();
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Import video or photo", Filter = "Video files|*.mp4;*.mov;*.mkv;*.avi;*.webm|Image files|*.jpg;*.jpeg;*.png;*.webp|All files|*.*", Multiselect = false };
        if (dialog.ShowDialog() != true) return;
        try
        {
            Player.Stop();
            Player.Source = new Uri(dialog.FileName);
            FileNameText.Text = System.IO.Path.GetFileName(dialog.FileName);
            VideoClipText.Text = "VIDEO 1    " + System.IO.Path.GetFileName(dialog.FileName);
            VideoClip.Visibility = Visibility.Visible;
            EmptyTitle.Visibility = Visibility.Collapsed;
            EmptyHint.Visibility = Visibility.Collapsed;
            EditStatus.Text = "Ready. Play, scrub, then split at the playhead.";
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Import failed"); }
    }

    private void Media_Click(object sender, RoutedEventArgs e) => Import_Click(sender, e);

    private void Player_MediaOpened(object sender, RoutedEventArgs e)
    {
        if (Player.NaturalDuration.HasTimeSpan)
        {
            SeekBar.Maximum = Player.NaturalDuration.TimeSpan.TotalSeconds;
            _timer.Start();
            Player.Play();
            PlayButton.Content = "❚❚ Pause";
        }
    }

    private void Player_MediaEnded(object sender, RoutedEventArgs e)
    {
        Player.Stop();
        PlayButton.Content = "▶ Play";
        UpdatePlaybackUi();
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (Player.Source == null) return;
        if (Player.CanPause && PlayButton.Content?.ToString()?.Contains("Pause") == true) { Player.Pause(); PlayButton.Content = "▶ Play"; }
        else { Player.Play(); PlayButton.Content = "❚❚ Pause"; }
    }

    private void SeekBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_seeking && Player.Source != null && Player.NaturalDuration.HasTimeSpan)
            Player.Position = TimeSpan.FromSeconds(e.NewValue);
    }

    private void UpdatePlaybackUi()
    {
        if (Player.Source == null || !Player.NaturalDuration.HasTimeSpan) return;
        _seeking = true;
        SeekBar.Value = Player.Position.TotalSeconds;
        _seeking = false;
        var now = FormatTime(Player.Position);
        var total = FormatTime(Player.NaturalDuration.TimeSpan);
        TimeText.Text = $"{now} / {total}";
        TimelineTime.Text = $"  •  {now}";
    }

    private static string FormatTime(TimeSpan t) => $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";

    private void Split_Click(object sender, RoutedEventArgs e)
    {
        if (Player.Source == null) { EditStatus.Text = "Import a video first."; return; }
        EditStatus.Text = $"Split point marked at {FormatTime(Player.Position)}. Clip model is ready for the next editing-engine step.";
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Player.Source == null) return;
        Player.Stop(); Player.Source = null; SeekBar.Value = 0; SeekBar.Maximum = 1;
        VideoClip.Visibility = Visibility.Collapsed; EmptyTitle.Visibility = Visibility.Visible; EmptyHint.Visibility = Visibility.Visible;
        FileNameText.Text = "No media selected"; EditStatus.Text = "Clip removed from the current project.";
    }

    private void Export_Click(object sender, RoutedEventArgs e) => MessageBox.Show("The next engine module will add real MP4 export with platform presets.", "CreatorStudio");
}