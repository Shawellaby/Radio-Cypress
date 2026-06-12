using System.Windows;
using System.Windows.Input;
using CSCore;
using CSCore.Codecs;
using CSCore.SoundOut;
using Microsoft.Win32;
using System.Windows.Threading;
using System.Diagnostics;
using NAudio.Lame;
using CSCore.Streams;
using NAudio.Dsp;
using RadioCypress.Visualizations;

namespace RadioCypress;

/// <summary>
/// Interaction logic for MainWindow.xaml
///
/// RadioCypress - (c) 2022 Shawellaby Software LLC.  All Rights Reserved.
///
///    RadioCypress is free software: you can redistribute it and/or modify
///    it under the terms of the GNU General Public License as published by
///    the Free Software Foundation, either version 3 of the License, or
///    (at your option) any later version.
///
///    RadioCypress is distributed in the hope that it will be useful,
///    but WITHOUT ANY WARRANTY; without even the implied warranty of
///    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
///    GNU General Public License for more details.
///
///    You should have received a copy of the GNU General Public License
///    along with RadioCypress.  It is normally found in LICENSE
///    If not, see https://www.gnu.org/licenses.
///
/// </summary>
public partial class MainWindow : Window
{
    private ISoundOut _soundOut;
    private IWaveSource _waveSource;
    private IWaveSource _recordingSource;
    private bool _isRecording = false;
    private LameMP3FileWriter _mp3Writer;
    private string _recordingPath;
    private DispatcherTimer _visualizationTimer;
    private float[] _audioData;
    private SingleBlockNotificationStream _notificationSource;
    private bool _isMuted = false;
    private readonly object _audioLevelLock = new();

    public readonly record struct Station(string Name, string Url);

    private int _currentStationNumber = 1;

    private static readonly Dictionary<int, Station> DefaultSourceUrls = new()
    {
          { 1, new Station("Cypress Radio", "https://CypressRadio.org:8000/stream") }
        , { 2, new Station("Big 80's", "https://ssl.nexuscast.com:9044/;") }
        , { 3, new Station("Smooth 70's", "https://ice3.securenetsystems.net/S70S") }
        , { 4, new Station("Pure Country", "https://ice23.securenetsystems.net/QXFM") }
        , { 5, new Station("The Spot 98.7", "https://live.amperwave.net/manifest/audacy-kspffmaac-imc") }
        , { 6, new Station("The Cat 104.9", "https://live.amperwave.net/manifest/eagleradio-kbctfmaac-ibc4") }
        , { 7, new Station("retro 94.7", "https://ice41.securenetsystems.net/KCCT") }
        , { 8, new Station("Pickle 99.3", "http://dogglounge.com:8000") }
        , { 9, new Station("BIG 102.1", "https://ice42.securenetsystems.net/BIG") }
    };

    private Dictionary<int, Station> _sourceUrls = new();

    private const int FftSize = 1024;
    private readonly float[] _fftBuffer = new float[FftSize];
    private int _fftBufferIndex = 0;
    private readonly object _fftLock = new();
    private readonly Complex[] _fftComplex = new Complex[FftSize];

    private VisualizationMode _visualizationMode = VisualizationMode.Equalizer;
    private SpectrumVisualizationContext _visualizationContext;
    private readonly Dictionary<VisualizationMode, IVisualizer> _visualizers = new();

    private enum VisualizationMode
    {
        Equalizer,
        Psychedelic,
        Wave,
        LedMatrix,
        Ethereal,
        StarTrekComputer,
        Oscilloscope
    }

    private static readonly double[] VisualizationBucketFrequencies =
    {
        20, 25, 31.5, 40, 50, 63, 80, 100, 125, 150, 200, 250, 315, 400, 500, 630, 800, 1000, 1250, 1600, 2000, 2500, 3150, 4000, 5000, 6300, 8000, 10000, 12500, 16000, 20000
    };

    private const double TargetLevel = 0.08;
    private const double MinGain = 0.5;
    private const double MaxGain = 4.0;
    private double _smoothedGain = 1.0;
    private double _smoothedLevel = 0.0;

