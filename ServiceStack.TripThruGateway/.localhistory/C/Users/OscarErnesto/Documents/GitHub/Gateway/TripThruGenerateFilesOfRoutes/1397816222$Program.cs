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
        private static IEnumerable<Location> IncreaseGranularityDistance(Location from, Location to, double maxLatLng)
        {
            var locations = new List<Location>();
            var lat = from.Lat - to.Lat;
            var lng = from.Lng - to.Lng;
            var latCount = from.Lat;
            var lngCount = from.Lng;
            var result = Math.Sqrt(Math.Pow(lat, 2) + Math.Pow(lng, 2)); //Calculamos la hipotenusa.
            if (result > maxLatLng)//Preguntamos si es necesario Granular
            {
                var repeatTimes = (uint)(result / maxLatLng); //Obtenemos las veces que se repetira el proceso.
                var stepLat = lat / repeatTimes;
                var stepLng = lng / repeatTimes;
                if(repeatTimes > 1)
                    for (var i = 0; i < repeatTimes; i++)
                    {
                        latCount += maxLatLng;
                        lngCount += maxLatLng;
                        locations.Add(new Location(latCount, lngCount));
                    }
            }
            locations.Add(new Location(to.Lat, to.Lng));
            return locations;
        }

        static void Main()
        {
            Location fromLocation = new Location(37.79022, -122.41597);
            Location toLocation = new Location(37.79006, -122.41724);

            double Lat = fromLocation.Lat - toLocation.Lat;
            double Lng = fromLocation.Lng - toLocation.Lng;

            List<Location> locations = DecodePolylinePoints(@"e|seFfhdjVHpA^|FFx@VrDHvAf@fIh@xH?L@Lj@|H");

            //var locations = IncreaseGranularityDistance(fromLocation, toLocation, 0.0005);

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

        private static List<Location> DecodePolylinePoints(string encodedPoints)
        {
            if (encodedPoints == null || encodedPoints == "") return null;
            List<Location> poly = new List<Location>();
            char[] polylinechars = encodedPoints.ToCharArray();
            int index = 0;

            int currentLat = 0;
            int currentLng = 0;
            int next5bits;
            int sum;
            int shifter;

            try
            {
                while (index < polylinechars.Length)
                {
                    // calculate next latitude
                    sum = 0;
                    shifter = 0;
                    do
                    {
                        next5bits = (int)polylinechars[index++] - 63;
                        sum |= (next5bits & 31) << shifter;
                        shifter += 5;
                    } while (next5bits >= 32 && index < polylinechars.Length);

                    if (index >= polylinechars.Length)
                        break;

                    currentLat += (sum & 1) == 1 ? ~(sum >> 1) : (sum >> 1);

                    //calculate next longitude
                    sum = 0;
                    shifter = 0;
                    do
                    {
                        next5bits = (int)polylinechars[index++] - 63;
                        sum |= (next5bits & 31) << shifter;
                        shifter += 5;
                    } while (next5bits >= 32 && index < polylinechars.Length);

                    if (index >= polylinechars.Length && next5bits >= 32)
                        break;

                    currentLng += (sum & 1) == 1 ? ~(sum >> 1) : (sum >> 1);
                    Location p = new Location(Convert.ToDouble(currentLat) / 100000.0, Convert.ToDouble(currentLng) / 100000.0);
                    poly.Add(p);
                }
            }
            catch (Exception)
            {
                // log it
            }
            return poly;
        }

        private static IEnumerable<PartnerConfiguration> GetPartnersConfigurations()
        {
            var partnerConfigurationsFiles = Directory.GetFiles("PartnerConfigurations/", "*.txt");
            return partnerConfigurationsFiles.Select(partnerConfigurationFile => JsonConvert.DeserializeObject<PartnerConfiguration>(File.ReadAllText(partnerConfigurationFile))).ToList();
        }
    }
}
