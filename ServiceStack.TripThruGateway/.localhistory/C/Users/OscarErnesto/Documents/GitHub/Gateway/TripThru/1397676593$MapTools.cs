using System;
using System.Collections.Generic;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Web;
using System.Net;
//using System.Web.DynamicData;
using System.Web.Script.Serialization;
using System.Xml;
using System.Xml.XPath;
using System.IO;
using System.Runtime.Serialization;
using System.Xml.Serialization;
//using ServiceStack.Text;
//using ServiceStack.Common.Utils;
//using ServiceStack.Common.Utils;
//using RestSharp;
using TripThruCore;
using ServiceStack.Redis;
using System.Linq.Expressions;

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
            using (StreamReader sr = new StreamReader(routes_Filename))
            {
                var serializer = new JavaScriptSerializer();
                var lines = sr.ReadToEnd();
                routes = serializer.Deserialize<Dictionary<string, Route>>(lines);
            }
            /*
            Dictionary<string, LinkedList<Waypoint>> routes = new Dictionary<string, LinkedList<Waypoint>>();
            using (CsvFileReader reader = new CsvFileReader(routes_Filename))
            {
                CsvRow row = new CsvRow();
                while (reader.ReadRow(row, ','))
                {
                    string routeID = row[0];
                    Waypoint waypoint = new Waypoint(Convert.ToDouble(row[1]), Convert.ToDouble(row[2]), TimeSpan.Parse(row[3]), Convert.ToDouble(row[4]), row[5]);
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
            }*/
        }

        private static void LoadGeoLocationNames()
        {
            using (StreamReader sr = new StreamReader(locationNames_Filename))
            {
                var serializer = new JavaScriptSerializer();
                var lines = sr.ReadToEnd();
                locationNames = serializer.Deserialize<Dictionary<string, string>>(lines);
                if(locationNames == null)
                    locationNames = new Dictionary<string, string>();
            }
            /*using (CsvFileReader reader = new CsvFileReader(locationNames_Filename))
            {
                CsvRow row = new CsvRow();
                while (reader.ReadRow(row, ','))
                    locationNames.Add(row[0], row[1]);
            }*/
        }

        private static void LoadGeoLocationAddress()
        {
            
            using (CsvFileReader reader = new CsvFileReader(locationAddresses_Filename))
            {
                CsvRow row = new CsvRow();
                while (reader.ReadRow(row, ','))
                    locationAddresses.Add(row[0], new Pair<string, string>(row[1], row[2]));
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
            if (locationAddresses_Filename != null)
            {
                using (CsvFileWriter writer = new CsvFileWriter(locationAddresses_Filename))
                {
                    foreach (string key in MapTools.locationAddresses.Keys)
                    {
                        Pair<string, string> address = MapTools.locationAddresses[key];
                        CsvRow row = new CsvRow();
                        //                row.Add(trip.id.ToString());
                        //                row.Add(trip.startTime.ToString());
                        row.Add(key);
                        row.Add(address.First);
                        row.Add(address.Second);
                        writer.WriteRow(row);
                    }
                }
            }
        }

        private static void WriteGeoLocationNames()
        {
            if (locationNames_Filename != null)
            {
                File.WriteAllText(locationNames_Filename, String.Empty);
                using (StreamWriter sr = new StreamWriter(locationNames_Filename))
                {
                    var serializer = new JavaScriptSerializer();
                    string locationNamesJson = serializer.Serialize(locationNames);
                    sr.Write(locationNamesJson);
                }

                /*
                using (CsvFileWriter writer = new CsvFileWriter(locationNames_Filename))
                {
                    foreach (string key in MapTools.locationNames.Keys)
                    {
                        string name = MapTools.locationNames[key];
                        CsvRow row = new CsvRow();
                        //                row.Add(trip.id.ToString());
                        //                row.Add(trip.startTime.ToString());
                        row.Add(key);
                        row.Add(name);
                        writer.WriteRow(row);
                    }
                }
                 * */
            }
        }

        private static void WriteGeoRoutes()
        {
            if (routes_Filename != null)
            {
                File.WriteAllText(locationNames_Filename, String.Empty);
                using (StreamWriter sr = new StreamWriter(routes_Filename))
                {
                    var serializer = new JavaScriptSerializer();
                    string routesJson = serializer.Serialize(routes);
                    sr.Write(routesJson);
                }
                /*
                using (CsvFileWriter writer = new CsvFileWriter(routes_Filename))
                {
                    int routeID = 0;
                    foreach (Route r in MapTools.routes.Values)
                    {
                        {
                            foreach (Waypoint w in r.waypoints)
                            {
                                CsvRow row = new CsvRow();
                                //                row.Add(trip.id.ToString());
                                //                row.Add(trip.startTime.ToString());
                                row.Add(routeID.ToString());
                                row.Add(w.Lat.ToString());
                                row.Add(w.Lng.ToString());
                                row.Add(w.elapse.ToString());
                                row.Add(w.distance.ToString());
                                row.Add(w.Address);
                                writer.WriteRow(row);
                            }
                        }
                        routeID++;
                    }
                }
                 * */
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

        /*
        // http://maps.googleapis.com/maps/api/directions/json?origin=Toronto&destination=Montreal&sensor=false
        public static Route GetRoute(Location from, Location to)
        {//GETROUTEOSCAR
            lock (routes)
            {
                string key = Route.GetKey(from, to);
                if (routes.ContainsKey(key))
                    return routes[key];
                double METERS_TO_MILES = 0.000621371192;
                int MAX_DURATION = 10;
                XmlDocument doc = new XmlDocument();
                TimeSpan elapse = new TimeSpan(0, 0, 0);
                double totalDistance = 0;
                string url = "http://maps.googleapis.com/maps/api/directions/xml?origin=" + from.Lat + ", " + from.Lng + "&destination=" + to.Lat + ", " + to.Lng + "&sensor=false&units=imperial";
                doc.Load(url);
                XmlNode status = doc.SelectSingleNode("//DirectionsResponse/status");
                if (status == null || status.InnerText == "ZERO_RESULTS")
                    return null;
                List<Waypoint> waypoints = new List<Waypoint>();
                waypoints.Add(new Waypoint(from, new TimeSpan(0), 0));
                var legs = doc.SelectNodes("//DirectionsResponse/route/leg");
                foreach (XmlNode leg in legs)
                {
                    var stepNodes = leg.SelectNodes("step");
                    foreach (XmlNode stepNode in stepNodes)
                    {
                        int duration = int.Parse(stepNode.SelectSingleNode("duration/value").InnerText);
                        double distance = double.Parse(stepNode.SelectSingleNode("distance/value").InnerText) * METERS_TO_MILES;
                        TimeSpan duration2 = new TimeSpan(0, 0, int.Parse(stepNode.SelectSingleNode("duration/value").InnerText));
                        Location end = new Location(double.Parse(stepNode.SelectSingleNode("end_location/lat").InnerText), double.Parse(stepNode.SelectSingleNode("end_location/lng").InnerText));
                        totalDistance += distance;
                        TimeSpan timeSpanTemp = elapse;
                        elapse += duration2;

                        if (duration > MAX_DURATION)
                        {
                            Location start = new Location(double.Parse(stepNode.SelectSingleNode("start_location/lat").InnerText), double.Parse(stepNode.SelectSingleNode("start_location/lng").InnerText));
                            waypoints.AddRange(IncreaseGranularity(duration, MAX_DURATION, timeSpanTemp.TotalSeconds, distance, start, end));
                        }
                        else
                        {
                            waypoints.Add(new Waypoint(end, elapse, totalDistance));
                        }

                    }
                }
                waypoints.Add(new Waypoint(to, elapse, totalDistance));
                Route route = new Route(waypoints.ToArray());
                routes.Add(key, route);
                return route;
            }
        }

        private static List<Waypoint> IncreaseGranularity(int duration, int maxDuration, double totalDuration, double distance, Location from, Location to)
        {
            List<Waypoint> wayPoints = new List<Waypoint>();

            double granularity = (duration / maxDuration);
            double stepDistance = distance / granularity;
            double stepDistaceCount = 0;
            double durationCount = totalDuration;
            double stepLat = from.Lat - to.Lat;
            double stepLng = from.Lng - to.Lng;
            double subStepLat = stepLat / granularity;
            double subStepLng = stepLng / granularity;
            for (int i = 0; i < (granularity - 1); i++)
            {
                from.Lat -= subStepLat;
                from.Lng -= subStepLng;
                Location location = new Location(from.Lat, from.Lng);

                durationCount += maxDuration;
                stepDistaceCount += stepDistance;

                TimeSpan timeSpan = new TimeSpan(0, 0, (int)durationCount);
                wayPoints.Add(new Waypoint(location, timeSpan, stepDistaceCount));

            }
            return wayPoints;
        } */


        /*public static Route GetCachedRoute(Location from, Location to)
        {
            string key = Route.GetKey(from, to);
            if (routes.ContainsKey(key))
                return routes[key];
            /*            foreach (Route r in routes.Values)
                        {
                            if (r.ContainsSubRoute(from, to))
                            {
                                Route route = r.MakeSubRoute(from, to);
                                routes.Add(key, route);
                            }
                        } */
          //  return null;
        //}

        // http://maps.googleapis.com/maps/api/directions/json?origin=Toronto&destination=Montreal&sensor=false
        /*public static Route GetRoute(Location from, Location to)
        {
            lock (routes)
            {
                Route route = GetCachedRoute(from, to);
                if (route != null)
                    return route;
                route = GetRouteFromMapService(from, to, route);
                string key = Route.GetKey(from, to);
                routes.Add(key, route);
                MapTools.WriteGeoRoutes();

                return route;
            }
        }*/

        /*private static Route GetRouteFromMapService(Location from, Location to, Route route)
        {
            double METERS_TO_MILES = 0.000621371192;
            XmlDocument doc = new XmlDocument();
            TimeSpan elapse = new TimeSpan(0, 0, 0);
            double totalDistance = 0;
            string url = "http://maps.googleapis.com/maps/api/directions/xml?origin=" + from.Lat + ", " + from.Lng + "&destination=" + to.Lat + ", " + to.Lng + "&sensor=false";
            doc.Load(url);
            XmlNode status = doc.SelectSingleNode("//DirectionsResponse/status");
            if (status != null && status.InnerText != "ZERO_RESULTS")
            {
                List<Waypoint> waypoints = new List<Waypoint>();
                waypoints.Add(new Waypoint(from, new TimeSpan(0), 0));
                var legs = doc.SelectNodes("//DirectionsResponse/route/leg");
                foreach (XmlNode leg in legs)
                {
                    var stepNodes = leg.SelectNodes("step");
                    foreach (XmlNode stepNode in stepNodes)
                    {
                        TimeSpan duration = new TimeSpan(0, 0, (int)(int.Parse(stepNode.SelectSingleNode("duration/value").InnerText) * distance_and_time_scale));
                        Location end = new Location(double.Parse(stepNode.SelectSingleNode("end_location/lat").InnerText), double.Parse(stepNode.SelectSingleNode("end_location/lng").InnerText));
                        double distance = double.Parse(stepNode.SelectSingleNode("distance/value").InnerText) * METERS_TO_MILES * distance_and_time_scale;
                        totalDistance += distance;
                        elapse += duration;
                        waypoints.Add(new Waypoint(end, elapse, totalDistance));
                    }
                }
                waypoints.Add(new Waypoint(to, elapse, totalDistance));
                route = new Route(waypoints.ToArray());
            }
            return route;
        }*/


        public static Route GetRoute(Location from, Location to)
        {
            lock (routes)
            {
                string key = Route.GetKey(from, to);
                if (routes.ContainsKey(key))
                    return routes[key];
                double METERS_TO_MILES = 0.000621371192;
                int MAX_DURATION = 10;
                XmlDocument doc = new XmlDocument();
                TimeSpan elapse = new TimeSpan(0, 0, 0);
                double totalDistance = 0;
                string url = "http://maps.googleapis.com/maps/api/directions/xml?origin=" + from.Lat + ", " + from.Lng + "&destination=" + to.Lat + ", " + to.Lng + "&sensor=false&units=imperial";
                doc.Load(url);
                XmlNode status = doc.SelectSingleNode("//DirectionsResponse/status");

                using (StreamWriter writer = new StreamWriter("debug.txt", true))
                {
                    writer.WriteLine("Status: " + status.InnerText);
                }

                if (status == null || status.InnerText == "ZERO_RESULTS")
                    return null;
                List<Waypoint> waypoints = new List<Waypoint>();
                waypoints.Add(new Waypoint(from, new TimeSpan(0), 0));
                var legs = doc.SelectNodes("//DirectionsResponse/route/leg");

                foreach (XmlNode leg in legs)
                {
                    var stepNodes = leg.SelectNodes("step");
                    foreach (XmlNode stepNode in stepNodes)
                    {

                        int duration = int.Parse(stepNode.SelectSingleNode("duration/value").InnerText);
                        double distance = double.Parse(stepNode.SelectSingleNode("distance/value").InnerText) * METERS_TO_MILES;
                        TimeSpan duration2 = new TimeSpan(0, 0, int.Parse(stepNode.SelectSingleNode("duration/value").InnerText));
                        Location end = new Location(double.Parse(stepNode.SelectSingleNode("end_location/lat").InnerText), double.Parse(stepNode.SelectSingleNode("end_location/lng").InnerText));
                        double totalDistanceTemp = totalDistance;
                        totalDistance += distance;
                        TimeSpan timeSpanTemp = elapse;
                        elapse += duration2;

                        if (duration > MAX_DURATION)
                        {

                            string polyline = stepNode.SelectSingleNode("polyline/points").InnerText;
                            List<Location> locations = DecodePolylinePoints(polyline);

                            waypoints.AddRange(increaseGranularityInPolylineList(locations, duration, MAX_DURATION, timeSpanTemp.TotalSeconds, distance, totalDistanceTemp));

                        }
                        else
                        {
                            waypoints.Add(new Waypoint(end, elapse, totalDistance));
                        }

                    }
                }

                waypoints.Add(new Waypoint(to, elapse, totalDistance));
                Route route = new Route(waypoints.ToArray());
                routes.Add(key, route);
                return route;
            }
        }

        private static List<Waypoint> increaseGranularityInPolylineList(List<Location> locations, int duration, int maxDuration, double totalDuration, double distance, double totalDistance)
        {
            double METERS_TO_MILES = 0.000621371192;
            var waypoints = new List<Waypoint>();
            XmlDocument doc = new XmlDocument();
            Location prevLocation = null;
            TimeSpan elapse = new TimeSpan(0, 0, (int)totalDuration);
            foreach (var location in locations)
            {
                if (prevLocation == null)
                {
                    prevLocation = location;
                    continue;
                }

                XmlNode status = null;
                int trying = 0;
                do
                {
                    if (trying > 0)
                        Thread.Sleep(1000);
                    string url = "http://maps.googleapis.com/maps/api/directions/xml?origin=" + prevLocation.Lat + ", " +
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

                        int durationInt = int.Parse(stepNode.SelectSingleNode("duration/value").InnerText);
                        double distance2 = double.Parse(stepNode.SelectSingleNode("distance/value").InnerText) * METERS_TO_MILES;
                        TimeSpan duration2 = new TimeSpan(0, 0, int.Parse(stepNode.SelectSingleNode("duration/value").InnerText));
                        Location end = new Location(double.Parse(stepNode.SelectSingleNode("end_location/lat").InnerText), double.Parse(stepNode.SelectSingleNode("end_location/lng").InnerText));

                        if (durationInt > maxDuration)
                        {
                            Location start = new Location(double.Parse(stepNode.SelectSingleNode("start_location/lat").InnerText), double.Parse(stepNode.SelectSingleNode("start_location/lng").InnerText));
                            waypoints.AddRange(IncreaseGranularity(durationInt, maxDuration, elapse.TotalSeconds, distance2, start, end));
                            elapse += duration2;
                        }
                        else
                        {
                            elapse += duration2;
                            totalDistance += distance;
                            waypoints.Add(new Waypoint(end, elapse, totalDistance));
                        }

                    }
                }
                prevLocation = location;
            }


            return waypoints;
        }

        private static List<Waypoint> IncreaseGranularity(int duration, int maxDuration, double totalDuration, double distance, Location from, Location to)
        {
            List<Waypoint> wayPoints = new List<Waypoint>();

            double granularity = duration / maxDuration;
            double stepDistance = distance / granularity;
            double stepDistaceCount = 0;
            double durationCount = totalDuration;
            double stepLat = from.Lat - to.Lat;
            double stepLng = from.Lng - to.Lng;
            double subStepLat = stepLat / granularity;
            double subStepLng = stepLng / granularity;

            //wayPoints.Add(new Waypoint(from, new TimeSpan(0, 0, (int)totalDuration), stepDistaceCount));

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