    /// <summary>
    /// Represents the main application window that hosts the radio player interface and audio visualization.
    /// Manages audio playback, station selection, audio effects processing, and real-time spectrum visualization.
    /// Provides interaction handling for user input including window dragging and keyboard shortcuts.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        _sourceUrls = StationStore.LoadOrCreateDefaultStations(DefaultSourceUrls);
        InitializeAudio();
        InitializeVisualization();
        this.MouseDown += delegate { DragMove(); };
        var window = Window.GetWindow(this);
        window.KeyUp += HandleKeyPress;
    }

    /// <summary>
    /// Initializes the audio visualization system by creating the visualization context,
    /// registering all available visualizer implementations, and starting the visualization
    /// update timer. Sets up the shared FFT buffer and synchronization mechanism for
    /// real-time spectrum analysis across multiple visualization modes.
    /// </summary>
    private void InitializeVisualization()
    {
        _visualizationContext = new SpectrumVisualizationContext(
            FftSize,
            _fftBuffer,
            _fftLock,
            () => _fftBufferIndex,
            () => _recordingSource?.WaveFormat?.SampleRate ?? 44100,
            () => _smoothedGain,
            VisualizationBucketFrequencies);

        _visualizers[VisualizationMode.Equalizer] = new EqualizerSpectrumVisualizer();
        _visualizers[VisualizationMode.Psychedelic] = new PsychedelicSpectrumVisualizer();
        _visualizers[VisualizationMode.Wave] = new WaveSpectrumVisualizer();
        _visualizers[VisualizationMode.LedMatrix] = new LedMatrixSpectrumVisualizer();
        _visualizers[VisualizationMode.Ethereal] = new EtherealSpectrumVisualizer();
        _visualizers[VisualizationMode.Oscilloscope] = new OscilloscopeWaveformVisualizer();

        _visualizationTimer = new DispatcherTimer();
        _visualizationTimer.Interval = TimeSpan.FromMilliseconds(50);
        _visualizationTimer.Tick += UpdateVisualization;
        _visualizationTimer.Start();
    }

    /// <summary>
    /// Updates the audio spectrum visualization on the canvas by delegating to the currently active visualizer based on the selected visualization mode.
    /// Retrieves the appropriate visualizer from the dictionary and invokes its draw method with the current visualization context and FFT data.
    /// </summary>
    /// <param name="sender">The source of the timer event that triggers the visualization update.</param>
    /// <param name="e">The event arguments associated with the timer tick event.</param>
    private void UpdateVisualization(object sender, EventArgs e)
    {
        if (_visualizers.TryGetValue(_visualizationMode, out IVisualizer visualizer))
            visualizer.Draw(VisualizationCanvas, _visualizationContext);
    }

    /// <summary>
    /// Initializes the audio system by setting the default radio station source and updating the status display.
    /// Handles any initialization errors by displaying an error message dialog to the user.
    /// </summary>
    private void InitializeAudio()
    {
        try
        {
            SwitchSource(1);
            UpdateStatusLabels();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error initializing audio: {ex.Message}", "Audio Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }


    /// <summary>
    /// Handles keyboard input events for controlling application functionality including station selection, visualization modes, recording, mute toggle, and application exit.
    /// Processes key presses for Q (quit), H (help), Enter (station selection), M (mute), R (recording), E/P/W/L (visualization modes), and numeric keys (station switching).
    /// </summary>
    /// <param name="sender">The source of the keyboard event.</param>
    /// <param name="e">The keyboard event arguments containing information about the pressed key.</param>
    private void HandleKeyPress(object sender, KeyEventArgs e)
    {
        if (e.IsRepeat)
            return;

        if (e.Key == Key.Q)
        {
            CleanupAudio();
            System.Windows.Application.Current.Shutdown();
            return;
        }


        if (e.Key == Key.H)
        {
            var helpWindow = new HelpWindow(_sourceUrls)
            {
                Owner = this
            };

            helpWindow.ShowDialog();
            return;
        }

        if (e.Key == Key.E && Keyboard.Modifiers == ModifierKeys.Control)
        {
            ShowStationEditorWindow();
            return;
        }

        if (e.Key == Key.Enter || e.Key == Key.Return)
        {
            ShowStationSelectionWindow();
            return;
        }

        if (e.Key == Key.M)
        {
            if (_soundOut != null)
            {
                _isMuted = !_isMuted;
                UpdateStatusLabels();
                _soundOut.Volume = _isMuted ? 0 : 1.0f;
            }

            return;
        }

        if (e.Key == Key.R)
        {
            if (_isRecording)
                StopRecording();
            else
                StartRecording();
            return;
        }

        if (e.Key == Key.E)
        {
            _visualizationMode = VisualizationMode.Equalizer;
            return;
        }

        if (e.Key == Key.P)
        {
            _visualizationMode = VisualizationMode.Psychedelic;
            return;
        }

        if (e.Key == Key.W)
        {
            _visualizationMode = VisualizationMode.Wave;
            return;
        }

        if (e.Key == Key.L)
        {
            _visualizationMode = VisualizationMode.LedMatrix;
            return;
        }

        if (e.Key == Key.T)
        {
            _visualizationMode = VisualizationMode.Ethereal;
            return;
        }

        if (e.Key == Key.S)
        {
            _visualizationMode = VisualizationMode.StarTrekComputer;
            return;
        }

        if (e.Key == Key.O)
        {
            _visualizationMode = VisualizationMode.Oscilloscope;
            return;
        }

        if (e.Key >= Key.D1 && e.Key <= Key.D9)
        {
            int sourceNumber = e.Key - Key.D0;
            SwitchSource(sourceNumber);
            _currentStationNumber = sourceNumber;
            return;
        }

        if (e.Key >= Key.NumPad1 && e.Key <= Key.NumPad9)
        {
            int sourceNumber = e.Key - Key.NumPad0;
            SwitchSource(sourceNumber);
            _currentStationNumber = sourceNumber;
            return;
        }
    }

    /// <summary>
    /// Opens a modal dialog window that displays the list of available radio stations and allows the user to select one.
    /// Creates a new StationSelectionWindow instance with the configured stations, sets the main window as its owner, and switches to the selected station if the dialog is confirmed.
    /// </summary>
    private void ShowStationSelectionWindow()
    {
        var stationSelectionWindow = new StationSelectionWindow(_sourceUrls)
        {
            Owner = this
        };

        if (stationSelectionWindow.ShowDialog() == true
            && stationSelectionWindow.SelectedStationNumber is int stationNumber)
        {
            SwitchSource(stationNumber);
            _currentStationNumber = stationNumber;
        }
    }

    /// <summary>
    /// Displays the station editor window, enabling users to view, edit, and save the list of radio stations.
    /// Updates the internal station list upon confirmation and saves changes using the StationStore.
    /// If the current active station is modified or removed, switches to a valid station automatically.
    /// </summary>
    private void ShowStationEditorWindow()
    {
        var stationEditorWindow = new StationEditorWindow(_sourceUrls)
        {
            Owner = this
        };

        if (stationEditorWindow.ShowDialog() != true)
            return;

        _sourceUrls = stationEditorWindow.EditedStations;
        StationStore.SaveStations(_sourceUrls);

        if (_sourceUrls.ContainsKey(_currentStationNumber))
        {
            SwitchSource(_currentStationNumber);
        }
        else
        {
            int firstStationNumber = _sourceUrls.Keys.OrderBy(number => number).First();
            _currentStationNumber = firstStationNumber;
            SwitchSource(firstStationNumber);
        }
    }

    /// <summary>
    /// Initiates audio recording from the current radio station stream by prompting the user to select a save location and initializing an MP3 writer.
    /// Creates a sanitized filename based on the station name and current timestamp, configures the audio format to 16-bit PCM at the source sample rate, and sets up recording with 128 kbps MP3 encoding.
    /// </summary>
    private void StartRecording()
    {
        try
        {
            if (_recordingSource == null)
                return;

            var currentStation = _sourceUrls.TryGetValue(_currentStationNumber, out Station station);
            var safeStationName = string.Concat(station.Name.Select(c =>
                System.IO.Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

            SaveFileDialog saveDialog = new SaveFileDialog
            {
                Filter = "MP3 files (*.mp3)|*.mp3", DefaultExt = "mp3", FileName = $"{safeStationName}_{DateTime.Now:yyyyMMdd_HHmmss}.mp3"
            };

            if (saveDialog.ShowDialog() == true)
            {
                _recordingPath = saveDialog.FileName;

                var waveFormat = new NAudio.Wave.WaveFormat(_recordingSource.WaveFormat.SampleRate, 16, _recordingSource.WaveFormat.Channels);

                _mp3Writer = new LameMP3FileWriter(_recordingPath, waveFormat, 128);
                _isRecording = true;
                UpdateStatusLabels();

                MessageBox.Show("Recording started! Press 'R' again to stop.", "Recording", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error starting recording: {ex.Message}", "Recording Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Stops the current audio recording session and finalizes the recorded file.
    /// Disposes of the MP3 writer resource, updates the recording status, and displays a message box with the saved file location.
    /// </summary>
    private void StopRecording()
    {
        if (_isRecording)
        {
            _isRecording = false;
            UpdateStatusLabels();

            lock (_mp3Writer!)
            {
                _mp3Writer?.Dispose();
                _mp3Writer = null;
            }

            MessageBox.Show($"Recording saved to: {_recordingPath}", "Recording Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    /// <summary>
    /// Switches the audio playback to a different radio station source based on the provided station number.
    /// Stops any current recording, disposes of existing audio resources, initializes a new audio stream from the selected station,
    /// sets up FFT processing for visualization, and resumes recording if it was active before the switch.
    /// </summary>
    /// <param name="sourceNumber">The numeric identifier of the station to switch to, corresponding to an entry in the station dictionary.</param>
    private void SwitchSource(int sourceNumber)
    {
        if (!_sourceUrls.TryGetValue(sourceNumber, out Station station))
            return;

        bool wasRecording = _isRecording;
        if (wasRecording)
            StopRecording();

        _soundOut?.Stop();
        _soundOut?.Dispose();
        _waveSource?.Dispose();

        _waveSource = CodecFactory.Instance.GetCodec(new Uri(station.Url));

        var sampleSource = _waveSource.ToSampleSource();

        _notificationSource = new SingleBlockNotificationStream(sampleSource);
        _notificationSource.SingleBlockRead += (s, a) =>
        {
            lock (_fftLock)
            {
                float mono = (a.Left + a.Right) * 0.5f;
                _fftBuffer[_fftBufferIndex++] = mono;

                if (_fftBufferIndex >= FftSize)
                    _fftBufferIndex = 0;
            }
        };

        var normalizedSampleSource = new NormalizedSampleSource(_notificationSource, this);
        _recordingSource = normalizedSampleSource.ToWaveSource();

        _soundOut = new CSCore.SoundOut.WasapiOut();
        _soundOut.Initialize(_recordingSource);
        _soundOut.Play();

        StationName.Text = CenterTextInField(station.Name, 22);
        //StationLogo.Source = new BitmapImage(new Uri("Content/" + station.ImageSource, UriKind.Relative));

        if (wasRecording)
            StartRecording();
    }

    /// <summary>
    /// Centers text within a field of specified width by adding padding spaces on both sides.
    /// If the text length equals or exceeds the specified width, truncates the text to fit the width.
    /// </summary>
    /// <param name="text">The text to center. Null values are treated as empty strings.</param>
    /// <param name="width">The total width of the field in characters.</param>
    /// <return>A string of the specified width with the text centered using space padding, or truncated if the text exceeds the width.</return>
    private static string CenterTextInField(string text, int width)
    {
        text ??= string.Empty;

        if (text.Length >= width)
            return text[..width];

        int totalPadding = width - text.Length;
        int leftPadding = totalPadding / 2;
        int rightPadding = totalPadding - leftPadding;

        return new string(' ', leftPadding) + text + new string(' ', rightPadding);
    }


    /// <summary>
    /// Updates the visibility of status labels in the user interface based on the current application state.
    /// Shows or hides the mute indicator label depending on whether audio is muted, and shows or hides the recording indicator label depending on whether recording is active.
    /// </summary>
    private void UpdateStatusLabels()
    {
        IsMutedLabel.Visibility = _isMuted ? Visibility.Visible : Visibility.Collapsed;
        IsRecordingLabel.Visibility = _isRecording ? Visibility.Visible : Visibility.Collapsed;
    }


    /// <summary>
    /// Applies level normalization to the provided audio sample values.
    /// This method is currently inactive as the normalization logic has been commented out to prevent disruptive corrections for specific music genres.
    /// </summary>
    /// <param name="left">The left channel audio sample value.</param>
    /// <param name="right">The right channel audio sample value.</param>
    internal void ApplyLevelNormalization(float left, float right)
    {
        // TODO This has been commented out as it is too disruptive for specific genres of music as it constantly attempts to correct
        // double currentLevel = Math.Sqrt((left * left + right * right) * 0.5);
        //
        // lock (_audioLevelLock)
        // {
        //     _smoothedLevel = _smoothedLevel * 0.995 + currentLevel * 0.005;
        //
        //     double safeLevel = Math.Max(_smoothedLevel, 0.0001);
        //     double targetGain = TargetLevel / safeLevel;
        //     targetGain = Math.Clamp(targetGain, MinGain, MaxGain);
        //
        //     _smoothedGain = _smoothedGain * 0.995 + targetGain * 0.005;
        // }
    }

    /// <summary>
    /// Writes normalized audio sample data to the MP3 recording file if recording is active.
    /// Converts normalized floating-point audio samples to 16-bit PCM format, clamps the values to prevent overflow, and writes the stereo data to the MP3 writer in a thread-safe manner.
    /// </summary>
    /// <param name="left">The normalized left channel audio sample value, ranging from -1.0 to 1.0.</param>
    /// <param name="right">The normalized right channel audio sample value, ranging from -1.0 to 1.0.</param>
    internal void WriteNormalizedRecording(float left, float right)
    {
        if (!_isRecording || _mp3Writer == null)
            return;

        short recordedLeft = (short)Math.Clamp(left * short.MaxValue, short.MinValue, short.MaxValue);
        short recordedRight = (short)Math.Clamp(right * short.MaxValue, short.MinValue, short.MaxValue);

        byte[] buffer = new byte[4];
        Buffer.BlockCopy(new short[] { recordedLeft, recordedRight }, 0, buffer, 0, buffer.Length);

        lock (_mp3Writer)
        {
            _mp3Writer.Write(buffer, 0, buffer.Length);
        }
    }

    /// <summary>
    /// Retrieves the current audio gain value in a thread-safe manner.
    /// The gain value is used for audio level normalization and is protected by a lock to ensure consistency across concurrent access.
    /// </summary>
    /// <return>
    /// The current smoothed gain value as a float, representing the audio amplification factor.
    /// </return>
    internal float GetCurrentGain()
    {
        lock (_audioLevelLock)
        {
            return (float)_smoothedGain;
        }
    }

    /// <summary>
    /// Releases all audio resources by stopping and disposing of the sound output device, wave source, and MP3 writer instances.
    /// Ensures proper cleanup of audio streams and prevents resource leaks when audio playback is no longer needed.
    /// </summary>
    private void CleanupAudio()
    {
        _soundOut?.Stop();
        _soundOut?.Dispose();
        _waveSource?.Dispose();
        _mp3Writer?.Dispose();
    }

    /// <summary>
    /// Handles the window closed event by performing cleanup of audio resources after the base window closure operations complete.
    /// Invokes the base class OnClosed method and then ensures all audio components are properly disposed of through the CleanupAudio method.
    /// </summary>
    /// <param name="e">The event arguments associated with the window closed event.</param>
    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        CleanupAudio();
    }

    /// <summary>
    /// Handles mouse button down events on the window to enable drag functionality.
    /// Initiates window dragging when the left mouse button is pressed, allowing the user to move the window by clicking and dragging anywhere on its surface.
    /// </summary>
    /// <param name="sender">The source of the mouse down event.</param>
    /// <param name="e">The event arguments containing information about which mouse button was pressed and the button state.</param>
    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            this.DragMove();
    }


}