using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Web;
using System.Net;
//using System.Web.DynamicData;
using System.Xml;
using System.Xml.XPath;
using System.IO;
using System.Runtime.Serialization;
using System.Xml.Serialization;
//using ServiceStack.Text;
//using ServiceStack.Common.Utils;
//using ServiceStack.Common.Utils;
using RestSharp;
using TripThruCore;

namespace Utils
{
    public class Pair<T, U>
    {
        public Pair()
        {
        }

        public Pair(T first, U second)
        {
            this.First = first;
            this.Second = second;
        }

        public T First { get; set; }
        public U Second { get; set; }
    }
    public class LambdaComparer<T> : IComparer<T>
    {
        public delegate int LambdaFunc(T a, T b);
        public LambdaComparer(LambdaFunc comparer)
        {
            this.comparer = comparer;
        }
        public int Compare(T a, T b)
        {
            return this.comparer(a, b);
        }
        public LambdaFunc comparer;
    }
    public class BinaryHeap<T> : IEnumerable<T>
    {
        private IComparer<T> Comparer;
        private List<T> Items = new List<T>();
        public BinaryHeap()
            : this(Comparer<T>.Default)
        {
        }
        public BinaryHeap(IComparer<T> comp)
        {
            Comparer = comp;
        }
        public BinaryHeap(LambdaComparer<T>.LambdaFunc comparer)
        {
            Comparer = new LambdaComparer<T>(comparer);
        }
        /// <summary>

        /// Get a count of the number of items in the collection.
        /// </summary>
        public int Count
        {
            get { return Items.Count; }
        }
        /// <summary>
        /// Removes all items from the collection.
        /// </summary>
        public void Clear()
        {
            Items.Clear();
        }
        /// <summary>
        /// Sets the capacity to the actual number of elements in the BinaryHeap,
        /// if that number is less than a threshold value.
        /// </summary>

        /// <remarks>
        /// The current threshold value is 90% (.NET 3.5), but might change in a future release.
        /// </remarks>
        public void TrimExcess()
        {
            Items.TrimExcess();
        }
        /// <summary>
        /// Inserts an item onto the heap.
        /// </summary>
        /// <param name="newItem">The item to be inserted.</param>

        public void Insert(T newItem)
        {
            int i = Count;
            Items.Add(newItem);
            while (i > 0 && Comparer.Compare(Items[(i - 1) / 2], newItem) > 0)
            {
                Items[i] = Items[(i - 1) / 2];
                i = (i - 1) / 2;
            }
            Items[i] = newItem;
        }
        /// <summary>
        /// Return the root item from the collection, without removing it.
        /// </summary>
        /// <returns>Returns the item at the root of the heap.</returns>

