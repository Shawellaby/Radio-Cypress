using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace RadioCypress;

public partial class StationSelectionWindow : Window
{
    public record StationSelectionItem(int Number, string Name);

    public IReadOnlyList<StationSelectionItem> Stations { get; }

    public int? SelectedStationNumber { get; private set; }

    public StationSelectionWindow(Dictionary<int, MainWindow.Station> stations)
    {
        InitializeComponent();

        Stations = stations
            .OrderBy(kvp => kvp.Key)
            .Select(kvp => new StationSelectionItem(kvp.Key, kvp.Value.Name))
            .ToList();

        DataContext = this;
        Loaded += (_, _) => Focus();
    }

    private void Station_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is int stationNumber)
        {
            SelectedStationNumber = stationNumber;
            DialogResult = true;
            Close();
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
        }
    }
}