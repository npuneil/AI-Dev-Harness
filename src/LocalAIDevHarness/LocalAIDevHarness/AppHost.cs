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

    private static IChatClient? _real;
    private static IChatClient? _mock;
    private static readonly object _gate = new();

    static AppHost()
    {
        Settings.MockToggled += (_, _) => { /* getter re-evaluates on next access */ };
    }

    /// <summary>
    /// Returns the mock client when <see cref="AppSettings.UseMock"/> is true,
    /// otherwise the real Foundry-backed client. Both share the same
    /// <see cref="CostLog"/> so the ticker doesn't lie.
    /// </summary>
    public static IChatClient Chat
    {
        get
        {
            if (Settings.UseMock)
            {
                if (_mock is not null) return _mock;
                lock (_gate)
                {
                    _mock ??= new MockChatClient("Local-Mock", CostLog);
                    return _mock;
                }
            }

            if (_real is not null) return _real;
            lock (_gate)
            {
                if (_real is not null) return _real;
                var alias = Settings.ModelAlias;
                if (string.IsNullOrWhiteSpace(alias))
                    alias = ModelCatalog.DefaultSmallFor(SiliconDetector.Current);
                _real = new FoundryLocalChatClient(
                    appName: "LocalAIDevHarness",
                    modelAlias: alias,
                    costLog: CostLog,
                    logger: NullLogger<FoundryLocalChatClient>.Instance);
                return _real;
            }
        }
    }

    public static void StartChatWarmup() =>
        _ = Chat.InitializeAsync(CancellationToken.None);
}
