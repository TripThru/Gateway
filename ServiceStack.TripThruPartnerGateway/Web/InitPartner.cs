using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using ServiceStack.Common.Utils;
using ServiceStack.Text;
using ServiceStack.TripThruGateway;
using TripThruCore;
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
        public object Any(IReturn<InitPartner> request)
		{
            MapTools.LoadGeoData("~/App_Data/Geo-Location-Names.csv".MapHostAbsolutePath(), "~/App_Data/Geo-Routes.csv".MapHostAbsolutePath(), "~/App_Data/Geo-Location-Addresses.csv".MapHostAbsolutePath());
            MapTools.WriteGeoData("~/App_Data/Geo-Location-Names.csv".MapHostAbsolutePath(), "~/App_Data/Geo-Routes.csv".MapHostAbsolutePath());
            PartnerConfiguration configuration = TripThruCore.Partner.LoadPartnerConfigurationFromJsonFile("~/PartnerConfiguration.txt".MapHostAbsolutePath());

            TripThruCore.Partner partner = new TripThruCore.Partner(configuration.Partner.ClientId, configuration.Partner.Name, new GatewayClient("TripThru", "TripThru", configuration.Partner.AccessToken, configuration.TripThruUrl), configuration.partnerFleets);

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
        private TripThruCore.Partner _partner;
        private PartnerConfiguration _configuration;
        private Thread _worker;
        private volatile bool _workerTerminateSignal = false;

        public SimulationThread(TripThruCore.Partner partner, PartnerConfiguration configuration)
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
                Logger.Log("Sim Configuration");
                _partner.Log();

                Logger.Log("Simulation started at " + DateTime.UtcNow);
                var interval = new TimeSpan(0, 0, _configuration.SimInterval);

                _partner.tripthru.RegisterPartner(
                    new GatewayClient(_configuration.Partner.ClientId, _configuration.Partner.Name, _configuration.Partner.AccessToken, _configuration.Partner.CallbackUrl));

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
}