using CreatorStudio.Shared;

namespace CreatorStudio.Mobile;

public partial class MainPage : ContentPage
{
    private readonly MediaProject _project = new() { Name = "Untitled Project" };

    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnCreateProjectClicked(object? sender, EventArgs e)
    {
        _project.Name = $"Project {DateTime.Now:yyyy-MM-dd HHmm}";
        StatusLabel.Text = $"Created: {_project.Name}";
        await DisplayAlert("CreatorStudio", "New project created.", "OK");
    }

    private async void OnImportMediaClicked(object? sender, EventArgs e)
    {
        try
        {
            var files = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                PickerTitle = "Select media"
            });

            if (files is null)
                return;

            foreach (var file in files)
            {
                var type = file.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true ? "photo" : "video";
                _project.Items.Add(new MediaItem { Path = file.FullPath, Type = type });
            }

            StatusLabel.Text = $"{_project.Items.Count} media item(s) in project";
        }
        catch (Exception ex)
        {
            await DisplayAlert("Import failed", ex.Message, "OK");
        }
    }
}
