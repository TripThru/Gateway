﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using ServiceStack.TripThruGateway;
using ServiceStack.TripThruPartnerGateway;
using Utils;


// Local
//using plugins;

namespace Program
{
    class Program
    {
        static Dictionary<string, Gateway> gatewayLookup = new Dictionary<string,Gateway>();
        static List<GatewayLocalClient> gatewayClientsToResolve = new List<GatewayLocalClient>();
        static GatewayClient GetGatewayClient(string accessToken, string callbackURL)
        {
            Gateway gateway = null;
            if (gatewayLookup.ContainsKey(callbackURL))
            {
                gateway = gatewayLookup[callbackURL];
                return new GatewayLocalClient(gateway);
            }
            GatewayLocalClient gatewayClient = new GatewayLocalClient(callbackURL);
            gatewayClientsToResolve.Add(gatewayClient);
            return gatewayClient;
        }

        static public void ResolveGatewayClients()
        {
            foreach (GatewayLocalClient gatewayClient in gatewayClientsToResolve)
            {
                foreach (string callbackUrl in gatewayLookup.Keys)
                {
                    if (callbackUrl == gatewayClient.callbackURL)
                    {
                        gatewayClient.gateway = gatewayLookup[callbackUrl];
                        break;
                    }
                }
            }
            gatewayClientsToResolve.Clear();
        }
        static ServiceStack.TripThruGateway.TripThru tripthru;

        static void Main(string[] args)
        {
            Logger.OpenLog("TripThruSimulation.log");

            MapTools.LoadGeoData("../../App_Data/Geo-Location-Names.csv", "../../App_Data/Geo-Routes.csv");
            MapTools.WriteGeoData("../../App_Data/Geo-Location-Names.csv", "../../App_Data/Geo-Routes.csv");
            string[] filePaths = Directory.GetFiles("../../Partner_Configurations/");
            string tripthruCallbackUrl = "";
            foreach (string filename in filePaths)
            {
                PartnerConfiguration configuration = ServiceStack.TripThruPartnerGateway.Partner.LoadPartnerConfigurationFromJsonFile(filename);
                tripthruCallbackUrl = configuration.TripThruUrl;
                break;
            }


            //gatewayLookup.Add(tripthru.)
            List<ServiceStack.TripThruPartnerGateway.Partner> partners = new List<ServiceStack.TripThruPartnerGateway.Partner>();
            tripthru = new ServiceStack.TripThruGateway.TripThru(GetGatewayClient);
            gatewayLookup.Add(tripthruCallbackUrl, tripthru);
            ResolveGatewayClients();

            foreach (string filename in filePaths)
            {
                PartnerConfiguration configuration = ServiceStack.TripThruPartnerGateway.Partner.LoadPartnerConfigurationFromJsonFile(filename);
                ServiceStack.TripThruPartnerGateway.Partner partner = new ServiceStack.TripThruPartnerGateway.Partner(GetGatewayClient, configuration.Partner.Name, configuration.Partner.ClientId, configuration.Partner.AccessToken, configuration.TripThruUrl, configuration.partnerFleets);
                tripthruCallbackUrl = configuration.TripThruUrl;
                partners.Add(partner);
                gatewayLookup.Add(configuration.Partner.CallbackUrl, partner);
                ResolveGatewayClients();
                partner.tripthru.RegisterPartner(
                    new Gateway.RegisterPartner.Request(configuration.Partner.ClientId, configuration.Partner.Name,
                        configuration.Partner.CallbackUrl, configuration.Partner.AccessToken));


            }
            MapTools.WriteGeoData("../../App_Data/Geo-Location-Names.csv", "../../App_Data/Geo-Routes.csv");



            Simulate(partners, DateTime.UtcNow + new TimeSpan(2, 30, 0));
            MapTools.WriteGeoData("../../App_Data/Geo-Location-Names.csv", "../../App_Data/Geo-Routes.csv");
        }
        public static void Simulate(List<ServiceStack.TripThruPartnerGateway.Partner> partners, DateTime until)
        {

            Logger.BeginRequest("", null);
            Logger.Log("Sim Configuration");
            Logger.Tab();
            foreach (ServiceStack.TripThruPartnerGateway.Partner p in partners)
                p.Log();
            Logger.Untab();
            Logger.EndRequest(null);

            TimeSpan simInterval = new TimeSpan(0, 0, 10);
            while (DateTime.UtcNow < until)
            {
                foreach (ServiceStack.TripThruPartnerGateway.Partner p in partners)
                    p.Simulate(until);
                MapTools.WriteGeoData("../../App_Data/Geo-Location-Names.csv", "../../App_Data/Geo-Routes.csv");
                System.Threading.Thread.Sleep(simInterval);
                tripthru.LogStats();
            }
            Logger.Untab();

        }
    }
}
