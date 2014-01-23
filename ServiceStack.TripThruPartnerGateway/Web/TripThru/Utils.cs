using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Web;
using System.Xml;
using System.Xml.XPath;
using System.IO;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using ServiceStack.Common.Utils;

namespace ServiceStack.TripThruGateway.TripThru
{
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

    [Serializable()]
    public class SerializableDictionary<TKey, TVal> : Dictionary<TKey, TVal>, IXmlSerializable, ISerializable
    {
        #region Constants
        private const string DictionaryNodeName = "Dictionary";
        private const string ItemNodeName = "Item";
        private const string KeyNodeName = "Key";
        private const string ValueNodeName = "Value";
        #endregion
        #region Constructors
        public SerializableDictionary()
        {
        }

        public SerializableDictionary(IDictionary<TKey, TVal> dictionary)
            : base(dictionary)
        {
        }

        public SerializableDictionary(IEqualityComparer<TKey> comparer)
            : base(comparer)
        {
        }

        public SerializableDictionary(int capacity)
            : base(capacity)
        {
        }

        public SerializableDictionary(IDictionary<TKey, TVal> dictionary, IEqualityComparer<TKey> comparer)
            : base(dictionary, comparer)
        {
        }

        public SerializableDictionary(int capacity, IEqualityComparer<TKey> comparer)
            : base(capacity, comparer)
        {
        }

        #endregion
        #region ISerializable Members

        protected SerializableDictionary(SerializationInfo info, StreamingContext context)
        {
            int itemCount = info.GetInt32("ItemCount");
            for (int i = 0; i < itemCount; i++)
            {
                KeyValuePair<TKey, TVal> kvp = (KeyValuePair<TKey, TVal>)info.GetValue(String.Format("Item{0}", i), typeof(KeyValuePair<TKey, TVal>));
                this.Add(kvp.Key, kvp.Value);
            }
        }

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("ItemCount", this.Count);
            int itemIdx = 0;
            foreach (KeyValuePair<TKey, TVal> kvp in this)
            {
                info.AddValue(String.Format("Item{0}", itemIdx), kvp, typeof(KeyValuePair<TKey, TVal>));
                itemIdx++;
            }
        }

        #endregion
        #region IXmlSerializable Members

        void IXmlSerializable.WriteXml(System.Xml.XmlWriter writer)
        {
            //writer.WriteStartElement(DictionaryNodeName);
            foreach (KeyValuePair<TKey, TVal> kvp in this)
            {
                writer.WriteStartElement(ItemNodeName);
                writer.WriteStartElement(KeyNodeName);
                KeySerializer.Serialize(writer, kvp.Key);
                writer.WriteEndElement();
                writer.WriteStartElement(ValueNodeName);
                ValueSerializer.Serialize(writer, kvp.Value);
                writer.WriteEndElement();
                writer.WriteEndElement();
            }
            //writer.WriteEndElement();
        }

        void IXmlSerializable.ReadXml(System.Xml.XmlReader reader)
        {
            if (reader.IsEmptyElement)
            {
                return;
            }

            // Move past container
            if (!reader.Read())
            {
                throw new XmlException("Error in Deserialization of Dictionary");
            }

            //reader.ReadStartElement(DictionaryNodeName);
            while (reader.NodeType != XmlNodeType.EndElement)
            {
                reader.ReadStartElement(ItemNodeName);
                reader.ReadStartElement(KeyNodeName);
                TKey key = (TKey)KeySerializer.Deserialize(reader);
                reader.ReadEndElement();
                reader.ReadStartElement(ValueNodeName);
                TVal value = (TVal)ValueSerializer.Deserialize(reader);
                reader.ReadEndElement();
                reader.ReadEndElement();
                this.Add(key, value);
                reader.MoveToContent();
            }
            //reader.ReadEndElement();

            reader.ReadEndElement(); // Read End Element to close Read of containing node
        }

        System.Xml.Schema.XmlSchema IXmlSerializable.GetSchema()
        {
            return null;
        }

        #endregion
        #region Private Properties
        protected XmlSerializer ValueSerializer
        {
            get
            {
                if (valueSerializer == null)
                {
                    valueSerializer = new XmlSerializer(typeof(TVal));
                }
                return valueSerializer;
            }
        }

