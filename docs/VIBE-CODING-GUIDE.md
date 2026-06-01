# Vibe-coding guide

This pack is built so a new prototype is mostly: **add a tab**, **add a system
prompt**, **add demo data**.

## Recipe: add a new tab

1. **Decide which harness.** Local-only? Use `LocalAIDevHarness`. Need a cloud
   path? Use `HybridAIDevHarness`.

2. **Fork.**
   ```powershell
   .\tools\new-prototype.ps1 -Source Local -Name MyHealthcareDemo
   ```

3. **Add a page.** In `src\MyHealthcareDemo\MyHealthcareDemo\Pages\` create:

   `TriagePage.xaml`
   ```xml
   <Page x:Class="MyHealthcareDemo.Pages.TriagePage" ...>
     <controls:StreamingChatView x:Name="Chat" xmlns:controls="using:LocalAiDemos.Shared.Controls" />
   </Page>
   ```

   `TriagePage.xaml.cs`
   ```csharp
   public sealed partial class TriagePage : Page
   {
       public TriagePage()
       {
           InitializeComponent();
           Chat.Client = AppHost.Chat;
           Chat.SystemPrompt = "You are a clinical triage assistant. Output ESI level 1–5 with reasoning.";
       }
   }
   ```

4. **Wire it into nav.** In `MainWindow.xaml` add a `<NavigationViewItem ... Tag="triage">`
   and in `MainWindow.xaml.cs` add a `case "triage": NavFrame.Navigate(typeof(TriagePage));`.

5. **Add demo data.** Drop a `chart.txt` in `demo_data\triage\`; read it with
   `await AppHost.DemoData.ReadAllTextAsync("triage/chart.txt")`.

## Recipe: stream a structured response

```csharp
var msgs = new[] {
  ChatMessage.System("Return JSON with keys: severity, reasoning."),
  ChatMessage.User(documentText),
};
var buf = new StringBuilder();
await foreach (var chunk in AppHost.Chat.StreamAsync(msgs, ct: token))
{
    buf.Append(chunk);
    OutputBox.Text = buf.ToString();
}
var json = ParseJson(buf.ToString()); // same _safe_parse_json pattern as the Python demos
```

## Recipe: hybrid pipeline

```csharp
var stages = new[] {
  new PipelineStage("plan",  HybridRouter.Target.Cloud, planMessages),
  new PipelineStage("draft", HybridRouter.Target.Local, draftMessages),
  new PipelineStage("review",HybridRouter.Target.Cloud, reviewMessages),
};
await foreach (var stage in AppHost.Router.RunPipelineAsync(stages, ct))
{
    Log($"{stage.Name} via {stage.Target} → {stage.Output.Length} chars");
}
```

## Rules of thumb (carried over from the Python pack)

- **Never call the OpenAI client directly from a page.** Go through `AppHost.Chat`
  / `AppHost.Router` so every call is recorded in `CostLog`.
- **Graceful degradation.** `IChatClient.IsAvailable == false` means show a
  banner, not a crash. `FoundryLocalChatClient` already does this for you.
- **PII before the cloud.** Any cloud-bound payload must pass through
  `PiiScanner.Redact` (or your domain-specific equivalent) first.
- **One screen, one job.** If a tab needs three sub-flows, prefer three tabs.
