using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
//using System.Web.DynamicData;
using System.Web.Script.Serialization;
using System.Xml;
using System.IO;
//using ServiceStack.Text;
//using ServiceStack.Common.Utils;
//using ServiceStack.Common.Utils;
//using RestSharp;
using TripThruCore;
using Newtonsoft.Json;

namespace Utils
{

    public class MapTools
    {
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
        public static Dictionary<string, Pair<string, string>> locationAddresses = new Dictionary<string, Pair<string, string>>();
        public static Dictionary<string, string> locationNames = new Dictionary<string, string>();
        public static Dictionary<string, Route> routes = new Dictionary<string, Route>();

        public static void ClearCache()
        {
            locationAddresses.Clear();
            locationNames.Clear();
            routes.Clear();
        }

        public static void SpeedUpRoutesForTesting(double scale)
        {
            foreach (Route route in routes.Values)
            {
                foreach (Waypoint waypoint in route.waypoints)
                {
                    waypoint.distance *= scale;
                    waypoint.elapse = new TimeSpan((long)(waypoint.elapse.Ticks * scale));
                }
            }
        }

        public static void CsvLoadTripRoutes(string filename, bool lngFirst)
        {
            // load trip routes
            Dictionary<string, LinkedList<Waypoint>> routes = new Dictionary<string, LinkedList<Waypoint>>();
            using (CsvFileReader reader = new CsvFileReader(filename))
            {
                CsvRow row = new CsvRow();
                while (reader.ReadRow(row, ','))
                {
                    string routeID = row[0];
                    double distance = 0;
                    double lat = Convert.ToDouble(lngFirst ? row[2] : row[1]);
                    double lng = Convert.ToDouble(lngFirst ? row[1] : row[2]);
                    if (routes.ContainsKey(routeID))
                        distance = routes[routeID].First.Value.GetDistance(new Location(lat, lng, "null"));
                    Waypoint waypoint = new Waypoint(lat, lng, TimeSpan.Parse(row[3]), distance, row[4].Replace("\"", ""));

                    // Scenario #1
                    if (!routes.ContainsKey(routeID))
                        routes[routeID] = new LinkedList<Waypoint>();
                    routes[routeID].AddLast(waypoint);

                }
            }
            foreach (LinkedList<Waypoint> w in routes.Values)
            {
                Route r = new Route(w.ToArray());
                string key = Route.GetKey(r.start, r.end);
                MapTools.routes.Add(key, r);
            }
        }

        static string locationNames_Filename;
        static string routes_Filename;
        static string locationAddresses_Filename;
        public static double distance_and_time_scale = 1;

        public static void SetGeodataFilenames(string locationNames, string routes, string locationAddresses)
        {
            locationNames_Filename = locationNames;
            routes_Filename = routes;
            locationAddresses_Filename = locationAddresses;
        }

        public static void LoadGeoData()
        {
            LoadGeoLocationAddress();
            LoadGeoLocationNames();
            LoadGeoRoutes();
        }

        private static void LoadGeoRoutes()
        {
            using (var sr = new StreamReader(routes_Filename))
            {
                var lines = sr.ReadToEnd();
                routes = JsonConvert.DeserializeObject<Dictionary<string, Route>>(lines) ??
                         new Dictionary<string, Route>();
            }
        }

        private static void LoadGeoLocationNames()
        {
            using (var sr = new StreamReader(locationNames_Filename))
            {
                var lines = sr.ReadToEnd();
                locationNames = JsonConvert.DeserializeObject<Dictionary<string, string>>(lines) ??
                                new Dictionary<string, string>();
            }
        }

        private static void LoadGeoLocationAddress()
        {
            using (var sr = new StreamReader(locationAddresses_Filename))
            {
                var lines = sr.ReadToEnd();
                locationAddresses = JsonConvert.DeserializeObject<Dictionary<string, Pair<string, string>>>(lines) ??
                                    new Dictionary<string, Pair<string, string>>();
            }
        }

