using System;
using System.Collections.Generic;
using System.IO;
using Utils;
using TripThruCore;
using ServiceStack.Redis;
using System.Linq.Expressions;


// Local
//using plugins;

namespace Program
{
    class Program
    {
        static TripThru tripthru;

        public static class MemberInfoGetting
        {
            public static string GetMemberName<T>(Expression<Func<T>> memberExpression)
            {
                MemberExpression expressionBody = (MemberExpression)memberExpression.Body;
                return expressionBody.Member.Name;
            }
        }

        public class Test
        {
            public string code { get; set; }
            public string Add()
            {
                string s = MemberInfoGetting.GetMemberName(() => this);
                return s;
            }
        }

        public class RedisList<T> : List<T>
        {
            RedisClient client;
            string id;
            public RedisList(string id)
            {
                this.id = id;
            }
        }


        static void Main(string[] args)
        {
            Logger.OpenLog("", "C:\\Users\\Edward\\");
            string[] filePaths = Directory.GetFiles("../../Partner_Configurations/");

            tripthru = new TripThru();


            List<Gateway> partners = new List<Gateway>();
            tripthru = new TripThru();


            foreach (string filename in filePaths)
            {
                if (filename.Contains("Luxor"))
                {
                    PartnerConfiguration configuration = Partner.LoadPartnerConfigurationFromJsonFile(filename);
                    Partner partner = new Partner(configuration.Partner.ClientId, configuration.Partner.Name, tripthru,
                        configuration.partnerFleets);
                    partners.Add(new GatewayLocalClient(partner));
                    tripthru.RegisterPartner(partner);
                }
            }



            Simulate(partners, DateTime.UtcNow + new TimeSpan(2, 30, 0));
        }

        public static void Simulate(List<Gateway> partners, DateTime until)
        {

            Logger.BeginRequest("", null);
            Logger.Log("Sim Configuration");
            Logger.Tab();
            foreach (Gateway p in partners)
                p.Log();
            Logger.Untab();
            Logger.EndRequest(null);

            TimeSpan simInterval = new TimeSpan(0, 0, 10);
            while (DateTime.UtcNow < until)
            {
                Logger.BeginRequest("Heartbeat", null);
                tripthru.Update();
                Logger.EndRequest(null);
                System.Threading.Thread.Sleep(simInterval);
                tripthru.LogStats();
            }
            Logger.Untab();

        }
    }
}
