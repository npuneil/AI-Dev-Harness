# CLAUDE.md — Project Guidelines

## Overview

**AI-Dev-Harness** is a skunkworks prototype pack for jump-starting on-device
and hybrid AI demos on Windows. WinUI 3 / .NET 9 only. Generator-first:
`new-demo.ps1` is the entry point for teammates, not `dotnet new`.

Sibling project to the older Python-based `surface-npu-demo` and Zava demos —
intentionally **not** a port. Same patterns (silicon auto-detect, cost ticker,
Mock fallback), reimplemented in native C# so demos can ship as Store apps.

## Architecture

```
new-demo.ps1  ->  copies src\<Local|Hybrid>AIDevHarness  ->  demos\<Name>\
                                                              └─ references src\Shared

src\Shared\AI\
  IChatClient             common surface
  FoundryLocalChatClient  native Microsoft.AI.Foundry.Local.WinML SDK; REST never touched
  MockChatClient          deterministic safety net; emits CostEvent like real clients
  SiliconDetector         WMI-based Intel/Qualcomm/AMD detection
  ModelCatalog            per-silicon default-model picker

src\Shared\Telemetry\
  CostEvent + CostLog     in-memory ring buffer (no SSE; in-process binding)
  AuditLogger             append-only JSONL with PiiScanner redaction

src\Shared\Controls\
  StreamingChatView       binds to IChatClient + AppSettings; renders MOCK badge live
  CostTickerControl       right-rail USD ticker
  DiagnosticsPanel        silicon / model / health
```

## Key conventions

- **All model calls go through `AppHost.Chat` (Local) or `AppHost.Router`
  (Hybrid).** Both record to `CostLog` — never instantiate `OpenAIClient` or
  `FoundryLocalManager` from a page, or the cost ticker will lie.
- **Mock toggle must short-circuit at the AppHost layer, not inside individual
  pages.** Pages should only ever see `IChatClient` — flipping
  `Settings.UseMock` swaps the singleton transparently.
- **Graceful degradation:** every chat client must handle initialisation
  failures with `IsAvailable = false` and a deterministic stub stream so the
  demo never dead-ends. `FoundryLocalChatClient` already does this; copy the
  pattern.
- **No trimming, no ReadyToRun.** WinUI 3 + dynamic Foundry SDK reflection
  do not survive trimming. `PublishReadyToRun=False` and `PublishTrimmed=False`
  are pinned in both reference csprojs and must stay that way for generated
  demos too.
- **Generator output is throwaway-friendly.** A teammate should be able to
  delete `demos\<Name>\` and re-run `new-demo.ps1` without touching `src\`.
- **Never use `robocopy /MIR` from the generator.** It has caused unexpected
  deletions in repo root. Use `Copy-Item -Recurse` instead and prune
  `bin/obj` afterwards.

## Key files

| File | Purpose |
|------|---------|
| `new-demo.ps1` | Interactive generator. Edit here when adding new questions or templating new files. |
| `src\Shared\AI\FoundryLocalChatClient.cs` | Native Foundry SDK wrapper (reflection/dynamic; tolerant of SDK version skew). |
| `src\Shared\AI\MockChatClient.cs` | Deterministic stub; recipe for any new client implementation. |
| `src\Shared\Settings\AppSettings.cs` | LocalSettings-backed prefs; raises `MockToggled` event. |
| `src\Shared\Controls\StreamingChatView.xaml.cs` | UI listens to `AppSettings.MockToggled` to flip the MOCK badge live. |
| `src\LocalAIDevHarness\LocalAIDevHarness\AppHost.cs` | Singleton bag; Mock-aware Chat property. Mirrored in Hybrid harness. |
| `Directory.Build.props` | Global versions: WinAppSDK 2.1.3, Foundry Local 1.2.0, OpenAI 2.1.0, Azure.AI.Inference 1.0.0-beta.2. |
| `.github\workflows\ci.yml` | Builds both solutions x64 Debug + Release on every push. |
| `.github\workflows\store-release.yml` | MSIX package + sign + (stub) Partner Center submit. |

## Conventions for new clients

When adding a new `IChatClient` (e.g. ONNX Runtime GenAI, OpenVINO GenAI):

1. Implement `IChatClient` in `src\Shared\AI\`.
2. Emit a `CostEvent` on every completion with the correct `Route`
   (`local` / `cloud` / `mock`) so the ticker classification stays right.
3. Always set `IsAvailable = false` on init failure and yield a stub stream
   instead of throwing — do not break pages that do not know about you.
4. Add a default-model entry to `ModelCatalog.DefaultSmallFor(...)`.

## Conventions for new tabs

Generator-generated tabs look like this — match the shape when adding
hand-coded ones too:

```csharp
public sealed partial class TriagePage : Page
{
    public TriagePage()
    {
        InitializeComponent();
        Chat.Client   = AppHost.Chat;         // or AppHost.Router for hybrid
        Chat.Settings = AppHost.Settings;     // wires the live MOCK badge
        Chat.SystemPrompt = "You are …";
    }
}
```

## Branding

Brand palette is per-generated-demo. Default theme (`Shared\Theming\Themes\Default.xaml`)
is intentionally neutral so generated demos start neutral and customise from
there. Placeholder logos only — not the official trademarked marks of any
organisation.

## Running

```powershell
# Generator (recommended)
.\new-demo.ps1

# Reference harnesses (for development of the pack itself)
dotnet build src\LocalAIDevHarness\LocalAIDevHarness.sln -c Debug -p:Platform=x64
dotnet build src\HybridAIDevHarness\HybridAIDevHarness.sln -c Debug -p:Platform=x64
```

## Open items

- Final model picks per silicon will track whatever the Foundry catalogue
  ships at the time of demo build.
- `store-release.yml` Partner Center submit step is a stub.
- No ONNX Runtime GenAI / OpenVINO GenAI clients yet (deliberate non-goal for v1).
- Vision page generated by the harness is currently a picker + preview stub;
  wire your preferred vision model into `OnPick`.
