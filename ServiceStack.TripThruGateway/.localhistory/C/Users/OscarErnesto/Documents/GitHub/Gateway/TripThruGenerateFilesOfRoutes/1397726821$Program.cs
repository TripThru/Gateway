﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TripThruCore;
using Utils;

namespace TripThruGenerateFilesOfRoutes
{
    class Program
    {
        static void Main(string[] args)
        {
            Dictionary<string, Route> routes;
            List<PartnerConfiguration> partnerConfigurations = GetPartnersConfigurations();

            foreach (var partnerConfiguration in partnerConfigurations)
            {
                foreach (var possibleTrip in partnerConfiguration.Fleets.ElementAt(0).PossibleTrips)
                {
                    MapTools.GetRoute(possibleTrip.Start, possibleTrip.End);
                }
            }

            var routesString = JsonConvert.SerializeObject(MapTools.routes);
            var locationNamesString = JsonConvert.SerializeObject(MapTools.locationNames);
            var locationAddresses = JsonConvert.SerializeObject(MapTools.locationAddresses);

            File.WriteAllText("App_Data\\Geo-Routes.txt", String.Empty);
            using (StreamWriter sr = new StreamWriter("App_Data\\Geo-Routes.txt"))
            {
                sr.Write(routesString);
            }
            File.WriteAllText("App_Data\\Geo-Location-Names.txt", String.Empty);
            using (StreamWriter sr = new StreamWriter("App_Data\\Geo-Location-Names.txt"))
            {
                sr.Write(locationNamesString);
            }
            File.WriteAllText("App_Data\\Geo-Location-Addresses.txt", String.Empty);
            using (StreamWriter sr = new StreamWriter("App_Data\\Geo-Location-Addresses.txt"))
            {
                sr.Write(locationAddresses);
            }
            int ocho = 9;
        }

        private static List<PartnerConfiguration> GetPartnersConfigurations()
        {
            List<PartnerConfiguration> partnerConfigurations = new List<PartnerConfiguration>();
            string[] partnerConfigurationsFiles = Directory.GetFiles("PartnerConfigurations/", "*.txt");
            foreach (var partnerConfigurationFile in partnerConfigurationsFiles)
            {
                var configuration =
                JsonConvert.DeserializeObject<PartnerConfiguration>(File.ReadAllText(partnerConfigurationFile));
                partnerConfigurations.Add(configuration);
            }
            return partnerConfigurations;
        }
    }
}
