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
    using MongoDB.Bson;
    using MongoDB.Driver;

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
            PartnerConfiguration configuration = TripThruCore.Partner.LoadPartnerConfigurationFromJsonFile("~/PartnerConfiguration.txt".MapHostAbsolutePath());

            if (configuration.host.debug)
            {
                StorageManager.OpenStorage(new MongoDbStorage("mongodb://localhost:27017/", configuration.Partner.ClientId));
            }
            else
            {
                StorageManager.OpenStorage(new MongoDbStorage("mongodb://SG-TripThru-3328.servers.mongodirector.com/", configuration.Partner.ClientId));
            }

            var account = StorageManager.GetPartnerAccountByClientId(configuration.Partner.ClientId);
            if (account != null)
            {
                configuration.Partner.AccessToken = account.AccessToken;
                configuration.Partner.Name = account.PartnerName;
                TripThruCore.Partner partner = new TripThruCore.Partner(configuration.Partner.ClientId, configuration.Partner.Name, new GatewayClient("TripThru", "TripThru", configuration.TripThruUrl ?? configuration.TripThruUrlMono, configuration.Partner.AccessToken), configuration.partnerFleets);

                GatewayService.gateway = partner;

                var sim = new SimulationThread(partner, configuration);
            }
            else
            {
                Console.WriteLine("Can not init partner, account not found");
            }
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

                var coverage = new List<Zone>();
                foreach (var fleet in _partner.PartnerFleets.Values)
                    coverage.AddRange(fleet.coverage);
                _partner.tripthru.RegisterPartner(
                    new Gateway.RegisterPartnerRequest(_configuration.Partner.Name,
                        _configuration.Partner.CallbackUrl ?? _configuration.Partner.CallbackUrlMono, 
                        _configuration.Partner.AccessToken, coverage));

                var lastHealthCheck = DateTime.UtcNow;
                Thread.Sleep(new TimeSpan(0,3,0)); //This Sleep to give other partners time to initialize
                Console.WriteLine("Simulation Start.");
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