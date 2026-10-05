using CreatorStudio.Shared;
using CommunityToolkit.Maui.Views;

namespace CreatorStudio.Mobile;

public sealed class MainPage : ContentPage
{
    private readonly MediaProject _project = new() { Name = "Untitled Project" };
    private readonly Label _statusLabel;
    private readonly Label _selectedLabel;
    private readonly Label _detailsLabel;
    private readonly Slider _brightness;
    private readonly Slider _trimStart;
    private readonly Slider _trimEnd;
    private readonly VerticalStackLayout _timeline;
    private readonly Image _photoPreview;
    private readonly MediaElement _videoPreview;
    private string? _projectPath;
    private int _selectedIndex = -1;

    public MainPage()
    {
        BackgroundColor = Color.FromArgb("#111412");

        var title = new Label { Text = "CreatorStudio", TextColor = Colors.White, FontSize = 30, FontAttributes = FontAttributes.Bold };
        var subtitle = new Label { Text = "Edit anywhere. Finish on phone or desktop.", TextColor = Color.FromArgb("#AEB8B1"), FontSize = 15 };

        var newButton = MakeButton("＋ New Project", "#315A45");
        newButton.Clicked += OnCreateProjectClicked;
        var importButton = MakeButton("＋ Import Media", "#242A26");
        importButton.Clicked += OnImportMediaClicked;
        var saveButton = MakeButton("Save Project", "#242A26");
        saveButton.Clicked += async (_, _) => await SaveProjectInternalAsync();
        var shareButton = MakeButton("Share Project", "#315A45");
        shareButton.Clicked += OnShareProjectClicked;

        _selectedLabel = new Label { Text = "No clip selected", TextColor = Colors.White, FontSize = 18, FontAttributes = FontAttributes.Bold };
        _detailsLabel = new Label { Text = "Import photos or videos to begin.", TextColor = Color.FromArgb("#AEB8B1"), FontSize = 13 };

        _photoPreview = new Image
        {
            HeightRequest = 220,
            Aspect = Aspect.AspectFit,
            BackgroundColor = Color.FromArgb("#090B0A"),
            IsVisible = false
        };

        _videoPreview = new MediaElement
        {
            HeightRequest = 220,
            Aspect = Aspect.AspectFit,
            BackgroundColor = Color.FromArgb("#090B0A"),
            ShouldAutoPlay = false,
            ShouldShowPlaybackControls = true,
            IsVisible = false
        };
        _videoPreview.MediaOpened += OnVideoOpened;
        _videoPreview.MediaFailed += (_, e) => _statusLabel.Text = $"Video preview failed: {e.ErrorMessage}";

        _timeline = new VerticalStackLayout { Spacing = 8 };

        var trimIn = MakeSmallButton("Trim In −1s");
        trimIn.Clicked += (_, _) => AdjustTrim(true);
        var trimOut = MakeSmallButton("Trim Out −1s");
        trimOut.Clicked += (_, _) => AdjustTrim(false);
        var reset = MakeSmallButton("Reset Trim");
        reset.Clicked += (_, _) => ResetTrim();

        var split = MakeSmallButton("Split Here");
        split.Clicked += (_, _) => SplitSelected();
        var delete = MakeSmallButton("Delete");
        delete.Clicked += (_, _) => DeleteSelected();
        var left = MakeSmallButton("← Move");
        left.Clicked += (_, _) => MoveSelected(-1);
        var right = MakeSmallButton("Move →");
        right.Clicked += (_, _) => MoveSelected(1);

        var rotate = MakeSmallButton("↻ Rotate");
        rotate.Clicked += (_, _) => RotateSelected();
        var zoom = MakeSmallButton("+ Zoom");
        zoom.Clicked += (_, _) => ZoomSelected();

        _trimStart = new Slider { Minimum = 0, Maximum = 1, IsVisible = false };
        _trimEnd = new Slider { Minimum = 0, Maximum = 1, IsVisible = false };
        _brightness = new Slider { Minimum = 0.25, Maximum = 1, Value = 1 };
        _brightness.ValueChanged += (_, e) =>
        {
            if (Selected is { } c && c.Type == "photo")
            {
                c.Brightness = e.NewValue;
                _photoPreview.Opacity = e.NewValue;
            }
        };

        var controls = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                _selectedLabel, _detailsLabel,
                new HorizontalStackLayout { Spacing = 6, Children = { trimIn, trimOut, reset } },
                new HorizontalStackLayout { Spacing = 6, Children = { split, delete } },
                new HorizontalStackLayout { Spacing = 6, Children = { left, right } },
                new HorizontalStackLayout { Spacing = 6, Children = { rotate, zoom } },
                new Label { Text = "Brightness", TextColor = Color.FromArgb("#AEB8B1"), FontSize = 12 },
                _brightness
            }
        };

        _statusLabel = new Label
        {
            Text = "Ready",
            TextColor = Color.FromArgb("#8FAF9A"),
            FontSize = 13
        };

        var layout = new VerticalStackLayout
        {
            Padding = new Thickness(18, 42, 18, 28),
            Spacing = 12,
            Children =
            {
                title, subtitle,
                new HorizontalStackLayout { Spacing = 6, Children = { newButton, importButton } },
                new HorizontalStackLayout { Spacing = 6, Children = { saveButton, shareButton } },
                _videoPreview,
                _photoPreview,
                new Label { Text = "TIMELINE", TextColor = Color.FromArgb("#7F9C88"), FontSize = 12, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 8, 0, 0) },
                _timeline,
                new Border { Stroke = Color.FromArgb("#2A302C"), StrokeThickness = 1, Padding = 12, Content = controls },
                _statusLabel
            }
        };

        Content = new ScrollView { Content = layout };
        RefreshTimeline();
    }

    private MediaItem? Selected => _selectedIndex >= 0 && _selectedIndex < _project.Items.Count ? _project.Items[_selectedIndex] : null;

    private static Button MakeButton(string text, string background) => new()
    {
        Text = text, BackgroundColor = Color.FromArgb(background), TextColor = Colors.White,
        CornerRadius = 10, HeightRequest = 48, HorizontalOptions = LayoutOptions.Fill
    };

    private static Button MakeSmallButton(string text) => new()
    {
        Text = text, BackgroundColor = Color.FromArgb("#242A26"), TextColor = Colors.White,
        CornerRadius = 8, FontSize = 12, HeightRequest = 42, HorizontalOptions = LayoutOptions.Fill
    };

    private async void OnCreateProjectClicked(object? sender, EventArgs e)
    {
        _project.Name = $"Project {DateTime.Now:yyyy-MM-dd HHmm}";
        _project.Items.Clear();
        _projectPath = null;
        _selectedIndex = -1;
        await SaveProjectInternalAsync();
        RefreshTimeline();
        _statusLabel.Text = $"Created: {_project.Name}";
    }

    private async void OnImportMediaClicked(object? sender, EventArgs e)
    {
        try
        {
            var files = await FilePicker.Default.PickMultipleAsync(new PickOptions { PickerTitle = "Select photos or videos" });
            if (files is null) return;

            foreach (var file in files)
            {
                var type = file.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true ? "photo" : "video";
                _project.Items.Add(new MediaItem
                {
                    Path = file.FullPath,
                    Type = type,
                    DurationSeconds = type == "photo" ? 5 : 0,
                    StartSeconds = 0,
                    EndSeconds = type == "photo" ? 5 : 0
                });
            }

            if (_selectedIndex < 0 && _project.Items.Count > 0) _selectedIndex = 0;
            await SaveProjectInternalAsync();
            RefreshTimeline();
            _statusLabel.Text = $"{_project.Items.Count} item(s) in timeline";
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Import failed", ex.Message, "OK");
        }
    }

    private async Task SaveProjectInternalAsync()
    {
        try
        {
            _projectPath ??= Path.Combine(FileSystem.AppDataDirectory, _project.Name + ProjectFileService.Extension);
            await ProjectFileService.SaveAsync(_project, _projectPath);
            _statusLabel.Text = $"Saved: {_project.Name}";
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Save failed", ex.Message, "OK");
        }
    }

    private async void OnShareProjectClicked(object? sender, EventArgs e)
    {
        try
        {
            await SaveProjectInternalAsync();
            if (!string.IsNullOrWhiteSpace(_projectPath) && File.Exists(_projectPath))
                await Share.Default.RequestAsync(new ShareFileRequest { Title = "Send CreatorStudio project", File = new ShareFile(_projectPath) });
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Share failed", ex.Message, "OK");
        }
    }

    private void OnVideoOpened(object? sender, EventArgs e)
    {
        if (Selected is not { } c || c.Type != "video") return;
        var seconds = _videoPreview.Duration.TotalSeconds;
        if (seconds > 0)
        {
            c.DurationSeconds = seconds;
            if (c.EndSeconds <= 0 || c.EndSeconds > seconds) c.EndSeconds = seconds;
            if (c.StartSeconds >= c.EndSeconds) c.StartSeconds = 0;
            _detailsLabel.Text = $"VIDEO • {c.EndSeconds - c.StartSeconds:0.0}s • In {c.StartSeconds:0.0}s • Out {c.EndSeconds:0.0}s";
            _ = SaveProjectInternalAsync();
            RefreshTimeline();
        }
    }

    private void Select(int index)
    {
        if (index < 0 || index >= _project.Items.Count) return;
        _selectedIndex = index;
        var c = Selected!;
        _selectedLabel.Text = $"{index + 1}. {Path.GetFileName(c.Path)}";
        var duration = Math.Max(0, c.EndSeconds - c.StartSeconds);
        _detailsLabel.Text = $"{c.Type.ToUpperInvariant()} • {duration:0.0}s • In {c.StartSeconds:0.0}s • Out {c.EndSeconds:0.0}s • Zoom {c.Zoom:0.0}x";
        _brightness.Value = c.Brightness;
        _videoPreview.IsVisible = false;
        _photoPreview.IsVisible = false;
        if (c.Type == "video" && File.Exists(c.Path))
        {
            _videoPreview.Source = MediaSource.FromFile(c.Path);
            _videoPreview.IsVisible = true;
            _ = _videoPreview.SeekTo(TimeSpan.FromSeconds(Math.Max(0, c.StartSeconds)));
        }
        else if (c.Type == "photo" && File.Exists(c.Path))
        {
            _photoPreview.Source = ImageSource.FromFile(c.Path);
            _photoPreview.IsVisible = true;
            _photoPreview.Opacity = c.Brightness;
            _photoPreview.Rotation = c.Rotation;
            _photoPreview.Scale = c.Zoom;
        }
    }

    private void AdjustTrim(bool trimIn)
    {
        if (Selected is not { } c || c.DurationSeconds <= 0) return;
        if (trimIn)
        {
            var next = c.StartSeconds + 1;
            if (next < c.EndSeconds - 0.1) c.StartSeconds = next;
        }
        else
        {
            var next = c.EndSeconds - 1;
            if (next > c.StartSeconds + 0.1) c.EndSeconds = next;
        }
        Select(_selectedIndex);
        _ = SaveProjectInternalAsync();
        RefreshTimeline();
    }

    private void ResetTrim()
    {
        if (Selected is not { } c) return;
        c.StartSeconds = 0;
        c.EndSeconds = c.DurationSeconds;
        Select(_selectedIndex);
        _ = SaveProjectInternalAsync();
    }

    private void SplitSelected()
    {
        if (Selected is not { } c || c.EndSeconds <= c.StartSeconds + 0.2) return;
        var cut = c.StartSeconds + (c.EndSeconds - c.StartSeconds) / 2.0;
        var left = new MediaItem { Path = c.Path, Type = c.Type, DurationSeconds = c.DurationSeconds, StartSeconds = c.StartSeconds, EndSeconds = cut, Rotation = c.Rotation, Zoom = c.Zoom, Brightness = c.Brightness };
        var right = new MediaItem { Path = c.Path, Type = c.Type, DurationSeconds = c.DurationSeconds, StartSeconds = cut, EndSeconds = c.EndSeconds, Rotation = c.Rotation, Zoom = c.Zoom, Brightness = c.Brightness };
        _project.Items.RemoveAt(_selectedIndex);
        _project.Items.Insert(_selectedIndex, left);
        _project.Items.Insert(_selectedIndex + 1, right);
        RefreshTimeline();
        Select(_selectedIndex + 1);
        _ = SaveProjectInternalAsync();
    }

    private void DeleteSelected()
    {
        if (Selected is null) return;
        _project.Items.RemoveAt(_selectedIndex);
        _selectedIndex = Math.Min(_selectedIndex, _project.Items.Count - 1);
        RefreshTimeline();
        if (_selectedIndex >= 0) Select(_selectedIndex);
        else { _selectedLabel.Text = "No clip selected"; _detailsLabel.Text = "Import media to begin."; _photoPreview.IsVisible = false; }
        _ = SaveProjectInternalAsync();
    }

    private void MoveSelected(int delta)
    {
        if (Selected is null) return;
        var target = _selectedIndex + delta;
        if (target < 0 || target >= _project.Items.Count) return;
        (_project.Items[_selectedIndex], _project.Items[target]) = (_project.Items[target], _project.Items[_selectedIndex]);
        _selectedIndex = target;
        RefreshTimeline();
        Select(_selectedIndex);
        _ = SaveProjectInternalAsync();
    }

    private void RotateSelected()
    {
        if (Selected is not { } c || c.Type != "photo") return;
        c.Rotation = (c.Rotation + 90) % 360;
        Select(_selectedIndex);
        _ = SaveProjectInternalAsync();
    }

    private void ZoomSelected()
    {
        if (Selected is not { } c || c.Type != "photo") return;
        c.Zoom = Math.Min(2.5, c.Zoom + 0.1);
        Select(_selectedIndex);
        _ = SaveProjectInternalAsync();
    }

    private void RefreshTimeline()
    {
        _timeline.Children.Clear();
        for (var i = 0; i < _project.Items.Count; i++)
        {
            var index = i;
            var c = _project.Items[i];
            var duration = Math.Max(0, c.EndSeconds - c.StartSeconds);
            var button = new Button
            {
                Text = $"{i + 1}. {Path.GetFileName(c.Path)}   •   {duration:0.0}s",
                BackgroundColor = i == _selectedIndex ? Color.FromArgb("#315A45") : Color.FromArgb("#202520"),
                TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.Fill,
                HeightRequest = 48,
                CornerRadius = 8
            };
            button.Clicked += (_, _) => Select(index);
            _timeline.Children.Add(button);
        }
    }
}
