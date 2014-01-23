using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using RestSharp;

namespace ServiceStack.TripThruGateway.TripThru
{
    public class Partner : Gateway
    {
        public new class GetPartnerInfo : Gateway.GetPartnerInfo
        {
            public Partner partner;
            public GetPartnerInfo(Partner partner)
            {
                this.partner = partner;
            }
            public override Response Get(Request request)
            {
                int tab = Logger.tab;
                try
                {
                    partner.requests++;
                    List<VehicleType> vehicleTypes = new List<VehicleType>();
                    foreach (PartnerFleet f in partner.PartnerFleets.Values)
                        vehicleTypes.AddRange(f.vehicleTypes);
                    List<Fleet> PartnerFleets = new List<Fleet>();
                    foreach (PartnerFleet f in partner.PartnerFleets.Values)
                        PartnerFleets.Add(new Fleet
                        {
                            FleetId = f.ID,
                            FleetName = f.name,
                            PartnerId = this.partner.ID,
                            PartnerName = this.partner.name,
                            Zone = f.coverage
                        });
                    Response response = new Response(PartnerFleets, vehicleTypes);
                    Logger.tab = tab;
                    Logger.Log("GetPartnerInfo called on " + partner.name + ": Response = " + response);
                    return response;
                }
                catch (Exception e)
                {
                    Logger.tab = tab;
                    partner.exceptions++;
                    Logger.Log("Exception :" + e.Message);
                    return new Response(result: Result.UnknownError);
                }
            }
        }
        public new class DispatchTrip : Gateway.DispatchTrip
        {
            public Partner partner;
            public Trip GetTrip(Request r)
            {
                return new Trip(
                    partner: partner,
                    ID: r.tripID,
                    origination: Trip.Origination.Foreign,
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
                    fleet: r.fleetID == null ? null : partner.PartnerFleets[r.fleetID],
                    driver: r.driverID == null ? null : partner.PartnerFleets[r.fleetID].drivers[r.driverID]);
            }
            public DispatchTrip(Partner partner)
            {
                this.partner = partner;
            }
            public override Response Post(Request r)
            {

                int tab = Logger.tab;
                try
                {
                    partner.requests++;
                    if (r.fleetID != null)
                    {
                        PartnerFleet f = partner.PartnerFleets[r.fleetID];
                        if (f.Serves(r.pickupLocation))
                        {
                            Trip trip = GetTrip(r);
                            if (f.QueueTrip(trip))
                            {
                                Response response = new Response();
                                Logger.Log("DispatchTrip successful on " + partner.name + ", Response: " + response);
                                return response;
                            }
                        }
                        return new Response(result: Result.Rejected);
                    }
                    // Note: GetTrip populates the foreignTripID
                    foreach (PartnerFleet f in partner.PartnerFleets.Values)
                    {
                        if (!f.Serves(r.pickupLocation))
                            continue;
                        Trip trip = GetTrip(r);
                        if (f.QueueTrip(trip))
                        {
                            Logger.tab = tab;
                            Response response = new Response();
                            Logger.Log("DispatchTrip successful on " + partner.name + ", Response: " + response);
                            return response;
                        }
                    }
                    {
                        Response response = new Response(result: Result.Rejected);
                        Logger.Log("DispatchTrip rejected on " + partner.name + ", no available drivers -- Response: " + response);
                        return response;
                    }
                }
                catch (Exception e)
                {



                    Logger.tab = tab;
                    partner.exceptions++;
                    Logger.Log("Exception :" + e.Message);
                    return new Response(result: Result.UnknownError);
                }
            }
        }
        public new class QuoteTrip : Gateway.QuoteTrip
        {
            public Trip GetTrip(Request r)
            {
                return new Trip(partner, null, Trip.Origination.Foreign, r.pickupLocation, r.pickupTime, r.paymentMethod, r.passengerID, r.passengerName, r.dropoffLocation,
                    null, r.waypoints, r.vehicleType, r.maxPrice, r.minRating, r.fleetID == null ? null : partner.PartnerFleets[r.fleetID], r.driverID == null ? null : partner.PartnerFleets[r.fleetID].drivers[r.driverID]);
            }
            public Partner partner;
            public QuoteTrip(Partner partner)
            {
                this.partner = partner;
            }
            public override Response Get(Request r)
            {
                int tab = Logger.tab;
                try
                {
                    partner.requests++;
                    List<Quote> quotes = new List<Quote>();
                    bool pickupLocationNotServed = true;
                    foreach (PartnerFleet f in partner.PartnerFleets.Values)
                    {
                        if (!f.Serves(r.pickupLocation))
                            continue;
                        pickupLocationNotServed = false;
                        foreach (VehicleType vehicleType in f.vehicleTypes)
                        {
                            if (r.vehicleType == vehicleType || r.vehicleType == null)
                            {
                                Trip trip = GetTrip(r);
                                trip.vehicleType = vehicleType;
                                quotes.Add(new Quote(
                                    partnerID: partner.ID,
                                    partnerName: partner.name,
                                    fleetID: f.ID, fleetName: f.name,
                                    vehicleType: vehicleType,
                                    price: f.GetPrice(trip),
                                    distance: f.GetDistance(trip),
                                    duration: trip.duration,
                                    ETA: f.GetETA(trip)));
                            }
                        }
                    }
                    Logger.tab = tab;
                    Response response = quotes.Count > 0 ? new Response(quotes) : new Response(result: Result.Rejected);
                    Logger.Log("QuoteTrip called on " + partner.name + ", Response: " + response + (pickupLocationNotServed ? " -- Pickup location is outside of coverage area" : ""));
                    return response;
                }
                catch (Exception e)
                {
                    Logger.tab = tab;
                    partner.exceptions++;
                    Logger.Log("Exception :" + e.Message);
                    return new Response(result: Result.UnknownError);
                }
            }
        }
        public new class GetTripStatus : Gateway.GetTripStatus
        {
            public Partner partner;
            public GetTripStatus(Partner partner)
            {
                this.partner = partner;
            }
            public override Response Get(Request r)
            {
                int tab = Logger.tab;
                try
                {
                    partner.requests++;
                    if (!partner.tripsByID.ContainsKey(r.tripID))
                        return new Response(result: Result.NotFound);

                    Trip t = partner.tripsByID[r.tripID];
                    DateTime? pickupTime = null;
                    if (t.status == Status.PickedUp || t.status == Status.DroppedOff || t.status == Status.Complete)
                        pickupTime = t.pickupTime; // Only if trip has been pickedup.

                    Response response;
                    if (t.driver != null && t.PartnerFleet != null)
                    {
                        if (t.price == null)
                            t.price = t.PartnerFleet.GetPrice(t);
                        DateTime? ETA = t.PartnerFleet.GetETA(t);
                        double? distance = t.PartnerFleet.GetDistance(t);
                        response = new Response(
                            partnerID: partner.ID, 
                            partnerName: partner.name, 
                            fleetID: t.PartnerFleet.ID, 
                            fleetName: t.PartnerFleet.name,
                            pickupTime: pickupTime,
                            driverID: t.driver.ID, 
                            driverName: t.driver.name, 
                            driverLocation: t.driver.location, 
                            dropoffTime: t.dropoffTime,
                            vehicleType: t.vehicleType,
                            ETA: ETA,
                            distance: distance,
                            price: t.price,
                            status: t.status
                            );
                    }
                    else
                    {
                        response = new Response(
                            partnerID: partner.ID,
                            partnerName: partner.name,
                            pickupTime: pickupTime,
                            status: t.status
                            );
                    }
                    Logger.tab = tab;
                    Logger.Log("GetTripStatus called on " + partner.name + ", Response: " + response);
                    return response;
                }
                catch (Exception e)
                {
                    Logger.tab = tab;
                    partner.exceptions++;
                    Logger.Log("Exception :" + e.Message);
                    return new Response(result: Result.UnknownError);
                }
            }
        }
        public new class UpdateTripStatus : Gateway.UpdateTripStatus
        {
            public Partner partner;
            public UpdateTripStatus(Partner partner)
            {
                this.partner = partner;
            }
            public override Response Post(Request r)
            {
                int tab = Logger.tab;
                try
                {
                    // Note: GetTrip populates the foreignTripID
                    partner.requests++;
                    if (!partner.tripsByID.ContainsKey(r.tripID))
                        return new Response(result: Result.NotFound);
                    Trip t = partner.tripsByID[r.tripID];
                    Response response = new Response();
                    Logger.Log("UpdateTripStatus called on " + partner.name + ", Response: " + response);
                    Logger.Tab();
                    t.SetStatus(r.status, notifyPartner: false);
                    Logger.Untab();
                    Logger.tab = tab;
                    return response;
                }
                catch (Exception e)
                {
                    Logger.tab = tab;
                    partner.exceptions++;
                    Logger.Log("Exception :" + e.Message);
                    return new Response(result: Result.UnknownError);
                }
            }
        }
        public string ID;
        public string name;
        public Dictionary<string, PartnerFleet> PartnerFleets;
        public Dictionary<string, Trip> tripsByID;
        public DateTime lastSim;
        public GatewayRestClient tripthru;
        // Configuration parameters
        public TimeSpan simInterval = new TimeSpan(0, 0, 10);
        public TimeSpan updateInterval = new TimeSpan(0, 0, 30); // for simluation

