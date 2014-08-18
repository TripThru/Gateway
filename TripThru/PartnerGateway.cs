using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using ServiceStack.Common.Utils;
using ServiceStack.Text;
using Utils;
using TripThruCore.Storage;
using System.Threading;

namespace TripThruCore
{
    public class Partner : GatewayServer
    {
        static readonly object locker = new object();

        public readonly Dictionary<string, PartnerTrip> tripsByID;
        public readonly Dictionary<string, PartnerFleet> PartnerFleets;
        public DateTime lastSim;
        public readonly Gateway tripthru;
        // Configuration parameters
        public readonly TimeSpan simInterval = new TimeSpan(0, 0, 10);
        public readonly TimeSpan updateInterval = new TimeSpan(0, 0, 30); // for simluation
        public string preferedPartnerId = null;


        static long nextID = 0;
        public override string GetName(string clientID)
        {
            return tripthru.name;
        }
        static public PartnerConfiguration LoadPartnerConfigurationFromJsonFile(string filename)
        {
            string partnerConfiguration = File.ReadAllText(filename);
            var configuration = JsonSerializer.DeserializeFromString<PartnerConfiguration>(partnerConfiguration);

            configuration.partnerFleets = new List<PartnerFleet>();

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
                var drivers = partnerFleet.Drivers != null ? partnerFleet.Drivers.Select(driver => new Driver(driver)).ToList() : new List<Driver>();
                var passengers = partnerFleet.Passengers.Select(passenger => new Passenger(passenger)).ToList();
                if (coverage == null)
                    coverage = new List<Zone>();
                if (vehicleTypes == null)
                    vehicleTypes = new List<VehicleType>();

                string urlTrips = null;
                if (configuration.UrlsTrips != null)
                    urlTrips = configuration.UrlsTrips.MapHostAbsolutePath();

                var fleet = new PartnerFleet(
                    name: partnerFleet.Name,
                    location: location,
                    coverage: coverage,
                    drivers: drivers,
                    vehicleTypes: vehicleTypes,
                    possibleTrips: trips,
                    urlTripsFile: urlTrips,
                    baseCost: partnerFleet.BaseCost,
                    costPerMile: partnerFleet.CostPerMile,
                    tripsPerHour: partnerFleet.TripsPerHour,
                    passengers: passengers);

                configuration.partnerFleets.Add(fleet);
            }
            return configuration;

        }

        public override GetPartnerInfoResponse GetPartnerInfo(GetPartnerInfoRequest request)
        {
            requests++;
            List<VehicleType> vehicleTypes = new List<VehicleType>();
            foreach (PartnerFleet f in this.PartnerFleets.Values)
                vehicleTypes.AddRange(f.vehicleTypes);
            List<Fleet> PartnerFleets = new List<Fleet>();
            foreach (PartnerFleet f in this.PartnerFleets.Values)
                PartnerFleets.Add(new Fleet
                {
                    FleetId = f.ID,
                    FleetName = f.name,
                    PartnerId = this.ID,
                    PartnerName = this.name,
                    Coverage = f.coverage
                });
            GetPartnerInfoResponse response = new GetPartnerInfoResponse(PartnerFleets, vehicleTypes);
            return response;
        }
        public PartnerTrip GetTrip(DispatchTripRequest r, bool autoDispatch = true)
        {
            return new PartnerTrip(
                partner: this,
                ID: r.tripID,
                origination: PartnerTrip.Origination.Foreign,
                pickupLocation: r.pickupLocation,
                pickupTime: r.pickupTime,
                paymentMethod: r.paymentMethod,
                passengerID: r.passengerID,
                passengerName: r.passengerName,
                dropoffLocation: r.dropoffLocation,
                waypoints: r.waypoints,
                vehicleType: r.vehicleType,
                maxPrice: r.maxPrice,
                minRating: r.minRating,
                fleet: r.fleetID == null ? null : this.PartnerFleets[r.fleetID],
                driver: r.driverID == null ? null : this.PartnerFleets[r.fleetID].drivers[r.driverID],
                autoDispatch: autoDispatch
                );
        }
        public override DispatchTripResponse DispatchTrip(DispatchTripRequest r)
        {
            DispatchTripResponse response;
            {
                requests++;
                r.tripID = PartnerFleet.GetPrivateID(r.tripID, this.ID);
                if (r.partnerID != null && r.partnerID != ID)
                {
                    Logger.Log("Dispatching trip to partner " + r.partnerID);
                    PartnerTrip trip = GetTrip(r, autoDispatch: false);
                    trip.origination = PartnerTrip.Origination.Local;
                    PartnerFleets.FirstOrDefault().Value.QueueTrip(trip);
                    if (TryToDispatchToForeignProvider(trip, r.partnerID))
                        response = new DispatchTripResponse(result: Result.OK);
                    else
                        response = new DispatchTripResponse(result: Result.Rejected);
                }
                else if (r.driverID != null)
                    response = DispatchToSpecificDriver(r);
                else if (r.fleetID != null)
                    response = DispatchToSpecificFleet(r);
                else
                    response = DispatchToFirstFleetThatServes(r);
            }
            return response;
        }

        private DispatchTripResponse DispatchToSpecificDriver(DispatchTripRequest r)
        {
            throw new Exception("Dispatch to specific driver not yet supported");
        }

        private DispatchTripResponse DispatchToSpecificFleet(DispatchTripRequest r)
        {
            Logger.Log("Dispatching to fleet " + r.fleetID);
            PartnerFleet f = PartnerFleets[r.fleetID];
            if (f.FleetServesLocation(r.pickupLocation))
            {
                PartnerTrip trip = GetTrip(r);
                if (f.QueueTrip(trip))
                {
                    DispatchTripResponse response = new DispatchTripResponse();
                    Logger.Log("DispatchTrip successful on " + name + ", Response: " + response);
                    Logger.SetServicingId(this.ID);
                    return response;
                }
            }
            return new DispatchTripResponse(result: Result.Rejected);
        }
        private DispatchTripResponse DispatchToFirstFleetThatServes(DispatchTripRequest r)
        {
            Logger.Log("Dispatching to first fleet that serves");
            DispatchTripResponse response = new DispatchTripResponse(result: Result.Rejected);
            foreach (PartnerFleet f in PartnerFleets.Values)
            {
                if (!f.FleetServesLocation(r.pickupLocation))
                    continue;
                PartnerTrip trip = GetTrip(r);
                if (f.QueueTrip(trip))
                {
                    DispatchTripResponse resp = new DispatchTripResponse();
                    Logger.Log("DispatchTrip successful on " + name + ", Response: " + resp);
                    return resp;
                }
            }
            return response;
        }
        public bool CreateLocalTripInTripThru(PartnerTrip trip)
        {
            return TryToDispatchToForeignProvider(trip, this.ID);
        }
        public bool TryToDispatchToForeignProvider(PartnerTrip trip, string partnerID = null)
        {
            Logger.Log("TryToDispatchToForeignProvider: partnerID = " + partnerID);
            Logger.Tab();
            Gateway.DispatchTripRequest request = new Gateway.DispatchTripRequest(
                clientID: ID,
                tripID: PartnerTrip.GetPublicID(trip.ID, this.ID),
                pickupLocation: trip.pickupLocation,
                pickupTime: trip.pickupTime,
                passengerID: trip.passengerID,
                passengerName: trip.passengerName,
                luggage: trip.luggage,
                persons: trip.persons,
                dropoffLocation: trip.dropoffLocation,
                waypoints: trip.waypoints,
                paymentMethod: trip.paymentMethod,
                vehicleType: trip.vehicleType,
                maxPrice: trip.maxPrice,
                minRating: trip.minRating,
                partnerID: partnerID == null ? preferedPartnerId : partnerID
                );
            Gateway.DispatchTripResponse response = tripthru.DispatchTrip(request);
            if (response.result == Gateway.Result.OK)
            {
                if (partnerID == this.ID)
                    Logger.Log("Local trip was successfully created in TripThru");
                else
                {
                    // Actually, at this point its just in the partner's queue, until its dispatch to the partner's drivers -- so no status update. 
                    trip.service = PartnerTrip.Origination.Foreign;
                    Logger.Log("Trip was successfully dispatched through TripThru");
                }

            }
            else
                Logger.Log("Trip was rejected by TripThru");
            Logger.Untab();
            return response.result == Gateway.Result.OK;
        }

