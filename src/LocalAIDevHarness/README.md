# Local AI Dev Harness

WinUI 3 starter for **on-device** AI prototypes. Foundry Local SDK only, zero
cloud dependencies, zero data egress.

## What's in this solution

| Page | Purpose |
|---|---|
| Home | Welcome + cost ticker + cheat sheet |
| Chat | Streaming chat against the on-device SLM |
| Doc Summary | Paste-or-load-sample → streamed summary |
| About | Diagnostics: detected silicon, Foundry SDK version, model status, cost totals |
| Settings | Model alias, device preference, theme, telemetry toggle |

All UI building blocks come from `..\Shared\` so adding a new tab is normally a
single `Pages\YourTab.xaml(.cs)` + a `NavigationViewItem` in `MainWindow.xaml`.

## Run

```powershell
dotnet build -c Debug -p:Platform=x64
# Or open LocalAIDevHarness.sln in Visual Studio and F5.
```

Foundry Local must be installed (`winget install Microsoft.FoundryLocal`). If it
isn't, the app still launches — `FoundryLocalChatClient` switches to a
deterministic stub stream so the UI never dead-ends.

## Forking

```powershell
..\..\tools\new-prototype.ps1 -Source Local -Name MyHealthcareDemo
```

This copies the solution, renames assemblies and the MSIX identity, and rewires
the asset folder.
