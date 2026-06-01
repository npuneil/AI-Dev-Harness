using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using LocalAiDemos.Shared.AI;

namespace HybridAIDevHarness.Router;

public sealed class HybridRouter
{
    public enum Target { Local, Cloud }

    private readonly IChatClient _local;
    private readonly Cloud.ICloudChatClient _cloud;

    public HybridRouter(IChatClient local, Cloud.ICloudChatClient cloud)
    {
        _local = local;
        _cloud = cloud;
    }

    public IChatClient Local => _local;
    public Cloud.ICloudChatClient Cloud => _cloud;

    public IAsyncEnumerable<string> StreamAsync(
        Target target,
        IReadOnlyList<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => target == Target.Local
            ? _local.StreamAsync(messages, options, cancellationToken)
            : _cloud.StreamAsync(messages, options, cancellationToken);

    public async IAsyncEnumerable<StageResult> RunPipelineAsync(
        IReadOnlyList<PipelineStage> stages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var stage in stages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var buffer = new System.Text.StringBuilder();
            await foreach (var chunk in StreamAsync(stage.Target, stage.Messages, stage.Options, cancellationToken))
            {
                buffer.Append(chunk);
            }
            yield return new StageResult(stage.Name, stage.Target, buffer.ToString());
        }
    }
}

public sealed record PipelineStage(
    string Name,
    HybridRouter.Target Target,
    IReadOnlyList<ChatMessage> Messages,
    ChatOptions? Options = null);

public sealed record StageResult(string Name, HybridRouter.Target Target, string Output);