        public PartnerTrip GetTrip(QuoteTripRequest r)
        {
            return new PartnerTrip(this, null, PartnerTrip.Origination.Foreign, r.pickupLocation, r.pickupTime, r.paymentMethod, r.passengerID, r.passengerName, r.dropoffLocation,
                null, r.waypoints, r.vehicleType, r.maxPrice, r.minRating, r.fleetID == null ? null : this.PartnerFleets[r.fleetID], r.driverID == null ? null : this.PartnerFleets[r.fleetID].drivers[r.driverID]);
        }
        public override QuoteTripResponse QuoteTrip(QuoteTripRequest r)
        {
            requests++;
            new Thread(() => CreateQuoteAndForwardToPartner(r)).Start();
            return new Gateway.QuoteTripResponse();
        }
        private void CreateQuoteAndForwardToPartner(QuoteTripRequest r)
        {
            Thread.Sleep(1000);
            List<Quote> quotes = new List<Quote>();
            foreach (PartnerFleet f in PartnerFleets.Values)
            {
                if (!f.FleetServesLocation(r.pickupLocation))
                    continue;
                foreach (VehicleType vehicleType in f.vehicleTypes)
                {
                    if (r.vehicleType == vehicleType || r.vehicleType == null)
                    {
                        PartnerTrip trip = GetTrip(r);
                        trip.vehicleType = vehicleType;
                        quotes.Add(new Quote(
                            partnerID: ID,
                            partnerName: name,
                            fleetID: f.ID, fleetName: f.name,
                            vehicleType: vehicleType,
                            price: trip.dropoffLocation == null ? (double?)null : f.GetPrice(trip),
                            distance: trip.dropoffLocation == null ? (double?)null : f.GetDistance(trip),
                            duration: trip.duration,
                            ETA: f.GetPickupETA(trip)));
                    }
                }
            }
            var request = new UpdateQuoteRequest(clientID: this.ID, tripId: r.tripId, quotes: quotes);
            Logger.BeginRequest("Sending quote update to TripThru. TripId: " + r.tripId, request);
            var response = this.tripthru.UpdateQuote(request);
            Logger.EndRequest(response);
        }

        public override GetTripsResponse GetTrips(GetTripsRequest r)
        {
            var trips = new List<Trip>();
            if (activeTrips.Count > 0)
            {
                if (r.status != null)
                    trips.AddRange(activeTrips.Values.Where(trip => trip.Status == r.status).ToList());
                else
                    trips.AddRange(activeTrips.Values);
                foreach (var trip in trips)
                    trip.Id = PartnerFleet.GetPublicID(trip.Id, this.ID);
            }
            return new GetTripsResponse(trips);
        }
        public override GetTripStatusResponse GetTripStatus(GetTripStatusRequest r)
        {
            GetTripStatusResponse response;
            {
                requests++;
                r.tripID = PartnerFleet.GetPrivateID(r.tripID, this.ID);
                if (!tripsByID.ContainsKey(r.tripID))
                {
                    Logger.Log("Trip " + r.tripID + " not found");
                    response = new GetTripStatusResponse(result: Result.NotFound);
                }
                else
                {

                    Logger.SetServicingId(this.ID);
                    PartnerTrip t = tripsByID[r.tripID];
                    //lock (t)
                    //{
                    DateTime? pickupTime = null;
                    if (t.status == Status.PickedUp || t.status == Status.DroppedOff || t.status == Status.Complete)
                        pickupTime = t.pickupTime; // Only if trip has been pickedup.

                    if (t.price == null && t.PartnerFleet != null)
                        t.price = t.PartnerFleet.GetPrice(t);

                    double? distance = null;
                    if (t.PartnerFleet != null)
                        distance = t.PartnerFleet.GetDistance(t);

                    double? driverRouteDuration = null;
                    if (t.driver != null && t.driver.route != null)
                        driverRouteDuration = t.driver.route.duration.TotalSeconds;

                    t.lastStatusNotifiedToPartner = t.status;

                    response = new GetTripStatusResponse(
                        partnerID: ID,
                        partnerName: name,
                        fleetID: t.PartnerFleet != null ? t.PartnerFleet.ID : null,
                        fleetName: t.PartnerFleet != null ? t.PartnerFleet.name : null,
                        pickupTime: pickupTime,
                        pickupLocation: t.pickupLocation,
                        driverID: t.driver != null ? t.driver.ID : null,
                        driverName: t.driver != null ? t.driver.name : null,
                        driverLocation: t.driver != null ? t.driver.location : null,
                        driverInitialLocation: t.driverInitiaLocation ?? null,
                        dropoffTime: t.dropoffTime,
                        dropoffLocation: t.dropoffLocation,
                        vehicleType: t.vehicleType,
                        ETA: t.ETA,
                        distance: distance,
                        driverRouteDuration: driverRouteDuration,
                        price: t.price,
                        status: t.status,
                        passengerName: t.passengerName
                    );
                    //}
                }
            }
            return response;
        }
        public override UpdateTripStatusResponse UpdateTripStatus(UpdateTripStatusRequest r)
        {
            // Note: GetTrip populates the foreignTripID
            UpdateTripStatusResponse response;
            {
                requests++;
                r.tripID = PartnerFleet.GetPrivateID(r.tripID, this.ID);
                if (!tripsByID.ContainsKey(r.tripID))
                {
                    response = new UpdateTripStatusResponse(result: Result.NotFound);
                }
                else
                {
                    PartnerTrip t = tripsByID[r.tripID];
                    //lock (t)
                    //{
                    response = new UpdateTripStatusResponse();
                    t.UpdateTripStatus(notifyPartner: false, status: r.status, driverLocation: r.driverLocation, eta: r.eta);
                    //}
                    Logger.SetServicingId(this.ID);
                }
            }
            return response;
        }