        static long nextID = 0;
        public string GenerateUniqueID() { nextID++; return nextID.ToString() + "@" + ID; }


        public Partner(string name, string clientId, string accessToken, string tripthruURL, List<PartnerFleet> PartnerFleets = null)
        {
            this.tripthru = new GatewayRestClient(accessToken, tripthruURL);
            this.ID = clientId;
            this.name = name;
            this.PartnerFleets = new Dictionary<string, PartnerFleet>();
            if (PartnerFleets != null)
            {
                foreach (PartnerFleet f in PartnerFleets)
                    AddPartnerFleet(f);
            }

            tripsByID = new Dictionary<string, Trip>();

            getPartnerInfo = new GetPartnerInfo(this);
            dispatchTrip = new DispatchTrip(this);
            quoteTrip = new QuoteTrip(this);
            getTripStatus = new GetTripStatus(this);
            updateTripStatus = new UpdateTripStatus(this);
        }
        public void Log()
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
        public void GetTripStatusFromForeignServiceProvider(Trip trip, bool force = false)
        {
            if (force || DateTime.UtcNow > trip.lastUpdate + updateInterval)
            {
                Logger.Log("Getting (Foreign) status of " + trip);
                Logger.Tab();
                Gateway.GetTripStatus.Request request = new Gateway.GetTripStatus.Request(clientID: ID, tripID: trip.ID);
                Gateway.GetTripStatus.Response response = tripthru.GetTripStatus(request);
                if (response.status != null)
                    trip.SetStatus((Status)response.status, notifyPartner: false);
                if (response.driverName != null)
                    trip.driver = new Driver(name: response.driverName, location: response.driverLocation);
                if (response.dropoffTime != null)
                    trip.dropoffTime = response.dropoffTime;
                if (response.vehicleType != null)
                    trip.vehicleType = response.vehicleType;
                Logger.Untab();
                trip.lastUpdate = DateTime.UtcNow;
            }
        }

