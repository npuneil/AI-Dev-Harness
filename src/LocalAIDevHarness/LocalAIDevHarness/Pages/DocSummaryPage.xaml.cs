using System;
using System.IO;
using System.Text;
using System.Threading;
using LocalAiDemos.Shared.AI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LocalAIDevHarness.Pages;

public sealed partial class DocSummaryPage : Page
{
    private CancellationTokenSource? _cts;

    public DocSummaryPage()
    {
        InitializeComponent();
    }

    private async void LoadSample_Click(object sender, RoutedEventArgs e)
    {
        var path = AppHost.DemoData.Resolve("sample_doc.txt");
        if (File.Exists(path))
        {
            InputBox.Text = await File.ReadAllTextAsync(path);
        }
        else
        {
            InputBox.Text = "Sample document not found at " + path;
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        InputBox.Text = string.Empty;
        OutputText.Text = string.Empty;
    }

    private async void Summarize_Click(object sender, RoutedEventArgs e)
    {
        var doc = (InputBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(doc)) return;

        OutputText.Text = string.Empty;
        _cts?.Cancel();
        _cts = new CancellationTokenSource();

        var messages = new[]
        {
            ChatMessage.System("You summarise documents in 3–5 short bullet points. Plain prose, no preamble."),
            ChatMessage.User($"Summarise this document:\n\n{doc}"),
        };

        var buffer = new StringBuilder();
        try
        {
            await foreach (var chunk in AppHost.Chat.StreamAsync(messages, cancellationToken: _cts.Token))
            {
                buffer.Append(chunk);
                OutputText.Text = buffer.ToString();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            OutputText.Text = "[error] " + ex.Message;
        }
    }
}
