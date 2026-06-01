# AI Dev Harness — WinUI 3 Prototype Pack

A two-solution prototype pack for jump-starting **Local** and **Hybrid** on-device
AI applications on Windows. Built with **WinUI 3 / Windows App SDK** and the
native **Foundry Local C# SDK** so prototypes ship as Store-ready desktop apps
from day one.

## What's in the box

| Solution | When to start here |
|---|---|
| [`src/LocalAIDevHarness`](src/LocalAIDevHarness/README.md) | On-device only. Zero cloud dependencies, zero data egress. Foundry Local SDK + small language model. |
| [`src/HybridAIDevHarness`](src/HybridAIDevHarness/README.md) | Local SLM + cloud frontier router (Azure OpenAI or Azure AI Foundry Inference, swappable). Cost-comparison narrative built in. |

Both apps share a `src/Shared` class library: cost ticker, silicon
auto-detect, streaming chat control, theming primitives, settings page, demo-data
loader, PII scanner, telemetry, diagnostics.

## Why this pack exists

Previous demos were single-file Flask + Python apps. Great for vibe-coding,
painful to harden for customers. This pack replaces that pattern with:

- **No Python.** Pure C# / .NET 9.
- **WinUI 3 + Windows App SDK.** MSIX-packaged, Store-shippable.
- **Native Foundry Local SDK only.** No REST calls to `localhost`.
- **Shared building blocks.** New prototypes inherit the chrome.
- **MSIX + GitHub Actions Store workflow.** Production path is already wired.

See [`docs/PROMOTING-TO-PRODUCTION.md`](docs/PROMOTING-TO-PRODUCTION.md) for the
teammate handoff checklist.

## Prerequisites

- Windows 11 (x64 or ARM64)
- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Windows App SDK 1.6+](https://learn.microsoft.com/windows/apps/windows-app-sdk/)
- Visual Studio 2022 17.10+ with the **Windows application development** workload
  (optional — `dotnet build` works headless too)
- [Foundry Local](https://learn.microsoft.com/azure/ai-foundry/foundry-local/)
  installed: `winget install Microsoft.FoundryLocal`

## Quick start

```powershell
# Local-only
cd src\LocalAIDevHarness
dotnet build LocalAIDevHarness.sln

# Hybrid
cd ..\HybridAIDevHarness
dotnet build HybridAIDevHarness.sln
```

Open either `.sln` in Visual Studio and F5 to launch.

## Forking into a new prototype

```powershell
.\tools\new-prototype.ps1 -Source Local  -Name MyHealthcareDemo
.\tools\new-prototype.ps1 -Source Hybrid -Name MyBankDemo
```

## Documentation

- [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — how the pack is structured
- [`docs/VIBE-CODING-GUIDE.md`](docs/VIBE-CODING-GUIDE.md) — how to add tabs / features fast
- [`docs/PROMOTING-TO-PRODUCTION.md`](docs/PROMOTING-TO-PRODUCTION.md) — handoff checklist

## License

MIT.
