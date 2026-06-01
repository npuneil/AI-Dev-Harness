using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using LocalAiDemos.Shared.AI;
using LocalAiDemos.Shared.Telemetry;

namespace HybridAIDevHarness.Cloud;

public interface ICloudChatClient : IChatClient
{
    string Endpoint { get; }
}

public sealed class MockFrontierClient : ICloudChatClient
{
    private readonly CostLog _costLog;
    private readonly int _delayMs;
    private readonly double _usdPerCall;

    public MockFrontierClient(CostLog costLog, int delayMs = 600, double usdPerCall = 0.0125)
    {
        _costLog = costLog;
        _delayMs = delayMs;
        _usdPerCall = usdPerCall;
    }

    public string DisplayName => "Mock cloud frontier";
    public string Endpoint => "mock";
    public bool IsAvailable => true;

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async IAsyncEnumerable<string> StreamAsync(
        IReadOnlyList<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var inputChars = 0;
        foreach (var m in messages) inputChars += m.Content?.Length ?? 0;

        var last = messages.Count > 0 ? messages[^1].Content : "";
        var preview = (last ?? "").Length > 60 ? last![..60] + "..." : last;
        var canned = $"[Mock cloud frontier] Routed via Azure-equivalent endpoint. " +
                     $"Reflecting on \"{preview}\". In production this would hit the real frontier model.";

        var outputChars = 0;
        foreach (var word in canned.Split(' '))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(_delayMs / 12, cancellationToken).ConfigureAwait(false);
            outputChars += word.Length + 1;
            yield return word + " ";
        }
        sw.Stop();

        _costLog.Record(new CostEvent(
            Route: "mock-cloud",
            Source: DisplayName,
            InputTokens: Math.Max(1, inputChars / 4),
            OutputTokens: Math.Max(1, outputChars / 4),
            DurationMs: sw.ElapsedMilliseconds,
            EstimatedUsd: _usdPerCall));
    }
}
