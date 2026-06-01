using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using LocalAiDemos.Shared.DemoData;

namespace LocalAiDemos.Shared.Telemetry;

/// <summary>
/// Append-only JSONL audit log. Every prompt and every cloud-bound payload
/// passes through <see cref="LogPrompt"/> / <see cref="LogCloudEgress"/>, which
/// run the text through <see cref="PiiScanner"/> redaction first.
/// </summary>
public sealed class AuditLogger
{
    private readonly string _path;
    private readonly object _gate = new();

    public AuditLogger(string? path = null)
    {
        _path = path ?? System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LocalAiDemos", "audit.jsonl");
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(_path)!);
    }

    public string Path => _path;

    public void LogPrompt(string source, string prompt) =>
        Append(new { kind = "prompt", source, text = PiiScanner.Redact(prompt) });

    public void LogResponse(string source, string text) =>
        Append(new { kind = "response", source, text = PiiScanner.Redact(text) });

    public void LogCloudEgress(string endpoint, string redactedPayloadPreview) =>
        Append(new { kind = "cloud-egress", endpoint, preview = redactedPayloadPreview });

    public void LogEvent(string kind, object payload) =>
        Append(new { kind, payload });

    private void Append(object record)
    {
        var line = JsonSerializer.Serialize(new
        {
            ts = DateTimeOffset.UtcNow.ToString("O"),
            thread = Thread.CurrentThread.ManagedThreadId,
            record
        });
        lock (_gate)
        {
            File.AppendAllText(_path, line + Environment.NewLine);
        }
    }
}
