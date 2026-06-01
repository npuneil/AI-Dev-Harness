using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LocalAIDevHarness.Pages;

public sealed partial class HomePage : Page
{
    public HomePage()
    {
        InitializeComponent();
        Loaded += (_, _) => Ticker.Attach(AppHost.CostLog);
    }
}
