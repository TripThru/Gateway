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
        public class SplunkClient
        {
            private RestClient restClient;
            private string requestUrl;
            private Queue<RequestLog> queue;
            private ManualResetEvent sendEvent;
            private Thread sendThread;

            public string Host = "api.g8ck-fsze.data.splunkstorm.com";

            public string ProjectId = "66e6f24286c111e386ab123139018851";

            public string AccessToken = "X6DK4PaBNp9x3neWuFVfmrFPMXJQTWSEtQX5R8sprvMoRiiqXCyg2cDV6gvogFYWOgaEHqQPHfw=";

            public string Source { get; set; }

            public int MaxQueueItems;

            public string TZ = "America/Los_Angeles";



            public SplunkClient()
            {
                this.Source = "default";
                this.MaxQueueItems = 10000;

                this.queue = new Queue<RequestLog>(MaxQueueItems);
                this.sendEvent = new ManualResetEvent(false);
                this.sendThread = new Thread(new ThreadStart(SendThread));

                string baseUrl = string.Format("https://{0}", Host);
                this.restClient = new RestClient
                {
                    BaseUrl = baseUrl,
                    Authenticator = new HttpBasicAuthenticator("x", AccessToken),
                    Timeout = 15000
                };

                this.requestUrl = string.Format("1/inputs/http?index={0}&sourcetype=json_predefined_timestamp&host={1}&source={2}",
                    ProjectId,
                    Environment.MachineName,
                    Source);

                if (!string.IsNullOrEmpty(TZ))
                    this.requestUrl += "&tz=" + TZ;

                this.sendThread.Start();
            }

            public void SetSource(string source)
            {
                Source = source;
                this.requestUrl = string.Format("1/inputs/http?index={0}&sourcetype=json_predefined_timestamp&host={1}&source={2}",
                    ProjectId,
                    Environment.MachineName,
                    source);
                if (!string.IsNullOrEmpty(TZ))
                    this.requestUrl += "&tz=" + TZ;
            }

            private void SendThread()
            {
                System.Threading.Thread.CurrentThread.IsBackground = true;
                var interval = new TimeSpan(0, 0, 10);
                while (true)
                {
                    try
                    {
                        if (queue.Count > 0)
                        {
                            var logEntries = new List<string>();
                            lock (queue)
                            {
                                RequestLog requestLog = this.queue.Dequeue();
                                foreach (Pair<int, string> msg in requestLog.Messages)
                                    logEntries.Add(msg.Second);
                            }

                            if (logEntries.Any())
                            {
                                var sb = new StringBuilder();

                                foreach (var logEntry in logEntries)
                                    sb.Append(logEntry + "\n");

                                RestRequest request = new RestRequest(this.requestUrl, Method.POST);
                                request.AddParameter("application/json", sb.ToString(), ParameterType.RequestBody);
                                request.AddHeader("content-type", "application/json");
                                restClient.ExecuteAsync(request, response =>
                                {
                                    if (response.ErrorMessage != null) 
                                        Console.WriteLine("Splunk message error: "+response.ErrorMessage);
                                });
                            }
                            else
                            {
                                if (this.sendEvent.WaitOne(10000))
                                    this.sendEvent.Reset();
                            }
                        }
                    }
                    catch (ThreadAbortException)
                    {
                        // Ignore
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Splunk exception: " + ex);
                        Console.WriteLine("Splunk exception: " + ex);
                    }
                    Thread.Sleep(interval);
                }
            }

            public void Close()
            {
                this.sendThread.Abort();
            }


            public void Enqueue(RequestLog log)
            {
                lock (queue)
                {
                    this.queue.Enqueue(log);
                }
            }
        }
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
        static SplunkClient splunkClient;
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
                splunkClient.Enqueue(requestLog[thread]);
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
            // Create new Service object
            splunkClient = new SplunkClient();
        }
        public static void SetLogId(string id)
        {
            splunkClient.SetSource(id);
        }
        static public void OpenLog(string filename)
        {
            requestLog = new Dictionary<object, RequestLog>();
            object thread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            requestLog[thread] = new RequestLog(null);
            //file = new System.IO.StreamWriter(("~/App_Data/" + filename).MapHostAbsolutePath());
            file = new System.IO.StreamWriter(filePath + filename);
            restReq = new RestRequest();
            // Create new Service object
            splunkClient = new SplunkClient();
        }

        public static void CloseLog()
        {
            if (file != null)
            {
                file.Close();
                file = null;
            }
            Queue.Clear();
            splunkClient.Close();
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