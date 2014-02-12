using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Utils;
using TripThruCore;
using CustomIntegrations;


// Local
//using plugins;

namespace Program
{
    class Program
    {
        static TripThru tripthru;

        static void Main(string[] args)
        {
            Logger.OpenLog("TripThruSimulation.log");
            MapTools.LoadGeoData("../../App_Data/Geo-Location-Names.csv", "../../App_Data/Geo-Routes.csv", "../../App_Data/Geo-Location-Addresses.csv");

            MapTools.WriteGeoData("../../App_Data/Geo-Location-Names.csv", "../../App_Data/Geo-Routes.csv", "../../App_Data/Geo-Location-Addresses.csv");
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
            MapTools.WriteGeoData("../../App_Data/Geo-Location-Names.csv", "../../App_Data/Geo-Routes.csv", "../../App_Data/Geo-Location-Addresses.csv");



            Simulate(partners, DateTime.UtcNow + new TimeSpan(2, 30, 0));
            MapTools.WriteGeoData("../../App_Data/Geo-Location-Names.csv", "../../App_Data/Geo-Routes.csv", "../../App_Data/Geo-Location-Addresses.csv");
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
                MapTools.WriteGeoData("../../App_Data/Geo-Location-Names.csv", "../../App_Data/Geo-Routes.csv", "../../App_Data/Geo-Location-Addresses.csv");
                System.Threading.Thread.Sleep(simInterval);
                tripthru.LogStats();
            }
            Logger.Untab();

        }
    }
}
