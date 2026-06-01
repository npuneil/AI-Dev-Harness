using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HybridAIDevHarness.Pages;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }
    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();
    private void Refresh() => Diag.Update(AppHost.Local, AppHost.CostLog);
}
