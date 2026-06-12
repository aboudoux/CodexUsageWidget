using System.Diagnostics;
using System.Text.Json;

namespace CodexUsageWidget.Core;

public interface IUsageProvider
{
    Task<UsageSnapshot> ReadAsync(CancellationToken cancellationToken);
}

public sealed class CodexAppServerClient : IUsageProvider
{
    private readonly TimeProvider _timeProvider;

    public CodexAppServerClient(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<UsageSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        using Process process = StartAppServer();
        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        try
        {
            await SendAsync(process, new
            {
                method = "initialize",
                id = 1,
                @params = new
                {
                    clientInfo = new
                    {
                        name = "codex_usage_widget",
                        title = "Codex Usage Widget",
                        version = "1.0.0"
                    }
                }
            }, cancellationToken);

            JsonElement initialize = await ReadResponseAsync(process, 1, cancellationToken);
            ThrowIfError(initialize);

            await SendAsync(process, new { method = "initialized", @params = new { } }, cancellationToken);
            await SendAsync(process, new { method = "account/rateLimits/read", id = 2 }, cancellationToken);

            JsonElement response = await ReadResponseAsync(process, 2, cancellationToken);
            ThrowIfError(response);
            return CodexRateLimitParser.Parse(
                response.GetProperty("result"),
                _timeProvider.GetUtcNow());
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            string details = await TryReadErrorAsync(errorTask);
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(details)
                    ? "Codex n'a pas renvoye les limites d'utilisation."
                    : $"Codex app-server: {details.Trim()}",
                exception);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
    }

    private static Process StartAppServer()
    {
        string commandProcessor = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
        var startInfo = new ProcessStartInfo
        {
            FileName = commandProcessor,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("/d");
        startInfo.ArgumentList.Add("/s");
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add("codex app-server");

        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("Impossible de demarrer Codex CLI.");
    }

    private static async Task SendAsync(
        Process process,
        object message,
        CancellationToken cancellationToken)
    {
        string json = JsonSerializer.Serialize(message);
        await process.StandardInput.WriteLineAsync(json.AsMemory(), cancellationToken);
        await process.StandardInput.FlushAsync(cancellationToken);
    }

    private static async Task<JsonElement> ReadResponseAsync(
        Process process,
        int expectedId,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            string? line = await process.StandardOutput.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                throw new EndOfStreamException("Codex app-server s'est arrete sans reponse.");
            }

            using JsonDocument document = JsonDocument.Parse(line);
            JsonElement root = document.RootElement;
            if (root.TryGetProperty("id", out JsonElement id) &&
                id.ValueKind == JsonValueKind.Number &&
                id.GetInt32() == expectedId)
            {
                return root.Clone();
            }
        }
    }

    private static void ThrowIfError(JsonElement response)
    {
        if (!response.TryGetProperty("error", out JsonElement error))
        {
            return;
        }

        string message = error.TryGetProperty("message", out JsonElement value)
            ? value.GetString() ?? "Erreur inconnue."
            : "Erreur inconnue.";
        throw new InvalidOperationException(message);
    }

    private static async Task<string> TryReadErrorAsync(Task<string> errorTask)
    {
        try
        {
            return await errorTask.WaitAsync(TimeSpan.FromMilliseconds(250));
        }
        catch
        {
            return string.Empty;
        }
    }
}
