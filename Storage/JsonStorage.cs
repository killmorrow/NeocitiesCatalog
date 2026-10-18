using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace MediaManager.Storage;

public class JsonStorage
{
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public async Task<T?> LoadAsync<T>(string path)
    {
        if (!File.Exists(path))
            return default;

        var json = await File.ReadAllTextAsync(path);

        if (string.IsNullOrWhiteSpace(json))
            return default;

        return JsonSerializer.Deserialize<T>(json, _options);
    }

    public async Task SaveAsync<T>(string path, T data)
    {
        var json = JsonSerializer.Serialize(data, _options);

        await File.WriteAllTextAsync(path, json);
    }
}