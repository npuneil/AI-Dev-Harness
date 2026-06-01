using System.Threading;
using LocalAiDemos.Shared.AI;
using LocalAiDemos.Shared.DemoData;
using LocalAiDemos.Shared.Settings;
using LocalAiDemos.Shared.Telemetry;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LocalAIDevHarness;

/// <summary>
/// Process-wide singletons. Plain-property bag instead of a DI container so the
/// pack stays approachable for vibe coding — wire your own container if you
/// prefer.
/// </summary>
public static class AppHost
{
    public static AppSettings Settings { get; } = new();
    public static CostLog CostLog { get; } = new();
    public static AuditLogger Audit { get; } = new();
    public static DemoDataLoader DemoData { get; } = new();

    private static IChatClient? _chat;
    private static readonly object _gate = new();

    public static IChatClient Chat
    {
        get
        {
            if (_chat is not null) return _chat;
            lock (_gate)
            {
                if (_chat is not null) return _chat;
                var alias = Settings.ModelAlias;
                if (string.IsNullOrWhiteSpace(alias))
                    alias = ModelCatalog.DefaultSmallFor(SiliconDetector.Current);
                _chat = new FoundryLocalChatClient(
                    appName: "LocalAIDevHarness",
                    modelAlias: alias,
                    costLog: CostLog,
                    logger: NullLogger<FoundryLocalChatClient>.Instance);
                return _chat;
            }
        }
    }

    public static void StartChatWarmup() =>
        _ = Chat.InitializeAsync(CancellationToken.None);
}
