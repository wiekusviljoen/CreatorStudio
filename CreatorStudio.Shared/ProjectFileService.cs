using System.Text.Json;

namespace CreatorStudio.Shared;

public static class ProjectFileService
{
    public const string Extension = ".creatorstudio";

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static async Task SaveAsync(MediaProject project, string path)
    {
        var json = JsonSerializer.Serialize(project, Options);
        await File.WriteAllTextAsync(path, json);
    }

    public static async Task<MediaProject> LoadAsync(string path)
    {
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<MediaProject>(json, Options)
            ?? new MediaProject();
    }
}
