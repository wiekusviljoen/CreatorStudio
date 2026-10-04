using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;

namespace CreatorStudio.App;

public sealed class VideoClip
{
    public string SourcePath { get; init; } = "";
    public string Label { get; set; } = "";
    public TimeSpan SourceIn { get; set; }
    public TimeSpan SourceOut { get; set; }
    public TimeSpan OriginalDuration { get; init; }
    public string DurationText => $"{(SourceOut - SourceIn).TotalSeconds:0.0}s";
}

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly ObservableCollection<VideoClip> _clips = new();
    private bool _seeking;
    private VideoClip? _selected;

    public MainWindow()
    {
        InitializeComponent();
        ClipList.ItemsSource = _clips;
        _timer.Tick += (_, _) => UpdatePlaybackUi();
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var d = new OpenFileDialog { Title = "Import video", Filter = "Video files|*.mp4;*.mov;*.mkv;*.avi;*.webm|All files|*.*" };
        if (d.ShowDialog() != true) return;
        Player.Stop(); Player.Source = new Uri(d.FileName);
        FileNameText.Text = System.IO.Path.GetFileName(d.FileName);
        EmptyTitle.Visibility = Visibility.Collapsed; EmptyHint.Visibility = Visibility.Collapsed;
        EditStatus.Text = "Loading videoâ€¦";
    }

    private void Media_Click(object sender, RoutedEventArgs e) => Import_Click(sender, e);

    private void Player_MediaOpened(object sender, RoutedEventArgs e)
    {
        if (!Player.NaturalDuration.HasTimeSpan) return;
        var duration = Player.NaturalDuration.TimeSpan;
        if (_clips.Count == 0)
        {
            _clips.Add(new VideoClip { SourcePath = Player.Source!.LocalPath, Label = "Clip 1 â€¢ " + System.IO.Path.GetFileName(Player.Source.LocalPath), SourceIn = TimeSpan.Zero, SourceOut = duration, OriginalDuration = duration });
            ClipList.SelectedIndex = 0;
        }
        SeekBar.Maximum = duration.TotalSeconds;
        _timer.Start(); Player.Play(); PlayButton.Content = "âšâš Pause"; EditStatus.Text = "Video loaded. Split or trim the selected clip.";
    }

    private void Player_MediaEnded(object sender, RoutedEventArgs e) { Player.Stop(); PlayButton.Content = "â–¶ Play"; }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (Player.Source == null) return;
        if (PlayButton.Content?.ToString()?.Contains("Pause") == true) { Player.Pause(); PlayButton.Content = "â–¶ Play"; }
        else { Player.Play(); PlayButton.Content = "âšâš Pause"; }
    }

    private void SeekBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_seeking && Player.Source != null) Player.Position = TimeSpan.FromSeconds(e.NewValue);
    }

    private void UpdatePlaybackUi()
    {
        if (Player.Source == null || !Player.NaturalDuration.HasTimeSpan) return;
        _seeking = true; SeekBar.Value = Player.Position.TotalSeconds; _seeking = false;
        TimeText.Text = $"{FormatTime(Player.Position)} / {FormatTime(Player.NaturalDuration.TimeSpan)}";
    }

    private void ClipList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        _selected = ClipList.SelectedItem as VideoClip;
        if (_selected == null) return;
        ClipInfo.Text = $"{_selected.Label}\nIn {_selected.SourceIn.TotalSeconds:0.0}s  â€¢  Out {_selected.SourceOut.TotalSeconds:0.0}s  â€¢  {_selected.DurationText}";
        if (Player.Source != null) Player.Position = _selected.SourceIn;
    }

    private void TrimIn_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        var next = _selected.SourceIn + TimeSpan.FromSeconds(1);
        if (next < _selected.SourceOut - TimeSpan.FromMilliseconds(100)) { _selected.SourceIn = next; RefreshSelected(); }
    }

    private void TrimOut_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        var next = _selected.SourceOut - TimeSpan.FromSeconds(1);
        if (next > _selected.SourceIn + TimeSpan.FromMilliseconds(100)) { _selected.SourceOut = next; RefreshSelected(); }
    }

    private void ResetTrim_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null) return;
        _selected.SourceIn = TimeSpan.Zero; _selected.SourceOut = _selected.OriginalDuration; RefreshSelected();
    }

    private void Split_Click(object sender, RoutedEventArgs e)
    {
        if (_selected == null || Player.Source == null) { EditStatus.Text = "Select a clip first."; return; }
        var cut = Player.Position;
        if (cut <= _selected.SourceIn + TimeSpan.FromMilliseconds(150) || cut >= _selected.SourceOut - TimeSpan.FromMilliseconds(150)) { EditStatus.Text = "Move the playhead inside the selected clip before splitting."; return; }
        var index = _clips.IndexOf(_selected);
        var left = new VideoClip { SourcePath = _selected.SourcePath, Label = $"Clip {index + 1} â€¢ Part A", SourceIn = _selected.SourceIn, SourceOut = cut, OriginalDuration = _selected.OriginalDuration };
        var right = new VideoClip { SourcePath = _selected.SourcePath, Label = $"Clip {index + 2} â€¢ Part B", SourceIn = cut, SourceOut = _selected.SourceOut, OriginalDuration = _selected.OriginalDuration };
        _clips.RemoveAt(index); _clips.Insert(index, left); _clips.Insert(index + 1, right); ClipList.SelectedIndex = index + 1;
        EditStatus.Text = $"Split at {FormatTime(cut)}. Two independent clip ranges now exist.";
    }

    private void RefreshSelected()
    {
        ClipList.Items.Refresh();
        ClipInfo.Text = $"{_selected!.Label}\nIn {_selected.SourceIn.TotalSeconds:0.0}s  â€¢  Out {_selected.SourceOut.TotalSeconds:0.0}s  â€¢  {_selected.DurationText}";
        EditStatus.Text = "Trim updated. The edit is non-destructive.";
    }

    private static string FormatTime(TimeSpan t) => $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("The clip model is now ready for real MP4 rendering. Next export step will render these ranges through FFmpeg.", "CreatorStudio");
    }
}