        public Partner(string ID, string name, Gateway tripthru, List<PartnerFleet> PartnerFleets = null, string preferedPartnerId = null)
            : base(ID, name)
        {
            this.tripthru = tripthru;
            this.preferedPartnerId = preferedPartnerId;
            this.PartnerFleets = new Dictionary<string, PartnerFleet>();
            if (PartnerFleets != null)
            {
                foreach (PartnerFleet f in PartnerFleets)
                    AddPartnerFleet(f);
            }

            tripsByID = new Dictionary<string, PartnerTrip>();

            partnerAccounts.Clear();
            clientIdByAccessToken.Clear();

            var accounts = StorageManager.GetPartnerAccounts();
            if (accounts != null)
                foreach (PartnerAccount account in accounts)
                {
                    if (Storage.Storage.UserRole.partner != account.Role && account.ClientId != "TripThru")
                        continue;
                    if (!partnerAccounts.ContainsKey(account.ClientId))
                        partnerAccounts[account.ClientId] = account;
                    if (!clientIdByAccessToken.ContainsKey(account.AccessToken))
                        clientIdByAccessToken[account.AccessToken] = account.ClientId;
                }

            PartnerTrip.nextID = base.activeTrips.lastID;
        }
        public override void Log()
        {
            Logger.Log("Partner = " + name + ": SimRate = " + simInterval + ", StatusUpdateRate = " + updateInterval);
            Logger.Tab();
            Logger.Log("PartnerFleets:");
            Logger.Tab();
            foreach (PartnerFleet f in this.PartnerFleets.Values)
                f.Log();
            Logger.Untab();
            Logger.Untab();
        }
        public void AddPartnerFleet(PartnerFleet f)
        {
            f.partner = this;
            PartnerFleets.Add(f.ID, f);
        }
        // speed is miles per hour
        public void GetTripStatusFromForeignServiceProvider(PartnerTrip trip, bool force = false)
        {
            if (force || DateTime.UtcNow > trip.lastUpdate + updateInterval && trip.status != Status.Complete)
            {
                Logger.Log("Getting (Foreign) status of " + trip);
                Logger.Tab();
                Gateway.GetTripStatusRequest request = new Gateway.GetTripStatusRequest(clientID: ID, tripID: PartnerTrip.GetPublicID(trip.ID, this.ID));
                Gateway.GetTripStatusResponse response = tripthru.GetTripStatus(request);
                if (response.status != null)
                    trip.UpdateTripStatus(notifyPartner: false, status: (Status)response.status, driverLocation: response.driverLocation, eta: response.ETA); // todo: not good -- fix this.
                if (response.driverName != null)
                    trip.driver = new Driver(name: response.driverName, location: response.driverLocation);
                if (response.dropoffTime != null)
                    trip.dropoffTime = response.dropoffTime;
                if (response.vehicleType != null)
                    trip.vehicleType = response.vehicleType;
                if (response.distance != null)
                    trip.distance = response.distance;
                Logger.Untab();
                trip.lastUpdate = DateTime.UtcNow;
            }
        }

        public override void Update()
        {
            if (!SimUpdateIntervalReached()) return;
            Logger.BeginRequest("Sim Update", null);
            foreach (var f in PartnerFleets.Values)
                f.Simulate();
            lastSim = DateTime.UtcNow;
            Logger.EndRequest(null);
        }

