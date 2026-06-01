using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using LocalAiDemos.Shared.Telemetry;
using Microsoft.Extensions.Logging;

#pragma warning disable CS1591

namespace LocalAiDemos.Shared.AI;

/// <summary>
/// <see cref="IChatClient"/> implementation backed by the native Foundry Local
/// C# SDK (<c>Microsoft.AI.Foundry.Local.WinML</c>). Never falls back to the
/// REST API.
///
/// Behaviour:
///   1. Lazy first-use init: <see cref="FoundryLocalManager.CreateAsync"/>,
///      then catalog lookup, download (if needed), load.
///   2. Streams via the native chat client returned by <c>model.GetChatClientAsync()</c>.
///   3. If the runtime is unavailable, switches <see cref="IsAvailable"/> to
///      false and yields a deterministic stub stream so the demo never dead-ends.
///   4. Every call emits a <see cref="CostEvent"/> tagged <c>route: "local"</c>.
/// </summary>
public sealed class FoundryLocalChatClient : IChatClient
{
    private readonly ILogger<FoundryLocalChatClient> _logger;
    private readonly CostLog _costLog;
    private readonly string _appName;
    private readonly string _modelAlias;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private object? _foundryManager;   // typed as object to keep the SDK loosely coupled at compile time
    private object? _model;
    private object? _chatClient;
    private bool _initialized;
    private bool _available;
    private string _displayName;

    public FoundryLocalChatClient(
        string appName,
        string modelAlias,
        CostLog costLog,
        ILogger<FoundryLocalChatClient> logger)
    {
        _appName = appName;
        _modelAlias = modelAlias;
        _costLog = costLog;
        _logger = logger;
        _displayName = $"Foundry Local · {modelAlias} (not initialised)";
    }

