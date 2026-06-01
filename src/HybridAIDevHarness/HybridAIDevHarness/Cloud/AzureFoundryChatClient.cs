using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.AI.Inference;
using LocalAiDemos.Shared.AI;
using LocalAiDemos.Shared.Telemetry;

namespace HybridAIDevHarness.Cloud;

/// <summary>
/// Wraps <c>Azure.AI.Inference</c> for Azure AI Foundry model endpoints.
/// </summary>
public sealed class AzureFoundryChatClient : ICloudChatClient
{
    private readonly CostLog _costLog;
    private readonly string _modelName;
    private readonly ChatCompletionsClient? _client;
    private readonly double _usdPer1KInput;
    private readonly double _usdPer1KOutput;

    public AzureFoundryChatClient(
        Uri endpoint,
        string apiKey,
        string modelName,
        CostLog costLog,
        double usdPer1KInput = 0.002,
        double usdPer1KOutput = 0.008)
    {
        _costLog = costLog;
        _modelName = modelName;
        _usdPer1KInput = usdPer1KInput;
        _usdPer1KOutput = usdPer1KOutput;
        try { _client = new ChatCompletionsClient(endpoint, new AzureKeyCredential(apiKey)); }
        catch { _client = null; }
    }

    public string DisplayName => $"Azure AI Foundry - {_modelName}";
    public string Endpoint => "azure-foundry";
    public bool IsAvailable => _client is not null;

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async IAsyncEnumerable<string> StreamAsync(
        IReadOnlyList<LocalAiDemos.Shared.AI.ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_client is null)
        {
            yield return "[Azure AI Foundry Inference not configured.]";
            yield break;
        }

        var sw = Stopwatch.StartNew();
        var inputChars = 0;
        foreach (var m in messages) inputChars += m.Content?.Length ?? 0;

        var sdkMessages = new List<ChatRequestMessage>(messages.Count);
        foreach (var m in messages)
        {
            sdkMessages.Add(m.Role switch
            {
                "system" => new ChatRequestSystemMessage(m.Content ?? ""),
                "assistant" => new ChatRequestAssistantMessage(m.Content ?? ""),
                _ => new ChatRequestUserMessage(m.Content ?? ""),
            });
        }
        var opts = new ChatCompletionsOptions(sdkMessages)
        {
            Model = options?.ModelOverride ?? _modelName,
            MaxTokens = options?.MaxTokens ?? 800,
            Temperature = options?.Temperature ?? 0.4f,
        };

        var outputChars = 0;
        var stream = await _client.CompleteStreamingAsync(opts, cancellationToken).ConfigureAwait(false);
        await foreach (var update in stream.ConfigureAwait(false))
        {
            var delta = update.ContentUpdate;
            if (!string.IsNullOrEmpty(delta))
            {
                outputChars += delta.Length;
                yield return delta;
            }
        }
        sw.Stop();

        var inputTokens = Math.Max(1, inputChars / 4);
        var outputTokens = Math.Max(1, outputChars / 4);
        var usd = (inputTokens / 1000.0) * _usdPer1KInput + (outputTokens / 1000.0) * _usdPer1KOutput;
        _costLog.Record(new CostEvent("cloud", DisplayName, inputTokens, outputTokens, sw.ElapsedMilliseconds, usd));
    }
}