        private bool SimUpdateIntervalReached()
        {
            return DateTime.UtcNow > lastSim + simInterval;
        }
        public void HealthCheck()
        {
            var tags = new Dictionary<string, string>();
            tags["ActiveTrips"] = this.activeTrips.Count.ToString();
            tags["TripsById"] = this.tripsByID.Count.ToString();
            tags["FleetQueue"] = this.PartnerFleets.First().Value.queue.Count.ToString();
            tags["LocationAddresses"] = MapTools.locationAddresses.Count.ToString();
            tags["LocationNames"] = MapTools.locationNames.Count.ToString();
            tags["LoggerQueue"] = Logger.Queue.Count.ToString();
            if (Logger.splunkEnabled)
                tags["SplunkQueue"] = Logger.splunkClient.queue.Count.ToString();
            Logger.LogDebug("Health check (latest 2)", null, tags);
        }
    }

    // a route consists of waypoints that are 5 mins apart.
    public class PartnerTrip : IDName
    {
        private Status _status;
        public Location driverLocation;
        public Location driverInitiaLocation;
        public string passengerID;
        public string passengerName;
        public Origination origination;
        public Origination service;
        public int? luggage;
        public int? persons;
        public Location pickupLocation;
        public DateTime pickupTime;
        public TimeSpan? duration;
        public Location dropoffLocation;
        public DateTime? dropoffTime;
        public List<Location> waypoints;
        public PaymentMethod? paymentMethod;
        public VehicleType? vehicleType;
        public double? price;
        public double? maxPrice;
        public int? minRating;
        public PartnerFleet PartnerFleet;
        public Driver driver;
        public DateTime lastUpdate;
        public Partner partner;
        public DateTime? ETA;
        public double? distance;
        public Status? lastStatusNotifiedToPartner;
        public Status status { get { return _status; } set { this._status = value; } }
        public enum Origination { Local, Foreign };
        public DateTime lastDispatchAttempt;
        public bool autoDispatch;

        private bool TripStatusHasChanged(Status status, Location driverLocation, DateTime? eta)
        {
            return this._status != status || driverLocation != this.driverLocation || this.ETA != eta;
        }

        public void UpdateTripStatus(bool notifyPartner, Status status, Location driverLocation = null, DateTime? eta = null)
        {
            if (TripStatusHasChanged(status, driverLocation, eta))
            {
                Logger.Log("Trip status changed from " + _status + " to " + status + (driverLocation != null ? (" and driver's location has changed to " + driverLocation) : "") + (eta != null ? (" and eta has changed to " + eta) : ""));
                _status = status;
                if (driverLocation != null)
                    UpdateDriverLocation(driverLocation);
                if (eta != null)
                    this.ETA = eta;
                if (IsOneOfTheActiveTrips())
                {
                    partner.activeTrips[ID].Status = status;
                    if (lastStatusNotifiedToPartner != status && notifyPartner)
                        NotifyForeignPartner(status, driverLocation, eta);
                }
                else
                    Logger.Log("Cannot set status: because cannot find active trip with ID = " + this.ID);
            }
            if (status == Status.Complete)
            {
                if (service == Origination.Foreign)
                {
                    partner.DeactivateTripAndUpdateStats(ID, Status.Complete, 0, 0);
                }
                else
                    partner.DeactivateTripAndUpdateStats(ID, Status.Complete, PartnerFleet.GetPrice(this), PartnerFleet.GetDistance(this));
            }
            else if (status == Status.Cancelled || status == Status.Rejected)
                partner.DeactivateTripAndUpdateStats(ID, status);

            lastStatusNotifiedToPartner = status;
        }
        private void UpdateDriverLocation(Location location)
        {
            this.driverLocation = location;
            if (driverInitiaLocation == null)
                this.driverInitiaLocation = location;
        }

        private bool IsOneOfTheActiveTrips()
        {
            return this.partner.activeTrips.ContainsKey(this.ID);
        }

        private void NotifyForeignPartner(Status status, Location driverLocation, DateTime? eta)
        {
            Logger.Log("Since trip has foreign dependency, notify partner through TripThru");
            Logger.Tab();
            Gateway.UpdateTripStatusRequest request = new Gateway.UpdateTripStatusRequest(
                clientID: partner.ID,
                tripID: PartnerTrip.GetPublicID(ID, this.partner.ID),
                status: status,
                driverLocation: driverLocation,
                eta: eta
            );
            partner.tripthru.UpdateTripStatus(request);
            Logger.Untab();
        }

        private bool TripHasForeignDependency()
        {
            return (origination == Origination.Foreign || service == Origination.Foreign);
        }


        public PartnerTrip(PartnerTrip t)
        {
            this.ID = t.ID;
            this.passengerID = t.passengerID;
            this.passengerName = t.passengerName;
            this.origination = t.origination;
            this.service = t.service;
            this.luggage = t.luggage;
            this.persons = t.persons;
            this.pickupLocation = t.pickupLocation;
            this.pickupTime = t.pickupTime;
            this.duration = t.duration;
            this.dropoffLocation = t.dropoffLocation;
            this.dropoffTime = t.dropoffTime;
            this.waypoints = t.waypoints;
            this.paymentMethod = t.paymentMethod;
            this.vehicleType = t.vehicleType;
            this.price = t.price;
            this.maxPrice = t.maxPrice;
            this.minRating = t.minRating;
            this.PartnerFleet = t.PartnerFleet;
            this.driver = t.driver;
            this.lastUpdate = t.lastUpdate;
            this._status = t._status;
            this.partner = t.partner;
            this.lastDispatchAttempt = DateTime.MinValue;
            this.autoDispatch = t.autoDispatch;
        }
        public override string ToString()
        {
            string s = "Trip " + ID;
            s += ", Status = " + status;
            s += ", Origination = " + origination;
            s += ", Service = " + service;
            s += ", PickupLocation = " + pickupLocation;
            s += ", PickupTime = " + pickupTime;
            if (passengerName != null)
                s += ", Passenger = " + passengerName;
            if (dropoffLocation != null)
                s += ", DropoffLocation = " + dropoffLocation;
            if (dropoffTime != null)
                s += ", DropoffTime = " + dropoffTime;
            if (price != null)
                s += ", Price = " + price;
            if (PartnerFleet != null)
                s += ", Fleet = " + PartnerFleet.name;
            if (driver != null)
                s += ", Driver = " + driver;
            if (ETA != null)
                s += ", ETA = " + ETA;
            else
                s += ", ETA is null";

            return s;
        }
        public PartnerTrip(Partner partner, string ID, Origination origination, Location pickupLocation, DateTime pickupTime, PaymentMethod? paymentMethod = null, string passengerID = null, string passengerName = null, Location dropoffLocation = null,
           DateTime? dropoffTime = null, List<Location> waypoints = null, VehicleType? vehicleType = null, double? maxPrice = null, int? minRating = null, PartnerFleet fleet = null, Driver driver = null, TimeSpan? duration = null, TimeSpan? driverRouteDuration = null, double? price = null,
            bool autoDispatch = true)
        {
            this.ID = ID;
            this.origination = origination;
            this.service = Origination.Local;
            this.partner = partner;
            this.passengerID = passengerID;
            this.passengerName = passengerName;
            this.pickupLocation = pickupLocation;
            this.pickupTime = pickupTime;
            this.duration = duration;
            this.dropoffLocation = dropoffLocation;
            this.dropoffTime = dropoffTime;
            this.waypoints = waypoints;
            this.paymentMethod = paymentMethod;
            this.vehicleType = vehicleType;
            this.maxPrice = maxPrice;
            this.minRating = minRating;
            this.PartnerFleet = fleet;
            this.driver = driver;
            this.price = price;
            this.autoDispatch = autoDispatch;
            this.UpdateTripStatus(notifyPartner: false, status: Status.New);
        }
    }
    public class Passenger : IDName
    {
        public PartnerFleet PartnerFleet;
        public Passenger(string name, PartnerFleet PartnerFleet = null)
            : base(name)
        {
            this.PartnerFleet = PartnerFleet;
        }
        public override string ToString()
        {
            return name;
        }
    }
    public class Driver : IDName
    {
        public PartnerFleet PartnerFleet; // if this driver is external then this value is null.
        public Location location; // For this sim, we don't care about initial location.  Driver's can magically get where they need to be.
        public DateTime routeStartTime;
        public Route route;
        public DateTime lastUpdate;
        public Driver(string name, PartnerFleet PartnerFleet = null, Location location = null)
            : base(name)
        {
            if (PartnerFleet != null)
                PartnerFleet.AddDriver(this);
            this.PartnerFleet = PartnerFleet;
            this.location = location;
            lastUpdate = DateTime.UtcNow;
        }
        public override string ToString()
        {
            string s = name + "<";
            if (location != null)
                s += "(@" + location + ")";
            if (route != null)
                s += ", Destination = " + route.end + ", ETA = " + (routeStartTime + route.duration);
            s += ">";
            return s;
        }
    }
    public class PartnerFleet : IDName
    {
        static readonly object locker = new object();
        public Partner partner;
        public readonly Location location;
        public readonly List<Zone> coverage;
        public readonly List<VehicleType> vehicleTypes;
        public Dictionary<string, Driver> drivers;
        public LinkedList<Driver> availableDrivers;
        public LinkedList<Driver> returningDrivers;
        public LinkedList<Driver> saveDrivers;
        public Pair<Location, Location>[] possibleTrips;

        public StreamReader TripReader;
        public StreamReader NameReader;

        public Dictionary<string, string> listDriverNames = new Dictionary<string, string>();
        public Dictionary<string, string> listPassengerNames = new Dictionary<string, string>();

        public LinkedList<PartnerTrip> queue;
        public List<Passenger> passengers;
        public readonly double tripsPerHour;
        public readonly double costPerMile; // in local currency
        public readonly double baseCost;
        public Random random;
        public readonly TimeSpan tripMaxAdvancedNotice = new TimeSpan(0, 15, 0); // minutes
        public readonly TimeSpan simInterval = new TimeSpan(0, 0, 10);
        public readonly TimeSpan updateInterval = new TimeSpan(0, 0, 30); // for simluation
        public readonly TimeSpan missedPeriod = new TimeSpan(0, 15, 0);
        public readonly TimeSpan retryInterval = new TimeSpan(0, 5, 0);
        public readonly TimeSpan criticalPeriod = new TimeSpan(0, 15, 0);
        public readonly TimeSpan removalAge = new TimeSpan(0, 5, 0);
        public int maxActiveTrips = 5;


        public PartnerFleet(string name, Location location, List<Zone> coverage, List<Driver> drivers, List<VehicleType> vehicleTypes,
            List<Pair<Location, Location>> possibleTrips, double costPerMile, double baseCost, double tripsPerHour, List<Passenger> passengers, Partner partner = null, string urlTripsFile = null, string urlNamesFile = null)
            : base(name)
        {
            this.coverage = coverage;
            this.partner = partner;
            this.location = location;
            foreach (Passenger p in passengers)
                p.PartnerFleet = this;
            random = new Random();

            this.passengers = passengers;
            this.possibleTrips = possibleTrips.ToArray();

            if (urlTripsFile != null)
            {
                try
                {
                    TripReader = new StreamReader(File.OpenRead(urlTripsFile));
                    TripReader.ReadLine();
                    TripReader.ReadLine();
                    maxActiveTrips = 100;
                    try
                    {
                        if (urlNamesFile != null)
                            NameReader = new StreamReader(File.OpenRead(urlNamesFile));
                    }
                    catch
                    {
                        NameReader = null;
                    }
                }
                catch
                {
                    TripReader = null;
                }

            }

            List<Pair<Location, Location>> coveredTrips = new List<Pair<Location, Location>>();
            foreach (Pair<Location, Location> trip in possibleTrips)
            {
                if (FleetServesLocation(trip.First))
                    coveredTrips.Add(trip);
            }

            this.tripsPerHour = tripsPerHour;
            this.costPerMile = costPerMile;
            this.baseCost = baseCost;

            this.queue = new LinkedList<PartnerTrip>();

            this.drivers = new Dictionary<string, Driver>();
            availableDrivers = new LinkedList<Driver>();
            returningDrivers = new LinkedList<Driver>();
            saveDrivers = new LinkedList<Driver>();
            this.vehicleTypes = new List<VehicleType>(vehicleTypes);
            if (drivers != null)
            {
                foreach (Driver d in drivers)
                    AddDriver(d);
            }
            if (partner != null)
                partner.AddPartnerFleet(this);

        }
        public override string ToString()
        {
            string s = name + ", Location = " + location;
            return s;
        }
        public bool FleetServesLocation(Location l)
        {
            foreach (Zone z in coverage)
            {
                if (z.IsInside(l))
                    return true;
            }
            return false;
        }
        public void Log()
        {
            Logger.Log("PartnerFleet = " + name + "(" + location + "): SimRate = " + simInterval + ", StatusUpdateRate = " + updateInterval + ", MaxTripAdvancedNotice = " + tripMaxAdvancedNotice + ", CriticalPeriod = " + criticalPeriod + ", MissedPeriod = " + missedPeriod + ", RemovalAge = " + removalAge + ", NumTripsPeriodHour = " + tripsPerHour);
            Logger.Tab();
            Logger.Log("Drivers:");
            Logger.Tab();
            foreach (Driver d in this.drivers.Values)
                Logger.Log(d.ToString());
            Logger.Untab();

            Logger.Log("Passengers:");
            Logger.Tab();
            foreach (Passenger p in this.passengers)
                Logger.Log(p.ToString());
            Logger.Untab();
            Logger.Untab();
        }
        public DateTime UpdateDriverRouteAndGetETA(Driver driver, Location destination)
        {
            driver.routeStartTime = DateTime.UtcNow;
            driver.route = MapTools.GetRoute(driver.location, destination);
            DateTime eta = DateTime.UtcNow + driver.route.duration;
            Logger.Log(driver.name + " has a new route from " + driver.location + " to " + destination + ": ETA = " + eta);
            return eta;

        }
        public void AddDriver(Driver d)
        {
            d.PartnerFleet = this;
            d.location = location;
            drivers.Add(d.ID, d);
            availableDrivers.AddLast(d);
        }
        public void CompleteTrip(PartnerTrip t)
        {
            returningDrivers.AddLast(t.driver);
            availableDrivers.AddLast(t.driver);
            UpdateDriverRouteAndGetETA(t.driver, location);
            t.ETA = DateTime.UtcNow;

        }
        public bool TryDispatchTripLocally(PartnerTrip t)
        {
            Logger.Log("DispatchTripLocally");
            if (!FleetServesLocation(t.pickupLocation))
            {
                Logger.Log("Pickup location " + t.pickupLocation + " is outside of coverage area");
                return false;
            }
            if (t.status != Status.Queued)
                throw new Exception("Invalid 'Dispatch' status");

            var readyForDispatch = true;
            if (ThereAreAvailableDrivers())
            {
                if (TripOriginatedLocally(t))
                    readyForDispatch = partner.CreateLocalTripInTripThru(t);
                if (!readyForDispatch) return false;
                DispatchToFirstAvailableDriver(t);
                t.UpdateTripStatus(notifyPartner: true, status: Status.Dispatched, driverLocation: t.driver.location, eta: t.pickupTime);
                return true;
            }
            Logger.Log("No drivers are currently available");
            return false;
        }

        private bool ThereAreAvailableDrivers()
        {
            return availableDrivers.Count > 0;
        }

        private void DispatchToFirstAvailableDriver(PartnerTrip t)
        {
            if (availableDrivers.Count == 0)
                throw new Exception("Invalid condition: no available drivers");
            t.driver = availableDrivers.First();
            t.PartnerFleet = this;
            availableDrivers.RemoveFirst();
            Logger.Log("Assigning to driver = " + t.driver + ", name = " + t.driver.name);
            if (t.driver == null)
                throw new Exception("Invalid condition: driver object null");
            Logger.Log("Dispatched to " + t.driver.name);
        }
        public void GenerateRandomTrips()
        {
            lock (locker)
            {
                if (queue.Count > maxActiveTrips)
                    return; // lets not let the queue get too big
                int numTripsToGenerate = (int)Math.Floor(simInterval.TotalHours * tripsPerHour);
                {
                    // this handles fractional trips.
                    double d = (simInterval.TotalHours * tripsPerHour) - (double)numTripsToGenerate;
                    if (d > random.NextDouble())
                        numTripsToGenerate++;
                }
                if (numTripsToGenerate > maxActiveTrips)
                    numTripsToGenerate = maxActiveTrips;
                if (numTripsToGenerate == 0)
                    return;
                DateTime now = DateTime.UtcNow;

                for (int n = 0; n < numTripsToGenerate; n++)
                    GenerateRandomTrip(now);
            }
        }

        private void GenerateRandomTrip(DateTime now)
        {
            Console.WriteLine("######Generate Trip###########");
            var listPossibleTrip = GetPossibleTrip();
            if (listPossibleTrip == null)
            {
                Passenger passenger = passengers[random.Next(passengers.Count)];
                Pair<Location, Location> fromTo = possibleTrips[random.Next(possibleTrips.Length)];
                DateTime pickupTime = now + new TimeSpan(0, random.Next((int)tripMaxAdvancedNotice.TotalMinutes), 0);
                QueueTrip(GenerateTrip(passenger, pickupTime, fromTo));
            }
            else
            {
                Console.WriteLine("#######TripsRandoms#####" + listPossibleTrip.Count);
                foreach (var possibleTrip in listPossibleTrip)
                {
                    Passenger passenger = GetPassenger(possibleTrip);
                    Pair<Location, Location> fromTo = getLocationPair(possibleTrip);
                    DateTime pickupTime = now + new TimeSpan(0, random.Next((int)tripMaxAdvancedNotice.TotalMinutes), 0);
                    QueueTrip(GenerateTrip(passenger, pickupTime, fromTo));
                }
            }
        }



        private List<string[]> GetPossibleTrip()
        {
            if (TripReader == null) return null;
            while (!TripReader.EndOfStream)
            {
                var locationsList = new List<string[]>();

                var tripLine = TripReader.ReadLine();
                if (tripLine == null) continue;
                var tripValues = tripLine.Split(',');

                var dateTimeTemp = DateTimeOffset.ParseExact(tripValues[5],
                "dd/M/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
                var time = dateTimeTemp.TimeOfDay;
                var timeNow = DateTime.UtcNow.TimeOfDay;

                while (!(time >= timeNow))
                {
                    tripLine = TripReader.ReadLine();
                    if (tripLine == null) break;
                    tripValues = tripLine.Split(',');
                    dateTimeTemp = DateTimeOffset.ParseExact(tripValues[5],
                    "dd/M/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
                    time = dateTimeTemp.TimeOfDay;
                }

                try
                {
                    while (time.Minutes == timeNow.Minutes)
                    {
                        var latDrop = Convert.ToDouble(tripValues[13]);
                        var lngDrop = Convert.ToDouble(tripValues[12]);
                        var latPick = Convert.ToDouble(tripValues[11]);
                        var lngPick = Convert.ToDouble(tripValues[10]);
                        if (CoordinateRange(latDrop) && CoordinateRange(lngDrop) && CoordinateRange(latPick) &&
                            CoordinateRange(lngPick))
                        {
                            if (IsInside(this.location, new Location(latPick, lngPick), 50) && IsInside(this.location, new Location(latDrop, lngDrop), 50))
                                locationsList.Add(tripValues);
                        }
                        tripLine = TripReader.ReadLine();
                        if (tripLine == null) break;
                        tripValues = tripLine.Split(',');
                        dateTimeTemp = DateTimeOffset.ParseExact(tripValues[5],
                        "dd/M/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
                        time = dateTimeTemp.TimeOfDay;
                    }

                    var finalListTrips = new List<string[]>();

                    var countList = locationsList.Count;
                    var counSteps = 0;
                    var steps1 = (int)countList / 50;
                    var steps = 0;
                    if (steps1 != 0)
                        steps = countList / steps1;
                    var tripsMinute = 0;

                    if (steps1 != 0)
                        foreach (var trip in locationsList)
                        {
                            tripsMinute++;
                            if (counSteps < steps)
                            {
                                counSteps++;
                                continue;
                            }
                            counSteps = 0;
                            finalListTrips.Add(trip);
                        }
                    else
                    {
                        if (locationsList.Count > 0)
                            finalListTrips.Add(locationsList.First());
                        else
                            return null;
                    }

                    if (finalListTrips.Count > 0)
                    {
                        if (availableDrivers.Count > finalListTrips.Count + 10)
                            while (availableDrivers.Count > finalListTrips.Count + 10 && availableDrivers.Count > 0)
                            {
                                var avaliable = availableDrivers.First();
                                saveDrivers.AddLast(avaliable);
                                availableDrivers.RemoveFirst();
                            }
                        else
                        {
                            while (availableDrivers.Count < finalListTrips.Count + 10 && saveDrivers.Count > 0)
                            {
                                var avaliable = saveDrivers.First();
                                availableDrivers.AddLast(avaliable);
                                saveDrivers.RemoveFirst();
                            }
                        }
                        Console.WriteLine("#######AVALIABLE:" + availableDrivers.Count);
                        Console.WriteLine("#######SAVE:" + saveDrivers.Count);
                        return finalListTrips;
                    }
                    else
                        return null;

                }
                catch (Exception)
                {
                }
            }
            if (!TripReader.EndOfStream) return null;
            TripReader.BaseStream.Position = 0;
            TripReader.DiscardBufferedData();
            return null;
        }

        public bool IsInside(Location l, Location Center, double Radius)
        {
            double lat1 = DegreesToRadians(Center.Lat);
            double lng1 = DegreesToRadians(Center.Lng);
            double lat2 = DegreesToRadians(l.Lat);
            double lng2 = DegreesToRadians(l.Lng);
            double dlon = lng2 - lng1;
            double dlat = lat2 - lat1;
            double a = Math.Pow(Math.Sin(dlat / 2.0), 2) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(dlon / 2.0), 2);
            double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            double d = 3961.0 * c; // (where 3961 is the radius of the Earth in miles
            return d < Radius;
        }
        public double DegreesToRadians(double angle)
        {
            return Math.PI * angle / 180.0;
        }
        private Pair<Location, Location> getLocationPair(string[] valueStrings)
        {
            var latDrop = Convert.ToDouble(valueStrings[13]);
            var lngDrop = Convert.ToDouble(valueStrings[12]);
            var latPick = Convert.ToDouble(valueStrings[11]);
            var lngPick = Convert.ToDouble(valueStrings[10]);
            return new Pair<Location, Location>(new Location(latPick, lngPick), new Location(latDrop, lngDrop));
        }
        private Passenger GetPassenger(IList<string> valueStrings)
        {
            if (NameReader == null || NameReader.EndOfStream) return passengers[random.Next(passengers.Count)];
            var completeName = "";
            if (listPassengerNames.ContainsKey(valueStrings[0])) return new Passenger(completeName);
            var identity = NameReader.ReadLine();
            if (identity == null) return new Passenger(completeName);
            var split = identity.Split('\t');
            completeName = split[3] + " " + split[4] + " " + split[5];
            listPassengerNames.Add(valueStrings[0], completeName);
            return new Passenger(completeName);
        }


        private static bool CoordinateRange(double coordinate)
        {
            return coordinate > -180 && coordinate < 180;
        }

        public PartnerTrip GenerateTrip(Passenger passenger, DateTime pickupTime, Pair<Location, Location> fromTo)
        {
            Route route = MapTools.GetRoute(fromTo.First, fromTo.Second);
            Logger.Log("Pickup request (" + name + ") " + passenger.name + " requests to be picked up at " + route.start + " on " + pickupTime + " and dropped off at " + route.end);
            Logger.Tab();
            PartnerTrip trip = new PartnerTrip(
                partner: this.partner,
                ID: PartnerTrip.GenerateUniqueID(this.partner.ID),
                origination: PartnerTrip.Origination.Local,
                pickupLocation: route.start,
                pickupTime: pickupTime,
                passengerID: passenger.ID,
                passengerName: passenger.name,
                dropoffLocation: route.end,
                paymentMethod: PaymentMethod.Cash,
                fleet: this
                );
            Logger.Untab();
            return trip;
        }
        public bool QueueTrip(PartnerTrip t)
        {
            lock (locker)
            {
                if (availableDrivers.Count == 0 && t.origination == PartnerTrip.Origination.Foreign)
                    return false; // don't except from parters if no available drivers
                Logger.Log("Queueing " + t);
                queue.AddLast(t);
                if (partner.activeTrips.ContainsKey(t.ID))
                    throw new Exception("Trip " + t + ": already exist in activeTrips dictionary -- Existing trip = " + partner.activeTrips[t.ID]);
                if (partner.tripsByID.ContainsKey(t.ID))
                    throw new Exception("Trip " + t + ": already exist in tripsByID dictionary");
                partner.tripsByID.Add(t.ID, t);
                partner.activeTrips.Add(t.ID, new Trip
                {
                    FleetId = t.PartnerFleet != null ? t.PartnerFleet.ID : null,
                    FleetName = t.PartnerFleet != null ? t.PartnerFleet.name : null,
                    DriverId = t.driver != null ? t.driver.ID : null,
                    DriverLocation = t.driver != null ? t.driver.location : null,
                    DriverName = t.driver != null ? t.driver.name : null,
                    DropoffLocation = t.dropoffLocation,
                    DriverInitiaLocation = t.driverInitiaLocation ?? null,
                    DropoffTime = t.dropoffTime,
                    Id = t.ID,
                    OriginatingPartnerId = this.ID,
                    OriginatingPartnerName = this.name,
                    PassengerName = t.passengerName,
                    PickupLocation = t.pickupLocation,
                    PickupTime = t.pickupTime,
                    Price = t.price,
                    Status = t.status,
                    VehicleType = t.vehicleType,
                    SamplingPercentage = 1
                });
                t.UpdateTripStatus(notifyPartner: false, status: Status.Queued);
                return true;
            }
        }
        public void RemoveTrip(PartnerTrip t)
        {
            queue.Remove(queue.Find(t));
        }
        public void Simulate()
        {
            GenerateRandomTrips();
            ProcessQueue();
            UpdateReturningDriverLocations();
        }

        public void UpdateReturningDriverLocations()
        {
            lock (locker)
            {
                LinkedListNode<Driver> next = null;
                for (LinkedListNode<Driver> node = returningDrivers.First; node != null; node = next)
                {
                    Driver driver = node.Value;
                    next = node.Next;
                    UpdateDriverReturningLocation(driver);
                    if (DriverHomeOfficeReached(driver))
                    {
                        Logger.Log("Driver " + driver.name + " has reached the home office ");
                        returningDrivers.Remove(node);
                    }
                    else if (DriverUpdateIntervalReached(driver))
                    {
                        Logger.Log("Driver update: " + driver);
                        driver.lastUpdate = DateTime.UtcNow;
                    }
                }
            }
        }

        private bool DriverHomeOfficeReached(Driver driver)
        {
            return driver.location.Equals(location);
        }

        private static Location UpdateDriverReturningLocation(Driver driver)
        {
            return driver.location = driver.route.GetCurrentWaypoint(driver.routeStartTime, DateTime.UtcNow);
        }

        private bool DriverUpdateIntervalReached(Driver driver)
        {
            return DateTime.UtcNow > driver.lastUpdate + updateInterval;
        }

        public void RemoveTrip(LinkedListNode<PartnerTrip> t)
        {
            partner.tripsByID.Remove(t.Value.ID);
            queue.Remove(t);
        }

        void DispatchTrip(PartnerTrip t)
        {
            if (TripOriginatedLocally(t))
            {
                if (MissedPeriodReached(t))
                {
                    CancelTrip(t);
                    return;
                }
                if (TripServicedByForeignProvider(t)) // means serviced through partner
                    return;
            }
            // If origination is foreign, then it means we're servicing the trip so we have to process it.
            if (CriticalPeriodNotYetReached(t))
                return;
            Logger.Log("Ready for dispatch: " + t);
            Logger.Tab();

            if (!TryDispatchTripLocally(t) && TripOriginatedLocally(t))
                partner.TryToDispatchToForeignProvider(t);
            Logger.Untab();
        }

        private bool CriticalPeriodNotYetReached(PartnerTrip t)
        {
            return DateTime.UtcNow < t.pickupTime - criticalPeriod;
        }

        private static bool TripOriginatedLocally(PartnerTrip t)
        {
            return t.origination == PartnerTrip.Origination.Local;
        }

        private static void CancelTrip(PartnerTrip t)
        {
            Logger.Log("Missed period reached: -- so cancel " + t);
            Logger.Tab();
            t.UpdateTripStatus(notifyPartner: true, status: Status.Cancelled);
            Logger.Untab();
        }

        private bool MissedPeriodReached(PartnerTrip t)
        {
            return DateTime.UtcNow > t.pickupTime + missedPeriod;
        }


        public void ProcessQueue()
        {
            lock (locker)
            {
                for (LinkedListNode<PartnerTrip> node = queue.First; node != null; )
                {
                    PartnerTrip t = node.Value;
                    LinkedListNode<PartnerTrip> next = node.Next;
                    try
                    {
                        ProcessTrip(t);
                    }
                    catch (Exception e)
                    {
                        Logger.LogDebug("ProcessTrip failed: " + t, e.ToString());
                    }
                    RemoveOldNonActiveTrips(node, t);
                    node = next;
                }
            }

        }

        private void RemoveOldNonActiveTrips(LinkedListNode<PartnerTrip> node, PartnerTrip t)
        {
            switch (t.status)
            {
                case Status.Cancelled:
                case Status.Rejected:
                    {
                        RemoveTripIfOld(node, t, GetAgeSinceCancelledOrRejected(t));
                        break;
                    }
                case Status.Complete:
                    {
                        if (AgeSinceCompletedClock_HasNotBeenSet(t))
                            StartTheAgeSinceCompletedClock_FromNow(t);
                        RemoveTripIfOld(node, t, GetAgeSinceCompleted(t));
                        break;
                    }
            }
        }

        public void ProcessTrip(PartnerTrip t)
        {
            //Logger.LogDebug("Processing " + t);
            lock (locker)
            {
                switch (t.status)
                {
                    case Status.New:
                        {
                            Logger.Log("Unexpected status (New): Something wrong with " + t);
                            break;
                        }
                    case Status.Queued:
                        {
                            ProcessStatusQueued(t);
                            break;
                        }
                    case Status.Dispatched:
                        {
                            ProcessStatusDispatched(t);
                            break;
                        }
                    case Status.Enroute:
                        {
                            ProcessStatusEnroute(t);
                            break;
                        }
                    case Status.PickedUp:
                        {
                            ProcessStatusPickedUp(t);
                            break;
                        }
                }
            }

        }

        private void ProcessStatusPickedUp(PartnerTrip t)
        {
            if (TripServicedByForeignProvider(t))
                return; // partner.GetTripStatusFromForeignServiceProvider(t, true);
            else
            {
                UpdateTripDriverLocation(t);
                if (DestinationReached(t))
                    MakeTripComplete(t);
                else if (TripStatusUpdateIntervalReached(t))
                    LogTheNewDriverLocation(t);
            }
        }

        private void ProcessStatusEnroute(PartnerTrip t)
        {
            if (TripServicedByForeignProvider(t))
                return; // partner.GetTripStatusFromForeignServiceProvider(t, true);
            else
            {
                UpdateTripDriverLocation(t);
                if (DriverHasReachedThePickupLocation(t))
                    MakeTripPickedUp(t);
                else if (TripStatusUpdateIntervalReached(t))
                    LogTheNewDriverLocation(t);
            }
        }

        private void ProcessStatusDispatched(PartnerTrip t)
        {
            if (TripServicedByForeignProvider(t))
                return; // partner.GetTripStatusFromForeignServiceProvider(t, true);
            else if (DriverWillBeLateIfHeDoesntLeaveNow(t))
                MakeTripEnroute(t);
            else if (TripStatusUpdateIntervalReached(t))
                LogTheNewDriverLocation(t);
        }

        private void ProcessStatusQueued(PartnerTrip t)
        {
            if (TripIsAutoDispatch(t) && DispatchRetryIntervalReached(t))
            {
                DispatchTrip(t);
                t.lastDispatchAttempt = DateTime.UtcNow;
            }
        }
        private bool TripIsAutoDispatch(PartnerTrip t)
        {
            return t.autoDispatch;
        }

        private static void StartTheAgeSinceCompletedClock_FromNow(PartnerTrip t)
        {
            t.dropoffTime = DateTime.UtcNow;
        }

        private static bool AgeSinceCompletedClock_HasNotBeenSet(PartnerTrip t)
        {
            return t.dropoffTime == null;
        }

        private static TimeSpan GetAgeSinceCompleted(PartnerTrip t)
        {
            return DateTime.UtcNow - (DateTime)t.dropoffTime;
        }

        private static TimeSpan GetAgeSinceCancelledOrRejected(PartnerTrip t)
        {
            return DateTime.UtcNow - (DateTime)t.pickupTime;
        }

        private static void UpdateTripDriverLocation(PartnerTrip t)
        {
            if (t.driver == null)
                throw new Exception("Driver is null for trip " + t);
            if (t.driver.route == null)
                throw new Exception("Driver route is null for trip " + t);
            t.driver.location = t.driver.route.GetCurrentWaypoint(t.driver.routeStartTime, DateTime.UtcNow);
        }

        private void RemoveTripIfOld(LinkedListNode<PartnerTrip> node, PartnerTrip t, TimeSpan age)
        {
            if (age > removalAge) // for now we use 1 minute.
            {
                Logger.Log("Since old, remove: " + t);
                RemoveTrip(node); // remove trips that are more than 1 day old.
            }
        }

        private void MakeTripComplete(PartnerTrip t)
        {
            Logger.Log("The destination has been reached for: " + t);
            Logger.Tab();
            t.dropoffTime = DateTime.UtcNow;
            CompleteTrip(t);
            t.UpdateTripStatus(notifyPartner: true, status: Status.Complete);
            Logger.Untab();
        }

        private static bool DestinationReached(PartnerTrip t)
        {
            return t.driver.location.Equals(t.driver.route.end);
        }

        private void MakeTripPickedUp(PartnerTrip trip)
        {
            Logger.Log("Picking up: " + trip);
            Logger.Tab();
            DateTime eta = UpdateDriverRouteAndGetETA(trip.driver, trip.dropoffLocation);
            trip.UpdateTripStatus(notifyPartner: true, status: Status.PickedUp, driverLocation: trip.driver.location, eta: eta);
            Logger.Untab();
        }

        private static bool DriverHasReachedThePickupLocation(PartnerTrip t)
        {
            return t.driver.location.Equals(t.pickupLocation);
        }

        private static bool TripServicedByForeignProvider(PartnerTrip t)
        {
            return t.service == PartnerTrip.Origination.Foreign;
        }

        private static void LogTheNewDriverLocation(PartnerTrip t)
        {
            Logger.Log("Getting status of: " + t);
            t.lastUpdate = DateTime.UtcNow;
        }

        public bool TripStatusUpdateIntervalReached(PartnerTrip t)
        {
            return DateTime.UtcNow > t.lastUpdate + updateInterval;
        }

        private void MakeTripEnroute(PartnerTrip trip)
        {
            Logger.Log("Driver is now enroute: " + trip);
            Logger.Tab();
            DateTime eta = UpdateDriverRouteAndGetETA(trip.driver, trip.pickupLocation);
            trip.UpdateTripStatus(notifyPartner: true, status: Status.Enroute, driverLocation: trip.driver.location, eta: eta);
            Logger.Untab();
        }

        private static bool DriverWillBeLateIfHeDoesntLeaveNow(PartnerTrip t)
        {
            Logger.Log("Entering: DriverWillBeLateIfHeDoesntLeaveNow");
            if (t == null)
                throw new Exception("null trip");
            if (t.driver == null)
                throw new Exception("Trip " + t + ": doesn't have a driver");
            if (t.driver.location == null)
                throw new Exception("Trip " + t + ": doesn't have a driver location");
            if (t.pickupLocation == null)
                throw new Exception("Trip " + t + ": doesn't have a pickup location");
            Route route = MapTools.GetRoute(t.driver.location, t.pickupLocation);
            if (route == null)
                throw new Exception("Trip " + t + ": has null route");
            return DateTime.UtcNow >= t.pickupTime - route.duration;
        }

        private bool DispatchRetryIntervalReached(PartnerTrip t)
        {
            return DateTime.UtcNow > t.lastDispatchAttempt + retryInterval;
        }
        public static implicit operator Fleet(PartnerFleet f)  // explicit byte to digit conversion operator
        {
            return new Fleet
            {
                FleetId = f.ID,
                FleetName = f.name,
                PartnerId = f.partner.ID,
                PartnerName = f.partner.name,
                Coverage = f.coverage
            };  // explicit conversion
        }

        // TODO: make these more real
        public double GetPrice(PartnerTrip trip)
        {
            return baseCost + (GetDistance(trip) * costPerMile); // once we have the distance it won't be too hard to come up with some prices.
        }
        public double GetDistance(PartnerTrip trip)
        {
            Route route = MapTools.GetRoute(trip.pickupLocation, trip.dropoffLocation);
            return route.distance;
        }
        static readonly TimeSpan expectedDelayWhenNoDriversAvailable = new TimeSpan(3, 0, 0);
        public DateTime GetPickupETA(PartnerTrip trip)
        {
            if (availableDrivers.Count == 0)
                return DateTime.UtcNow + MapTools.GetRoute(location, trip.pickupLocation).duration + expectedDelayWhenNoDriversAvailable; // if there are no drivers avaialble we add 3 hrs.  TODO: make this more realistic
            else
                return DateTime.UtcNow + MapTools.GetRoute(availableDrivers.First.Value.location, trip.pickupLocation).duration; // if there are no drivers avaialble we add 3 hrs.  TODO: make this more realistic
        }
    }

}