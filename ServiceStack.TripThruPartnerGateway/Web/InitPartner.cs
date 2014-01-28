using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using ServiceStack.Common.Utils;
using ServiceStack.Text;
using ServiceStack.TripThruGateway;
using Utils;

namespace ServiceStack.TripThruPartnerGateway
{
	using System;
	using System.Collections.Generic;
	using ServiceStack.OrmLite;
	using ServiceStack.ServiceHost;
	using ServiceStack.ServiceInterface;

    public class InitPartner : IReturn<InitPartnerResponse>
    {
    }
    public class InitPartnerResponse
    {
    }

	public class InitPartnerService : Service
	{
        GatewayClient GetGatewayClient(string accessToken, string callbackURL)
        {
            return new GatewayRestClient(accessToken, callbackURL);
        }

        public object Any(IReturn<InitPartner> request)
		{
            MapTools.LoadGeoData("~/App_Data/Geo-Location-Names.csv".MapHostAbsolutePath(), "~/App_Data/Geo-Routes.csv".MapHostAbsolutePath());
            MapTools.WriteGeoData("~/App_Data/Geo-Location-Names.csv".MapHostAbsolutePath(), "~/App_Data/Geo-Routes.csv".MapHostAbsolutePath());
            PartnerConfiguration configuration = Partner.LoadPartnerConfigurationFromJsonFile("~/PartnerConfiguration.txt".MapHostAbsolutePath());

            Partner partner = new Partner(GetGatewayClient, configuration.Partner.Name, configuration.Partner.ClientId, configuration.Partner.AccessToken, configuration.TripThruUrl, configuration.partnerFleets);

            GatewayService.gateway = partner;

            MapTools.WriteGeoData("~/App_Data/Geo-Location-Names.csv".MapHostAbsolutePath(), "~/App_Data/Geo-Routes.csv".MapHostAbsolutePath());

            Db.CreateTableIfNotExists<User>();
            Db.DeleteAll<User>();

            var tripthru = new User
            {
                UserName = "TripThru",
                Password = "password",
                Email = "tripthru@tripthru.com",
                AccessToken = "jaosid1201231",
                RefreshToken = "jaosid1201231",
                ClientId = "TripThru",
                ClientSecret = "23noiasdn2123"
            };
            Db.Insert(tripthru);

		    var sim = new SimulationThread(partner, configuration);
            return new InitPartnerResponse();

		}
	}

    public class SimulationThread : IDisposable
    {
        private Partner _partner;
        private PartnerConfiguration _configuration;
        private Thread _worker;
        private volatile bool _workerTerminateSignal = false;

        public SimulationThread(Partner partner, PartnerConfiguration configuration)
        {
            this._partner = partner;
            this._configuration = configuration;
            _worker = new Thread(StartSimulation);
            _worker.Start();
        }

        private void StartSimulation()
        {
            try
            {
                Console.WriteLine(_partner.name + ": sim start");
                Logger.OpenLog();
                Logger.BeginRequest("Simulation started at " + DateTime.UtcNow, null);
                Logger.Log("Sim Configuration");
                _partner.Log();
                Logger.EndRequest(null);
                var interval = new TimeSpan(0, 0, _configuration.SimInterval);

                _partner.tripthru.RegisterPartner(
                    new Gateway.RegisterPartner.Request(_configuration.Partner.ClientId, _configuration.Partner.Name,
                        _configuration.Partner.CallbackUrl, _configuration.Partner.AccessToken));

                while (true)
                {
                    lock (_partner)
                    {
                        _partner.Simulate(DateTime.UtcNow + interval);
                    }
                    MapTools.WriteGeoData("~/App_Data/Geo-Location-Names.csv".MapHostAbsolutePath(), "~/App_Data/Geo-Routes.csv".MapHostAbsolutePath());
                    System.Threading.Thread.Sleep(interval);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(_partner.name+", error :"+e.Message);
            }
        }

        public void Dispose()
        {
            Console.WriteLine(_partner.name+": simulation ended");
        }
    }

    public class PartnerConfiguration
    {
        public ConfigPartner Partner { get; set; }
        public List<Fleet> Fleets { get; set; }
        public string TripThruUrl { get; set; }
        public int SimInterval { get; set; }
        public List<PartnerFleet> partnerFleets;

        public class ConfigPartner
        {
            public string Name { get; set; }
            public string ClientId { get; set; }
            public string AccessToken { get; set; }
            public string CallbackUrl { get; set; }
        }

        public class Fleet
        {
            public string Name { get; set; }
            public Double BaseCost { get; set; }
            public Double CostPerMile { get; set; }
            public int TripsPerHour { get; set; }
            public List<Trip> PossibleTrips { get; set; }
            public List<VehicleType> VehicleTypes { get; set; }
            public List<string> Drivers { get; set; }
            public List<string> Passengers { get; set; }
            public Location Location { get; set; }
            public List<Zone> Coverage { get; set; }

        }

        public class Trip
        {
            public Location Start { get; set; }
            public Location End { get; set; }
        }
    }
}