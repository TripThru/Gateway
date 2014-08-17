using System;
using System.Collections.Generic;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
//using System.Web.DynamicData;
using System.IO;
//using ServiceStack.Text;
//using ServiceStack.Common.Utils;
//using ServiceStack.Common.Utils;
//using RestSharp;
using TripThruCore;
using ServiceStack.Redis;
using System.Linq.Expressions;

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
        public Waypoint(double lat, double lng, TimeSpan elapse, double distance)
            : base(lat, lng)
        {
            this.elapse = elapse;
            this.distance = distance;
        }
        public Waypoint(Location l, TimeSpan elapse, double distance)
            : base(l.Lat, l.Lng)
        {
            this.elapse = elapse;
            this.distance = distance;
        }

        private TimeSpan _elapseTimeSpan;

        public TimeSpan elapse
        {
            get { return _elapseTimeSpan; }
            set
            {
                _elapseTimeSpan = value;
            }
        }
        public double distance;
    }

    public class SubrouteMap
    {
        public SubrouteMap(Waypoint[] waypoints)
        {
            this.waypoints = waypoints;
            map = new Dictionary<string, Pair<int, Waypoint>>();
            int index = 0;
            foreach (Waypoint w in waypoints)
            {
                map.Add(w.getID(), new Pair<int, Waypoint>(index, w));
                index++;
            }
        }

        public bool ContainsSubRoute(Location start, Location end)
        {
            return map.ContainsKey(start.getID()) && map.ContainsKey(end.getID());
        }

        public Route MakeSubRoute(Location start, Location end)
        {
            if (!ContainsSubRoute(start, end))
                throw new Exception("Error: subroute does not exist");
            int startIndex = map[start.getID()].First;
            int endIndex = map[end.getID()].First;
            int dir = startIndex < endIndex ? 1 : -1;
            List<Waypoint> waypoints = new List<Waypoint>();
            for (int i = startIndex; i != endIndex; i += dir)
                waypoints.Add(this.waypoints[i]);
            return new Route(waypoints.ToArray());
        }
        public Dictionary<string, Pair<int, Waypoint>> map;
        public Waypoint[] waypoints;
    }

    public class Route
    {
        public Route(Waypoint[] waypoints)
        {
            this.waypoints = waypoints;
            this.Id = GetKey(waypoints[0], waypoints[waypoints.Length - 1]);
        }

        public static string GetKey(Location start, Location end) { return start.getID() + ":" + end.getID(); }        // Daniel, you will need to implement this

        public Location start
        {
            get
            {
                if (waypoints != null && waypoints.Count() > 0)
                    return waypoints[0];
                return null;
            }
        }

        public Location end
        {
            get
            {
                if(waypoints != null && waypoints.Count() > 0)
                    return waypoints[waypoints.Length - 1];
                return null;
            }
        }

        public TimeSpan duration { get { return waypoints[waypoints.Length - 1].elapse; }  }
        public double distance { get { return waypoints[waypoints.Length - 1].distance; } }
        public Location GetCurrentWaypoint(DateTime start, DateTime current)
        {
            TimeSpan elapse = current - start;
            int waypoint;
            for (waypoint = 0; elapse > waypoints[waypoint].elapse && waypoint < waypoints.Length - 1; waypoint++) ;
            return waypoints[waypoint];
        }
        public Waypoint[] waypoints {get; set;}
        public string Id { get; set; }
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

        static public long nextID = 0;
        static public string GenerateUniqueID() { nextID++; return nextID.ToString(); }
        static public string GenerateUniqueID(string clientID) { nextID++; return nextID.ToString() + "@" + clientID; }

    }


    public class GarbageCleanup<T>
    {
        TimeSpan maxAge;
        public Queue<Pair<DateTime, T>> garbage;
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

    public static class MemberInfoGetting
    {
        public static string GetMemberName<T>(Expression<Func<T>> memberExpression)
        {
            MemberExpression expressionBody = (MemberExpression)memberExpression.Body;
            return expressionBody.Member.Name;
        }
    }
    public class RedisDictionary<K, T> : ConcurrentDictionary<K, T>
    {
        string id;
        public RedisDictionary(RedisClient redis, string id, Expression<Func<T>> member)
        {
            this.id = id + ":" + MemberInfoGetting.GetMemberName(member);
        }
        public RedisDictionary(RedisClient redis, string id)
        {
        }
        public T Remove(K key)
        {
            T value;
            TryRemove(key, out value);
            return value;
        }
        public void Add(K key, T value)
        {
            TryAdd(key, value);
        }

        public T this[K key]
        {
            get
            {
                if (!base.ContainsKey(key))
                    throw new Exception("Fatal Error: key " + key + " not found in " + id);
                return base[key];
            }
            set
            {
                if (base.ContainsKey(key))
                    throw new Exception("Fatal Error: key " + key + " already exists in " + id);
                base[key] = value;
            }
        }

    }
    /*
        public class RedisDictionary<K, T> : IEnumerable<T>
        {
            RedisClient redis;
            string id;
            public RedisDictionary(RedisClient redis, string id, Expression<Func<T>> member)
            {
                this.redis = redis;
                this.id = id + ":" + MemberInfoGetting.GetMemberName(member);
            }
            public RedisDictionary(RedisClient redis, string id)
            {
                this.redis = redis;
                this.id = id;
            }
            public bool ContainsKey(K key)
            {
                return redis.As<T>().GetHash<K>(id).ContainsKey(key);
            }
            public void Clear()
            {
                redis.As<T>().GetHash<K>(id).Clear();
            }
            public void Remove(K key)
            {
                redis.As<T>().GetHash<K>(id).Remove(key);
            }
            public void Add(K key, T item)
            {
                this[key] = item;
            }

            public IEnumerable<K> Keys
            {
                get
                {
                    return redis.As<T>().GetHash<K>(id).Keys;
                }
            }

            public IEnumerable<T> Values
            {
                get
                {
                    return redis.As<T>().GetHash<K>(id).Values;
                }
            }

            public T this[K key]
            {
                get
                {
                    return redis.As<T>().GetHash<K>(id)[key];
                }
                set
                {
                    redis.As<T>().GetHash<K>(id)[key] = value;
                }
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                // Lets call the generic version here
                return redis.As<T>().GetHash<K>(id).Values.GetEnumerator();
            }

            public int Count
            {
                get
                {
                    return redis.As<T>().GetHash<K>(id).Count;
                }
            }

            public IEnumerator<T> GetEnumerator<T>()
            {
                return redis.As<T>().GetHash<K>(id).Values.GetEnumerator();
            }
            public IEnumerator<T> GetEnumerator()
            {
                return redis.As<T>().GetHash<K>(id).Values.GetEnumerator();
            }
        } */


    public class RedisExpiryCounter : RedisExpiryList<double>
    {
        TimeSpan expiresIn;
        RedisObject<double> count;
        public RedisExpiryCounter(RedisClient redis, string id, TimeSpan expiresIn)
            : base(redis, id)
        {
            this.expiresIn = expiresIn;
            count = new RedisObject<double>(redis, id + ":" + MemberInfoGetting.GetMemberName(() => count));
        }
        public static RedisExpiryCounter operator ++(RedisExpiryCounter c)
        {
            c.Add(1);
            return c;
        }
        public static RedisExpiryCounter operator +(RedisExpiryCounter c, double d)
        {
            c.Add(d);
            return c;
        }
        private void Add(double value)
        {
            count += value;
            base.Add(value, DateTime.UtcNow + expiresIn);
        }
        public void ExpirationAction(double value)
        {
            count -= value;
        }
        public static implicit operator double(RedisExpiryCounter o)
        {
            return o.count;
        }

        public double Value
        {
            get
            {
                return this.count.value;
            }
        }
    }

    public class RedisExpiryList<T> : IEnumerable<Pair<DateTime, T>>
    {
        RedisClient redis;
        string id;
        public delegate void ExpirationAction(T item);
        public ExpirationAction expirationAction;
        public RedisExpiryList(RedisClient redis, string id, Expression<Func<T>> member, ExpirationAction expirationAction = null)
        {
            this.redis = redis;
            this.id = id + ":" + MemberInfoGetting.GetMemberName(member);
            this.expirationAction = expirationAction;
        }
        public RedisExpiryList(RedisClient redis, string id)
        {
            this.redis = redis;
            this.id = id;
        }
        //        ServiceStack.Redis.Generic.IRedisList<Pair<DateTime, T>> List { get { return redis.As<Pair<DateTime, T>>().Lists[id]; } }
        ConcurrentQueue<Pair<DateTime, T>> List = new ConcurrentQueue<Pair<DateTime, T>>();
        void Cleanup()
        {
            while (List.Count > 0)
            {
                //                    if (List.Count > 0 && List.Last().First <= DateTime.UtcNow)
                Pair<DateTime, T> end;
                List.TryPeek(out end);
                if (List.Count > 0 && end.First <= DateTime.UtcNow)
                {
                    if (expirationAction != null)
                        expirationAction(List.Last().Second);
                    List.TryDequeue(out end); // RemoveEnd();  // for Redis
                }
                else
                    break;
            }
        }

        public int Count
        {
            get
            {
                Cleanup();
                return List.Count;
            }
        }
        public void Clear()
        {
            Pair<DateTime, T> end;
            while (List.Count > 0)
                List.TryDequeue(out end); // RemoveEnd();  // for Redis
            //            List.Clear();
        }
        public void Add(T item, DateTime expireAt)
        {
            List.Enqueue(new Pair<DateTime, T>(expireAt, item));
            //List.Push(new Pair<DateTime, T>(expireAt, item)); for Redis
        }
        public void Add(T item, TimeSpan expireIn)
        {
            List.Enqueue(new Pair<DateTime, T>(DateTime.UtcNow + expireIn, item));
            // List.Push(new Pair<DateTime, T>(DateTime.UtcNow + expireIn, item)); for Redis
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            // Lets call the generic version here
            Cleanup();
            return List.GetEnumerator();
        }
        /*        public IEnumerator<Pair<DateTime, T>> GetEnumerator<T>() for Redis?
                {
                    Cleanup();
        //            return (IEnumerator<Pair<DateTime, T>>)List.GetEnumerator();
                    return (IEnumerator<Pair<DateTime, T>>) List.GetEnumerator();
                } */
        public IEnumerator<Pair<DateTime, T>> GetEnumerator()
        {
            Cleanup();
            return List.GetEnumerator();
        }
    }


    public class RedisObject<T>
    {
        readonly RedisClient redis;
        readonly string id;
        private T _value; // when disabling redis;
        private static readonly object valueTypeLock = new object();

        public RedisObject(RedisClient redis, string id, Expression<Func<T>> member)
        {
            this.redis = redis;
            this.id = id + ":" + MemberInfoGetting.GetMemberName(member);
        }
        public RedisObject(RedisClient redis, string id)
        {
            this.redis = redis;
            this.id = id;
        }
        public static RedisObject<T> operator ++(RedisObject<T> obj)
        {
            if (obj.id.Contains("exceptions"))
                Logger.Log("exception");
            ((dynamic)obj).value++;
            return obj;
        }
        public static RedisObject<T> operator --(RedisObject<T> obj)
        {
            ((dynamic)obj).value--;
            return obj;
        }
        public static RedisObject<T> operator +(RedisObject<T> obj, T value)
        {
            if (obj.id.Contains("exceptions"))
                Logger.Log("exception");
            ((dynamic)obj).value += value;
            return obj;
        }
        public static RedisObject<T> operator -(RedisObject<T> obj, T value)
        {
            ((dynamic)obj).value -= value;
            return obj;
        }

        public static implicit operator T(RedisObject<T> o)
        {
            lock (valueTypeLock)
            {
                return o._value;
            }
            //return o.redis.As<T>().GetById(o.id); put back for Redis
        }
        public T value
        {
            get
            {
                lock (valueTypeLock)
                {
                    return _value;
                }
                //return redis.As<T>().GetValue(id);put back for Redis
            }
            set
            {
                lock (valueTypeLock)
                {
                    _value = value;
                }
                //redis.As<T>().SetEntry(id, value);put back for Redis
            }
        }
    }

    public class Counter
    {
        TimeSpan maxAge;
        Queue<Pair<DateTime, double>> counts;
        double count;
        public static implicit operator int(Counter c)
        {
            return (int)c.count;
        }
        public static implicit operator long(Counter c)
        {
            return (long)c.count;
        }
        public static implicit operator double(Counter c)
        {
            return (int)c.count;
        }
        public Counter(TimeSpan maxAge)
        {
            this.maxAge = maxAge;
            counts = new Queue<Pair<DateTime, double>>();
        }
        void Cleanup()
        {
            while (counts.Count > 0 && DateTime.UtcNow - counts.Peek().First > maxAge)
            {
                count -= counts.Peek().Second;
                counts.Dequeue();
            }
        }

        public static Counter operator ++(Counter c)
        {
            // Increment this widget.
            c.counts.Enqueue(new Pair<DateTime, double>(DateTime.UtcNow, 1));
            c.count += 1;
            c.Cleanup();
            return c;
        }
        public static Counter operator +(Counter c, double d)
        {
            // Increment this widget.
            c.counts.Enqueue(new Pair<DateTime, double>(DateTime.UtcNow, d));
            c.count += d;
            c.Cleanup();
            return c;
        }
        public override string ToString()
        {
            return count.ToString();
        }
    }

}