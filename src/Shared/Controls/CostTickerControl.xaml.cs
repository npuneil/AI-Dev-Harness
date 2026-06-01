using System.Globalization;
using LocalAiDemos.Shared.Telemetry;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LocalAiDemos.Shared.Controls;

/// <summary>
/// A compact live ticker showing total local-vs-cloud spend. Subscribe to a
/// <see cref="CostLog"/> via <see cref="Attach"/> and the ticker keeps itself
/// updated on the dispatcher thread.
/// </summary>
public sealed partial class CostTickerControl : UserControl
{
    private CostLog? _log;

    public CostTickerControl()
    {
        this.InitializeComponent();
    }

    public void Attach(CostLog log)
    {
        if (_log is not null) _log.EventRecorded -= OnEvent;
        _log = log;
        _log.EventRecorded += OnEvent;
        Refresh();
    }

    private void OnEvent(CostEvent _) => DispatcherQueue.TryEnqueue(Refresh);

    public void Reset()
    {
        _log?.Reset();
        Refresh();
    }

    private void Refresh()
    {
        if (_log is null) return;
        var s = _log.Summarize();
        LocalText.Text = $"local · {s.LocalCalls} calls · {s.LocalTokens:N0} tok · ${s.LocalUsd.ToString("F4", CultureInfo.InvariantCulture)}";
        CloudText.Text = $"cloud · {s.CloudCalls} calls · {s.CloudTokens:N0} tok · ${s.CloudUsd.ToString("F4", CultureInfo.InvariantCulture)}";
        SavingsText.Text = s.SavingsUsd > 0
            ? $"saved ${s.SavingsUsd.ToString("F4", CultureInfo.InvariantCulture)}"
            : "";
    }
}
