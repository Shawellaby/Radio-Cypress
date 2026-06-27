using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Shawellaby.RadioCypress.Models;

namespace Shawellaby.RadioCypress;

public partial class StationEditorWindow : Window, INotifyPropertyChanged
{
    private StationEditorItem? _selectedStation;

    public ObservableCollection<StationEditorItem> Stations { get; }

    public StationEditorItem? SelectedStation
    {
        get => _selectedStation;
        set
        {
            if (_selectedStation == value)
                return;

            _selectedStation = value;
            OnPropertyChanged();
        }
    }

    public Dictionary<int, Station> EditedStations { get; private set; } = new();

    public StationEditorWindow(Dictionary<int, Station> stations)
    {
        InitializeComponent();

        Stations = new ObservableCollection<StationEditorItem>(
            stations
                .OrderBy(station => station.Key)
                .Select(station => new StationEditorItem
                {
                    Number = station.Key,
                    Name = station.Value.Name,
                    Url = station.Value.Url
                }));

        DataContext = this;

        Loaded += (_, _) =>
        {
            Focus();

            if (Stations.Count > 0)
                SelectedStation = Stations[0];
        };
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        int nextNumber = GetNextAvailableStationNumber();

        if (nextNumber == 0)
        {
            MessageBox.Show(
                "All 9 station slots are already in use.",
                "Station Limit Reached",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        StationEditorItem newStation = new()
        {
            Number = nextNumber,
            Name = "New Station",
            Url = "https://",
            ImageSource = string.Empty
        };

        Stations.Add(newStation);
        SelectedStation = newStation;
        StationsGrid.ScrollIntoView(newStation);
        StationsGrid.BeginEdit();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedStation is null)
            return;

        MessageBoxResult result = MessageBox.Show(
            $"Delete station {SelectedStation.Number} - {SelectedStation.Name}?",
            "Delete Station",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        Stations.Remove(SelectedStation);
        SelectedStation = Stations.FirstOrDefault();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        StationsGrid.CommitEdit();
        StationsGrid.CommitEdit(DataGridEditingUnit.Row, true);

        string? validationError = ValidateStations();

        if (validationError is not null)
        {
            MessageBox.Show(
                validationError,
                "Invalid Station Configuration",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        EditedStations = Stations
            .OrderBy(station => station.Number)
            .ToDictionary(
                station => station.Number,
                station => new Station(
                    station.Number,
                    station.Name.Trim(),
                    station.Url.Trim()));

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            Close();
    }

    private int GetNextAvailableStationNumber()
    {
        HashSet<int> usedNumbers = Stations
            .Select(station => station.Number)
            .ToHashSet();

        for (int number = 1; number <= 9; number++)
        {
            if (!usedNumbers.Contains(number))
                return number;
        }

        return 0;
    }

    private string? ValidateStations()
    {
        if (Stations.Count == 0)
            return "At least one station is required.";

        if (Stations.Count > 9)
            return "Only 9 stations are allowed.";

        foreach (StationEditorItem station in Stations)
        {
            if (station.Number is < 1 or > 9)
                return "Station numbers must be between 1 and 9.";

            if (string.IsNullOrWhiteSpace(station.Name))
                return $"Station {station.Number} is missing a name.";

            if (string.IsNullOrWhiteSpace(station.Url))
                return $"Station {station.Number} is missing a stream URL.";

            if (!Uri.TryCreate(station.Url.Trim(), UriKind.Absolute, out Uri? uri)
                || uri.Scheme is not "http" and not "https")
            {
                return $"Station {station.Number} has an invalid stream URL.";
            }
        }

        int duplicateNumber = Stations
            .GroupBy(station => station.Number)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .FirstOrDefault();

        if (duplicateNumber > 0)
            return $"Station number {duplicateNumber} is used more than once.";

        return null;
    }



    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class StationEditorItem : INotifyPropertyChanged
{
    private int _number;
    private string _name = string.Empty;
    private string _url = string.Empty;
    private string _imageSource = string.Empty;

    public int Number
    {
        get => _number;
        set
        {
            if (_number == value)
                return;

            _number = value;
            OnPropertyChanged();
        }
    }

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
                return;

            _name = value;
            OnPropertyChanged();
        }
    }

    public string Url
    {
        get => _url;
        set
        {
            if (_url == value)
                return;

            _url = value;
            OnPropertyChanged();
        }
    }

    public string ImageSource
    {
        get => _imageSource;
        set
        {
            if (_imageSource == value)
                return;

            _imageSource = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}