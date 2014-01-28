using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Text;
using Utils;
using RestSharp;
namespace Utils
{
    public class Logger
    {
        public class FixedSizeQueue : Queue<RequestLog>
        {
            public FixedSizeQueue(int limit)
            {
                this.Limit = limit;
            }

            public int Limit { get; set; }

            public new void Enqueue(RequestLog r)
            {
                base.Enqueue(r);
                var expired = DateTime.UtcNow - new TimeSpan(0, 0, 30, 0);
                lock (this)
                {
                    while (Peek().Time < expired)
                        Dequeue();
                }
            }
        }

        public class RequestLog
        {
            public DateTime Time;
            public List<Pair<int,string>> Messages;
            public string Request;
            public string Response;
            public int MaxTab;
            public int Tab;

            public RequestLog(string request)
            {
                this.Request = request;
                Time = DateTime.UtcNow;
                Messages = new List<Pair<int,string>>();
                MaxTab = 0;
                Tab = 0;
            }
            public string GetTab()
            {
                string tabs = "";
                for (int n = 0; n < Tab; n++)
                    tabs += '\t';
                //                tabs += "-----";
                return tabs;
            }
        }

        public static FixedSizeQueue Queue;
        public static Dictionary<object, RequestLog> requestLog;
        public static string filePath = "c:\\Users\\Edward\\";
        static System.IO.StreamWriter file;
        static RestRequest restReq;
        static int numBegunRequests = 0;
        public static void BeginRequest(string msg, object request)
        {
            if (numBegunRequests == 0)
            {
                object thread = System.Threading.Thread.CurrentThread.ManagedThreadId;
                var json = "";
                if(request != null)
                    json = restReq.JsonSerializer.Serialize(request);
                requestLog[thread] = new RequestLog(json);
            }
            numBegunRequests++;
            if (request != null)
                msg += ": Request = " + restReq.JsonSerializer.Serialize(request);
            if (msg.Length > 0)
                Logger.Log(msg);
            if (request != null)
                Logger.Tab();
        }

        public static void EndRequest(object response)
        {
            if (response != null)
                Logger.Untab();
            if (response != null)
                Logger.Log("EndRequest: Response = " + restReq.JsonSerializer.Serialize(response));
            numBegunRequests--;
            if (numBegunRequests == 0)
            {
                object thread = System.Threading.Thread.CurrentThread.ManagedThreadId;
                var json = "";
                if (response != null)
                    json = restReq.JsonSerializer.Serialize(response);
                requestLog[thread].Response = json;
                if (file == null)
                    Queue.Enqueue(requestLog[thread]);
                requestLog.Remove(thread);
            }
        }
        public static void Log(string message)
        {
            object thread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            if (!requestLog.ContainsKey(thread))
                throw new Exception("Log with no enclosing request");
            string str = requestLog[thread].GetTab() + message;
            if (file != null)
            {
                file.WriteLine(str);
                file.Flush();
            }
            requestLog[thread].Messages.Add(new Pair<int, string>(requestLog[thread].Tab*40, str));
        }

        public static void OpenLog()
        {
            requestLog = new Dictionary<object, RequestLog>();
            Queue = new FixedSizeQueue(300);
            restReq = new RestRequest();
        }
        static public void OpenLog(string filename)
        {
            requestLog = new Dictionary<object, RequestLog>();
            object thread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            requestLog[thread] = new RequestLog(null);
            //file = new System.IO.StreamWriter(("~/App_Data/" + filename).MapHostAbsolutePath());
            file = new System.IO.StreamWriter(filePath + filename);
            restReq = new RestRequest();
        }

        public static void CloseLog()
        {
            if (file != null)
            {
                file.Close();
                file = null;
            }
            Queue.Clear();
        }

        public static void Tab()
        {
            object thread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            var r = requestLog[thread];
            r.Tab++;
            if (r.MaxTab < r.Tab)
                r.MaxTab = r.Tab;
        }

        public static void Untab()
        {
            object thread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            requestLog[thread].Tab--;
        }
    }

}