        private XmlSerializer KeySerializer
        {
            get
            {
                if (keySerializer == null)
                {
                    keySerializer = new XmlSerializer(typeof(TKey));
                }
                return keySerializer;
            }
        }
        #endregion
        #region Private Members
        private XmlSerializer keySerializer = null;
        private XmlSerializer valueSerializer = null;
        #endregion
    }
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
    };
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

    class MapTools
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
        public static SerializableDictionary<string, string> locationNames = new SerializableDictionary<string, string>();
        public static SerializableDictionary<string, Route> routes = new SerializableDictionary<string, Route>();

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


        public static void LoadGeoData()
        {
            using (CsvFileReader reader = new CsvFileReader("~/App_Data/Geo-Location-Names.csv".MapHostAbsolutePath()))
            {
                CsvRow row = new CsvRow();
                while (reader.ReadRow(row, ','))
                {
                    locationNames.Add(row[0], row[1]);
                }
            }
            // load trip routes
            Dictionary<string, LinkedList<Waypoint>> routes = new Dictionary<string, LinkedList<Waypoint>>();
            using (CsvFileReader reader = new CsvFileReader("~/App_Data/Geo-Routes.csv".MapHostAbsolutePath()))
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

        public static void WriteGeoData()
        {
            using (CsvFileWriter writer = new CsvFileWriter("~/App_Data/Geo-Routes.csv".MapHostAbsolutePath()))
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

            using (CsvFileWriter writer = new CsvFileWriter("~/App_Data/Geo-Location-Names.csv".MapHostAbsolutePath()))
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


        // http://code.google.com/apis/maps/documentation/geocoding/#ReverseGeocoding
        public static string GetReverseGeoLoc(Location location)
        {
            string key = location.getID();
            if (locationNames.ContainsKey(key))
                return locationNames[key];
            //return "Google -- Over query limit";

            XmlDocument doc = new XmlDocument();
            {
                doc.Load("http://maps.googleapis.com/maps/api/geocode/xml?latlng=" + location.Lat + "," + location.Lng + "&sensor=false");
                XmlNode element = doc.SelectSingleNode("//GeocodeResponse/status");
                if (element.InnerText == "OVER_QUERY_LIMIT")
                {

                    System.Threading.Thread.Sleep(new TimeSpan(0, 1, 10));
                    doc.Load("http://maps.googleapis.com/maps/api/geocode/xml?latlng=" + location.Lat + "," + location.Lng + "&sensor=false");
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
        // http://maps.googleapis.com/maps/api/directions/json?origin=Toronto&destination=Montreal&sensor=false
        public static Route GetRoute(Location from, Location to)
        {
            string key = Route.GetKey(from, to);
            if (routes.ContainsKey(key))
                return routes[key];
            double METERS_TO_MILES = 0.000621371192;
            XmlDocument doc = new XmlDocument();
            TimeSpan elapse = new TimeSpan(0, 0, 0);
            double totalDistance = 0;
            string url = "http://maps.googleapis.com/maps/api/directions/xml?origin=" + from.Lat + ", " + from.Lng + "&destination=" + to.Lat + ", " + to.Lng + "&sensor=false";
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
                    TimeSpan duration = new TimeSpan(0, 0, int.Parse(stepNode.SelectSingleNode("duration/value").InnerText));
                    Location end = new Location(double.Parse(stepNode.SelectSingleNode("end_location/lat").InnerText), double.Parse(stepNode.SelectSingleNode("end_location/lng").InnerText));
                    double distance = double.Parse(stepNode.SelectSingleNode("distance/value").InnerText) * METERS_TO_MILES;
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

    public class Logger
    {
        public static int tab;
        public static int logLine;
        public static bool enabled { get; set; }
        public static bool forceOn { get; set; }
        public static string filePath = "~/App_Data/".MapHostAbsolutePath();
        static int on;
        static System.IO.StreamWriter logFile;

        public static FixedSizeQueue<Pair<DateTime, string>> LogQueue;

        //        [Conditional("DEBUG")]
        static public void Tab() { tab++; }
        //        [Conditional("DEBUG")]
        static public void Untab() { tab--; }
        static public string GetTab()
        {
            string tabs = "";
            for (int n = 0; n < tab; n++)
                tabs += '\t';
            return tabs;
        }
        static public void Off()
        {
            on--;
        }
        static public void On()
        {
            on++;
        }

        static public void Log(string msg, string filename)
        {
            if (filename.Length == 0)
            {
                Log(msg);
                return;
            }
            OpenLog(filename, true, true);
            logFile.WriteLine(GetTab() + msg);
            logFile.Flush();
            CloseLog();
        }
        [Conditional("DEBUG")]
        static public void Log(string msg)
        {
            LogQueue.Enqueue(new Tuple<DateTime, int, string>(DateTime.UtcNow, tab * 40, msg));
            {
                if (logFile == null)
                    return;
                if (!enabled && !forceOn)
                    return;
            }
            logFile.WriteLine(GetTab() + msg);
            logFile.Flush();
        }
        static public void OpenLog(string filename, bool enabled_, bool append = false)
        {
            if (!append)
            {
                tab = 0;
                logLine = 0;
                forceOn = false;
                enabled = enabled_;
            }
            enabledMethods = new LinkedList<string>();
            LogQueue = new FixedSizeQueue<Pair<DateTime, string>>(300);
            //logFile = new System.IO.StreamWriter("~/App_Data/".MapHostAbsolutePath() + filename);
        }

        static public void CloseLog()
        {
            if (logFile != null)
                logFile.Close();
            logFile = null;
            LogQueue.Queue.Clear();
        }
        static public LinkedList<string> enabledMethods;
    }

    public class FixedSizeQueue<T>
    {
        public Queue<Tuple<DateTime, int, string>> Queue { get; set; }

        public FixedSizeQueue(int limit)
        {
            this.Limit = limit;
            Queue = new Queue<Tuple<DateTime, int, string>>();
        }

        public int Limit { get; set; }
        public void Enqueue(Tuple<DateTime, int, string> obj)
        {
            Queue.Enqueue(obj);
            var expired = DateTime.UtcNow - new TimeSpan(0, 0, 30, 0);
            lock (this)
            {
                while (Queue.Peek().Item1 < expired)
                    Queue.Dequeue();
            }
        }
    }
}
