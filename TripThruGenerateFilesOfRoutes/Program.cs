using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using MongoDB.Driver;
using Newtonsoft.Json;
using TripThruCore;
using TripThruCore.Storage;
using Utils;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;


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
            const double maxStepLntLng = 0.0005;
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

        #region "GenerateRoutes"
        //static void Main()
        //{

        //    var locationsLists = GetLocationsLists();

        //    using (var sr = new StreamReader("App_Data\\Geo-Routes.txt"))
        //    {
        //        var lines = sr.ReadToEnd();
        //        MapTools.routes = JsonConvert.DeserializeObject<Dictionary<string, Route>>(lines) ??
        //                          new Dictionary<string, Route>();
        //    }
        //    using (var sr = new StreamReader("App_Data\\Geo-Location-Names.txt"))
        //    {
        //        var lines = sr.ReadToEnd();
        //        MapTools.locationNames = JsonConvert.DeserializeObject<Dictionary<string, string>>(lines) ??
        //                                 new Dictionary<string, string>();
        //    }
        //    using (var sr = new StreamReader("App_Data\\Geo-Location-Addresses.txt"))
        //    {
        //        var lines = sr.ReadToEnd();
        //        MapTools.locationAddresses = JsonConvert.DeserializeObject<Dictionary<string, Pair<string, string>>>(lines) ??
        //                                     new Dictionary<string, Pair<string, string>>();
        //    }

        //    int tripCount = 0;
        //    foreach (LocationsList locationList in locationsLists)
        //        foreach (Location location in locationList.locations)
        //            foreach (Location l in locationList.locations.Where(l => !l.Equals(location)))
        //            {
        //                tripCount++;
        //                System.Threading.Thread.Sleep(2000);
        //                MapTools.GetRoute(location, l);
        //            }

        //    var routesString = JsonConvert.SerializeObject(MapTools.routes);
        //    var locationNamesString = JsonConvert.SerializeObject(MapTools.locationNames);
        //    var locationAddresses = JsonConvert.SerializeObject(MapTools.locationAddresses);

        //    File.WriteAllText("App_Data\\Geo-Routes.txt", String.Empty);
        //    using (var sr = new StreamWriter("App_Data\\Geo-Routes.txt"))
        //    {
        //        sr.Write(routesString);
        //    }
        //    File.WriteAllText("App_Data\\Geo-Location-Names.txt", String.Empty);
        //    using (var sr = new StreamWriter("App_Data\\Geo-Location-Names.txt"))
        //    {
        //        sr.Write(locationNamesString);
        //    }
        //    File.WriteAllText("App_Data\\Geo-Location-Addresses.txt", String.Empty);
        //    using (var sr = new StreamWriter("App_Data\\Geo-Location-Addresses.txt"))
        //    {
        //        sr.Write(locationAddresses);
        //    }


        //    Console.WriteLine(tripCount + " trips generated");
        //    Console.ReadKey();
        //}
        #endregion

        #region "MongoDB"
        //static void Main()
        //{
        //    const string host = "mongodb://SG-tripthru-3110.servers.mongodirector.com:27017/";
        //    const string database = "TripThru";
        //    const string nameCollection = "trips";
        //    const string pathTripData = @"C:\Users\OscarErnesto\Downloads\tripData2013\trip_data_1.csv\trip_data_1.csv";
        //    const string pathTripFare = @"C:\Users\OscarErnesto\Downloads\faredata2013\trip_fare_1.csv\trip_fare_1.csv";
        //    const string pathRandomsNames =
        //        @"C:\Users\OscarErnesto\Documents\Visual Studio 2013\Projects\Gateway\Db\FakeName.csv";

        //    MongoDB.Bson.Serialization.BsonClassMap.RegisterClassMap<Trip>(cm =>
        //    {
        //        cm.AutoMap();
        //        foreach (var mm in cm.AllMemberMaps)
        //            mm.SetIgnoreIfNull(true);
        //        cm.GetMemberMap(c => c.Status).SetRepresentation(BsonType.String);
        //        cm.GetMemberMap(c => c.PickupTime).SetIgnoreIfNull(true);
        //        cm.GetMemberMap(c => c.PickupLocation).SetIgnoreIfNull(true);
        //        cm.GetMemberMap(c => c.FleetId).SetIgnoreIfNull(true);
        //        cm.GetMemberMap(c => c.FleetName).SetIgnoreIfNull(true);
        //        cm.GetMemberMap(c => c.ETA).SetIgnoreIfNull(true);
        //        cm.GetMemberMap(c => c.DropoffLocation).SetIgnoreIfNull(true);
        //        cm.GetMemberMap(c => c.DropoffTime).SetIgnoreIfNull(true);
        //        cm.GetMemberMap(c => c.DriverRouteDuration).SetIgnoreIfNull(true);
        //        cm.GetMemberMap(c => c.DriverId).SetIgnoreIfNull(true);
        //        cm.GetMemberMap(c => c.OccupiedDistance).SetIgnoreIfNull(true);
        //        cm.GetMemberMap(c => c.DriverName).SetIgnoreIfNull(true);
        //        cm.GetMemberMap(c => c.Price).SetIgnoreIfNull(true);
        //        cm.GetMemberMap(c => c.ServicingPartnerName).SetIgnoreIfNull(true);
        //        cm.GetMemberMap(c => c.ServicingPartnerId).SetIgnoreIfNull(true);
        //        cm.GetMemberMap(c => c.VehicleType).SetIgnoreIfNull(true);
        //        cm.GetMemberMap(c => c.DriverLocation).SetIgnoreIfNull(true);
        //        cm.GetMemberMap(c => c.DriverInitiaLocation).SetIgnoreIfNull(true);
        //        cm.GetMemberMap(c => c.LastUpdate).SetIgnoreIfNull(true);
        //        cm.GetMemberMap(c => c.loc);
        //    });

        //    MongoCollection<Trip> trips;
        //    var server = MongoServer.Create(host);
        //    var tripsDatabase = server.GetDatabase(database);
        //    trips = tripsDatabase.GetCollection<Trip>(nameCollection);
        //    var tripReader = new StreamReader(File.OpenRead(pathTripData));
        //    var fareReader = new StreamReader(File.OpenRead(pathTripFare));
        //    var namesReader = new StreamReader(File.OpenRead(pathRandomsNames));

        //    var r = new Random(DateTime.Now.Millisecond);

        //    var listNames = new Dictionary<string, string>();
        //    var listPassengerNames = new Dictionary<string, string>();

        //    var countLines = 1;

        //    namesReader.ReadLine();
        //    tripReader.ReadLine();
        //    fareReader.ReadLine();
        //    int re = 0;
        //    while (re < 1000)
        //    {
        //        tripReader.ReadLine();
        //        fareReader.ReadLine();
        //        re++;
        //    }

        //    while (countLines <= 5000)
        //    {
        //        var trip = new Trip();
        //        var tripLine = tripReader.ReadLine();
        //        var fareLine = fareReader.ReadLine();
        //        if (tripLine == null) continue;
        //        var tripValues = tripLine.Split(',');
        //        if (fareLine == null) continue;
        //        var fareValues = fareLine.Split(',');

        //        if (!listNames.ContainsKey(tripValues[0]))
        //        {
        //            var identity = namesReader.ReadLine();
        //            if (identity != null)
        //            {
        //                var name = identity.Split('\t');
        //                var completeName = name[3] + " " + name[4] + " " + name[5];
        //                listNames.Add(tripValues[0], completeName);
        //            }
        //        }
        //        if (!listPassengerNames.ContainsKey(tripValues[1]))
        //        {
        //            var identity = namesReader.ReadLine();
        //            if (identity != null)
        //            {
        //                var name = identity.Split('\t');
        //                var completeName = name[3] + " " + name[4] + " " + name[5];
        //                listPassengerNames.Add(tripValues[1], completeName);
        //            }
        //        }


        //        try
        //        {
        //            trip.Id = countLines + "d@nytaxi@tripthru.com";
        //            trip.DriverName = listNames[tripValues[0]];
        //            var lat = Convert.ToDouble(tripValues[13]);
        //            var lng = Convert.ToDouble(tripValues[12]);
        //            if (Math.Abs(lat) > 0 && Math.Abs(lng) > 0 && Math.Abs(lng) < 180 && Math.Abs(lat) < 180)
        //                trip.DropoffLocation = new Location(lat, lng);
        //            else
        //            {
        //                Console.WriteLine("Incomplete Trip");
        //                continue;
        //            }
        //            trip.DriverInitiaLocation = new Location(40.769004, -73.981376);
        //            trip.DropoffTime = Convert.ToDateTime(tripValues[6]).AddYears(1).AddMonths(5);
        //            trip.DropoffTime = trip.DropoffTime.Value.AddDays(-1 * trip.DropoffTime.Value.Day);
        //            trip.DropoffTime = trip.DropoffTime.Value.AddDays(r.Next(1, 31)); 
        //            trip.EnrouteDistance = Convert.ToDouble(tripValues[9]);
        //            trip.FleetId = "30";
        //            trip.FleetName = "NY Taxi";
        //            trip.OriginatingPartnerId = "nytaxi@tripthru.com";
        //            trip.OriginatingPartnerName = "NY Taxi";
        //            trip.PassengerName = listPassengerNames[tripValues[1]];
        //            lat = Convert.ToDouble(tripValues[11]);
        //            lng = Convert.ToDouble(tripValues[10]);
        //            if (Math.Abs(lat) > 0 && Math.Abs(lng) > 0 && Math.Abs(lng) < 180 && Math.Abs(lat) < 180)
        //                trip.PickupLocation = new Location(lat, lng);
        //            else
        //            {
        //                Console.WriteLine("Incomplete Trip");
        //                continue;
        //            }
        //            trip.PickupTime = Convert.ToDateTime(tripValues[5]).AddYears(1).AddMonths(5);
        //            trip.PickupTime = trip.PickupTime.Value.AddDays(-1 * trip.PickupTime.Value.Day);
        //            trip.PickupTime = trip.PickupTime.Value.AddDays(trip.DropoffTime.Value.Day);
        //            trip.LastUpdate = trip.DropoffTime;
        //            trip.Price = Convert.ToDouble(fareValues[10]);
        //            trip.ServicingPartnerId = "nytaxi@tripthru.com";
        //            trip.ServicingPartnerName = "NY Taxi";
        //            trip.Status = Status.Complete;

        //            trips.Insert(trip);
        //            Console.WriteLine(trip.Id);
        //            countLines++;
        //        }
        //        catch (Exception)
        //        {
        //            Console.WriteLine("ERROR Incomplete Trip");
        //        }
        //    }
        //    foreach (var variable in listNames)
        //    {
        //        Console.WriteLine(variable.Value);
        //    }
        //    Console.WriteLine(listNames.Count);
        //    Console.WriteLine(listPassengerNames.Count);
        //    Console.ReadLine();
        //}

