using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Shawellaby.RadioCypress.Models;
using Shawellaby.RadioCypress.Services.Audio;
using Shawellaby.RadioCypress.Services.Keyboard;
using Shawellaby.RadioCypress.Services.Stations;
using Shawellaby.RadioCypress.Services.Visualization;
using Shawellaby.RadioCypress.Visualizations;

namespace Shawellaby.RadioCypress;

/// <summary>
/// Main application window that hosts the radio player interface and audio visualization.
/// Manages audio playback, station selection, recording, real-time visualization, window dragging,
/// and keyboard shortcuts.
/// </summary>
public partial class MainWindow : Window
{
    private readonly IStationStore _stationStore;
    private readonly IRadioPlaybackService _playbackService;
    private readonly IRecordingService _recordingService;
    private readonly IAudioAnalysisService _audioAnalysisService;
    private readonly VisualizationRegistry _visualizationRegistry;
    private readonly KeyboardShortcutService _keyboardShortcutService;

    private Dictionary<int, Station> _stations = new();
    private int _currentStationNumber = 1;
    private VisualizationMode _visualizationMode = VisualizationMode.Equalizer;
    private TimeSpan _lastVisualizationRenderTime = TimeSpan.Zero;
    private static readonly TimeSpan VisualizationRenderInterval = TimeSpan.FromMilliseconds(33);
    private bool _isAudioInitialized;
    private bool _isCleanedUp;

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class and wires up
    /// playback, recording, visualization, station loading, and input handling.
    /// </summary>
    public MainWindow(
        IStationStore stationStore,
        IRadioPlaybackService playbackService,
        IRecordingService recordingService,
        IAudioAnalysisService audioAnalysisService,
        VisualizationRegistry visualizationRegistry)
    {
        InitializeComponent();

        _stationStore = stationStore ?? throw new ArgumentNullException(nameof(stationStore));
        _playbackService = playbackService ?? throw new ArgumentNullException(nameof(playbackService));
        _recordingService = recordingService ?? throw new ArgumentNullException(nameof(recordingService));
        _audioAnalysisService = audioAnalysisService ?? throw new ArgumentNullException(nameof(audioAnalysisService));
        _visualizationRegistry = visualizationRegistry ?? throw new ArgumentNullException(nameof(visualizationRegistry));
        _keyboardShortcutService = new KeyboardShortcutService();

        RegisterKeyboardShortcuts();

        _playbackService.SamplesAvailable += PlaybackService_SamplesAvailable;

        MouseDown += MainWindow_MouseDown;
        PreviewKeyDown += HandleKeyPress;

        InitializeStations();
        InitializeAudio();
        InitializeVisualization();
    }



    private void InitializeStations()
    {
        _stations = _stationStore
            .LoadOrCreateDefaultStations()
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        if (_stations.Count == 0)
            throw new InvalidOperationException("No stations are configured.");

        if (!_stations.ContainsKey(_currentStationNumber))
            _currentStationNumber = _stations.Keys.OrderBy(number => number).First();
    }

