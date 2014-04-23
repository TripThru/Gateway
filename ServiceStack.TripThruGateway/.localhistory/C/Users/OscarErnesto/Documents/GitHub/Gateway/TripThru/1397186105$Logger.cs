using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using System.Threading;
using System.Text;
using Utils;
using RestSharp;
using ServiceStack.Text;
namespace Utils
{
    public class Logger
    {
        public class SplunkClient
        {
            private RestClient restClient;
            private string requestUrl;
            public FixedSizeQueue queue;
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
                this.Source = "gateway";
                this.MaxQueueItems = 100;
                this.queue = new FixedSizeQueue(MaxQueueItems);
                this.sendEvent = new ManualResetEvent(false);
                this.sendThread = new Thread(new ThreadStart(SendThread));

                string baseUrl = string.Format("https://{0}", Host);
                this.restClient = new RestClient
                {
                    BaseUrl = baseUrl,
                    Authenticator = new HttpBasicAuthenticator("x", AccessToken),
                    Timeout = 15000
                };

                this.requestUrl = string.Format("1/inputs/http?index={0}&sourcetype=storm_multi_line&host={1}&source={2}",
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
                this.requestUrl = string.Format("1/inputs/http?index={0}&sourcetype=storm_multi_line&host={1}&source={2}",
                    ProjectId,
                    Environment.MachineName,
                    source);
                if (!string.IsNullOrEmpty(TZ))
                    this.requestUrl += "&tz=" + TZ;
            }

