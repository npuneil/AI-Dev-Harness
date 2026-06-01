using System;
using System.Threading;
using HybridAIDevHarness.Cloud;
using HybridAIDevHarness.Router;
using LocalAiDemos.Shared.AI;
using LocalAiDemos.Shared.DemoData;
using LocalAiDemos.Shared.Settings;
using LocalAiDemos.Shared.Telemetry;
using Microsoft.Extensions.Logging.Abstractions;

namespace HybridAIDevHarness;

/// <summary>
/// Process-wide singletons for the Hybrid harness. Same shape as the Local
/// harness's <c>AppHost</c>, plus the cloud client and router.
/// </summary>
public static class AppHost
{
    public static AppSettings Settings { get; } = new();
    public static CostLog CostLog { get; } = new();
    public static AuditLogger Audit { get; } = new();
    public static DemoDataLoader DemoData { get; } = new();

    private static IChatClient? _local;
    private static IChatClient? _localMock;
    private static ICloudChatClient? _cloud;
    private static HybridRouter? _router;
    private static readonly object _gate = new();

    public static IChatClient Local
    {
        get
        {
            if (Settings.UseMock)
            {
                if (_localMock is not null) return _localMock;
                lock (_gate)
                {
                    _localMock ??= new MockChatClient("Local-Mock", CostLog);
                    return _localMock;
                }
            }

            if (_local is not null) return _local;
            lock (_gate)
            {
                if (_local is not null) return _local;
                var alias = Settings.ModelAlias;
                if (string.IsNullOrWhiteSpace(alias))
                    alias = ModelCatalog.DefaultSmallFor(SiliconDetector.Current);
                _local = new FoundryLocalChatClient(
                    appName: "HybridAIDevHarness",
                    modelAlias: alias,
                    costLog: CostLog,
                    logger: NullLogger<FoundryLocalChatClient>.Instance);
                return _local;
            }
        }
    }

    /// <summary>
    /// Cloud client. Defaults to <see cref="MockFrontierClient"/>; swap with
    /// <see cref="ConfigureAzureOpenAI"/> or <see cref="ConfigureAzureFoundry"/>
    /// once real credentials are wired through Settings. When <see cref="AppSettings.UseMock"/>
    /// is true, always returns the deterministic stub regardless of configuration.
    /// </summary>
    public static ICloudChatClient Cloud
    {
        get
        {
            if (Settings.UseMock)
            {
                lock (_gate)
                {
                    if (_cloud is MockFrontierClient existing) return existing;
                    var stub = new MockFrontierClient(CostLog);
                    _cloud = stub;
                    _router = new HybridRouter(Local, stub);
                    return stub;
                }
            }

            if (_cloud is not null) return _cloud;
            lock (_gate)
            {
                _cloud ??= new MockFrontierClient(CostLog);
                return _cloud;
            }
        }
    }

    public static HybridRouter Router
    {
        get
        {
            if (_router is not null) return _router;
            lock (_gate)
            {
                _router ??= new HybridRouter(Local, Cloud);
                return _router;
            }
        }
    }

    public static void ConfigureAzureOpenAI(Uri endpoint, string apiKey, string deployment)
    {
        lock (_gate)
        {
            _cloud = new AzureOpenAIChatClient(endpoint, apiKey, deployment, CostLog);
            _router = new HybridRouter(Local, _cloud);
        }
    }

    public static void ConfigureAzureFoundry(Uri endpoint, string apiKey, string modelName)
    {
        lock (_gate)
        {
            _cloud = new AzureFoundryChatClient(endpoint, apiKey, modelName, CostLog);
            _router = new HybridRouter(Local, _cloud);
        }
    }

    public static void StartChatWarmup() =>
        _ = Local.InitializeAsync(CancellationToken.None);
}
