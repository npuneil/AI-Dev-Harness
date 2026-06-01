using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace LocalAiDemos.Shared.Telemetry;

/// <summary>
/// In-memory ring buffer of <see cref="CostEvent"/>s with an observable surface
/// suitable for binding to a XAML ticker. Replaces the SSE event bus used in
/// the Python demos — no transport needed because everything is in-process.
/// </summary>
public sealed class CostLog
{
    private readonly object _gate = new();
    private readonly int _capacity;
    private readonly ObservableCollection<CostEvent> _events = new();

    public CostLog(int capacity = 256)
    {
        _capacity = capacity;
    }

    public ReadOnlyObservableCollection<CostEvent> Events => new(_events);

    public event Action<CostEvent>? EventRecorded;

    public void Record(CostEvent ev)
    {
        lock (_gate)
        {
            _events.Add(ev);
            while (_events.Count > _capacity) _events.RemoveAt(0);
        }
        EventRecorded?.Invoke(ev);
    }

    public void Reset()
    {
        lock (_gate) { _events.Clear(); }
    }

    public CostSummary Summarize()
    {
        lock (_gate)
        {
            var local = _events.Where(e => e.Route == "local").ToList();
            var cloud = _events.Where(e => e.Route is "cloud" or "mock-cloud").ToList();
            return new CostSummary(
                LocalCalls: local.Count,
                CloudCalls: cloud.Count,
                LocalTokens: local.Sum(e => e.TotalTokens),
                CloudTokens: cloud.Sum(e => e.TotalTokens),
                LocalUsd: local.Sum(e => e.EstimatedUsd),
                CloudUsd: cloud.Sum(e => e.EstimatedUsd));
        }
    }
}

public sealed record CostSummary(
    int LocalCalls,
    int CloudCalls,
    int LocalTokens,
    int CloudTokens,
    double LocalUsd,
    double CloudUsd)
{
    public int TotalCalls => LocalCalls + CloudCalls;
    public double TotalUsd => LocalUsd + CloudUsd;
    public double SavingsUsd => CloudUsd - LocalUsd;
}
