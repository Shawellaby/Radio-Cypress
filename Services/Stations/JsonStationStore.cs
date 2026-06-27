using System.IO;
using System.Text.Json;
using Shawellaby.RadioCypress.Models;

namespace Shawellaby.RadioCypress.Services.Stations;

public sealed class JsonStationStore : IStationStore
{
    private const string ApplicationFolderName = "RadioCypress";
    private const string StationsFileName = "stations.json";
    private const int MinimumStationNumber = 1;
    private const int MaximumStationNumber = 9;

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _stationsFilePath;

    public JsonStationStore()
        : this(GetDefaultStationsFilePath())
    {
    }

    public JsonStationStore(string stationsFilePath)
    {
        _stationsFilePath = stationsFilePath;
    }

    public IReadOnlyDictionary<int, Station> LoadOrCreateDefaultStations()
    {
        try
        {
            EnsureStorageDirectoryExists();

            if (!File.Exists(_stationsFilePath))
            {
                IReadOnlyDictionary<int, Station> defaultStations = DefaultStationProvider.GetDefaultStations();
                SaveStations(defaultStations);
                return defaultStations;
            }

            string json = File.ReadAllText(_stationsFilePath);

            if (string.IsNullOrWhiteSpace(json))
                return RestoreDefaults();

            List<Station>? loadedStations = JsonSerializer.Deserialize<List<Station>>(
                json,
                JsonSerializerOptions);

            Dictionary<int, Station> validStations = NormalizeStations(loadedStations);

            if (validStations.Count == 0)
                return RestoreDefaults();

            return validStations;
        }
        catch
        {
            return RestoreDefaults();
        }
    }

    public void SaveStations(IReadOnlyDictionary<int, Station> stations)
    {
        EnsureStorageDirectoryExists();

        List<Station> normalizedStations = NormalizeStations(stations.Values)
            .Values
            .OrderBy(station => station.Number)
            .ToList();

        string json = JsonSerializer.Serialize(normalizedStations, JsonSerializerOptions);
        File.WriteAllText(_stationsFilePath, json);
    }

    private IReadOnlyDictionary<int, Station> RestoreDefaults()
    {
        IReadOnlyDictionary<int, Station> defaultStations = DefaultStationProvider.GetDefaultStations();
        SaveStations(defaultStations);

        return defaultStations;
    }

    private static Dictionary<int, Station> NormalizeStations(IEnumerable<Station>? stations)
    {
        Dictionary<int, Station> validStations = new();

        if (stations is null)
            return validStations;

        foreach (Station station in stations)
        {
            if (!IsValidStation(station))
                continue;

            validStations[station.Number] = station;
        }

        return validStations
            .OrderBy(pair => pair.Key)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    private static bool IsValidStation(Station station)
    {
        if (station.Number is < MinimumStationNumber or > MaximumStationNumber)
            return false;

        if (string.IsNullOrWhiteSpace(station.Name))
            return false;

        if (string.IsNullOrWhiteSpace(station.Url))
            return false;

        if (!Uri.TryCreate(station.Url, UriKind.Absolute, out Uri? uri))
            return false;

        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    }

    private void EnsureStorageDirectoryExists()
    {
        string? directoryPath = Path.GetDirectoryName(_stationsFilePath);

        if (!string.IsNullOrWhiteSpace(directoryPath))
            Directory.CreateDirectory(directoryPath);
    }

    private static string GetDefaultStationsFilePath()
    {
        string localApplicationDataPath = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);

        return Path.Combine(
            localApplicationDataPath,
            ApplicationFolderName,
            StationsFileName);
    }
}