    public string DisplayName => _displayName;
    public bool IsAvailable => _available;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized) return;
        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized) return;
            try
            {
                await InitializeCoreAsync(cancellationToken).ConfigureAwait(false);
                _available = true;
                _displayName = $"Foundry Local · {_modelAlias}";
                _logger.LogInformation("Foundry Local ready: {Model}", _modelAlias);
            }
            catch (Exception ex)
            {
                _available = false;
                _displayName = $"Foundry Local · unavailable ({ex.GetType().Name})";
                _logger.LogWarning(ex, "Foundry Local init failed; falling back to stub stream.");
            }
            finally
            {
                _initialized = true;
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task InitializeCoreAsync(CancellationToken ct)
    {
        // Late-bound to the Foundry Local SDK so this file compiles even if the
        // package surface shifts between minor versions. The package is required
        // at runtime; this is purely a defensive measure for the prototype pack.
        var sdkAssembly = System.Reflection.Assembly.Load("Microsoft.AI.Foundry.Local");
        var managerType = sdkAssembly.GetType("Microsoft.AI.Foundry.Local.FoundryLocalManager")
            ?? throw new InvalidOperationException("FoundryLocalManager type not found in Microsoft.AI.Foundry.Local.");
        var configType = sdkAssembly.GetType("Microsoft.AI.Foundry.Local.Configuration")
            ?? throw new InvalidOperationException("Configuration type not found in Microsoft.AI.Foundry.Local.");

        dynamic config = Activator.CreateInstance(configType)!;
        config.AppName = _appName;

        var createAsync = managerType.GetMethod("CreateAsync")
            ?? throw new InvalidOperationException("FoundryLocalManager.CreateAsync not found.");
        var createTask = (Task)createAsync.Invoke(null, new object?[] { config, null })!;
        await createTask.ConfigureAwait(false);

        var instanceProp = managerType.GetProperty("Instance")
            ?? throw new InvalidOperationException("FoundryLocalManager.Instance not found.");
        _foundryManager = instanceProp.GetValue(null);

        dynamic mgr = _foundryManager!;
        var catalog = await mgr.GetCatalogAsync().ConfigureAwait(false);
        _model = await catalog.GetModelAsync(_modelAlias).ConfigureAwait(false);

        dynamic model = _model!;
        if (!(bool)model.IsCached)
        {
            await model.DownloadAsync().ConfigureAwait(false);
        }
        if (!(bool)model.IsLoaded)
        {
            await model.LoadAsync().ConfigureAwait(false);
        }
        _chatClient = await model.GetChatClientAsync().ConfigureAwait(false);
    }

    public async IAsyncEnumerable<string> StreamAsync(
        IReadOnlyList<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        options ??= new ChatOptions();

        var stopwatch = Stopwatch.StartNew();
        var inputChars = 0;
        foreach (var m in messages) inputChars += m.Content?.Length ?? 0;
        var outputChars = 0;

        if (!_available || _chatClient is null)
        {
            await foreach (var chunk in StubStreamAsync(messages, options, cancellationToken).ConfigureAwait(false))
            {
                outputChars += chunk.Length;
                yield return chunk;
            }
        }
        else
        {
            await foreach (var chunk in StreamFromSdkAsync(messages, options, cancellationToken).ConfigureAwait(false))
            {
                outputChars += chunk.Length;
                yield return chunk;
            }
        }

        stopwatch.Stop();
        _costLog.Record(new CostEvent(
            Route: "local",
            Source: _displayName,
            InputTokens: ApproximateTokens(inputChars),
            OutputTokens: ApproximateTokens(outputChars),
            DurationMs: stopwatch.ElapsedMilliseconds,
            EstimatedUsd: 0.0));
    }

    private async IAsyncEnumerable<string> StreamFromSdkAsync(
        IReadOnlyList<ChatMessage> messages,
        ChatOptions options,
        [EnumeratorCancellation] CancellationToken ct)
    {
        dynamic client = _chatClient!;
        var payload = new List<object>(messages.Count);
        foreach (var m in messages)
        {
            payload.Add(new Dictionary<string, object?> { ["role"] = m.Role, ["content"] = m.Content });
        }

        var stream = (System.Collections.IEnumerable)client.CompleteStreamingChatAsync(payload);
        // Note: Foundry returns IAsyncEnumerable; cast to non-generic IAsyncEnumerable via dynamic-typed
        // enumerator pattern so we don't pin to a concrete chunk type at compile time.
        var enumeratorMethod = stream.GetType().GetMethod("GetAsyncEnumerator");
        if (enumeratorMethod is null)
        {
            // Synchronous fallback
            foreach (var chunk in stream)
            {
                ct.ThrowIfCancellationRequested();
                var text = TryReadChunkText((dynamic)chunk);
                if (!string.IsNullOrEmpty(text)) yield return text!;
            }
            yield break;
        }

        dynamic asyncEnum = client.CompleteStreamingChatAsync(payload);
        var enumerator = asyncEnum.GetAsyncEnumerator(ct);
        try
        {
            while (await ((System.Threading.Tasks.ValueTask<bool>)enumerator.MoveNextAsync()).ConfigureAwait(false))
            {
                ct.ThrowIfCancellationRequested();
                string? text = TryReadChunkText(enumerator.Current);
                if (!string.IsNullOrEmpty(text)) yield return text!;
            }
        }
        finally
        {
            await ((System.Threading.Tasks.ValueTask)enumerator.DisposeAsync()).ConfigureAwait(false);
        }
    }

    private static string? TryReadChunkText(dynamic chunk)
    {
        try { return (string?)chunk.Text; } catch { }
        try { return (string?)chunk.Content; } catch { }
        try { return chunk.ToString(); } catch { return null; }
    }

    /// <summary>Deterministic fallback so demos never dead-end.</summary>
    private static async IAsyncEnumerable<string> StubStreamAsync(
        IReadOnlyList<ChatMessage> messages,
        ChatOptions options,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var last = messages.Count > 0 ? messages[^1].Content : string.Empty;
        var preview = (last ?? string.Empty);
        if (preview.Length > 60) preview = preview[..60] + "…";
        var canned = $"[Foundry Local offline — demo stub] You asked: \"{preview}\". " +
                     "In a live demo this would stream from the on-device SLM.";
        foreach (var word in canned.Split(' '))
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(35, ct).ConfigureAwait(false);
            yield return word + " ";
        }
    }

    private static int ApproximateTokens(int chars) => Math.Max(1, chars / 4);
}
