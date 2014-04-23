using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TripThruCore;
using Utils;

namespace TripThruGenerateFilesOfRoutes
{
    class Program
    {
        static void Main()
        {
            Location fromLocation = new Location(-22.910194, -43.212211);
            Location toLocation = new Location(-22.9105337, -43.2123576);

            double Lat = fromLocation.Lat - toLocation.Lat;
            double Lng = fromLocation.Lng - toLocation.Lng;

            var result = Math.Sqrt(Math.Pow(Lat, 2) + Math.Pow(Lng, 2));
            int ocho = 9;
            /*

            var partnerConfigurations = GetPartnersConfigurations();

            using (var sr = new StreamReader("App_Data\\Geo-Routes.txt"))
            {
                var lines = sr.ReadToEnd();
                MapTools.routes = JsonConvert.DeserializeObject<Dictionary<string, Route>>(lines) ??
                                  new Dictionary<string, Route>();
            }
            using (var sr = new StreamReader("App_Data\\Geo-Location-Names.txt"))
            {
                var lines = sr.ReadToEnd();
                MapTools.locationNames = JsonConvert.DeserializeObject<Dictionary<string, string>>(lines) ??
                                         new Dictionary<string, string>();
            }
            using (var sr = new StreamReader("App_Data\\Geo-Location-Addresses.txt"))
            {
                var lines = sr.ReadToEnd();
                MapTools.locationAddresses = JsonConvert.DeserializeObject<Dictionary<string, Pair<string, string>>>(lines) ??
                                             new Dictionary<string, Pair<string, string>>();
            }

            foreach (var possibleTrip in partnerConfigurations.SelectMany(partnerConfiguration => partnerConfiguration.Fleets.ElementAt(0).PossibleTrips))
            {
                MapTools.GetRoute(possibleTrip.Start, possibleTrip.End);
            }

            var routesString = JsonConvert.SerializeObject(MapTools.routes);
            var locationNamesString = JsonConvert.SerializeObject(MapTools.locationNames);
            var locationAddresses = JsonConvert.SerializeObject(MapTools.locationAddresses);

            File.WriteAllText("App_Data\\Geo-Routes.txt", String.Empty);
            using (var sr = new StreamWriter("App_Data\\Geo-Routes.txt"))
            {
                sr.Write(routesString);
            }
            File.WriteAllText("App_Data\\Geo-Location-Names.txt", String.Empty);
            using (var sr = new StreamWriter("App_Data\\Geo-Location-Names.txt"))
            {
                sr.Write(locationNamesString);
            }
            File.WriteAllText("App_Data\\Geo-Location-Addresses.txt", String.Empty);
            using (var sr = new StreamWriter("App_Data\\Geo-Location-Addresses.txt"))
            {
                sr.Write(locationAddresses);
            }
             * */
        }

        private static IEnumerable<PartnerConfiguration> GetPartnersConfigurations()
        {
            var partnerConfigurationsFiles = Directory.GetFiles("PartnerConfigurations/", "*.txt");
            return partnerConfigurationsFiles.Select(partnerConfigurationFile => JsonConvert.DeserializeObject<PartnerConfiguration>(File.ReadAllText(partnerConfigurationFile))).ToList();
        }
    }
}
