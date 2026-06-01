using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LocalAiDemos.Shared.Telemetry;

namespace LocalAiDemos.Shared.AI;

/// <summary>
/// Deterministic fallback client. Always available, never touches Foundry or the
/// cloud, emits a CostEvent like the real clients so the ticker still moves.
/// Used by the "Mock" toggle in the harness — the same safety net that ships in
/// surface-npu-demo and the Zava demos so live demos never dead-end.
/// </summary>
public sealed class MockChatClient : IChatClient
{
    private readonly CostLog? _costLog;
    private readonly string _label;

    public MockChatClient(string label = "Mock", CostLog? costLog = null)
    {
        _label = label;
        _costLog = costLog;
    }

    public string DisplayName => $"{_label} · deterministic stub";

    public bool IsAvailable => true;

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async IAsyncEnumerable<string> StreamAsync(
        IReadOnlyList<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        var last = messages.Count > 0 ? messages[messages.Count - 1].Content : string.Empty;
        var reply = Compose(last);
        var emitted = new StringBuilder();

        foreach (var token in Tokenize(reply))
        {
            cancellationToken.ThrowIfCancellationRequested();
            emitted.Append(token);
            yield return token;
            try { await Task.Delay(18, cancellationToken).ConfigureAwait(false); }
            catch (TaskCanceledException) { yield break; }
        }

        sw.Stop();
        _costLog?.Record(new CostEvent(
            Route: "mock",
            Source: $"{_label} · stub",
            InputTokens: Approx(last),
            OutputTokens: Approx(emitted.ToString()),
            DurationMs: sw.ElapsedMilliseconds,
            EstimatedUsd: 0));
    }

    private static string Compose(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            return "(mock) Ready. Type a question and I'll echo a deterministic reply for the demo.";
        var summary = prompt.Length > 120 ? prompt.Substring(0, 120) + "…" : prompt;
        return
            "**(Mock response — safety net for live demos)**\n\n" +
            $"You asked: _{summary}_\n\n" +
            "Here is a stand-in answer with three bullet points so the UI looks right:\n" +
            "- Point one — what the real model would say.\n" +
            "- Point two — a second supporting detail.\n" +
            "- Point three — a closing recommendation.\n\n" +
            "Flip Mock off in Settings to call the real Foundry / Azure client.";
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        var buf = new StringBuilder();
        foreach (var ch in text)
        {
            buf.Append(ch);
            if (ch == ' ' || ch == '\n')
            {
                yield return buf.ToString();
                buf.Clear();
            }
        }
        if (buf.Length > 0) yield return buf.ToString();
    }

    private static int Approx(string s) => string.IsNullOrEmpty(s) ? 0 : Math.Max(1, s.Length / 4);
}