        public T Peek()
        {
            if (Items.Count == 0)
            {
                throw new InvalidOperationException("The heap is empty.");
            }
            return Items[0];
        }
        /// <summary>
        /// Removes and returns the root item from the collection.
        /// </summary>
        /// <returns>Returns the item at the root of the heap.</returns>
        public T RemoveRoot()
        {
            if (Items.Count == 0)
            {
                throw new InvalidOperationException("The heap is empty.");
            }
            // Get the first item
            T rslt = Items[0];
            // Get the last item and bubble it down.
            T tmp = Items[Items.Count - 1];
            Items.RemoveAt(Items.Count - 1);
            if (Items.Count > 0)
            {
                int i = 0;
                while (i < Items.Count / 2)
                {
                    int j = (2 * i) + 1;
                    if ((j < Items.Count - 1) && (Comparer.Compare(Items[j], Items[j + 1]) > 0))
                    {
                        ++j;
                    }
                    if (Comparer.Compare(Items[j], tmp) >= 0)
                    {
                        break;
                    }
                    Items[i] = Items[j];
                    i = j;
                }
                Items[i] = tmp;
            }
            return rslt;
        }
        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            foreach (var i in Items)
            {
                yield return i;
            }
        }
        public IEnumerator GetEnumerator()
        {
            return Items.GetEnumerator();
        }
    }

    public class CsvRow : List<string>
    {
        public string LineText { get; set; }
    }
    /// <summary>
    /// Class to write data to a CSV file
    /// </summary>
    public class CsvFileWriter : StreamWriter
    {
        public CsvFileWriter(Stream stream)
            : base(stream)
        {
        }

        public CsvFileWriter(string filename)
            : base(filename)
        {
        }

        /// <summary>
        /// Writes a single row to a CSV file.
        /// </summary>
        /// <param name="row">The row to be written</param>
        public void WriteRow(CsvRow row)
        {
            StringBuilder builder = new StringBuilder();
            bool firstColumn = true;
            foreach (string value in row)
            {
                // Add separator if this isn't the first value
                if (!firstColumn)
                    builder.Append(',');
                // Implement special handling for values that contain comma or quote
                // Enclose in quotes and double up any double quotes
                if (value.IndexOfAny(new char[] { '"', ',' }) != -1)
                    builder.AppendFormat("\"{0}\"", value.Replace("\"", "\"\""));
                else
                    builder.Append(value);
                firstColumn = false;
            }
            row.LineText = builder.ToString();
            WriteLine(row.LineText);
        }
    }
    /// <summary>
    /// Class to read data from a CSV file
    /// </summary>
    public class CsvFileReader : StreamReader
    {
        public CsvFileReader(Stream stream)
            : base(stream)
        {
        }

        public CsvFileReader(string filename)
            : base(filename)
        {
        }

        /// <summary>
        /// Reads a row of data from a CSV file
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        public bool ReadRow(CsvRow row, char separator)
        {
            row.LineText = ReadLine();
            if (String.IsNullOrEmpty(row.LineText))
                return false;

            int pos = 0;
            int rows = 0;

            while (pos < row.LineText.Length)
            {
                string value;

                // Special handling for quoted field
                if (row.LineText[pos] == '"')
                {
                    // Skip initial quote
                    pos++;

                    // Parse quoted value
                    int start = pos;
                    while (pos < row.LineText.Length)
                    {
                        // Test for quote character
                        if (row.LineText[pos] == '"')
                        {
                            // Found one
                            pos++;

                            // If two quotes together, keep one
                            // Otherwise, indicates end of value
                            if (pos >= row.LineText.Length || row.LineText[pos] != '"')
                            {
                                pos--;
                                break;
                            }
                        }
                        pos++;
                    }
                    value = row.LineText.Substring(start, pos - start);
                }
                else
                {
                    // Parse unquoted value
                    int start = pos;
                    while (pos < row.LineText.Length && row.LineText[pos] != separator)
                        pos++;
                    value = row.LineText.Substring(start, pos - start);
                }

                // Add field to list
                if (rows < row.Count)
                    row[rows] = value;
                else
                    row.Add(value);
                rows++;

                // Eat up to and including next comma
                while (pos < row.LineText.Length && row.LineText[pos] != separator)
                    pos++;
                if (pos < row.LineText.Length)
                    pos++;
            }
            // Delete any unused items
            while (row.Count > rows)
                row.RemoveAt(rows);

            // Return true if any columns read
            return (row.Count > 0);
        }
    }

    public class Waypoint : Location
    {
        public Waypoint()
        {
        }
        public Waypoint(double lat, double lng, TimeSpan elapse, double distance, string name = null)
            : base(lat, lng, name)
        {
            this.elapse = elapse;
            this.distance = distance;
        }
        public Waypoint(Location l, TimeSpan elapse, double distance, string name = null)
            : base(l.Lat, l.Lng, name)
        {
            this.elapse = elapse;
            this.distance = distance;
        }
        public TimeSpan elapse;
        public double distance;
    }
    public class Route
    {
        public Route()
        {
        }
        public Route(Waypoint[] waypoints)
        {
            this.waypoints = waypoints;
        }
        public static string GetKey(Location start, Location end) { return start.getID() + ":" + end.getID(); }        // Daniel, you will need to implement this
        public Location start { get { return waypoints[0]; } }
        public Location end { get { return waypoints[waypoints.Length - 1]; } }
        public TimeSpan duration { get { return waypoints[waypoints.Length - 1].elapse; } }
        public double distance { get { return waypoints[waypoints.Length - 1].distance; } }
        public Location GetCurrentWaypoint(DateTime start, DateTime current)
        {
            TimeSpan elapse = current - start;
            int waypoint;
            for (waypoint = 0; elapse > waypoints[waypoint].elapse && waypoint < waypoints.Length - 1; waypoint++) ;
            return waypoints[waypoint];
        }
        public Waypoint[] waypoints;
    }
    public class IDName
    {
        public string ID;
        public string name;
        public IDName(string ID, string name)
        {
            this.ID = ID;
            this.name = name;
        }
        public IDName(string name)
        {
            this.ID = GenerateUniqueID();
            this.name = name;
        }
        public IDName()
        {
            this.ID = null;
            this.name = null;
        }
        public override string ToString()
        {
            return "(ID = " + ID + ", Name = " + name + ")";
        }

        static long nextID = 0;
        static public string GenerateUniqueID() { nextID++; return nextID.ToString(); }
        static public string GenerateUniqueID(string clientID) { nextID++; return nextID.ToString() + "@" + clientID; }
    
    }

    public class MapTools
    {
        private List<Location> DecodePolylinePoints(string encodedPoints)
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


        public static void LoadGeoData(string filenameLocationNames, string filenameRoutes, string filenameLocationAddresses)
        {
            using (CsvFileReader reader = new CsvFileReader(filenameLocationAddresses))
            {
                CsvRow row = new CsvRow();
                while (reader.ReadRow(row, ','))
                {
                    locationAddresses.Add(row[0], new Pair<string, string>(row[1], row[2]));
                }
            }
            using (CsvFileReader reader = new CsvFileReader(filenameLocationNames))
            {
                CsvRow row = new CsvRow();
                while (reader.ReadRow(row, ','))
                {
                    locationNames.Add(row[0], row[1]);
                }
            }
            // load trip routes
            Dictionary<string, LinkedList<Waypoint>> routes = new Dictionary<string, LinkedList<Waypoint>>();
            using (CsvFileReader reader = new CsvFileReader(filenameRoutes))
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
            }


        }

        public static void WriteGeoData(string locationNames = null, string routes = null, string locationAddresses = null)
        {
            if (routes != null)
            {
                using (CsvFileWriter writer = new CsvFileWriter(routes))
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
                                row.Add(w.Name);
                                writer.WriteRow(row);
                            }
                        }
                        routeID++;
                    }
                }
            }
            if (locationNames != null)
            {
                using (CsvFileWriter writer = new CsvFileWriter(locationNames))
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
            }
            if (locationAddresses != null)
            {
                using (CsvFileWriter writer = new CsvFileWriter(locationAddresses))
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


        // http://code.google.com/apis/maps/documentation/geocoding/#ReverseGeocoding
        public static string GetReverseGeoLoc(Location location)
        {
            lock (locationNames)
            {
                string key = location.getID();
                if (locationNames.ContainsKey(key))
                    return locationNames[key];
                //return "Google -- Over query limit";

                XmlDocument doc = new XmlDocument();
                {
                    doc.Load("http://maps.googleapis.com/maps/api/geocode/xml?latlng=" + location.Lat + "," +
                             location.Lng + "&sensor=false");
                    XmlNode element = doc.SelectSingleNode("//GeocodeResponse/status");
                    if (element.InnerText == "OVER_QUERY_LIMIT")
                    {

                        System.Threading.Thread.Sleep(new TimeSpan(0, 1, 10));
                        doc.Load("http://maps.googleapis.com/maps/api/geocode/xml?latlng=" + location.Lat + "," +
                                 location.Lng + "&sensor=false");
                        element = doc.SelectSingleNode("//GeocodeResponse/status");

                    }
                    if (element.InnerText == "ZERO_RESULTS" || element.InnerText == "OVER_QUERY_LIMIT")
                        return "Google -- Over query limit";
                    else
                    {

                        element = doc.SelectSingleNode("//GeocodeResponse/result/formatted_address");
                        locationNames.Add(location.getID(), element.InnerText);
                        return element.InnerText;
                    }
                }
            }
        }

        public static Pair<string, string> GetReverseGeoLocAddress(Location location)
        {
            lock (locationAddresses)
            {
                Pair<string, string> address = new Pair<string, string>();
                string key = location.getID();
                if (locationAddresses.ContainsKey(key))
                    return locationAddresses[key];
                //return "Google -- Over query limit";

                XmlDocument doc = new XmlDocument();
                doc.Load("http://maps.googleapis.com/maps/api/geocode/xml?latlng=" + location.Lat + "," + location.Lng +
                         "&sensor=false");
                XmlNode element = doc.SelectSingleNode("//GeocodeResponse/status");
                if (element.InnerText == "OVER_QUERY_LIMIT")
                {

                    System.Threading.Thread.Sleep(new TimeSpan(0, 1, 10));
                    doc.Load("http://maps.googleapis.com/maps/api/geocode/xml?latlng=" + location.Lat + "," +
                             location.Lng + "&sensor=false");
                    element = doc.SelectSingleNode("//GeocodeResponse/status");

                }
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
                    return address;

                }
                return null;
            }
        }

        // http://maps.googleapis.com/maps/api/directions/json?origin=Toronto&destination=Montreal&sensor=false
        public static Route GetRoute(Location from, Location to)
        {
            lock (routes)
            {
                string key = Route.GetKey(from, to);
                if (routes.ContainsKey(key))
                    return routes[key];
                double METERS_TO_MILES = 0.000621371192;
                XmlDocument doc = new XmlDocument();
                TimeSpan elapse = new TimeSpan(0, 0, 0);
                double totalDistance = 0;
                string url = "http://maps.googleapis.com/maps/api/directions/xml?origin=" + from.Lat + ", " + from.Lng +
                             "&destination=" + to.Lat + ", " + to.Lng + "&sensor=false";
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
                        TimeSpan duration = new TimeSpan(0, 0,
                            int.Parse(stepNode.SelectSingleNode("duration/value").InnerText));
                        Location end =
                            new Location(double.Parse(stepNode.SelectSingleNode("end_location/lat").InnerText),
                                double.Parse(stepNode.SelectSingleNode("end_location/lng").InnerText));
                        double distance = double.Parse(stepNode.SelectSingleNode("distance/value").InnerText)*
                                          METERS_TO_MILES;
                        totalDistance += distance;
                        elapse += duration;
                        waypoints.Add(new Waypoint(end, elapse, totalDistance));
                    }
                }
                waypoints.Add(new Waypoint(to, elapse, totalDistance));
                Route route = new Route(waypoints.ToArray());
                routes.Add(key, route);
                return route;
            }
        }
    }
    public class GarbageCleanup<T>
    {
        TimeSpan maxAge;
        Queue<Pair<DateTime, T>> garbage;
        Action<T> cleanup;
        public GarbageCleanup(TimeSpan maxAge, Action<T> cleanup)
        {
            this.maxAge = maxAge;
            garbage = new Queue<Pair<DateTime, T>>();
            this.cleanup = cleanup;
        }
        public void Add(T item)
        {
            // Increment this widget.
            garbage.Enqueue(new Pair<DateTime, T>(DateTime.UtcNow, item));
            while (garbage.Count > 0 && DateTime.UtcNow - garbage.Peek().First > maxAge)
            {
                cleanup.Invoke(garbage.Peek().Second);
                garbage.Dequeue();
            }
        }
    }

}
