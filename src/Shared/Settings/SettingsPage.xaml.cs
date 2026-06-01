using System.Collections.Generic;
using LocalAiDemos.Shared.Settings;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace LocalAiDemos.Shared.Settings;

/// <summary>
/// Generic Settings page reused by both harnesses. Edits persist to
/// <see cref="AppSettings"/> immediately; consumers re-read on demand.
/// </summary>
public sealed partial class SettingsPage : Page
{
    private readonly AppSettings _settings = new();

    public SettingsPage()
    {
        this.InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ModelBox.Text = _settings.ModelAlias;
        foreach (var item in DeviceBox.Items)
        {
            if (item is ComboBoxItem cbi && (cbi.Tag?.ToString() ?? "") == _settings.DevicePreference)
            {
                DeviceBox.SelectedItem = cbi;
                break;
            }
        }
        foreach (var item in ThemeBox.Items)
        {
            if (item is ComboBoxItem cbi && (cbi.Tag?.ToString() ?? "") == _settings.ThemeName)
            {
                ThemeBox.SelectedItem = cbi;
                break;
            }
        }
        TelemetryToggle.IsOn = _settings.TelemetryEnabled;
        MockToggle.IsOn = _settings.UseMock;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.ModelAlias = ModelBox.Text?.Trim() ?? AI.ModelCatalog.DefaultSmallAlias;
        if (DeviceBox.SelectedItem is ComboBoxItem d) _settings.DevicePreference = d.Tag?.ToString() ?? "auto";
        if (ThemeBox.SelectedItem is ComboBoxItem t) _settings.ThemeName = t.Tag?.ToString() ?? "Default";
        _settings.TelemetryEnabled = TelemetryToggle.IsOn;
        _settings.UseMock = MockToggle.IsOn;
        SavedText.Text = _settings.UseMock
            ? "saved. mock mode ON — chat will return deterministic stubs until you toggle it off."
            : "saved. restart the app for theme + model changes.";
    }
}
