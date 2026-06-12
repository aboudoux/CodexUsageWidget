using System.Text.Json;

namespace CodexUsageWidget.Core;

public interface IWidgetStateStore
{
    Task<WidgetSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(WidgetSettings settings, CancellationToken cancellationToken = default);
}

public sealed class WidgetStateStore : IWidgetStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public WidgetStateStore(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexUsageWidget",
            "settings.json");
    }

    public async Task<WidgetSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return new WidgetSettings();
        }

        try
        {
            await using FileStream stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<WidgetSettings>(
                stream,
                JsonOptions,
                cancellationToken) ?? new WidgetSettings();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return new WidgetSettings();
        }
    }

    public async Task SaveAsync(
        WidgetSettings settings,
        CancellationToken cancellationToken = default)
    {
        string? directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string temporaryPath = _filePath + ".tmp";
        await using (FileStream stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken);
        }

        File.Move(temporaryPath, _filePath, overwrite: true);
    }
}