            private void SendThread()
            {
                System.Threading.Thread.CurrentThread.IsBackground = true;
                var interval = new TimeSpan(0, 0, 5);
                while (true)
                {
                    try
                    {
                        if (queue.Count > 0)
                        {
                            var logEntries = new List<string>();
                            RequestLog requestLog = null;
                            lock (locker)
                            {
                                requestLog = queue.Dequeue();
                                if(requestLog.Messages.Count > 0)
                                    logEntries.Add(requestLog.Time.ToString("yyyy-MM-ddTHH:mm:ss"));
                                logEntries.AddRange(requestLog.Messages.Select(msg => msg.Text));
                                logEntries.AddRange(requestLog.Tags.Select(tag => tag.Name + "=" + tag.Value));
                            }

                            if (logEntries.Any())
                            {
                                var sb = new StringBuilder();

                                foreach (var logEntry in logEntries)
                                    sb.Append(logEntry + "\n");

                                var request = new RestRequest(this.requestUrl, Method.POST);
                                request.AddParameter("application/json", sb.ToString(), ParameterType.RequestBody);
                                request.AddHeader("content-type", "application/json");
                                restClient.ExecuteAsync(request, response =>
                                {
                                    if (response.ErrorMessage == null) return;
                                    Console.WriteLine("Splunk error: " + response.ErrorMessage + ", for: "+logEntries.First());
                                    if (response.ErrorMessage.Contains("SendFailure") || response.ErrorMessage.Contains("ReceiveFailure"))
                                    {
                                        Enqueue(requestLog);
                                    }
                                });
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
                lock (locker)
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
                    if (Count > 0)
                    {
                        while (Peek().Time < expired)
                            Dequeue();
                    }
                }
            }
        }

        public class Message
        {
            public Message(int indent, string text, string json = null)
            {
                this.Indent = indent;
                this.Text = text;
                this.Json = json;
            }

            public int Indent { get; set; }
            public string Text { get; set; }
            public string Json { get; set; }

        }

        public class Tag
        {
            public Tag(string name, string value)
            {
                this.Name = name;
                this.Value = value;
            }

            public string Name { get; set; }
            public string Value { get; set; }
        }

        public class RequestLog
        {
            public DateTime Time { get; set; }
            public List<Message> Messages { get; set; }
            public List<Tag> Tags { get; set; }
            public string Request { get; set; }
            public string Response { get; set; }
            public int MaxTab { get; set; }
            public int Tab { get; set; }
            public string tripID { get; set; }

            public RequestLog(string request, string tripID = null)
            {
                this.Request = request;
                Time = DateTime.UtcNow;
                Messages = new List<Message>();
                Tags = new List<Tag>();
                MaxTab = 0;
                Tab = 0;
                this.tripID = tripID;
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
        static readonly object locker = new object(); 
        public static FixedSizeQueue Queue;
        public static Dictionary<object, RequestLog> requestLog;
        public static string filePath = null;
        static System.IO.StreamWriter file;
        public static SplunkClient splunkClient;
        static RestRequest restReq;
        static Dictionary<object, int> numBegunRequests;
        static Dictionary<object, bool> threadsEnabled; 

        public static void BeginRequest(string msg, object request, string tripID = null)
        {
            lock (locker)
            {
                object thread = System.Threading.Thread.CurrentThread.ManagedThreadId;
                if (requestLog == null || (threadsEnabled.ContainsKey(thread) && !threadsEnabled[thread]))
                    return;
                if (!requestLog.ContainsKey(thread))
                {
                    var json = "";
                    if (request != null)
                        json = JsonSerializer.SerializeToString(request);
                    requestLog[thread] = new RequestLog(json, tripID);
                    numBegunRequests[thread] = 0;
                }

                if (!numBegunRequests.ContainsKey(thread))
                    numBegunRequests[thread] = 0;
                numBegunRequests[thread] = numBegunRequests[thread] + 1;
                threadsEnabled[thread] = true;
                Logger.Log(msg, request);
                Logger.Tab();
            }
        }

        public static void EndRequest(object response)
        {
            lock (locker)
            {
                object thread = System.Threading.Thread.CurrentThread.ManagedThreadId;
                if (requestLog == null || !threadsEnabled.ContainsKey(thread) ||
                    (threadsEnabled.ContainsKey(thread) && !threadsEnabled[thread]) ||
                    (numBegunRequests.ContainsKey(thread) && numBegunRequests[thread] == 0)
                )
                    return;

                Logger.Untab();
                if (response != null)
                    Logger.Log("Response", response);

                numBegunRequests[thread] = numBegunRequests[thread] - 1;
                if (splunkEnabled)
                    splunkClient.Enqueue(requestLog[thread]);
                if (numBegunRequests[thread] == 0)
                {
                    Logger.AddTag("Type", "INFO");
                    Logger.AddTag("Memory", (System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / 1048576).ToString() + "Mb");
                    var json = "";
                    if (response != null)
                        json = JsonSerializer.SerializeToString(response);
                    requestLog[thread].Response = json;
                    if (splunkEnabled)
                    {
                        splunkClient.Enqueue(requestLog[thread]);
                        Queue.Enqueue(requestLog[thread]);
                    }
                    requestLog.Remove(thread);
                    numBegunRequests.Remove(thread);
                }
            }
        }

        public static void AddTag(string name, string value)
        {
            lock (locker)
            {
                object thread = System.Threading.Thread.CurrentThread.ManagedThreadId;
                if (requestLog == null || !threadsEnabled.ContainsKey(thread) ||
                    (threadsEnabled.ContainsKey(thread) && !threadsEnabled[thread]))
                    return;
                if (!requestLog.ContainsKey(thread))
                    return;
                requestLog[thread].Tags.Add(new Tag(name, value));
            }
        }

        public static void LogDebug(string message, string detailed = null, Dictionary<string, string> tags = null)
        {
            lock (locker)
            {
                Console.WriteLine(message + (detailed != null ? " | " + detailed : "") + "\n\n");
                RequestLog error = new RequestLog("");
                error.Messages.Add(new Message(0, message));
                if (detailed != null)
                    error.Messages.Add(new Message(40, detailed));
                error.Tags.Add(new Tag("Type", "DEBUG"));
                error.Tags.Add(
                    new Tag("Memory", (System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / 1048576).ToString() + "Mb"));
                if (tags != null)
                    foreach(var key in tags.Keys)
                        error.Tags.Add(new Tag(key, tags[key]));
                error.Messages.Add(new Message(0, "End"));
                error.Response = "";
                if (splunkEnabled && splunkClient != null)
                    splunkClient.Enqueue(error);
            }
        }

        public static void Log(string message, object json = null)
        {
            lock (locker)
            {
                object thread = System.Threading.Thread.CurrentThread.ManagedThreadId;
                if (requestLog == null)
                    return;
                if (!requestLog.ContainsKey(thread))
                    BeginRequest(message, json);
                else
                {
                    string str = requestLog[thread].GetTab() + message;
                    if (file != null)
                    {
                        file.WriteLine(str);
                        file.Flush();
                    }
                    Console.WriteLine(thread + ":" + str);
                    var jsonString = json != null ? JsonSerializer.SerializeToString(json) : null;
                    requestLog[thread].Messages.Add(new Message(requestLog[thread].Tab * 40, str, jsonString));
                }
            }
		}

        public static void Log(Message message)
        {
            lock (locker)
            {
                object thread = System.Threading.Thread.CurrentThread.ManagedThreadId;
                if (requestLog == null || !threadsEnabled.ContainsKey(thread) ||
                    (threadsEnabled.ContainsKey(thread) && !threadsEnabled[thread]))
                    return;
                if (!requestLog.ContainsKey(thread))
                    throw new Exception("Log with no enclosing request");
                message.Indent = requestLog[thread].Tab * 40;
                requestLog[thread].Messages.Add(message);
            }
        }

        public static bool splunkEnabled = true;
        public static bool LogFileOpen() { return filePath != null;  }

        public static void OpenLog(string id, string filePath = null, bool splunkEnabled = true)
        {
            Logger.splunkEnabled = splunkEnabled;
            lock (locker)
            {
                requestLog = new Dictionary<object, RequestLog>();
                Queue = new FixedSizeQueue(300);
                numBegunRequests = new Dictionary<object, int>();
                threadsEnabled = new Dictionary<object, bool>();
                restReq = new RestRequest();
                // Create new Service object
                if (splunkEnabled)
                {
                    splunkClient = new SplunkClient();
                    splunkClient.SetSource(id);
                }
                if (filePath == null) return;
                Logger.filePath = filePath;
                file = new System.IO.StreamWriter(filePath + "TripThru-" + id + ".log");
            }
        }

        public static void CloseLog()
        {
            lock (locker)
            {
                if (requestLog == null)
                    return;
                if (file != null)
                {
                    file.Close();
                    file = null;
                }
                Queue.Clear();
                if (splunkEnabled)
                    splunkClient.Close();
            }
        }

        public static void Tab()
        {
            lock (locker)
            {
                object thread = System.Threading.Thread.CurrentThread.ManagedThreadId;
                if (requestLog == null || !threadsEnabled.ContainsKey(thread) ||
                    (threadsEnabled.ContainsKey(thread) && !threadsEnabled[thread]))
                    return;
                var r = requestLog[thread];
                if (r.Tab < 50)
                    r.Tab++;
                if (r.MaxTab < r.Tab)
                    r.MaxTab = r.Tab;
            }
        }

        public static void Untab()
        {
            lock (locker)
            {
                object thread = System.Threading.Thread.CurrentThread.ManagedThreadId;
                if (requestLog == null || !threadsEnabled.ContainsKey(thread) ||
                    (threadsEnabled.ContainsKey(thread) && !threadsEnabled[thread]))
                    return;
                if (requestLog[thread].Tab > 0)
                    requestLog[thread].Tab--;
            }
        }

        public static void Enable()
        {
            lock(locker)
            {
                if (threadsEnabled == null)
                    return;
                object thread = System.Threading.Thread.CurrentThread.ManagedThreadId;
                threadsEnabled[thread] = true;
            }
        }

        public static void Disable()
        {
            lock (locker)
            {
                if (threadsEnabled == null)
                    return;
                object thread = System.Threading.Thread.CurrentThread.ManagedThreadId;
                threadsEnabled[thread] = false;
            }
        }
    }

}