        public static void WriteGeoData()
        {
            WriteGeoRoutes();
            WriteGeoLocationNames();
            WriteGeoLocationAddresses();
        }

        private static void WriteGeoLocationAddresses()
        {
            if (locationAddresses_Filename == null) return;
            File.WriteAllText(locationAddresses_Filename, String.Empty);
            using (var sr = new StreamWriter(locationAddresses_Filename))
            {
                var serializer = new JavaScriptSerializer();
                var locationAddressesJson = JsonConvert.SerializeObject(locationAddresses);
                sr.Write(locationAddressesJson);
            }
        }

        private static void WriteGeoLocationNames()
        {
            if (locationNames_Filename == null) return;
            File.WriteAllText(locationNames_Filename, String.Empty);
            using (var sr = new StreamWriter(locationNames_Filename))
            {
                var locationNamesJson = JsonConvert.SerializeObject(locationNames);
                sr.Write(locationNamesJson);
            }
        }

        private static void WriteGeoRoutes()
        {
            if (routes_Filename == null) return;
            File.WriteAllText(routes_Filename, String.Empty);
            using (var sr = new StreamWriter(routes_Filename))
            {
                var routesJson = JsonConvert.SerializeObject(routes);
                sr.Write(routesJson);
            }
        }


        // http://code.google.com/apis/maps/documentation/geocoding/#ReverseGeocoding
        public static string GetReverseGeoLoc(Location location)
        {
            lock (locationNames)
            {
                string key = location.getID();
                if (locationNames.ContainsKey(key))
                    return locationNames[key];
                return GetReverseGeoLocationNameFromMapService(location);
            }
        }

        private static string GetReverseGeoLocationNameFromMapService(Location location)
        {
            //return "Google -- Over query limit";

            XmlDocument doc = new XmlDocument();
            {
                doc.Load("http://maps.googleapis.com/maps/api/geocode/xml?latlng=" + location.Lat + "," + location.Lng + "&sensor=false");
                XmlNode element = doc.SelectSingleNode("//GeocodeResponse/status");
                /*if (element.InnerText == "OVER_QUERY_LIMIT")
                {

                    System.Threading.Thread.Sleep(new TimeSpan(0, 1, 10));
                    doc.Load("http://maps.googleapis.com/maps/api/geocode/xml?latlng=" + location.Lat + "," + location.Lng + "&sensor=false");
                    element = doc.SelectSingleNode("//GeocodeResponse/status");

                }*/
                if (element.InnerText == "ZERO_RESULTS" || element.InnerText == "OVER_QUERY_LIMIT")
                    return "Google -- Over query limit";
                else
                {

                    element = doc.SelectSingleNode("//GeocodeResponse/result/formatted_address");
                    locationNames.Add(location.getID(), element.InnerText);
                    WriteGeoLocationNames();
                    return element.InnerText;
                }
            }
        }

        public static Pair<string, string> GetReverseGeoLocAddress(Location location)
        {
            lock (locationAddresses)
            {
                string key = location.getID();
                if (locationAddresses.ContainsKey(key))
                    return locationAddresses[key];
                return GetReverseGeoAddressFromMapService(location);
            }
        }

