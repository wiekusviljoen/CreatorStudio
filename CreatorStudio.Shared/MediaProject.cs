namespace CreatorStudio.Shared;

public sealed class MediaProject
{
    public string Name { get; set; } = "Untitled Project";
    public List<MediaItem> Items { get; set; } = new();
}

public sealed class MediaItem
{
    public string Path { get; set; } = "";
    public string Type { get; set; } = "video";
    public double DurationSeconds { get; set; }
    public double StartSeconds { get; set; }
    public double EndSeconds { get; set; }
    public double Rotation { get; set; }
    public double Zoom { get; set; } = 1;
    public double Brightness { get; set; } = 1;
}
