using System.IO;
using System.Text.Json;

namespace RadioCypress;

public sealed record StationDefinition(
    int Number,
    string Name,
    string Url);

public static class StationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static string StationsFolder =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RadioCypress");

    private static string StationsFilePath =>
        Path.Combine(StationsFolder, "stations.json");

    public static Dictionary<int, MainWindow.Station> LoadOrCreateDefaultStations(
        IReadOnlyDictionary<int, MainWindow.Station> defaultStations)
    {
        Directory.CreateDirectory(StationsFolder);

        if (!File.Exists(StationsFilePath))
        {
            SaveStations(defaultStations);
            return new Dictionary<int, MainWindow.Station>(defaultStations);
        }

        try
        {
            string json = File.ReadAllText(StationsFilePath);

            List<StationDefinition>? stationDefinitions =
                JsonSerializer.Deserialize<List<StationDefinition>>(json, JsonOptions);

            if (stationDefinitions is null || stationDefinitions.Count == 0)
            {
                SaveStations(defaultStations);
                return new Dictionary<int, MainWindow.Station>(defaultStations);
            }

            Dictionary<int, MainWindow.Station> loadedStations = stationDefinitions
                .Where(station => station.Number is >= 1 and <= 9)
                .GroupBy(station => station.Number)
                .Select(group => group.First())
                .OrderBy(station => station.Number)
                .ToDictionary(
                    station => station.Number,
                    station => new MainWindow.Station(
                        station.Name,
                        station.Url));

            if (loadedStations.Count == 0)
            {
                SaveStations(defaultStations);
                return new Dictionary<int, MainWindow.Station>(defaultStations);
            }

            return loadedStations;
        }
        catch
        {
            SaveStations(defaultStations);
            return new Dictionary<int, MainWindow.Station>(defaultStations);
        }
    }

    public static void SaveStations(IReadOnlyDictionary<int, MainWindow.Station> stations)
    {
        Directory.CreateDirectory(StationsFolder);

        List<StationDefinition> stationDefinitions = stations
            .Where(station => station.Key is >= 1 and <= 9)
            .OrderBy(station => station.Key)
            .Select(station => new StationDefinition(
                station.Key,
                station.Value.Name,
                station.Value.Url))
            .ToList();

        string json = JsonSerializer.Serialize(stationDefinitions, JsonOptions);
        File.WriteAllText(StationsFilePath, json);
    }

    public static string GetStationsFilePath()
    {
        Directory.CreateDirectory(StationsFolder);
        return StationsFilePath;
    }
}