using Microsoft.UI.Xaml.Controls;

namespace LocalAIDevHarness.Pages;

public sealed partial class ChatPage : Page
{
    public ChatPage()
    {
        InitializeComponent();
        Chat.Client = AppHost.Chat;
        Chat.SystemPrompt = "You are a concise on-device assistant running on a Windows laptop. Answer in plain prose; never claim to access the internet.";
    }
}
