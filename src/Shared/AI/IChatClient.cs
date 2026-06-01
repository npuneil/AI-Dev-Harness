using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace LocalAiDemos.Shared.AI;

/// <summary>
/// Common chat-client surface used by every harness. Both the local Foundry
/// implementation and any cloud router implementation conform to this shape so
/// pages can stay agnostic about where inference is running.
/// </summary>
public interface IChatClient
{
    /// <summary>Friendly name shown in the diagnostics panel (e.g. "Foundry Local · phi-4-mini").</summary>
    string DisplayName { get; }

    /// <summary>True once the underlying runtime/model is ready to answer requests.</summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Returns a streamed completion as text chunks. The implementation is responsible
    /// for emitting a <see cref="Telemetry.CostEvent"/> when the call ends (success or fallback).
    /// </summary>
    IAsyncEnumerable<string> StreamAsync(
        IReadOnlyList<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>Initialise the client (start runtime, download/load model). Safe to call repeatedly.</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

public sealed record ChatMessage(string Role, string Content)
{
    public static ChatMessage System(string content) => new("system", content);
    public static ChatMessage User(string content) => new("user", content);
    public static ChatMessage Assistant(string content) => new("assistant", content);
}

public sealed record ChatOptions(
    int MaxTokens = 800,
    float Temperature = 0.4f,
    string? ModelOverride = null);
