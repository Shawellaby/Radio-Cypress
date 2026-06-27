using System.Diagnostics;
using System.Windows;
using Shawellaby.RadioCypress.Services.Audio;
using Shawellaby.RadioCypress.Services.Stations;
using Shawellaby.RadioCypress.Services.Visualization;

namespace Shawellaby.RadioCypress;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            MainWindow mainWindow = new(
                new JsonStationStore(),
                new RadioPlaybackService(),
                new Mp3RecordingService(),
                new AudioAnalysisService(),
                new VisualizationRegistry());

            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            Trace.TraceError(ex.ToString());

            MessageBox.Show(
                ex.ToString(),
                "Radio Cypress Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(1);
        }
    }
}