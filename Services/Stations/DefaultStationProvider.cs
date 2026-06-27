using Shawellaby.RadioCypress.Models;

namespace Shawellaby.RadioCypress.Services.Stations;

public static class DefaultStationProvider
{
    public static IReadOnlyDictionary<int, Station> GetDefaultStations()
    {
        return new Dictionary<int, Station>
        {
            [1] = new Station(1, "Cypress Radio", "https://CypressRadio.org:8000/stream"),
            [2] = new Station(2, "Big 80's", "https://ssl.nexuscast.com:9044/;"),
            [3] = new Station(3, "Smooth 70's", "https://ice3.securenetsystems.net/S70S"),
            [4] = new Station(4, "Pure Country", "https://ice23.securenetsystems.net/QXFM"),
            [5] = new Station(5, "The Spot 98.7", "https://live.amperwave.net/manifest/audacy-kspffmaac-imc"),
            [6] = new Station(6, "The Cat 104.9", "https://live.amperwave.net/manifest/eagleradio-kbctfmaac-ibc4"),
            [7] = new Station(7, "retro 94.7", "https://ice41.securenetsystems.net/KCCT"),
            [8] = new Station(8, "Pickle 99.3", "http://dogglounge.com:8000"),
            [9] = new Station(9, "BIG 102.1", "https://ice42.securenetsystems.net/BIG")
        };
    }
}