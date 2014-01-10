using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using ServiceStack.Common.Utils;
using ServiceStack.Text;
using ServiceStack.TripThruGateway;
using ServiceStack.TripThruGateway.TripThru;

namespace ServiceStack.TripThruGateway
{
	using System;
	using System.Collections.Generic;
	using ServiceStack.OrmLite;
	using ServiceStack.ServiceHost;
	using ServiceStack.ServiceInterface;


	public class InitPartners : IReturn<InitPartnersResponse>
	{
	}
	public class InitPartnersResponse
	{
	}

	public class InitPartnersService : Service
	{
		
		public object Any(InitPartners request)
		{
            string partnerConfiguration = File.ReadAllText("~/PartnerConfiguration.txt".MapHostAbsolutePath());
		    var configuration = JsonSerializer.DeserializeFromString<PartnerConfiguration>(partnerConfiguration);

            MapTools.LoadGeoData();
            MapTools.WriteGeoData();
            var fleets = new List<PartnerFleet>();

		    foreach (var partnerFleet in configuration.Fleets)
		    {
		        var vehicleTypes = partnerFleet.VehicleTypes;

                var trips = new List<Pair<Location, Location>>();
		        foreach (var trip in partnerFleet.PossibleTrips)
		        {
                    trips.Add(new Pair<Location, Location>(new Location(trip.Start.Lat, trip.Start.Lng), new Location(trip.End.Lat, trip.End.Lng)));
		        }

		        var location = partnerFleet.Location;
		        var coverage = partnerFleet.Coverage;
                var drivers = partnerFleet.Drivers.Select(driver => new Driver(driver)).ToList();
		        var passengers = partnerFleet.Passengers.Select(passenger => new Passenger(passenger)).ToList();

                var fleet = new PartnerFleet(
                    name: partnerFleet.Name,
                    location: location,
                    coverage: coverage,
                    drivers: drivers,
                    vehicleTypes: vehicleTypes,
                    possibleTrips: trips,
                    baseCost: 3.00,
                    costPerMile: 2.70,
                    tripsPerHour: 200,
                    passengers: passengers);

                fleets.Add(fleet);
		    }
            
		    GatewayService.TripThruPartner = new TripThru.Partner(configuration.Partner.Name, configuration.Partner.ClientId, configuration.Partner.AccessToken, configuration.TripThruUrl, fleets);

            MapTools.WriteGeoData();

		    var sim = new SimulationThread(GatewayService.TripThruPartner, configuration.SimInterval);

			return new InitPartnersResponse();
		}
	}

    public class SimulationThread : IDisposable
    {
        private TripThru.Partner _partner;
        private int _simInterval;
        private Thread _worker;
        private volatile bool _workerTerminateSignal = false;

        public SimulationThread(TripThru.Partner partner, int simInterval)
        {
            this._partner = partner;
            this._simInterval = simInterval;
            _worker = new Thread(StartSimulation);
            _worker.Start();
        }

        private void StartSimulation()
        {
            while (!_workerTerminateSignal)
            {
                Logger.OpenLog("TripThruSimulation.log", true);
                Logger.Log("Sim Configuration");
                Logger.Tab();
                _partner.Log();
                Logger.Untab();

                Logger.Log("Simulation started at " + DateTime.UtcNow);
                Logger.Tab();
                var interval = new TimeSpan(0, 0, _simInterval);
                while (true)
                {
                    Logger.Log("Time = " + DateTime.UtcNow);
                    Logger.Tab();
                    lock (_partner)
                    {
                        _partner.Simulate(DateTime.UtcNow + interval);
                    }
                    MapTools.WriteGeoData();
                    System.Threading.Thread.Sleep(interval);
                    Logger.Untab();
                }
            }
        }

        public void Dispose()
        {
            if (_worker != null)
            {
                _workerTerminateSignal = true;
                if (!_worker.Join(TimeSpan.FromMinutes(1)))
                {
                    Console.WriteLine("Worker busy, aborting the simulation thread.");
                    _worker.Abort();
                }
                _worker = null;
            }
        }
    }

    public class PartnerConfiguration
    {
        public ConfigPartner Partner { get; set; }
        public List<Fleet> Fleets { get; set; }
        public string TripThruUrl { get; set; }
        public int SimInterval { get; set; }

        public class ConfigPartner
        {
            public string Name { get; set; }
            public string ClientId { get; set; }
            public string AccessToken { get; set; }
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