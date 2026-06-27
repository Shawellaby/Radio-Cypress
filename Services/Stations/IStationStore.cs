using Shawellaby.RadioCypress.Models;

namespace Shawellaby.RadioCypress.Services.Stations;

public interface IStationStore
{
    IReadOnlyDictionary<int, Station> LoadOrCreateDefaultStations();

    void SaveStations(IReadOnlyDictionary<int, Station> stations);
}