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
                for (var i = 1; i < repeatTimes; i++)
                {
                    latCount -= stepLat;
                    lngCount -= stepLng;
                    locations.Add(new Location(latCount, lngCount));
                }
            }
            locations.Add(new Location(to.Lat, to.Lng));
            return locations;
        }

        private static IEnumerable<Waypoint> IncreaseLocationsEnumerable(IEnumerable<Location> locations, double duration, double totalDuration, double distance, double totalDistance)
        {
            const double maxStepLntLng = 0.0001;
            var wayPoints = new List<Waypoint>();

            var countDuration = totalDuration;
            var countDistance = totalDistance;

            double stepDuration = duration / locations.Count();
            double stepDistance = distance / locations.Count();

            Location tempLocation = null;

            foreach (var location in locations)
            {
                if (tempLocation == null)
                {
                    tempLocation = location;
                    countDuration += stepDuration;
                    countDistance += stepDistance;
                    continue;
                }
                var tempLocations = IncreaseGranularityDistance(tempLocation, location, maxStepLntLng);
                double stepDurationTemp = stepDuration / tempLocations.Count();
                double stepDistanceTemp = stepDistance / tempLocations.Count();
                var countDurationTemp = countDuration;
                var countDistanceTemp = countDistance;

                foreach (var tempLocation1 in tempLocations)
                {
                    countDurationTemp += stepDurationTemp;
                    countDistanceTemp += stepDistanceTemp;
                    wayPoints.Add(new Waypoint(tempLocation1, new TimeSpan(0, 0, (int)countDurationTemp), countDistanceTemp));
                }
                countDuration += stepDuration;
                countDistance += stepDistance;
                tempLocation = location;
            }

            return wayPoints;
        }

        static void Main()
        {

            var locationsLists = GetLocationsLists();

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

            int tripCount = 0;
            foreach (LocationsList locationList in locationsLists)
                foreach (Location location in locationList.locations)
                    foreach (Location l in locationList.locations.Where(l => !l.Equals(location)))
                    {
                        tripCount++;
                        System.Threading.Thread.Sleep(2000);
                        MapTools.GetRoute(location, l);
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


            Console.WriteLine(tripCount + " trips generated");
            Console.ReadKey();
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

        private static List<LocationsList> GetLocationsLists()
        {
            var locationsListsFiles = Directory.GetFiles("Locations/", "*.txt");
            return locationsListsFiles.Select(tripsListFile => JsonConvert.DeserializeObject<LocationsList>(File.ReadAllText(tripsListFile))).ToList();
        }
    }

    class LocationsList
    {
        public List<Location> locations { get; set; }
    }
}