#endregion

        static void Main()
        {
            

            var locationList = new List<Tuple<string, Location>>
            {
                //new Tuple<string, Location>("Hubai",new Location(25.271139, 55.307485)), //hubai
                //new Tuple<string, Location>("LosTaxiBlus",new Location(48.837246, 2.347844)), //losTaxiBlus
                //new Tuple<string, Location>("Mellow",new Location(37.78906, -122.402127)), //mellow
                //new Tuple<string, Location>("Miami",new Location(25.7871768, -80.1294254)), //miami
                //new Tuple<string, Location>("Netro",new Location(42.356217, -71.137512)), //netro
                //new Tuple<string, Location>("NYtaxi",new Location(40.769004, -73.981376)), //nytaxi
                //new Tuple<string, Location>("TampaCab",new Location(26.1275183, -80.103452)), //tampaCab
                new Tuple<string, Location>("Tuxor",new Location(37.78906, -122.402127)) //tuxor
            };

            

            var count = 0;

            foreach (var location in locationList)
            {
                var csv = new StringBuilder();
                StorageManager.OpenStorage(new MongoDbStorage("mongodb://192.168.0.106:27017/", "TripThruRoute" + location.Item1));
                while (count <= 50)
                {
                    Thread.Sleep(1000);
                    var driverLocation = new Location(location.Item2.Lat, location.Item2.Lng);
                    var pickUpLocation = GetLocation(location.Item2.Lat, location.Item2.Lng, 2000);
                    var dropOffLocation = GetLocation(pickUpLocation.Lat, pickUpLocation.Lng, 2000);
                    Route pickUpRoute;
                    Route dropOffRoute;

                    try
                    {
                        pickUpRoute = GetRoute(driverLocation, pickUpLocation, false,true);
                        dropOffRoute = GetRoute(pickUpRoute.end, dropOffLocation, false, true);
                        if (pickUpRoute.waypoints.Length == 2 || dropOffRoute.waypoints.Length == 2)
                            continue;
                    }
                    catch
                    {
                        continue;
                    }

                    var arrayString = new string[4];

                    arrayString[0] = pickUpRoute.end.Lat.ToString();
                    arrayString[1] = pickUpRoute.end.Lng.ToString();
                    arrayString[2] = dropOffRoute.end.Lat.ToString();
                    arrayString[3] = dropOffRoute.end.Lng.ToString();

                    csv.Append(string.Join(",", arrayString));
                    csv.Append(Environment.NewLine);

                    

                    //StorageManager.SaveRoute(pickUpRoute);
                    //StorageManager.SaveRoute(dropOffRoute);
                    count++;
                    Console.WriteLine(location.Item1 + " : " + count);
                }
                File.AppendAllText(@"C:\Users\OscarErnesto\Documents\routes" + location.Item1 + ".csv", csv.ToString());
            }
        }

        public static Location GetLocation(double x0, double y0, int radius)
        {
            var random = new Random();

            // Convert radius from meters to degrees
            double radiusInDegrees = radius / 111000f;

            var u = random.NextDouble();
            var v = random.NextDouble();
            var w = radiusInDegrees * Math.Sqrt(u);
            var t = 2 * Math.PI * v;
            var x = w * Math.Cos(t);
            var y = w * Math.Sin(t);

            // Adjust the x-coordinate for the shrinking of the east-west distances
            var newX = x / Math.Cos(y0);

            var foundLongitude = newX + x0;
            var foundLatitude = y + y0;
            return new Location(Math.Round(foundLongitude, 6), Math.Round(foundLatitude, 6));
        }

        public static Route GetRoute(Location from, Location to, bool removeFirst = false ,  bool removeFinal = false)
        {
            var key = Route.GetKey(from, to);
            var route = StorageManager.GetRoute(key);
            if (route != null)
                return route;
            const double metersToMiles = 0.000621371192;
            const int maxDuration = 10;
            var doc = new XmlDocument();
            var elapse = new TimeSpan(0, 0, 0);
            double totalDistance = 0;
            var url = "http://maps.googleapis.com/maps/api/directions/xml?origin=" + from.Lat + ", " + from.Lng + "&destination=" + to.Lat + ", " + to.Lng + "&sensor=false&units=imperial";
            doc.Load(url);
            var status = doc.SelectSingleNode("//DirectionsResponse/status");

            if (status == null || status.InnerText == "ZERO_RESULTS" || status.InnerText == "OVER_QUERY_LIMIT")
            {
                Logger.LogDebug("Google request error", status != null ? status.InnerText : "status is null");
                throw new Exception("Bad route request");
            }

            Console.WriteLine(status.InnerText);

            List<Waypoint> waypoints;

            if (removeFirst)
                waypoints = new List<Waypoint>();
            else
                waypoints = new List<Waypoint> {new Waypoint(@from, new TimeSpan(0), 0)};
            var legs = doc.SelectNodes("//DirectionsResponse/route/leg");
            var flag = true;
            var count = 0;
            foreach (XmlNode leg in legs)
            {
                var stepNodes = leg.SelectNodes("step");
                foreach (XmlNode stepNode in stepNodes)
                {
                    count++;

                    var duration = int.Parse(stepNode.SelectSingleNode("duration/value").InnerText);
                    var distance = double.Parse(stepNode.SelectSingleNode("distance/value").InnerText) * metersToMiles;
                    var duration2 = new TimeSpan(0, 0, int.Parse(stepNode.SelectSingleNode("duration/value").InnerText));
                    var end = new Location(double.Parse(stepNode.SelectSingleNode("end_location/lat").InnerText), double.Parse(stepNode.SelectSingleNode("end_location/lng").InnerText));
                    var totalDistanceTemp = totalDistance;
                    totalDistance += distance;
                    var timeSpanTemp = elapse;
                    elapse += duration2;

                    if (duration > maxDuration)
                    {
                        var polyline = stepNode.SelectSingleNode("polyline/points").InnerText;
                        var locations = DecodePolylinePoints(polyline);
                        waypoints.AddRange(IncreaseLocationsEnumerable(locations, duration, timeSpanTemp.TotalSeconds, distance, totalDistanceTemp));
                    }
                    else
                    {
                        waypoints.Add(new Waypoint(end, elapse, totalDistance));
                    }
                }
            }

            if(!removeFinal)
                waypoints.Add(new Waypoint(to, elapse, totalDistance));
            route = new Route(waypoints.ToArray());
            StorageManager.SaveRoute(route);
            return route;
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
