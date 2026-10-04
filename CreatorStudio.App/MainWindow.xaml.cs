using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
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
        FileNameText.Text = Path.GetFileName(d.FileName);
        EmptyTitle.Visibility = Visibility.Collapsed; EmptyHint.Visibility = Visibility.Collapsed;
        EditStatus.Text = "Loading video…";
    }

    private void Media_Click(object sender, RoutedEventArgs e) => Import_Click(sender, e);

    private void Player_MediaOpened(object sender, RoutedEventArgs e)
    {
        if (!Player.NaturalDuration.HasTimeSpan) return;
        var duration = Player.NaturalDuration.TimeSpan;
        if (_clips.Count == 0)
        {
            _clips.Add(new VideoClip { SourcePath = Player.Source!.LocalPath, Label = "Clip 1 • " + Path.GetFileName(Player.Source.LocalPath), SourceIn = TimeSpan.Zero, SourceOut = duration, OriginalDuration = duration });
            ClipList.SelectedIndex = 0;
        }
        SeekBar.Maximum = duration.TotalSeconds;
        _timer.Start(); Player.Play(); PlayButton.Content = "❚❚ Pause";
        EditStatus.Text = "Video loaded. Split, trim, reorder, then export.";
    }

    private void Player_MediaEnded(object sender, RoutedEventArgs e) { Player.Stop(); PlayButton.Content = "▶ Play"; }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (Player.Source == null) return;
        if (PlayButton.Content?.ToString()?.Contains("Pause") == true) { Player.Pause(); PlayButton.Content = "▶ Play"; }
        else { Player.Play(); PlayButton.Content = "❚❚ Pause"; }
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
        ClipInfo.Text = $"{_selected.Label}\nIn {_selected.SourceIn.TotalSeconds:0.0}s  •  Out {_selected.SourceOut.TotalSeconds:0.0}s  •  {_selected.DurationText}";
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
        var left = new VideoClip { SourcePath = _selected.SourcePath, Label = $"Clip {index + 1} • Part A", SourceIn = _selected.SourceIn, SourceOut = cut, OriginalDuration = _selected.OriginalDuration };
        var right = new VideoClip { SourcePath = _selected.SourcePath, Label = $"Clip {index + 2} • Part B", SourceIn = cut, SourceOut = _selected.SourceOut, OriginalDuration = _selected.OriginalDuration };
        _clips.RemoveAt(index); _clips.Insert(index, left); _clips.Insert(index + 1, right); ClipList.SelectedIndex = index + 1;
        EditStatus.Text = $"Split at {FormatTime(cut)}.";
    }

    private void RefreshSelected()
    {
        ClipList.Items.Refresh();
        ClipInfo.Text = $"{_selected!.Label}\nIn {_selected.SourceIn.TotalSeconds:0.0}s  •  Out {_selected.SourceOut.TotalSeconds:0.0}s  •  {_selected.DurationText}";
        EditStatus.Text = "Trim updated. The edit is non-destructive.";
    }

    private static string FormatTime(TimeSpan t) => $"{(int)t.TotalMinutes:00}:{t.Seconds:00}";

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_clips.Count == 0) { MessageBox.Show("Import a video first.", "CreatorStudio"); return; }
        var ffmpeg = FindTool("ffmpeg.exe");
        if (ffmpeg == null)
        {
            MessageBox.Show("FFmpeg is not installed yet. The export engine is ready, but the encoder must be installed on this PC.", "CreatorStudio");
            return;
        }

        var dialog = new SaveFileDialog { Title = "Export CreatorStudio video", Filter = "MP4 video|*.mp4", FileName = "creatorstudio-export.mp4" };
        if (dialog.ShowDialog() != true) return;

        try
        {
            EditStatus.Text = "Preparing multi-clip export…";
            var hasAudio = await HasAudioAsync(ffmpeg, _clips[0].SourcePath);
            var result = await RenderTimelineAsync(ffmpeg, dialog.FileName, hasAudio);
            EditStatus.Text = "Export complete.";
            MessageBox.Show(result ? $"Export complete:\n{dialog.FileName}" : "FFmpeg reported an export error. Check the source media and try again.", "CreatorStudio");
        }
        catch (Exception ex)
        {
            EditStatus.Text = "Export failed.";
            MessageBox.Show(ex.Message, "CreatorStudio export error");
        }
    }

    private async System.Threading.Tasks.Task<bool> HasAudioAsync(string ffprobeOrFfmpeg, string source)
    {
        var ffprobe = FindTool("ffprobe.exe");
        if (ffprobe == null) return true;
        var psi = new ProcessStartInfo(ffprobe, $"-v error -select_streams a:0 -show_entries stream=index -of csv=p=0 \"{source}\"")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
        using var p = Process.Start(psi)!;
        var output = await p.StandardOutput.ReadToEndAsync();
        await p.WaitForExitAsync();
        return !string.IsNullOrWhiteSpace(output);
    }

    private async System.Threading.Tasks.Task<bool> RenderTimelineAsync(string ffmpeg, string output, bool hasAudio)
    {
        var sources = _clips.Select(c => c.SourcePath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var args = new StringBuilder("-y ");
        foreach (var source in sources) args.Append($"-i \"{source}\" ");

        var filters = new StringBuilder();
        for (var i = 0; i < _clips.Count; i++)
        {
            var clip = _clips[i];
            var input = sources.FindIndex(s => string.Equals(s, clip.SourcePath, StringComparison.OrdinalIgnoreCase));
            var start = clip.SourceIn.TotalSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            var end = clip.SourceOut.TotalSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            filters.Append($"[{input}:v]trim=start={start}:end={end},setpts=PTS-STARTPTS[v{i}];");
            if (hasAudio) filters.Append($"[{input}:a]atrim=start={start}:end={end},asetpts=PTS-STARTPTS[a{i}];");
        }

        filters.Append("[v0]");
        for (var i = 1; i < _clips.Count; i++) filters.Append($"[v{i}]");
        if (hasAudio)
        {
            for (var i = 0; i < _clips.Count; i++) filters.Append($"[a{i}]");
            filters.Append($"concat=n={_clips.Count}:v=1:a=1[v][a]");
        }
        else filters.Append($"concat=n={_clips.Count}:v=1:a=0[v]");

        args.Append($"-filter_complex \"{filters}\" -map \"[v]\" ");
        if (hasAudio) args.Append("-map \"[a]\" -c:a aac ");
        args.Append($"-c:v libx264 -preset medium -crf 18 -pix_fmt yuv420p -movflags +faststart \"{output}\"");

        var psi = new ProcessStartInfo(ffmpeg, args.ToString())
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        using var process = Process.Start(psi)!;
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return process.ExitCode == 0;
    }

    private static string? FindTool(string fileName)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, fileName),
            Path.Combine(AppContext.BaseDirectory, "tools", fileName),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "tools", fileName))
        };
        foreach (var candidate in candidates) if (File.Exists(candidate)) return candidate;
        try
        {
            var p = Process.Start(new ProcessStartInfo("where", fileName) { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true });
            if (p != null) { var path = p.StandardOutput.ReadLine(); p.WaitForExit(1000); if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) return path; }
        }
        catch { }
        return null;
    }
}