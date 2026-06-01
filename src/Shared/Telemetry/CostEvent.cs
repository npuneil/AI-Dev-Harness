using System;

namespace LocalAiDemos.Shared.Telemetry;

/// <summary>
/// One model call. Recorded by every <see cref="AI.IChatClient"/> implementation
/// so the cost ticker can show local-vs-cloud spend in real time.
/// </summary>
public sealed record CostEvent(
    string Route,           // "local" | "cloud" | "mock-cloud"
    string Source,          // human-readable model / endpoint
    int InputTokens,
    int OutputTokens,
    long DurationMs,
    double EstimatedUsd)
{
    public DateTimeOffset At { get; init; } = DateTimeOffset.UtcNow;
    public int TotalTokens => InputTokens + OutputTokens;
}
