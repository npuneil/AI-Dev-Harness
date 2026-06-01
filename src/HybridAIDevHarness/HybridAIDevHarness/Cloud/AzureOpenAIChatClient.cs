using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using LocalAiDemos.Shared.AI;
using LocalAiDemos.Shared.Telemetry;
using OpenAI.Chat;

namespace HybridAIDevHarness.Cloud;

/// <summary>
/// Wraps <c>Azure.AI.OpenAI</c> via the <c>OpenAI</c> SDK v2.
/// </summary>
public sealed class AzureOpenAIChatClient : ICloudChatClient
{
    private readonly CostLog _costLog;
    private readonly string _deployment;
    private readonly AzureOpenAIClient? _client;
    private readonly double _usdPer1KInput;
    private readonly double _usdPer1KOutput;

    public AzureOpenAIChatClient(
        Uri endpoint,
        string apiKey,
        string deployment,
        CostLog costLog,
        double usdPer1KInput = 0.0025,
        double usdPer1KOutput = 0.01)
    {
        _costLog = costLog;
        _deployment = deployment;
        _usdPer1KInput = usdPer1KInput;
        _usdPer1KOutput = usdPer1KOutput;
        try { _client = new AzureOpenAIClient(endpoint, new AzureKeyCredential(apiKey)); }
        catch { _client = null; }
    }

    public string DisplayName => $"Azure OpenAI - {_deployment}";
    public string Endpoint => "azure-openai";
    public bool IsAvailable => _client is not null;

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async IAsyncEnumerable<string> StreamAsync(
        IReadOnlyList<LocalAiDemos.Shared.AI.ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_client is null)
        {
            yield return "[Azure OpenAI not configured.]";
            yield break;
        }

        var sw = Stopwatch.StartNew();
        var inputChars = 0;
        foreach (var m in messages) inputChars += m.Content?.Length ?? 0;

        var chat = _client.GetChatClient(options?.ModelOverride ?? _deployment);
        var sdkMessages = new List<OpenAI.Chat.ChatMessage>(messages.Count);
        foreach (var m in messages)
        {
            sdkMessages.Add(m.Role switch
            {
                "system" => OpenAI.Chat.ChatMessage.CreateSystemMessage(m.Content ?? ""),
                "assistant" => OpenAI.Chat.ChatMessage.CreateAssistantMessage(m.Content ?? ""),
                _ => OpenAI.Chat.ChatMessage.CreateUserMessage(m.Content ?? ""),
            });
        }
        var opts = new ChatCompletionOptions
        {
            MaxOutputTokenCount = options?.MaxTokens ?? 800,
            Temperature = options?.Temperature ?? 0.4f,
        };

        var outputChars = 0;
        var updates = chat.CompleteChatStreamingAsync(sdkMessages, opts, cancellationToken);
        await foreach (var update in updates.ConfigureAwait(false))
        {
            foreach (var part in update.ContentUpdate)
            {
                if (!string.IsNullOrEmpty(part.Text))
                {
                    outputChars += part.Text.Length;
                    yield return part.Text;
                }
            }
        }
        sw.Stop();

        var inputTokens = Math.Max(1, inputChars / 4);
        var outputTokens = Math.Max(1, outputChars / 4);
        var usd = (inputTokens / 1000.0) * _usdPer1KInput + (outputTokens / 1000.0) * _usdPer1KOutput;
        _costLog.Record(new CostEvent("cloud", DisplayName, inputTokens, outputTokens, sw.ElapsedMilliseconds, usd));
    }
}