        private static Pair<string, string> GetReverseGeoAddressFromMapService(Location location)
        {
            Pair<string, string> address = new Pair<string, string>();
            XmlDocument doc = new XmlDocument();
            doc.Load("http://maps.googleapis.com/maps/api/geocode/xml?latlng=" + location.Lat + "," + location.Lng + "&sensor=false");
            XmlNode element = doc.SelectSingleNode("//GeocodeResponse/status");
            /*if (element.InnerText == "OVER_QUERY_LIMIT")
            {
                System.Threading.Thread.Sleep(new TimeSpan(0, 1, 10));
                doc.Load("http://maps.googleapis.com/maps/api/geocode/xml?latlng=" + location.Lat + "," + location.Lng + "&sensor=false");
                element = doc.SelectSingleNode("//GeocodeResponse/status");

            }*/
            if (!(element.InnerText == "ZERO_RESULTS" || element.InnerText == "OVER_QUERY_LIMIT"))
            {
                var streetNumberNode =
                    doc.SelectSingleNode(
                        "//GeocodeResponse/result/address_component[type=\"street_number\"]/short_name");
                string street_number = streetNumberNode != null ? streetNumberNode.InnerText : "";

                var routeNode =
                    doc.SelectSingleNode("//GeocodeResponse/result/address_component[type=\"route\"]/short_name");
                string route = routeNode != null ? routeNode.InnerText : "";

                var postalCodeNode =
                    doc.SelectSingleNode(
                        "//GeocodeResponse/result/address_component[type=\"postal_code\"]/short_name");
                string postal_code = postalCodeNode != null ? postalCodeNode.InnerText : "";

                address = new Pair<string, string>(street_number + " " + route, postal_code);
                locationAddresses.Add(location.getID(), address);
                WriteGeoLocationAddresses();
                return address;

            }
            else
            {
                return new Pair<string, string>("reached query limit", "reached query limit");
            }
        }

        public static Route GetRoute(Location from, Location to)
        {
            lock (routes)
            {
                var key = Route.GetKey(from, to);
                if (routes.ContainsKey(key))
                    return routes[key];
                const double metersToMiles = 0.000621371192;
                const int maxDuration = 10;
                var doc = new XmlDocument();
                var elapse = new TimeSpan(0, 0, 0);
                double totalDistance = 0;
                var url = "http://maps.googleapis.com/maps/api/directions/xml?origin=" + from.Lat + ", " + from.Lng + "&destination=" + to.Lat + ", " + to.Lng + "&sensor=false&units=imperial";
                doc.Load(url);
                var status = doc.SelectSingleNode("//DirectionsResponse/status");

                if (status == null || status.InnerText == "ZERO_RESULTS")
                    return null;
                var waypoints = new List<Waypoint> {new Waypoint(@from, new TimeSpan(0), 0)};
                var legs = doc.SelectNodes("//DirectionsResponse/route/leg");

                foreach (XmlNode leg in legs)
                {
                    var stepNodes = leg.SelectNodes("step");
                    foreach (XmlNode stepNode in stepNodes)
                    {

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

                            waypoints.AddRange(IncreaseGranularityInPolylineList(locations, duration, maxDuration, timeSpanTemp.TotalSeconds, distance, totalDistanceTemp));

                        }
                        else
                        {
                            waypoints.Add(new Waypoint(end, elapse, totalDistance));
                        }
                    }
                }

                waypoints.Add(new Waypoint(to, elapse, totalDistance));
                var route = new Route(waypoints.ToArray());
                routes.Add(key, route);
                return route;
            }
        }

