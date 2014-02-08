using System;
using System.Collections.Generic;
namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            Stat distance = new Stat();
            double? x = null;
            distance += (double)1;
            distance += (double)1;
            distance += (double)1;
            distance += (double)1;
            distance += (double) x;
            Console.WriteLine(distance);
        }
    }

    public class Stat
    {
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
        public double allTime;
        public Counter last24Hrs;
        public Counter lastHour;
        public Stat()
        {
            last24Hrs = new Counter(new TimeSpan(24, 0, 0));
            lastHour = new Counter(new TimeSpan(1, 0, 0));
        }
        public static Stat operator ++(Stat s)
        {
            // Increment this widget.
            s.allTime++;
            s.last24Hrs++;
            s.lastHour++;
            return s;
        }
        public static Stat operator +(Stat s, double d)
        {
            // Increment this widget.
            s.allTime += d;
            s.last24Hrs += d;
            s.lastHour += d;
            return s;
        }
        public override string ToString()
        {
            return "AllTime = " + allTime + ", Last24Hrs = " + ((long)last24Hrs) + ", LastHour = " + ((long)lastHour);
        }
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

}