        public override void Simulate(DateTime until)
        {
            if (DateTime.UtcNow < lastSim + simInterval)
                return;

            foreach (PartnerFleet f in PartnerFleets.Values)
                f.Simulate(until);
            lastSim = DateTime.UtcNow;
        }
    }

    // a route consists of waypoints that are 5 mins apart.
    public class Trip : IDName
    {
        public Status status { get { return _status; } }
        public enum Origination { Local, Foreign };
        public void SetStatus(Status value, bool notifyPartner = true)
        {
            if (value == Status.Complete)
            {
                if (service == Origination.Foreign)
                {
                    Gateway.GetTripStatus.Response resp = partner.tripthru.GetTripStatus(new Gateway.GetTripStatus.Request(partner.ID, ID));
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
                if ((origination == Origination.Foreign || service == Origination.Foreign) && notifyPartner)
                {
                    Logger.Log("Since trip originated elsewhere, notify originating partner through TripThru");
                    Logger.Tab();
                    Gateway.UpdateTripStatus.Request request = new Gateway.UpdateTripStatus.Request(
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
        public Trip(Trip t)
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
            string s = "Trip" + ID;
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
        public Trip(Partner partner, string ID, Origination origination, Location pickupLocation, DateTime pickupTime, PaymentMethod? paymentMethod = null, string passengerID = null, string passengerName = null, Location dropoffLocation = null,
           DateTime? dropoffTime = null, List<Location> waypoints = null, VehicleType? vehicleType = null, double? maxPrice = null, int? minRating = null, PartnerFleet fleet = null, Driver driver = null, string foreignID = null, TimeSpan? duration = null, double? price = null)
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
        public LinkedList<Trip> queue;
        public Passenger[] passengers;
        public double tripsPerHour;
        public double costPerMile; // in local currency
        public double baseCost;
        public Random random;
        public TimeSpan tripMaxAdvancedNotice = new TimeSpan(0, 0, 15); // minutes
        public TimeSpan simInterval = new TimeSpan(0, 0, 10);
        public TimeSpan updateInterval = new TimeSpan(0, 0, 30); // for simluation
        public TimeSpan missedPeriod = new TimeSpan(0, 1, 0);
        public TimeSpan criticalPeriod = new TimeSpan(0, 1, 0);
        public TimeSpan removalAge = new TimeSpan(0, 5, 0);


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

            this.queue = new LinkedList<Trip>();

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
        public void CompleteTrip(Trip t)
        {
            returningDrivers.AddLast(t.driver);
            availableDrivers.AddLast(t.driver);
            t.driver.routeStartTime = DateTime.UtcNow;
            t.driver.route = GetDriverRoute(t.dropoffLocation, location);
        }
        public bool Dispatch(Trip t)
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
            if (queue.Count > 100)
                return; // lets not let the queue get too big
            int numTripsToGenerate = (int)Math.Floor(simInterval.TotalHours * tripsPerHour);
            {
                // this handles fractional trips.
                double d = (simInterval.TotalHours * tripsPerHour) - (double)numTripsToGenerate;
                if (d > random.NextDouble())
                    numTripsToGenerate++;
            }
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
                Trip trip = new Trip(
                    partner: this.partner,
                    ID: Trip.GenerateUniqueID(this.partner.ID),
                    origination: Trip.Origination.Local,
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
        public bool QueueTrip(Trip t)
        {
            if (availableDrivers.Count == 0 && t.origination == Trip.Origination.Foreign)
                return false; // don't except from parters if no available drivers
            Logger.Log("Queueing " + t);
            queue.AddLast(t);
            partner.tripsByID.Add(t.ID, t);
            return true;
        }
        public void RemoveTrip(Trip t)
        {
            queue.Remove(queue.Find(t));
        }
        public void Simulate(DateTime until)
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
                    Logger.Log("Driver update: " + driver);
            }
        }

        public void RemoveTrip(LinkedListNode<Trip> t)
        {
            partner.tripsByID.Remove(t.Value.ID);
            queue.Remove(t);
        }
        public void ProcessQueue()
        {
            for (LinkedListNode<Trip> node = queue.First; node != null; )
            {
                Trip t = node.Value;
                LinkedListNode<Trip> next = node.Next;

                switch (t.status)
                {
                    case Status.Queued:
                        {
                            if (t.origination == Trip.Origination.Local)
                            {
                                if (DateTime.UtcNow > t.pickupTime + missedPeriod)
                                {
                                    Logger.Log("Missed period reached: -- so cancel " + t);
                                    Logger.Tab();
                                    t.SetStatus(Status.Cancelled, notifyPartner: true);
                                    Logger.Untab();
                                    break;
                                }
                                if (t.service == Trip.Origination.Foreign) // means serviced through partner
                                    break;
                            }
                            // If origination is foreign, then it means we're servicing the trip so we have to process it.
                            if (DateTime.UtcNow < t.pickupTime - criticalPeriod)
                                break;
                            Logger.Log("Ready for dispatch: " + t);
                            Logger.Tab();

                            if (!Dispatch(t) && t.origination == Trip.Origination.Local)
                            {
                                Logger.Tab();
                                Gateway.DispatchTrip.Request request = new Gateway.DispatchTrip.Request(
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
                                    minRating: t.minRating);
                                Gateway.DispatchTrip.Response response = partner.tripthru.DispatchTrip(request);
                                if (response.result == Gateway.Result.OK)
                                {
                                    // Actually, at this point its just in the partner's queue, until its dispatch to the partner's drivers -- so no status update. 
                                    t.service = Trip.Origination.Foreign;
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
                            if (t.service == Trip.Origination.Foreign)
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
                            if (t.service == Trip.Origination.Foreign)
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
                            if (t.service == Trip.Origination.Foreign)
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
                                partner.GetTripStatusFromForeignServiceProvider(t, true);
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
                Zone = f.coverage
            };  // explicit conversion
        }

        // TODO: make these more real
        public double GetPrice(Trip trip)
        {
            return baseCost + (GetDistance(trip) * costPerMile); // once we have the distance it won't be too hard to come up with some prices.
        }
        public double GetDistance(Trip trip)
        {
            Route route = GetTripRoute(trip.pickupLocation, trip.dropoffLocation);
            return route.distance;
        }
        public DateTime GetETA(Trip trip)
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