        private static IEnumerable<Waypoint> IncreaseLocationsEnumerable(IEnumerable<Location> locations, int duration, int maxDuration, double totalDuration, double distance, double totalDistance)
        {
            var maxStepLntLng = 0.0005;
            var enumerable = locations as Location[] ?? locations.ToArray();
            var count = enumerable.Count();
            int durationStep = duration/count;
            double totalDurationTemp = totalDuration;
            foreach (var location in enumerable)
            {
                
            }

            return null;
        }

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
                var repeatTimes = (int)(result/maxLatLng); //Obtenemos las veces que se repetira el proceso.
                var stepLat = lat / repeatTimes;
                var stepLng = lng / repeatTimes;
                for (var i = 0; i < repeatTimes; i++)
                {
                    latCount += stepLat;
                    lngCount += stepLng;
                    locations.Add(new Location(latCount,lngCount));
                }
            }
            locations.Add(new Location(to.Lat,to.Lng));
            return locations;
        }

        private static IEnumerable<Waypoint> IncreaseGranularityInPolylineList(IEnumerable<Location> locations, int duration, int maxDuration, double totalDuration, double distance, double totalDistance)
        {
            const double metersToMiles = 0.000621371192;
            var waypoints = new List<Waypoint>();
            var doc = new XmlDocument();
            Location prevLocation = null;
            var elapse = new TimeSpan(0, 0, (int)totalDuration);
            foreach (var location in locations)
            {
                if (prevLocation == null)
                {
                    prevLocation = location;
                    continue;
                }
                XmlNode status;
                var trying = 0;
                do
                {
                    if (trying > 0)
                        Thread.Sleep(1000);
                    var url = "http://maps.googleapis.com/maps/api/directions/xml?origin=" + prevLocation.Lat + ", " +
                                 prevLocation.Lng + "&destination=" + location.Lat + ", " + location.Lng +
                                 "&sensor=false&units=imperial";
                    doc.Load(url);
                    status = doc.SelectSingleNode("//DirectionsResponse/status");
                    if (status == null || status.InnerText == "ZERO_RESULTS" || trying == 5)
                        break;
                    trying++;
                } while (status.InnerText == "OVER_QUERY_LIMIT");
                var legs = doc.SelectNodes("//DirectionsResponse/route/leg");
                foreach (XmlNode leg in legs)
                {
                    var stepNodes = leg.SelectNodes("step");
                    foreach (XmlNode stepNode in stepNodes)
                    {
                        var durationInt = int.Parse(stepNode.SelectSingleNode("duration/value").InnerText);
                        var distance2 = double.Parse(stepNode.SelectSingleNode("distance/value").InnerText) * metersToMiles;
                        var duration2 = new TimeSpan(0, 0, int.Parse(stepNode.SelectSingleNode("duration/value").InnerText));
                        var end = new Location(double.Parse(stepNode.SelectSingleNode("end_location/lat").InnerText), double.Parse(stepNode.SelectSingleNode("end_location/lng").InnerText));

                        if (durationInt > maxDuration)
                        {
                            var start = new Location(double.Parse(stepNode.SelectSingleNode("start_location/lat").InnerText), double.Parse(stepNode.SelectSingleNode("start_location/lng").InnerText));
                            waypoints.AddRange(IncreaseGranularity(durationInt, maxDuration, elapse.TotalSeconds, distance2, totalDistance, start, end));
                            totalDistance += distance2;
                            elapse += duration2;
                        }
                        else
                        {
                            elapse += duration2;
                            totalDistance += distance;
                            waypoints.Add(new Waypoint(end, elapse, totalDistance + distance2));
                        }

                    }
                }
                prevLocation = location;
            }
            return waypoints;
        }

        private static IEnumerable<Waypoint> IncreaseGranularity(int duration, int maxDuration, double totalDuration, double distance, double totalDistance, Location from, Location to)
        {
            List<Waypoint> wayPoints = new List<Waypoint>();

            double granularity = duration / maxDuration;
            double stepDistance = (distance / granularity) + totalDistance;
            double stepDistaceCount = 0;
            double durationCount = totalDuration;
            double stepLat = from.Lat - to.Lat;
            double stepLng = from.Lng - to.Lng;
            double subStepLat = stepLat / granularity;
            double subStepLng = stepLng / granularity;

            for (int i = 0; i <= (granularity - 1); i++)
            {
                from.Lat -= subStepLat;
                from.Lng -= subStepLng;
                Location location = new Location(from.Lat, from.Lng);

                durationCount += maxDuration;
                stepDistaceCount += stepDistance;

                TimeSpan timeSpan = new TimeSpan(0, 0, (int)durationCount);
                wayPoints.Add(new Waypoint(location, timeSpan, stepDistaceCount));
            }
            wayPoints.Add(new Waypoint(from, new TimeSpan(0, 0, ((int)totalDuration + duration)), stepDistaceCount));

            return wayPoints;
        }

    }
}

