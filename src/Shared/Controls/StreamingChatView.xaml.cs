using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LocalAiDemos.Shared.AI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LocalAiDemos.Shared.Controls;

/// <summary>
/// Minimal streaming chat surface: a transcript list, a prompt box, a Send button.
/// Bind it to any <see cref="IChatClient"/> via <see cref="Client"/> and a system
/// prompt via <see cref="SystemPrompt"/>; the control owns the message history.
/// </summary>
public sealed partial class StreamingChatView : UserControl
{
    private readonly List<ChatMessage> _history = new();
    private CancellationTokenSource? _cts;

    public StreamingChatView()
    {
        this.InitializeComponent();
    }

    public IChatClient? Client { get; set; }

    public string SystemPrompt { get; set; } =
        "You are a concise, helpful on-device assistant. Answer in plain prose.";

    /// <summary>
    /// Optionally bind to <see cref="LocalAiDemos.Shared.Settings.AppSettings"/> so the
    /// "MOCK MODE" badge lights up automatically whenever the user flips the Settings toggle.
    /// </summary>
    public LocalAiDemos.Shared.Settings.AppSettings? Settings
    {
        get => _settings;
        set
        {
            if (_settings is not null) _settings.MockToggled -= OnMockToggled;
            _settings = value;
            if (_settings is not null)
            {
                _settings.MockToggled += OnMockToggled;
                MockBadge.Visibility = _settings.UseMock ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
    private LocalAiDemos.Shared.Settings.AppSettings? _settings;

    private void OnMockToggled(object? sender, bool isOn)
    {
        DispatcherQueue.TryEnqueue(() =>
            MockBadge.Visibility = isOn ? Visibility.Visible : Visibility.Collapsed);
    }

    public void Clear()
    {
        _history.Clear();
        Transcript.Items.Clear();
    }

    private async void Send_Click(object sender, RoutedEventArgs e) => await SendAsync();

    private async Task SendAsync()
    {
        if (Client is null) return;
        var prompt = PromptBox.Text?.Trim();
        if (string.IsNullOrEmpty(prompt)) return;
        PromptBox.Text = string.Empty;
        SendButton.IsEnabled = false;
        StatusText.Text = $"streaming via {Client.DisplayName}…";

        AppendUser(prompt);
        var assistant = AppendAssistantPlaceholder();
        _history.Add(ChatMessage.User(prompt));
        var responseBuffer = new System.Text.StringBuilder();

        if (_history.Count == 1 || _history[0].Role != "system")
        {
            _history.Insert(0, ChatMessage.System(SystemPrompt));
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        try
        {
            await foreach (var chunk in Client.StreamAsync(_history, cancellationToken: _cts.Token))
            {
                responseBuffer.Append(chunk);
                assistant.Text = responseBuffer.ToString();
                Transcript.UpdateLayout();
                ScrollToBottom();
            }
            _history.Add(ChatMessage.Assistant(responseBuffer.ToString()));
            StatusText.Text = "done.";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "cancelled.";
        }
        catch (Exception ex)
        {
            assistant.Text = $"[error] {ex.Message}";
            StatusText.Text = "error.";
        }
        finally
        {
            SendButton.IsEnabled = true;
        }
    }

    private void AppendUser(string text)
    {
        Transcript.Items.Add(new TextBlock
        {
            Text = $"You: {text}",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
    }

    private TextBlock AppendAssistantPlaceholder()
    {
        var tb = new TextBlock
        {
            Text = "…",
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 8),
        };
        Transcript.Items.Add(tb);
        return tb;
    }

    private void ScrollToBottom()
    {
        if (Transcript.Items.Count == 0) return;
        Transcript.ScrollIntoView(Transcript.Items[^1]);
    }
}
