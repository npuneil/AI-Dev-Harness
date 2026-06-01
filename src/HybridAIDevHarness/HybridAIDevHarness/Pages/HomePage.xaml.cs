using Microsoft.UI.Xaml.Controls;

namespace HybridAIDevHarness.Pages;

public sealed partial class HomePage : Page
{
    public HomePage()
    {
        InitializeComponent();
        Loaded += (_, _) => Ticker.Attach(AppHost.CostLog);
    }
}
