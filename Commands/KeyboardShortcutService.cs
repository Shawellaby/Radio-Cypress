using System.Windows.Input;
using Shawellaby.RadioCypress.Models;

namespace Shawellaby.RadioCypress.Services.Keyboard;

public sealed class KeyboardShortcutService
{
    private readonly Dictionary<ShortcutKey, Action> _applicationShortcuts = new();
    private readonly Dictionary<ShortcutKey, Action> _dialogShortcuts = new();
    private readonly Dictionary<ShortcutKey, Action> _playbackShortcuts = new();
    private readonly Dictionary<ShortcutKey, VisualizationMode> _visualizationShortcuts = new();

    public KeyboardShortcutService()
    {
        RegisterDefaultVisualizationShortcuts();
    }

    public void RegisterApplicationShortcut(Key key, ModifierKeys modifiers, Action action)
    {
        _applicationShortcuts[new ShortcutKey(key, modifiers)] = action;
    }

    public void RegisterDialogShortcut(Key key, ModifierKeys modifiers, Action action)
    {
        _dialogShortcuts[new ShortcutKey(key, modifiers)] = action;
    }

    public void RegisterPlaybackShortcut(Key key, ModifierKeys modifiers, Action action)
    {
        _playbackShortcuts[new ShortcutKey(key, modifiers)] = action;
    }

    public void RegisterVisualizationShortcut(Key key, VisualizationMode mode)
    {
        _visualizationShortcuts[new ShortcutKey(key, ModifierKeys.None)] = mode;
    }

    public bool TryExecute(KeyEventArgs e)
    {
        ShortcutKey shortcutKey = new(e.Key, System.Windows.Input.Keyboard.Modifiers);

        if (_applicationShortcuts.TryGetValue(shortcutKey, out Action? applicationAction))
        {
            applicationAction();
            return true;
        }

        if (_dialogShortcuts.TryGetValue(shortcutKey, out Action? dialogAction))
        {
            dialogAction();
            return true;
        }

        if (_playbackShortcuts.TryGetValue(shortcutKey, out Action? playbackAction))
        {
            playbackAction();
            return true;
        }

        return false;
    }

    public bool TryGetVisualizationMode(KeyEventArgs e, out VisualizationMode visualizationMode)
    {
        ShortcutKey shortcutKey = new(e.Key, System.Windows.Input.Keyboard.Modifiers);

        return _visualizationShortcuts.TryGetValue(shortcutKey, out visualizationMode);
    }

    public static int? GetStationNumber(Key key)
    {
        if (key >= Key.D1 && key <= Key.D9)
            return key - Key.D0;

        if (key >= Key.NumPad1 && key <= Key.NumPad9)
            return key - Key.NumPad0;

        return null;
    }

    private void RegisterDefaultVisualizationShortcuts()
    {
        RegisterVisualizationShortcut(Key.E, VisualizationMode.Equalizer);
        RegisterVisualizationShortcut(Key.P, VisualizationMode.Psychedelic);
        RegisterVisualizationShortcut(Key.W, VisualizationMode.Wave);
        RegisterVisualizationShortcut(Key.L, VisualizationMode.LedMatrix);
        RegisterVisualizationShortcut(Key.T, VisualizationMode.Ethereal);
        RegisterVisualizationShortcut(Key.S, VisualizationMode.Starfield);
        RegisterVisualizationShortcut(Key.O, VisualizationMode.Oscilloscope);
        RegisterVisualizationShortcut(Key.V, VisualizationMode.VuMeter);
        RegisterVisualizationShortcut(Key.N, VisualizationMode.MatrixRain);
        RegisterVisualizationShortcut(Key.X, VisualizationMode.LissajousScope);
    }

    private readonly record struct ShortcutKey(
        Key Key,
        ModifierKeys Modifiers);
}