using System;
using System.Reflection;
using LocalAiDemos.Shared.AI;
using LocalAiDemos.Shared.Telemetry;
using Microsoft.UI.Xaml.Controls;

namespace LocalAiDemos.Shared.Controls;

/// <summary>
/// Read-only status panel for the About tab: detected silicon, Foundry Local
/// runtime version, loaded model, cost-log totals.
/// </summary>
public sealed partial class DiagnosticsPanel : UserControl
{
    public DiagnosticsPanel()
    {
        this.InitializeComponent();
    }

    public void Update(IChatClient? chat, CostLog? log)
    {
        CpuText.Text = $"{SiliconDetector.Current} — {SiliconDetector.CpuName}";
        ArchText.Text = $"process: {SiliconDetector.ProcessArchitecture} · os: {SiliconDetector.OsArchitecture}";
        ChatClientText.Text = chat is null
            ? "no chat client wired"
            : $"{chat.DisplayName} · available={chat.IsAvailable}";
        FoundryVersionText.Text = TryGetFoundryVersion();
        if (log is not null)
        {
            var s = log.Summarize();
            TotalsText.Text = $"{s.TotalCalls} calls · {s.LocalTokens + s.CloudTokens:N0} tokens · ${s.TotalUsd:F4}";
        }
    }

    private static string TryGetFoundryVersion()
    {
        try
        {
            var asm = Assembly.Load("Microsoft.AI.Foundry.Local");
            return $"Microsoft.AI.Foundry.Local {asm.GetName().Version}";
        }
        catch (Exception ex)
        {
            return $"Foundry Local SDK not loaded ({ex.GetType().Name})";
        }
    }
}
