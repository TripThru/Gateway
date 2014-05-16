using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using ServiceStack.Common.Utils;
using ServiceStack.Text;
using ServiceStack.TripThruGateway;
using TripThruCore;
using Utils;
using TripThruCore.Storage;

namespace ServiceStack.TripThruPartnerGateway
{
	using System;
	using System.Collections.Generic;
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
            StorageManager.OpenStorage(new SqliteStorage("~/App_Data/db.sqlite".MapHostAbsolutePath()));
            MapTools.SetGeodataFilenames("~/App_Data/Geo-Location-Names.txt".MapHostAbsolutePath(), "~/App_Data/Geo-Routes.txt".MapHostAbsolutePath(), "~/App_Data/Geo-Location-Addresses.txt".MapHostAbsolutePath());
            MapTools.LoadGeoData();
            MapTools.WriteGeoData();
            PartnerConfiguration configuration = TripThruCore.Partner.LoadPartnerConfigurationFromJsonFile("~/PartnerConfiguration.txt".MapHostAbsolutePath());

            TripThruCore.Partner partner = new TripThruCore.Partner(configuration.Partner.ClientId, configuration.Partner.Name, new GatewayClient("TripThru", "TripThru", configuration.Partner.AccessToken, configuration.TripThruUrl ?? configuration.TripThruUrlMono), configuration.partnerFleets);

            GatewayService.gateway = partner;

            MapTools.WriteGeoData();

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
                Logger.OpenLog(_partner.name);
                Logger.Disable();
                //Logger.OpenLog(_partner.name, "c:\\Users\\Edward\\");
                Logger.BeginRequest("Simulation started", null);
                Logger.Log("Sim Configuration");
                _partner.Log();
                Logger.Log("Simulation started at " + DateTime.UtcNow);
                Logger.EndRequest(null);
                var interval = new TimeSpan(0, 0, _configuration.SimInterval);

                _partner.tripthru.RegisterPartner(
                    new Gateway.RegisterPartnerRequest(_configuration.Partner.ClientId, _configuration.Partner.Name,
                        _configuration.Partner.CallbackUrl ?? _configuration.Partner.CallbackUrlMono, _configuration.Partner.AccessToken));

                var lastHealthCheck = DateTime.UtcNow;
                Thread.Sleep(3000); //This Sleep to give other partners time to initialize
                while (true)
                {
                    try
                    {
                        lock (_partner)
                        {
                            _partner.Update();
                            if (DateTime.UtcNow > lastHealthCheck + new TimeSpan(0, 1, 0))
                            {
                                _partner.HealthCheck();
                                lastHealthCheck = DateTime.UtcNow;
                            }
                        }
                        MapTools.WriteGeoData();
                    }
                    catch (Exception e)
                    {
                        Logger.LogDebug(_partner.name + ", simulation cycle error :" + e.Message, e.StackTrace);
                    }
                    System.Threading.Thread.Sleep(interval);
                }
            }
            catch (Exception e)
            {
                Logger.LogDebug(_partner.name + ", simulation start error :" + e.Message, e.StackTrace);
            }
        }

        public void Dispose()
        {
            Logger.LogDebug(_partner.name + ": simulation ended");
        }
    }
}