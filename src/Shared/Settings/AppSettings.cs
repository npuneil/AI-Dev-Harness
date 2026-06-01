using System;
using Windows.Storage;

namespace LocalAiDemos.Shared.Settings;

/// <summary>
/// Persisted user preferences. Backed by <see cref="ApplicationData.LocalSettings"/>
/// when running as a packaged app; falls back to in-memory storage otherwise so
/// the unpackaged debug experience still works.
/// </summary>
public sealed class AppSettings
{
    private readonly ISettingsBackend _backend;

    public AppSettings()
    {
        _backend = TryGetPackagedBackend() ?? new InMemoryBackend();
    }

    public string ModelAlias
    {
        get => _backend.Get(nameof(ModelAlias), AI.ModelCatalog.DefaultSmallAlias);
        set => _backend.Set(nameof(ModelAlias), value);
    }

    public string DevicePreference
    {
        get => _backend.Get(nameof(DevicePreference), "auto"); // auto | npu | gpu | cpu
        set => _backend.Set(nameof(DevicePreference), value);
    }

    public string ThemeName
    {
        get => _backend.Get(nameof(ThemeName), "Default");
        set => _backend.Set(nameof(ThemeName), value);
    }

    public bool TelemetryEnabled
    {
        get => _backend.Get(nameof(TelemetryEnabled), true);
        set => _backend.Set(nameof(TelemetryEnabled), value);
    }

    private static ISettingsBackend? TryGetPackagedBackend()
    {
        try { return new PackagedBackend(ApplicationData.Current.LocalSettings); }
        catch { return null; }
    }

    private interface ISettingsBackend
    {
        T Get<T>(string key, T fallback);
        void Set<T>(string key, T value);
    }

    private sealed class PackagedBackend : ISettingsBackend
    {
        private readonly ApplicationDataContainer _container;
        public PackagedBackend(ApplicationDataContainer container) => _container = container;

        public T Get<T>(string key, T fallback)
        {
            if (_container.Values.TryGetValue(key, out var raw) && raw is T typed) return typed;
            return fallback;
        }

        public void Set<T>(string key, T value)
        {
            _container.Values[key] = value;
        }
    }

    private sealed class InMemoryBackend : ISettingsBackend
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, object?> _map = new();
        public T Get<T>(string key, T fallback) =>
            _map.TryGetValue(key, out var raw) && raw is T typed ? typed : fallback;
        public void Set<T>(string key, T value) => _map[key] = value;
    }
}
