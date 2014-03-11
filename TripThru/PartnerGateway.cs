using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Web;
using System.IO;
using ServiceStack.Text;
using Utils;
using ServiceStack.Redis;

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
            try
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
            catch (Exception e)
            {
                exceptions++;
                Logger.Log("Exception=" + e.Message);
                Logger.LogDebug("GetPartnerInfo=" + e.Message, e.ToString());
                return new GetPartnerInfoResponse(result: Result.UnknownError);
            }
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

            try
            {
                requests++;
                if (r.fleetID != null)
                {
                    PartnerFleet f = PartnerFleets[r.fleetID];
                    if (f.Serves(r.pickupLocation))
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
                    if (!f.Serves(r.pickupLocation))
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
            catch (Exception e)
            {
                exceptions++;
                Logger.Log("Exception=" + e.Message);
                Logger.LogDebug("DispatchTrip=" + e.Message, e.ToString());
                return new DispatchTripResponse(result: Result.UnknownError);
            }
        }
        public PartnerTrip GetTrip(QuoteTripRequest r)
        {
            return new PartnerTrip(this, null, PartnerTrip.Origination.Foreign, r.pickupLocation, r.pickupTime, r.paymentMethod, r.passengerID, r.passengerName, r.dropoffLocation,
                null, r.waypoints, r.vehicleType, r.maxPrice, r.minRating, r.fleetID == null ? null : this.PartnerFleets[r.fleetID], r.driverID == null ? null : this.PartnerFleets[r.fleetID].drivers[r.driverID]);
        }
        public override QuoteTripResponse QuoteTrip(QuoteTripRequest r)
        {
            try
            {
                requests++;
                List<Quote> quotes = new List<Quote>();
                bool pickupLocationNotServed = true;
                foreach (PartnerFleet f in PartnerFleets.Values)
                {
                    if (!f.Serves(r.pickupLocation))
                        continue;
                    pickupLocationNotServed = false;
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
                                distance: trip.dropoffLocation == null ? (double?) null : f.GetDistance(trip),
                                duration: trip.duration,
                                ETA: f.GetETA(trip)));
                        }
                    }
                }
                QuoteTripResponse response = quotes.Count > 0 ? new QuoteTripResponse(quotes) : new QuoteTripResponse(result: Result.Rejected);
                return response;
            }
            catch (Exception e)
            {
                exceptions++;
                Logger.Log("Exception=" + e.Message);
                Logger.LogDebug("QuoteTrip=" + e.Message, e.ToString());
                return new QuoteTripResponse(result: Result.UnknownError);
            }
        }
        public override GetTripsResponse GetTrips(GetTripsRequest r)
        {
            try
            {
                var trips = new List<Trip>();
                if (activeTrips.Count > 0)
                {
                    if (r.status != null)
                    {
                        trips.AddRange(activeTrips.Values.Where(trip => trip.Status == r.status).ToList());
                    }
                    else
                    {
                        trips.AddRange(activeTrips.Values);
                    }
                }
                return new GetTripsResponse(trips);
            }
            catch (Exception e)
            {
                exceptions++;
                Logger.Log("Exception=" + e.Message);
                Logger.LogDebug("GetTrips=" + e.Message, e.ToString());
                return new GetTripsResponse(new List<Trip>(), Result.UnknownError);
            }
        }
        public override GetTripStatusResponse GetTripStatus(GetTripStatusRequest r)
        {
            try
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
                if (t.driver != null && t.PartnerFleet != null)
                {
                    if (t.price == null)
                        t.price = t.PartnerFleet.GetPrice(t);
                    DateTime? ETA = t.PartnerFleet.GetETA(t);
                    double? distance = t.PartnerFleet.GetDistance(t);
                    response = new GetTripStatusResponse(
                        partnerID: ID, 
                        partnerName: name, 
                        fleetID: t.PartnerFleet.ID, 
                        fleetName: t.PartnerFleet.name,
                        pickupTime: pickupTime,
                        pickupLocation: t.pickupLocation,
                        driverID: t.driver.ID, 
                        driverName: t.driver.name, 
                        driverLocation: t.driver.location, 
                        dropoffTime: t.dropoffTime,
                        dropoffLocation: t.dropoffLocation,
                        vehicleType: t.vehicleType,
                        ETA: ETA,
                        distance: distance,
                        price: t.price,
                        status: t.status,
                        passengerName: t.passengerName
                        );
                }
                else
                {
                    response = new GetTripStatusResponse(
                        partnerID: ID,
                        partnerName: name,
                        pickupTime: pickupTime,
                        pickupLocation: t.pickupLocation,
                        dropoffTime: t.dropoffTime,
                        dropoffLocation: t.dropoffLocation,
                        passengerName: t.passengerName,
                        status: t.status
                        );
                }
                return response;
            }
            catch (Exception e)
            {
                exceptions++;
                Logger.Log("Exception=" + e.Message);
                Logger.LogDebug("GetTripStatus=" + e.Message, e.ToString());
                return new GetTripStatusResponse(result: Result.UnknownError);
            }
        }
        public override UpdateTripStatusResponse UpdateTripStatus(UpdateTripStatusRequest r)
        {
            try
            {
                // Note: GetTrip populates the foreignTripID
                requests++;
                if (!tripsByID.ContainsKey(r.tripID))
                    return new UpdateTripStatusResponse(result: Result.NotFound);
                PartnerTrip t = tripsByID[r.tripID];
                UpdateTripStatusResponse response = new UpdateTripStatusResponse();
                t.SetStatus(r.status, notifyPartner: false);
                return response;
            }
            catch (Exception e)
            {
                exceptions++;
                Logger.Log("Exception=" + e.Message);
                Logger.LogDebug("UpdateTripStatus=" + e.Message, e.ToString());
                return new UpdateTripStatusResponse(result: Result.UnknownError);
            }
        }

        public Partner(string ID, string name, Gateway tripthru, List<PartnerFleet> PartnerFleets = null, string preferedPartnerId = null) : base(ID, name)
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
            if (force || DateTime.UtcNow > trip.lastUpdate + updateInterval)
            {
                Logger.Log("Getting (Foreign) status of " + trip);
                Logger.Tab();
                Gateway.GetTripStatusRequest request = new Gateway.GetTripStatusRequest(clientID: ID, tripID: trip.ID);
                Gateway.GetTripStatusResponse response = tripthru.GetTripStatus(request);
                if (response.status != null)
                    trip.SetStatus((Status)response.status, notifyPartner: false);
                if (response.driverName != null)
                    trip.driver = new Driver(name: response.driverName, location: response.driverLocation);
                if (response.dropoffTime != null)
                    trip.dropoffTime = response.dropoffTime;
                if (response.vehicleType != null)
                    trip.vehicleType = response.vehicleType;
                if (response.ETA != null)
                    trip.ETA = response.ETA;
                if (response.distance != null)
                    trip.distance = response.distance;
                Logger.Untab();
                trip.lastUpdate = DateTime.UtcNow;
            }
        }

        public override void Update()
        {
            if (DateTime.UtcNow < lastSim + simInterval)
                return;
            Logger.BeginRequest("", null);
            foreach (PartnerFleet f in PartnerFleets.Values)
                f.Simulate();
            lastSim = DateTime.UtcNow;
            Logger.EndRequest(null);
        }
    }

    // a route consists of waypoints that are 5 mins apart.
    public class PartnerTrip : IDName
    {
        public Status status { get { return _status; } }
        public enum Origination { Local, Foreign };
        public void SetStatus(Status value, bool notifyPartner = true)
        {
            if (value == Status.Complete)
            {
                if (service == Origination.Foreign)
                {
                    Gateway.GetTripStatusResponse resp = partner.tripthru.GetTripStatus(new Gateway.GetTripStatusRequest(partner.ID, ID));
                    partner.DeactivateTrip(ID, Status.Complete, resp.price, resp.distance);
                }
                else
                    partner.DeactivateTrip(ID, Status.Complete, PartnerFleet.GetPrice(this), PartnerFleet.GetDistance(this));
            }
            else if (value == Status.Cancelled || value == Status.Rejected)
                partner.DeactivateTrip(ID, value);
            if (_status != value)
            {
                Logger.Log("Trip status changed from " + _status + " to " + value);
                _status = value;
                this.partner.activeTrips[this.ID].Status = value;
                if ((origination == Origination.Foreign || service == Origination.Foreign) && notifyPartner)
                {
                    Logger.Log("Since trip originated elsewhere, notify originating partner through TripThru");
                    Logger.Tab();
                    Gateway.UpdateTripStatusRequest request = new Gateway.UpdateTripStatusRequest(
                        clientID: partner.ID,
                        tripID: ID,
                        status: value);
                    partner.tripthru.UpdateTripStatus(request);
                    Logger.Untab();
                }
            }
        }

        private Status _status;
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

            return s;
        }
        public PartnerTrip(Partner partner, string ID, Origination origination, Location pickupLocation, DateTime pickupTime, PaymentMethod? paymentMethod = null, string passengerID = null, string passengerName = null, Location dropoffLocation = null,
           DateTime? dropoffTime = null, List<Location> waypoints = null, VehicleType? vehicleType = null, double? maxPrice = null, int? minRating = null, PartnerFleet fleet = null, Driver driver = null, TimeSpan? duration = null, double? price = null)
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
            this.SetStatus(Status.Queued, notifyPartner: false);
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
            string s = name;
            if (location != null)
                s += "(@" + location + ")";
            if (route != null)
                s += ", Destination = " + route.end + ", ETA = " + (routeStartTime + route.duration);
            return s;
        }
    }
    public class PartnerFleet : IDName
    {
        public Partner partner;
        public Location location;
        public List<Zone> coverage;
        public List<VehicleType> vehicleTypes;
        public Dictionary<string, Driver> drivers;
        public LinkedList<Driver> availableDrivers;
        public LinkedList<Driver> returningDrivers;
        public Pair<Location, Location>[] possibleTrips;
        public LinkedList<PartnerTrip> queue;
        public Passenger[] passengers;
        public double tripsPerHour;
        public double costPerMile; // in local currency
        public double baseCost;
        public Random random;
        public TimeSpan tripMaxAdvancedNotice = new TimeSpan(0, 15, 0); // minutes
        public TimeSpan simInterval = new TimeSpan(0, 0, 10);
        public TimeSpan updateInterval = new TimeSpan(0, 0, 30); // for simluation
        public TimeSpan missedPeriod = new TimeSpan(0, 15, 0);
        public TimeSpan criticalPeriod = new TimeSpan(0, 15, 0);
        public TimeSpan removalAge = new TimeSpan(0, 5, 0);
        public int maxActiveTrips = 2;


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
                if (Serves(trip.First))
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
        public bool Serves(Location l)
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
        public Route GetDriverRoute(Location from, Location to)
        {
            return MapTools.GetRoute(from, to);

        }
        public Route GetTripRoute(Location from, Location to)
        {
            return MapTools.GetRoute(from, to);
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
            t.driver.routeStartTime = DateTime.UtcNow;
            t.driver.route = GetDriverRoute(t.dropoffLocation, location);
        }
        public bool Dispatch(PartnerTrip t)
        {
            if (!Serves(t.pickupLocation))
            {
                Logger.Log("Pickup location " + t.pickupLocation + " is outside of coverage area");
                return false;
            }
            if (t.status != Status.Queued)
                throw new Exception("Invalid 'Dispatch' status");
            if (availableDrivers.Count > 0)
            {
                t.driver = availableDrivers.First();
                t.PartnerFleet = this;
                availableDrivers.RemoveFirst();
                Logger.Log("Dispatched to " + t.driver.name);
                t.SetStatus(Status.Dispatched);
                return true;
            }
            Logger.Log("No drivers are currently available");
            return false;
        }
        public void GenerateTrips()
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
            {
                Passenger passenger = passengers[random.Next(passengers.Length)];
                Pair<Location, Location> fromTo = possibleTrips[random.Next(possibleTrips.Length)];
                Route route = GetTripRoute(fromTo.First, fromTo.Second);
                DateTime pickupTime = now + new TimeSpan(0, random.Next((int)tripMaxAdvancedNotice.TotalMinutes), 0);
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
                    paymentMethod: PaymentMethod.Cash);
                partner.requests++;
                QueueTrip(trip);
                Logger.Untab();
            }
        }
        public bool QueueTrip(PartnerTrip t)
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
                    DropoffTime = t.dropoffTime,
                    Id = t.ID,
                    OriginatingPartnerId = this.ID,
                    OriginatingPartnerName = this.name,
                    PassengerName = t.passengerName,
                    PickupLocation = t.pickupLocation,
                    PickupTime = t.pickupTime,
                    Price = t.price,
                    Status = t.status,
                    VehicleType = t.vehicleType
                });
            return true;
        }
        public void RemoveTrip(PartnerTrip t)
        {
            queue.Remove(queue.Find(t));
        }
        public void Simulate()
        {
            GenerateTrips();
            ProcessQueue();
            LinkedListNode<Driver> next = null;
            for (LinkedListNode<Driver> node = returningDrivers.First; node != null; node = next)
            {
                Driver driver = node.Value;
                next = node.Next;
                driver.location = driver.route.GetCurrentWaypoint(driver.routeStartTime, DateTime.UtcNow);
                if (driver.location == location)
                {
                    Logger.Log("Driver " + driver.name + " has reached the home office ");
                    returningDrivers.Remove(node);
                }
                else if (DateTime.UtcNow > driver.lastUpdate + updateInterval)
                {
                    Logger.Log("Driver update: " + driver);
                    driver.lastUpdate = DateTime.UtcNow;
                }
            }
        }

        public void RemoveTrip(LinkedListNode<PartnerTrip> t)
        {
            partner.tripsByID.Remove(t.Value.ID);
            queue.Remove(t);
        }
        public void ProcessQueue()
        {
            for (LinkedListNode<PartnerTrip> node = queue.First; node != null; )
            {
                PartnerTrip t = node.Value;
                LinkedListNode<PartnerTrip> next = node.Next;

                switch (t.status)
                {
                    case Status.Queued:
                    {
                        if (t.origination == PartnerTrip.Origination.Local)
                        {
                            if (DateTime.UtcNow > t.pickupTime + missedPeriod)
                            {
                                Logger.Log("Missed period reached: -- so cancel " + t);
                                Logger.Tab();
                                t.SetStatus(Status.Cancelled, notifyPartner: true);
                                Logger.Untab();
                                break;
                            }
                            if (t.service == PartnerTrip.Origination.Foreign) // means serviced through partner
                                break;
                        }
                        // If origination is foreign, then it means we're servicing the trip so we have to process it.
                        if (DateTime.UtcNow < t.pickupTime - criticalPeriod)
                            break;
                        Logger.Log("Ready for dispatch: " + t);
                        Logger.Tab();

                        if (!Dispatch(t) && t.origination == PartnerTrip.Origination.Local)
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
                        Logger.Untab();
                        break;
                    }
                    case Status.Dispatched:
                    {
                        if (t.service == PartnerTrip.Origination.Foreign)
                            partner.GetTripStatusFromForeignServiceProvider(t);
                        else if (DateTime.UtcNow >= t.pickupTime - t.PartnerFleet.GetDriverRoute(t.driver.location, t.pickupLocation).duration)
                        {
                            Logger.Log("Driver is now enroute: " + t);
                            Logger.Tab();
                            t.driver.route = GetDriverRoute(t.driver.location, t.pickupLocation);
                            if (t.driver.route == null)
                                throw new Exception("fatal error");
                            t.driver.routeStartTime = DateTime.UtcNow;
                            t.SetStatus(Status.Enroute);
                            Logger.Untab();
                        }
                        else if (DateTime.UtcNow > t.lastUpdate + updateInterval)
                        {
                            Logger.Log("Getting status of: " + t);
                            t.lastUpdate = DateTime.UtcNow;
                        }
                        break;
                    }
                    case Status.Enroute:
                    {
                        if (t.service == PartnerTrip.Origination.Foreign)
                            partner.GetTripStatusFromForeignServiceProvider(t);
                        else
                        {
                            t.driver.location = t.driver.route.GetCurrentWaypoint(t.driver.routeStartTime, DateTime.UtcNow);
                            if (t.driver.location.Equals(t.pickupLocation))
                            {
                                Logger.Log("Picking up: " + t);
                                Logger.Tab();
                                t.driver.route = t.PartnerFleet.GetTripRoute(t.pickupLocation, t.dropoffLocation);
                                t.driver.routeStartTime = DateTime.UtcNow;
                                t.SetStatus(Status.PickedUp);
                                Logger.Untab();
                            }
                            else if (DateTime.UtcNow > t.lastUpdate + updateInterval)
                            {
                                Logger.Log("Getting status of: " + t);
                                t.lastUpdate = DateTime.UtcNow;
                            }
                        }
                        break;
                    }
                    case Status.PickedUp:
                    {
                        if (t.service == PartnerTrip.Origination.Foreign)
                            partner.GetTripStatusFromForeignServiceProvider(t);
                        else
                        {
                            Route route = t.PartnerFleet.GetTripRoute(t.pickupLocation, t.dropoffLocation);
                            t.driver.location = t.driver.route.GetCurrentWaypoint(t.driver.routeStartTime, DateTime.UtcNow);
                            if (t.driver.location == route.end)
                            {
                                Logger.Log("The destination has been reached for: " + t);
                                Logger.Tab();
                                t.dropoffTime = DateTime.UtcNow;
                                t.driver.PartnerFleet.CompleteTrip(t);
                                t.SetStatus(Status.Complete);
                                Logger.Untab();
                            }
                            else if (DateTime.UtcNow > t.lastUpdate + updateInterval)
                            {
                                Logger.Log("Getting status of: " + t);
                                t.lastUpdate = DateTime.UtcNow;
                            }
                        }
                        break;
                    }
                    case Status.Cancelled:
                    case Status.Rejected:
                    {
                        TimeSpan age = DateTime.UtcNow - (DateTime)t.pickupTime;
                        if (age > removalAge) // for now we use 1 minute.
                        {
                            Logger.Log("Since old, remove: " + t);
                            RemoveTrip(node); // remove trips that are more than 1 day old.
                        }
                        break;
                    }
                    case Status.Complete:
                    {
                        if (t.dropoffTime == null)
                            t.dropoffTime = DateTime.UtcNow;
                        TimeSpan age = DateTime.UtcNow - (DateTime)t.dropoffTime;
                        if (age > removalAge) // for now we use 1 minute.
                        {
                            Logger.Log("Since old, remove: " + t);
                            RemoveTrip(node); // remove trips that are more than 1 day old.
                        }
                        break;
                    }
                }
                node = next;
            }

        }
        public static implicit operator Fleet(PartnerFleet f)  // explicit byte to digit conversion operator
        {
            return new Fleet{
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
            Route route = GetTripRoute(trip.pickupLocation, trip.dropoffLocation);
            return route.distance;
        }
        public DateTime GetETA(PartnerTrip trip)
        {
            DateTime ETA;
            if (trip.status == Status.PickedUp)
                ETA = DateTime.UtcNow + GetDriverRoute(location, trip.dropoffLocation).duration;
            else
            {
                ETA = DateTime.UtcNow + GetDriverRoute(location, trip.pickupLocation).duration;
                if (trip.pickupTime > ETA)
                    ETA = trip.pickupTime;
            }
            if (availableDrivers.Count == 0)
                ETA += new TimeSpan(3, 0, 0); // if there are no drivers avaialble we add 3 hrs.  TODO: make this more realistic
            return ETA; // TODO: for now all trips are picked up on time
        }
    }
    
}