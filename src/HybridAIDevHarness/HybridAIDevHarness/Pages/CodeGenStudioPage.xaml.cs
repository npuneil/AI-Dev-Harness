using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HybridAIDevHarness.Router;
using LocalAiDemos.Shared.AI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HybridAIDevHarness.Pages;

public sealed partial class CodeGenStudioPage : Page
{
    private CancellationTokenSource? _cts;
    private string _specPath = "specs/term_deposit_api.json";
    private string? _specJson;

    public CodeGenStudioPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            var resolved = AppHost.DemoData.Resolve(_specPath);
            if (File.Exists(resolved))
            {
                _specJson = await File.ReadAllTextAsync(resolved);
                SpecText.Text = $"spec: {_specPath}";
            }
            else
            {
                _specJson = "{\"name\":\"term_deposit_api\",\"description\":\"Sample API spec.\"}";
                SpecText.Text = $"spec: (inline fallback)";
            }
        };
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        Output.Text = "";
        StatusText.Text = "ready.";
    }

    private async void RunHybrid_Click(object sender, RoutedEventArgs e) =>
        await RunPipelineAsync(hybrid: true);

    private async void RunCloudOnly_Click(object sender, RoutedEventArgs e) =>
        await RunPipelineAsync(hybrid: false);

    private async Task RunPipelineAsync(bool hybrid)
    {
        if (_specJson is null) return;
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        Output.Text = "";
        var buf = new StringBuilder();

        var stages = BuildStages(_specJson, hybrid);
        var modeLabel = hybrid ? "HYBRID" : "CLOUD-ONLY";
        StatusText.Text = $"running pipeline · {modeLabel} · {stages.Count} stages…";

        try
        {
            await foreach (var stage in AppHost.Router.RunPipelineAsync(stages, ct))
            {
                buf.AppendLine($"── stage: {stage.Name} [{stage.Target}] ──");
                buf.AppendLine(stage.Output.Trim());
                buf.AppendLine();
                Output.Text = buf.ToString();
            }
            StatusText.Text = $"{modeLabel} pipeline complete.";
        }
        catch (OperationCanceledException) { StatusText.Text = "cancelled."; }
        catch (Exception ex)
        {
            buf.AppendLine($"[error] {ex.Message}");
            Output.Text = buf.ToString();
            StatusText.Text = "error.";
        }
    }

    private static IReadOnlyList<PipelineStage> BuildStages(string spec, bool hybrid)
    {
        var planMsgs = new[] { ChatMessage.System("You are a planner. Output a numbered build plan in 5–7 short bullets."), ChatMessage.User($"Plan the build for this spec:\n{spec}") };
        var scaffoldMsgs = new[] { ChatMessage.System("You scaffold project structure. Output a tree."), ChatMessage.User($"Scaffold for this spec:\n{spec}") };
        var codeMsgs = new[] { ChatMessage.System("You write code. Output a single file."), ChatMessage.User($"Write the main handler for this spec:\n{spec}") };
        var testsMsgs = new[] { ChatMessage.System("You write tests. Output 3 short test cases."), ChatMessage.User($"Tests for this spec:\n{spec}") };
        var docsMsgs = new[] { ChatMessage.System("You write README sections. Output a short usage section."), ChatMessage.User($"Docs for this spec:\n{spec}") };
        var reviewMsgs = new[] { ChatMessage.System("You review code for issues. Output 3 bullets."), ChatMessage.User($"Review the build for spec:\n{spec}") };

        if (hybrid)
        {
            return new[]
            {
                new PipelineStage("plan",     HybridRouter.Target.Cloud, planMsgs),
                new PipelineStage("scaffold", HybridRouter.Target.Local, scaffoldMsgs),
                new PipelineStage("code",     HybridRouter.Target.Local, codeMsgs),
                new PipelineStage("tests",    HybridRouter.Target.Local, testsMsgs),
                new PipelineStage("docs",     HybridRouter.Target.Local, docsMsgs),
                new PipelineStage("review",   HybridRouter.Target.Cloud, reviewMsgs),
            };
        }
        else
        {
            return new[]
            {
                new PipelineStage("plan",     HybridRouter.Target.Cloud, planMsgs),
                new PipelineStage("scaffold", HybridRouter.Target.Cloud, scaffoldMsgs),
                new PipelineStage("code",     HybridRouter.Target.Cloud, codeMsgs),
                new PipelineStage("tests",    HybridRouter.Target.Cloud, testsMsgs),
                new PipelineStage("docs",     HybridRouter.Target.Cloud, docsMsgs),
                new PipelineStage("review",   HybridRouter.Target.Cloud, reviewMsgs),
            };
        }
    }
}
