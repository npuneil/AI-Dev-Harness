using System.Globalization;
using LocalAiDemos.Shared.Telemetry;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.System;

namespace HybridAIDevHarness.Pages;

public sealed partial class CostDashboardPage : Page
{
    public CostDashboardPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            Ticker.Attach(AppHost.CostLog);
            Refresh();
            AppHost.CostLog.EventRecorded += _ => DispatcherQueue.TryEnqueue(Refresh);
            KeyDown += (_, e) =>
            {
                if (e.Key == VirtualKey.R &&
                    Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift)
                        .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
                {
                    Reset_Click(this, e);
                }
            };
        };
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        AppHost.CostLog.Reset();
        Ticker.Reset();
        Refresh();
    }

    private void Refresh()
    {
        EventList.Items.Clear();
        foreach (var ev in AppHost.CostLog.Events)
        {
            EventList.Items.Add(new TextBlock
            {
                Text = $"{ev.At:HH:mm:ss}  {ev.Route,-10}  {ev.Source,-32}  " +
                       $"tokens {ev.TotalTokens,5}  {ev.DurationMs,5} ms  " +
                       $"${ev.EstimatedUsd.ToString("F4", CultureInfo.InvariantCulture)}",
                FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                FontSize = 12,
            });
        }
    }
}
