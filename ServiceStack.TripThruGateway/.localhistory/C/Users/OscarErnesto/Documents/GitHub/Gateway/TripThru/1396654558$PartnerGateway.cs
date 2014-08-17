using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using ServiceStack.Text;
using Utils;

namespace TripThruCore
{
    public class Partner : GatewayServer
    {
        public readonly Dictionary<string, PartnerTrip> tripsByID;
        public readonly Dictionary<string, PartnerFleet> PartnerFleets;
        public DateTime lastSim;
        public readonly Gateway tripthru;
        // Configuration parameters
        public readonly TimeSpan simInterval = new TimeSpan(0, 0, 10);
        public readonly TimeSpan updateInterval = new TimeSpan(0, 0, 30); // for simluation
        public string preferedPartnerId = null;

        static long nextID = 0;
        public string GenerateUniqueID() { nextID++; return nextID.ToString() + "@" + ID; }
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

                var fleet = new PartnerFleet(
                    name: partnerFleet.Name,
                    location: location,
                    coverage: coverage,
                    drivers: drivers,
                    vehicleTypes: vehicleTypes,
                    possibleTrips: trips,
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
        public PartnerTrip GetTrip(DispatchTripRequest r)
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
                driver: r.driverID == null ? null : this.PartnerFleets[r.fleetID].drivers[r.driverID]);
        }
        public override DispatchTripResponse DispatchTrip(DispatchTripRequest r)
        {

            requests++;
            if (r.fleetID != null)
            {
                PartnerFleet f = PartnerFleets[r.fleetID];
                if (f.FleetServesLocation(r.pickupLocation))
                {
                    PartnerTrip trip = GetTrip(r);
                    if (f.QueueTrip(trip))
                    {
                        DispatchTripResponse response = new DispatchTripResponse();
                        Logger.Log("DispatchTrip successful on " + name + ", Response: " + response);
                        return response;
                    }
                }
                return new DispatchTripResponse(result: Result.Rejected);
            }
            // Note: GetTrip populates the foreignTripID
            foreach (PartnerFleet f in PartnerFleets.Values)
            {
                if (!f.FleetServesLocation(r.pickupLocation))
                    continue;
                PartnerTrip trip = GetTrip(r);
                if (f.QueueTrip(trip))
                {
                    DispatchTripResponse response = new DispatchTripResponse();
                    Logger.Log("DispatchTrip successful on " + name + ", Response: " + response);
                    return response;
                }
            }
            {
                DispatchTripResponse response = new DispatchTripResponse(result: Result.Rejected);
                Logger.Log("DispatchTrip rejected on " + name + ", no available drivers -- Response: " + response);
                return response;
            }
        }
        public PartnerTrip GetTrip(QuoteTripRequest r)
        {
            return new PartnerTrip(this, null, PartnerTrip.Origination.Foreign, r.pickupLocation, r.pickupTime, r.paymentMethod, r.passengerID, r.passengerName, r.dropoffLocation,
                null, r.waypoints, r.vehicleType, r.maxPrice, r.minRating, r.fleetID == null ? null : this.PartnerFleets[r.fleetID], r.driverID == null ? null : this.PartnerFleets[r.fleetID].drivers[r.driverID]);
        }
        public override QuoteTripResponse QuoteTrip(QuoteTripRequest r)
        {
            requests++;
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
            QuoteTripResponse response = quotes.Count > 0 ? new QuoteTripResponse(quotes) : new QuoteTripResponse(result: Result.Rejected);
            return response;
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
            }
            return new GetTripsResponse(trips);
        }
        public override GetTripStatusResponse GetTripStatus(GetTripStatusRequest r)
        {
            requests++;
            if (!tripsByID.ContainsKey(r.tripID))
            {
                Logger.Log("Trip " + r.tripID + " not found");
                return new GetTripStatusResponse(result: Result.NotFound);
            }

            PartnerTrip t = tripsByID[r.tripID];
            DateTime? pickupTime = null;
            if (t.status == Status.PickedUp || t.status == Status.DroppedOff || t.status == Status.Complete)
                pickupTime = t.pickupTime; // Only if trip has been pickedup.

            GetTripStatusResponse response;
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
            return response;
        }
        public override UpdateTripStatusResponse UpdateTripStatus(UpdateTripStatusRequest r)
        {
            // Note: GetTrip populates the foreignTripID
            requests++;
            if (!tripsByID.ContainsKey(r.tripID))
                return new UpdateTripStatusResponse(result: Result.NotFound);
            PartnerTrip t = tripsByID[r.tripID];
            UpdateTripStatusResponse response = new UpdateTripStatusResponse();
            t.UpdateTripStatus(notifyPartner: false, status: r.status, driverLocation: r.driverLocation, eta: r.eta);
            
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
            {
                PartnerAccount partnerAccount = new PartnerAccount
                {
                    UserName = "TripThru",
                    Password = "password",
                    Email = "tripthru@tripthru.com",
                    AccessToken = "jaosid1201231",
                    RefreshToken = "jaosid1201231",
                    ClientId = "TripThru",
                    ClientSecret = "23noiasdn2123"
                };
                partnerAccounts[partnerAccount.ClientId] = partnerAccount;
                clientIdByAccessToken[partnerAccount.AccessToken] = partnerAccount.ClientId;
            }

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
                Gateway.GetTripStatusRequest request = new Gateway.GetTripStatusRequest(clientID: ID, tripID: trip.ID);
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
            if (SimUpdateIntervalReached())
            {
                Logger.BeginRequest("", null);
                foreach (PartnerFleet f in PartnerFleets.Values)
                    f.Simulate();
                lastSim = DateTime.UtcNow;
                Logger.EndRequest(null);
                HealthCheck();
            }
        }

        private bool SimUpdateIntervalReached()
        {
            return DateTime.UtcNow > lastSim + simInterval;
        }
        public void HealthCheck()
        {
            var tags = new Dictionary<string, string>();
            tags["ActiveTrips"] = this.activeTrips.Count.ToString();
            tags["Routes"] = MapTools.routes.Count.ToString();
            tags["LocationAddresses"] = MapTools.locationAddresses.Count.ToString();
            tags["LocationNames"] = MapTools.locationNames.Count.ToString();
            tags["LoggerQueue"] = Logger.Queue.Count.ToString();
            if(Logger.splunkEnabled)
                tags["SplunkQueue"] = Logger.splunkClient.queue.Count.ToString();
            Logger.LogDebug("Health check", null, tags);
        }
    }

    // a route consists of waypoints that are 5 mins apart.
    public class PartnerTrip : IDName
    {
        private Status _status;
        public Location driverLocation;
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
        public Status status { get { return _status; } }
        public enum Origination { Local, Foreign };
        public DateTime lastDispatchAttempt;

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
                    this.driverLocation = driverLocation;
                if (eta != null)
                    this.ETA = eta;
                if (IsOneOfTheActiveTrips())
                {
                    partner.activeTrips[ID].Status = status;
                    if (TripHasForeignDependency() && lastStatusNotifiedToPartner != status && notifyPartner)
                        NotifyForeignPartner(status, driverLocation, eta);
                }
                else
                    Logger.Log("Cannot set status: because cannot find active trip with ID = " + this.ID);
            }
            if (status == Status.Complete)
            {
                if (service == Origination.Foreign)
                {
                    Gateway.GetTripStatusResponse resp = GetStatsFromForeignServiceProvider();
                    partner.DeactivateTripAndUpdateStats(ID, Status.Complete, resp.price, resp.distance);
                }
                else
                    partner.DeactivateTripAndUpdateStats(ID, Status.Complete, PartnerFleet.GetPrice(this), PartnerFleet.GetDistance(this));
            }
            else if (status == Status.Cancelled || status == Status.Rejected)
                partner.DeactivateTripAndUpdateStats(ID, status);

            lastStatusNotifiedToPartner = status;
        }
        private Gateway.GetTripStatusResponse GetStatsFromForeignServiceProvider()
        {
            Gateway.GetTripStatusResponse resp = partner.tripthru.GetTripStatus(new Gateway.GetTripStatusRequest(partner.ID, ID));
            return resp;
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
                tripID: ID,
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
           DateTime? dropoffTime = null, List<Location> waypoints = null, VehicleType? vehicleType = null, double? maxPrice = null, int? minRating = null, PartnerFleet fleet = null, Driver driver = null, TimeSpan? duration = null, TimeSpan? driverRouteDuration = null, double? price = null)
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
        public Pair<Location, Location>[] possibleTrips;
        public LinkedList<PartnerTrip> queue;
        public Passenger[] passengers;
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
        public const int maxActiveTrips = 2;


        public PartnerFleet(string name, Location location, List<Zone> coverage, List<Driver> drivers, List<VehicleType> vehicleTypes,
            List<Pair<Location, Location>> possibleTrips, double costPerMile, double baseCost, double tripsPerHour, List<Passenger> passengers, Partner partner = null)
            : base(name)
        {
            this.coverage = coverage;
            this.partner = partner;
            this.location = location;
            foreach (Passenger p in passengers)
                p.PartnerFleet = this;
            random = new Random();

            this.passengers = passengers.ToArray();
            this.possibleTrips = possibleTrips.ToArray();


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
            if (ThereAreAvailableDrivers())
            {
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
            //throw new Exception("driver = " + t.driver + ", name = " + t.driver.name);
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
            Passenger passenger = passengers[random.Next(passengers.Length)];
            Pair<Location, Location> fromTo = possibleTrips[random.Next(possibleTrips.Length)];
            DateTime pickupTime = now + new TimeSpan(0, random.Next((int)tripMaxAdvancedNotice.TotalMinutes), 0);
            QueueTrip(GenerateTrip(passenger, pickupTime, fromTo));
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
                fleet:this
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
                partner.tripsByID.Add(t.ID, t);
                partner.activeTrips.Add(t.ID, new Trip
                {
                    FleetId = t.PartnerFleet != null ? t.PartnerFleet.ID : null,
                    FleetName = t.PartnerFleet != null ? t.PartnerFleet.name : null,
                    DriverId = t.driver != null ? t.driver.ID : null,
                    DriverLocation = t.driver != null ? t.driver.location : null,
                    DriverName = t.driver != null ? t.driver.name : null,
                    DropoffLocation = t.dropoffLocation,
                    DriverInitiaLocation = null,
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
                TryToDispatchToForeignProvider(t);
            Logger.Untab();
        }

        private void TryToDispatchToForeignProvider(PartnerTrip t)
        {
            Logger.Tab();
            Gateway.DispatchTripRequest request = new Gateway.DispatchTripRequest(
                clientID: partner.ID,
                tripID: t.ID,
                pickupLocation: t.pickupLocation,
                pickupTime: t.pickupTime,
                passengerID: t.passengerID,
                passengerName: t.passengerName,
                luggage: t.luggage,
                persons: t.persons,
                dropoffLocation: t.dropoffLocation,
                waypoints: t.waypoints,
                paymentMethod: t.paymentMethod,
                vehicleType: t.vehicleType,
                maxPrice: t.maxPrice,
                minRating: t.minRating,
                partnerID: partner.preferedPartnerId
                );
            Gateway.DispatchTripResponse response = partner.tripthru.DispatchTrip(request);
            if (response.result == Gateway.Result.OK)
            {
                // Actually, at this point its just in the partner's queue, until its dispatch to the partner's drivers -- so no status update. 
                t.service = PartnerTrip.Origination.Foreign;
                Logger.Log("Trip was successfully dispatched through TripThru");

            }
            else
                Logger.Log("Trip was rejected by TripThru");
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
                    ProcessTrip(t);
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
            if (DispatchRetryIntervalReached(t))
            {
                DispatchTrip(t);
                t.lastDispatchAttempt = DateTime.UtcNow;
            }
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
            return DateTime.UtcNow >= t.pickupTime - MapTools.GetRoute(t.driver.location, t.pickupLocation).duration;
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