    private void InitializeAudio()
    {
        try
        {
            SwitchStation(_currentStationNumber);
            UpdateStatusLabels();
            _isAudioInitialized = true;
        }
        catch (Exception ex)
        {
            _isAudioInitialized = false;
            Trace.TraceError(ex.ToString());

            MessageBox.Show(
                $"Error initializing audio: {ex.Message}",
                "Audio Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Starts the audio visualization timer when audio initialization succeeded.
    /// </summary>
    private void InitializeVisualization()
    {
        if (!_isAudioInitialized)
            return;

        CompositionTarget.Rendering += UpdateVisualization;
    }

    private void RegisterKeyboardShortcuts()
    {
        _keyboardShortcutService.RegisterApplicationShortcut(
            Key.Q,
            ModifierKeys.None,
            () =>
            {
                Cleanup();
                Application.Current.Shutdown();
            });

        _keyboardShortcutService.RegisterDialogShortcut(
            Key.H,
            ModifierKeys.None,
            ShowHelpWindow);

        _keyboardShortcutService.RegisterDialogShortcut(
            Key.E,
            ModifierKeys.Control,
            ShowStationEditorWindow);

        _keyboardShortcutService.RegisterDialogShortcut(
            Key.Enter,
            ModifierKeys.None,
            ShowStationSelectionWindow);

        _keyboardShortcutService.RegisterDialogShortcut(
            Key.Return,
            ModifierKeys.None,
            ShowStationSelectionWindow);

        _keyboardShortcutService.RegisterPlaybackShortcut(
            Key.M,
            ModifierKeys.None,
            ToggleMute);

        _keyboardShortcutService.RegisterPlaybackShortcut(
            Key.R,
            ModifierKeys.None,
            ToggleRecording);
    }

    private void PlaybackService_SamplesAvailable(object? sender, AudioBufferEventArgs e)
    {
        if (_isCleanedUp)
            return;

        try
        {
            _audioAnalysisService.PushSamples(
                e.Buffer,
                e.Offset,
                e.SampleCount,
                e.ChannelCount);

            if (_recordingService.IsRecording)
            {
                _recordingService.WriteSamples(
                    e.Buffer,
                    e.Offset,
                    e.SampleCount,
                    e.ChannelCount);
            }
        }
        catch (ObjectDisposedException ex)
        {
            Trace.TraceWarning(ex.ToString());
        }
        catch (InvalidOperationException ex)
        {
            Trace.TraceWarning(ex.ToString());
        }
    }

    /// <summary>
    /// Updates the audio spectrum visualization on the canvas by delegating to the active visualizer.
    /// </summary>
    /// <param name="sender">The source of the timer event that triggers the visualization update.</param>
    /// <param name="e">The event arguments associated with the timer tick event.</param>
    private void UpdateVisualization(object? sender, EventArgs e)
    {
        if (_isCleanedUp || !IsLoaded)
            return;

        if (e is RenderingEventArgs renderingEventArgs)
        {
            if (renderingEventArgs.RenderingTime - _lastVisualizationRenderTime < VisualizationRenderInterval)
                return;

            _lastVisualizationRenderTime = renderingEventArgs.RenderingTime;
        }

        if (VisualizationCanvas.ActualWidth <= 0 || VisualizationCanvas.ActualHeight <= 0)
            return;

        IVisualizer? visualizer = _visualizationRegistry.GetVisualizer(_visualizationMode);

        if (visualizer is null)
            return;

        visualizer.Draw(
            VisualizationCanvas,
            _audioAnalysisService.VisualizationContext);
    }

    private void HandleKeyPress(object sender, KeyEventArgs e)
    {
        if (e.IsRepeat)
            return;

        if (_keyboardShortcutService.TryExecute(e))
        {
            e.Handled = true;
            return;
        }

        if (_keyboardShortcutService.TryGetVisualizationMode(e, out VisualizationMode mode))
        {
            _visualizationMode = mode;
            e.Handled = true;
            return;
        }

        if (HandleStationShortcut(e))
            e.Handled = true;
    }

    private bool HandleStationShortcut(KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None)
            return false;

        int? stationNumber = KeyboardShortcutService.GetStationNumber(e.Key);

        if (stationNumber is null)
            return false;

        SwitchStation(stationNumber.Value);
        return true;
    }

    private void ShowHelpWindow()
    {
        HelpWindow helpWindow = new(_stations)
        {
            Owner = this
        };

        helpWindow.ShowDialog();
    }

    private void ShowStationSelectionWindow()
    {
        StationSelectionWindow stationSelectionWindow = new(_stations)
        {
            Owner = this
        };

        if (stationSelectionWindow.ShowDialog() != true)
            return;

        if (stationSelectionWindow.SelectedStationNumber is not int stationNumber)
            return;

        SwitchStation(stationNumber);
    }

    private void ShowStationEditorWindow()
    {
        StationEditorWindow stationEditorWindow = new(_stations)
        {
            Owner = this
        };

        if (stationEditorWindow.ShowDialog() != true)
            return;

        _stations = stationEditorWindow.EditedStations;
        _stationStore.SaveStations(_stations);

        if (_stations.ContainsKey(_currentStationNumber))
        {
            SwitchStation(_currentStationNumber);
            return;
        }

        int firstStationNumber = _stations.Keys.OrderBy(number => number).First();
        SwitchStation(firstStationNumber);
    }

    private void SwitchStation(int stationNumber)
    {
        if (!_stations.TryGetValue(stationNumber, out Station? station))
            return;

        bool resumeRecording = _recordingService.IsRecording;

        if (resumeRecording)
            StopRecording(showCompletionMessage: false);

        _playbackService.Play(station.Url);

        _currentStationNumber = stationNumber;
        StationName.Text = CenterTextInField(station.Name, 22);

        if (resumeRecording)
            StartRecording();

        UpdateStatusLabels();
    }

    private void ToggleMute()
    {
        _playbackService.SetMuted(!_playbackService.IsMuted);
        UpdateStatusLabels();
    }

    private void ToggleRecording()
    {
        if (_recordingService.IsRecording)
            StopRecording(showCompletionMessage: true);
        else
            StartRecording();
    }

    private void StartRecording()
    {
        if (!_stations.TryGetValue(_currentStationNumber, out Station? currentStation))
            return;

        try
        {
            string safeStationName = CreateSafeFileName(currentStation.Name);

            SaveFileDialog saveDialog = new()
            {
                Filter = "MP3 files (*.mp3)|*.mp3",
                DefaultExt = "mp3",
                FileName = $"{safeStationName}_{DateTime.Now:yyyyMMdd_HHmmss}.mp3"
            };

            if (saveDialog.ShowDialog() != true)
                return;

            _recordingService.Start(
                saveDialog.FileName,
                _playbackService.SampleRate,
                _playbackService.ChannelCount);

            UpdateStatusLabels();

            MessageBox.Show(
                "Recording started! Press 'R' again to stop.",
                "Recording",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Trace.TraceError(ex.ToString());

            MessageBox.Show(
                $"Error starting recording: {ex.Message}",
                "Recording Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void StopRecording(bool showCompletionMessage)
    {
        if (!_recordingService.IsRecording)
            return;

        string? recordingPath = _recordingService.CurrentPath;

        _recordingService.Stop();
        UpdateStatusLabels();

        if (!showCompletionMessage)
            return;

        MessageBox.Show(
            $"Recording saved to: {recordingPath}",
            "Recording Complete",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void UpdateStatusLabels()
    {
        IsMutedLabel.Visibility = _playbackService.IsMuted
            ? Visibility.Visible
            : Visibility.Collapsed;

        IsRecordingLabel.Visibility = _recordingService.IsRecording
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static string CenterTextInField(string? text, int width)
    {
        text ??= string.Empty;

        if (text.Length >= width)
            return text[..width];

        int totalPadding = width - text.Length;
        int leftPadding = totalPadding / 2;
        int rightPadding = totalPadding - leftPadding;

        return new string(' ', leftPadding) + text + new string(' ', rightPadding);
    }

    private static string CreateSafeFileName(string fileName)
    {
        char[] invalidCharacters = System.IO.Path.GetInvalidFileNameChars();

        return string.Concat(fileName.Select(character =>
            invalidCharacters.Contains(character) ? '_' : character));
    }

    private void MainWindow_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void Cleanup()
    {
        if (_isCleanedUp)
            return;

        _isCleanedUp = true;

        CompositionTarget.Rendering -= UpdateVisualization;

        _playbackService.SamplesAvailable -= PlaybackService_SamplesAvailable;

        MouseDown -= MainWindow_MouseDown;
        PreviewKeyDown -= HandleKeyPress;

        _recordingService.Dispose();
        _playbackService.Dispose();
        _audioAnalysisService.Dispose();
    }

    protected override void OnClosed(EventArgs e)
    {
        Cleanup();
        base.OnClosed(e);
    }
}