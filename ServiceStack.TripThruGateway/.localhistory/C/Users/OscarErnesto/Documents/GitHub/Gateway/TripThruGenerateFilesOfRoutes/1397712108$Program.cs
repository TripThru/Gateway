using System;
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
             List<PartnerConfiguration> partnerConfigurations = GetPartnersConfigurations();

            foreach (var partnerConfiguration in partnerConfigurations)
            {
                foreach (var possibleTrip in partnerConfiguration.Fleets.ElementAt(0).PossibleTrips)
                {
                    MapTools.GetRoute(possibleTrip.Start, possibleTrip.End);
                }
            }
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
