# Hybrid AI Dev Harness

WinUI 3 starter for **hybrid** AI prototypes. Local SLM via Foundry Local plus a
swappable cloud frontier (Azure OpenAI, Azure AI Foundry Inference, or
deterministic mock). Cost-contrast narrative built in.

## What's in this solution

| Page | Purpose |
|---|---|
| Home | Welcome + cost ticker + cheat sheet |
| Hybrid Chat | Per-turn target picker (Local SLM / Cloud frontier) |
| Code-Gen Studio | Multi-stage pipeline; replay as hybrid vs cloud-only |
| Cost Dashboard | Live event log + per-event tokens, ms, USD |
| About | Diagnostics |
| Settings | Model, device, theme, telemetry |

## Cloud client wiring

The default cloud client is `MockFrontierClient` (deterministic, no network).
Swap for real implementations at app startup or from Settings:

```csharp
AppHost.ConfigureAzureOpenAI(
    endpoint: new Uri("https://YOUR-RESOURCE.openai.azure.com/"),
    apiKey:   "<key>",
    deployment: "gpt-4o");

// OR

AppHost.ConfigureAzureFoundry(
    endpoint: new Uri("https://YOUR-FOUNDRY.services.ai.azure.com/models"),
    apiKey:   "<key>",
    modelName: "DeepSeek-V3");
```

Both implementations share `ICloudChatClient`, both record `CostEvent`s tagged
`cloud` so the ticker shows the contrast against the local SLM in real time.

## Run

```powershell
dotnet build -c Debug -p:Platform=x64
# Or open HybridAIDevHarness.sln in Visual Studio and F5.
```

Foundry Local must be installed (`winget install Microsoft.FoundryLocal`); the
local side falls back to a stub stream if not.

## Forking

```powershell
..\..\tools\new-prototype.ps1 -Source Hybrid -Name MyBankDemo
```
