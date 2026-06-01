# Architecture

## One repo, two solutions, one shared library

```
C:\LocalAIDemos\                    (repo root)
└── src\
    ├── Shared\                     class library (net9.0-windows10)
    │   ├── AI\                     IChatClient, FoundryLocalChatClient, SiliconDetector, ModelCatalog
    │   ├── Telemetry\              CostEvent, CostLog, AuditLogger
    │   ├── Controls\               CostTickerControl, StreamingChatView, DiagnosticsPanel
    │   ├── DemoData\               DemoDataLoader, PiiScanner
    │   ├── Settings\               AppSettings, SettingsPage
    │   └── Theming\                BrandTokens + theme resource dictionaries
    ├── LocalAIDevHarness\          solution + app + MSIX packaging
    └── HybridAIDevHarness\         solution + app + Cloud\ + Router\ + MSIX packaging
```

Both apps reference `Shared` via `ProjectReference`. Adding a new harness =
copy one with `tools\new-prototype.ps1` and start customising pages.

## Foundry Local integration

`FoundryLocalChatClient` is the only thing in the pack that talks to Foundry
Local. It uses the native `Microsoft.AI.Foundry.Local` SDK (the `WinML` Windows
flavour for hardware acceleration). The REST API is intentionally never
contacted.

Lifecycle:

1. Lazy `InitializeAsync` on first call.
2. `FoundryLocalManager.CreateAsync(Configuration)` then `.Instance`.
3. `manager.GetCatalogAsync()` → `catalog.GetModelAsync(alias)`.
4. `model.DownloadAsync()` / `model.LoadAsync()` if needed.
5. `model.GetChatClientAsync()` returns the native chat client used for streaming.
6. On any failure the client switches to a deterministic stub stream and sets
   `IsAvailable = false` so callers can show a banner without crashing the demo.

## Hybrid cloud surface

`ICloudChatClient` extends `IChatClient` with an `Endpoint` tag. Three
implementations:

| Class | SDK | Endpoint tag |
|---|---|---|
| `AzureOpenAIChatClient` | `Azure.AI.OpenAI` v2 (OpenAI SDK v2) | `azure-openai` |
| `AzureFoundryChatClient` | `Azure.AI.Inference` | `azure-foundry` |
| `MockFrontierClient` | none — deterministic | `mock` |

`HybridRouter` accepts a per-call `Target` (Local / Cloud) and exposes a
`RunPipelineAsync` helper for multi-stage flows like the Code-Gen Studio.

## Telemetry

`CostLog` is the in-process replacement for the SSE event bus used in the
Python demos. Every chat call (local *or* cloud) records a `CostEvent` with
route, source, tokens, ms, USD estimate. `CostTickerControl` and
`CostDashboardPage` bind to it.

`AuditLogger` is an append-only JSONL writer with `PiiScanner` redaction. Use it
for anything that crosses the cloud boundary in the Hybrid harness.

## Packaging

Each app uses the WinUI 3 single-project MSIX pattern (`EnableMsixTooling=true`
in its csproj, `Package.appxmanifest` next to `App.xaml`). MSIX bundles are
produced by `dotnet publish ... -p:WindowsPackageType=MSIX`; see
`.github\workflows\store-release.yml`.
