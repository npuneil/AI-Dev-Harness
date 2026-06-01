using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using HybridAIDevHarness.Router;
using LocalAiDemos.Shared.AI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HybridAIDevHarness.Pages;

public sealed partial class HybridChatPage : Page
{
    private readonly List<ChatMessage> _history = new();
    private CancellationTokenSource? _cts;

    public HybridChatPage()
    {
        InitializeComponent();
        LocalLabel.Text = $"local: {AppHost.Local.DisplayName}";
        CloudLabel.Text = $"cloud: {AppHost.Cloud.DisplayName}";
    }

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        var prompt = (PromptBox.Text ?? "").Trim();
        if (string.IsNullOrEmpty(prompt)) return;
        PromptBox.Text = "";

        if (_history.Count == 0)
        {
            _history.Add(ChatMessage.System("You are a helpful assistant. Be concise."));
        }
        _history.Add(ChatMessage.User(prompt));

        var target = TargetLocal.IsChecked == true ? HybridRouter.Target.Local : HybridRouter.Target.Cloud;
        var tag = target == HybridRouter.Target.Local ? "LOCAL" : "CLOUD";
        AppendLine($"You: {prompt}");
        AppendLine($"[{tag}] ", newline: false);
        StatusText.Text = $"streaming via {tag}…";

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var buf = new StringBuilder();
        try
        {
            await foreach (var chunk in AppHost.Router.StreamAsync(target, _history, cancellationToken: _cts.Token))
            {
                buf.Append(chunk);
                ReplaceTail(buf.ToString(), tag);
            }
            _history.Add(ChatMessage.Assistant(buf.ToString()));
            AppendLine("");
            StatusText.Text = "done.";
        }
        catch (OperationCanceledException) { StatusText.Text = "cancelled."; }
        catch (Exception ex) { AppendLine($"\n[error] {ex.Message}"); StatusText.Text = "error."; }
    }

    private string _pending = "";
    private string _tailTag = "";
    private void AppendLine(string text, bool newline = true)
    {
        _pending += text + (newline ? "\n" : "");
        Transcript.Text = _pending;
    }
    private void ReplaceTail(string body, string tag)
    {
        if (_tailTag != tag)
        {
            _tailTag = tag;
        }
        // Strip the previous tail body (everything after the last "[TAG] ")
        var marker = $"[{tag}] ";
        var idx = _pending.LastIndexOf(marker, StringComparison.Ordinal);
        if (idx < 0) return;
        var head = _pending[..(idx + marker.Length)];
        Transcript.Text = head + body;
    }
}
