using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Utils;

namespace TripThruCore
{
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
    public enum Status { Queued, Dispatched, Enroute, PickedUp, DroppedOff, Complete, Rejected, Cancelled };
    public enum VehicleType { Compact, Sedan };
    public enum PaymentMethod { Cash, Credit, Account };
    public class Zone
    {
        public Location Center { get; set; }
        public double Radius { get; set; }

        public Zone()
        {
            
        }
        public Zone(Location center, double radius)
        {
            this.Center = center;
            this.Radius = radius;
        }
        public double DegreesToRadians(double angle)
        {
            return Math.PI * angle / 180.0;
        }
        public bool IsInside(Location l)
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

    }
    public class Location
    {
        public Double Lat { get; set; }
        public Double Lng { get; set; }
//        public string StreetAddress { get; set; }
//        public string PostalCode { get; set; }
        public string Name { get; set; } // temp until we hookup with a geolocator serviced
        public Location()
        {
        }
        public Location(double lat, double lng, string name = null)
        {
            this.Lng = lng;
            this.Lat = lat;
            if (name == null)
                this.Name = MapTools.GetReverseGeoLoc(this);
            else
                this.Name = name;
        }
        public double DegreesToRadians(double angle)
        {
            return Math.PI * angle / 180.0;
        }
        public double GetDistance(Location l)
        {
            double lat1 = DegreesToRadians(Lat);
            double lng1 = DegreesToRadians(Lng);
            double lat2 = DegreesToRadians(l.Lat);
            double lng2 = DegreesToRadians(l.Lng);
            double dlon = lng2 - lng1;
            double dlat = lat2 - lat1;
            double a = Math.Pow(Math.Sin(dlat / 2.0), 2) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(dlon / 2.0), 2);
            double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            double d = 3961.0 * c; // (where 3961 is the radius of the Earth in miles
            return d;
        }
        public bool Equals(Location l)
        {
            double distance = Math.Sqrt(Math.Pow(l.Lat - Lat, 2) + Math.Pow(l.Lng - Lng, 2));
            return distance < .00001;
        }
        public string getID()
        {
            return "<" + Lat + ":" + Lng + ">";
        }
        public override string ToString()
        {
            if (Name != null)
                return Name;
            return "(" + Lat + ", " + Lng + ")";
        }
    }

    public class Fleet
    {
        public string PartnerId { get; set; }
        public string PartnerName { get; set; }
        public string FleetId { get; set; }
        public string FleetName { get; set; }
        public List<Zone> Coverage { get; set; }
        public Fleet()
        {
        }
        public Fleet(string partnerID, string partnerName, string fleetID, string fleetName, List<Zone> coverage)
        {
            this.PartnerId = partnerID;
            this.PartnerName = partnerName;
            this.FleetId = fleetID;
            this.FleetName = fleetName;
            this.Coverage = coverage;
        }

        public override string ToString()
        {
            string s = FleetName + ", Location = " + Coverage;
            return s;
        }
    }

    public class Quote
    {
        public Quote()
        {

        }
        public Quote(string partnerID, string partnerName = null, string fleetID = null, string fleetName = null, VehicleType? vehicleType = null, double? price = null, double? distance = null, TimeSpan? duration = null, DateTime? ETA = null)
        {
            this.PartnerId = partnerID;
            this.PartnerName = partnerName;
            this.FleetId = fleetID;
            this.FleetName = fleetName;
            this.VehicleType = vehicleType;
            this.Price = price;
            this.Duration = duration;
            this.ETA = ETA;
        }
        public override string ToString()
        {
            string s = "Partner = " + (PartnerName == null ? PartnerId : PartnerName);
            if (FleetName != null)
                s += ", FleetName = " + FleetName;
            else if (FleetId != null)
                s += ", FleetID = " + FleetId;
            if (VehicleType != null)
                s += ", VehicleType = " + VehicleType;
            if (Price != null)
                s += ", Price = " + String.Format("{0:C}", Price);
            if (ETA != null)
                s += ", ETA = " + ETA;
            return s;
        }

        public string PartnerId { get; set; } // partners don't need to supply this
        public string PartnerName { get; set; } // partners don't need to supply this
        public string FleetId { get; set; }
        public string FleetName { get; set; }
        public VehicleType? VehicleType { get; set; }
        public double? Price { get; set; } // in local currency
        public double? Distance { get; set; } // in km
        public TimeSpan? Duration { get; set; } // estimated duration of the trip
        public DateTime? ETA { get; set; } // estimated time of arrival
    }
    public class Gateway
    {
        public string ID;
        public string name;
        public enum Result { OK = 100, MethodNotSupported = 200, Rejected = 300, UnknownError = 400, InvalidParameters = 500, NotFound = 600, AuthenticationError = 700 };
        public Gateway(string ID, string name)
        {
            this.ID = ID;
            this.name = name;
        }
        public class GetGatewayStatsRequest
        {
            public override string ToString()
            {
                return "stats";
            }
        }
        public class GetGatewayStatsResponse
        {
            public Result result;
            public long activeTrips;
            public long rejectsAllTime;
            public long rejectsLast24Hrs;
            public long rejectsLastHour;
            public long cancelsAllTime;
            public long cancelsLast24Hrs;
            public long cancelsLastHour;
            public long requestsAllTime;
            public long requestsLast24Hrs;
            public long requestsLastHour;
            public double exceptionsAllTime;
            public double exceptionsLast24Hrs;
            public double exceptionsLastHour;
            public long tripsAllTime;
            public double distanceAllTime;
            public double fareAllTime;
            public long tripsLast24Hrs;
            public double distanceLast24Hrs;
            public double fareLast24Hrs;
            public long tripsLastHour;
            public double distanceLastHour;
            public double fareLastHour;
            public GetGatewayStatsResponse(long activeTrips,
                long requestsAllTime, long requestsLast24Hrs, long requestsLastHour,
                long rejectsAllTime, long rejectsLast24Hrs, long rejectsLastHour,
                long cancelsAllTime, long cancelsLast24Hrs, long cancelsLastHour,
                double exceptionsAllTime, long exceptionsLast24Hrs, long exceptionsLastHour,
                long tripsAllTime, long tripsLast24Hrs, long tripsLastHour,
                double distanceAllTime, double distanceLast24Hours, double distanceLastHour,
                double fareAllTime, double fareLast24Hrs, double fareLastHour,
                Result result = Result.OK)
            {
                this.activeTrips = activeTrips;
                this.requestsAllTime = requestsAllTime;
                this.requestsLast24Hrs = requestsLast24Hrs;
                this.requestsLastHour = requestsLastHour;
                this.rejectsAllTime = rejectsAllTime;
                this.rejectsLast24Hrs = rejectsLast24Hrs;
                this.rejectsLastHour = rejectsLastHour;
                this.cancelsAllTime = cancelsAllTime;
                this.cancelsLast24Hrs = cancelsLast24Hrs;
                this.cancelsLastHour = cancelsLastHour;
                this.exceptionsAllTime = exceptionsAllTime;
                this.exceptionsLast24Hrs = exceptionsLast24Hrs;
                this.exceptionsLastHour = exceptionsLastHour;
                this.tripsAllTime = tripsAllTime;
                this.tripsLast24Hrs = tripsLast24Hrs;
                this.tripsLastHour = tripsLastHour;
                this.distanceAllTime = distanceAllTime;
                this.distanceLast24Hrs = distanceLast24Hours;
                this.distanceLastHour = distanceLastHour;
                this.fareAllTime = fareAllTime;
                this.fareLast24Hrs = fareLast24Hrs;
                this.fareLastHour = fareLastHour;
                this.result = result;
            }
            public GetGatewayStatsResponse(Result result = Result.UnknownError)
            {
                this.result = result;
            }
            public override string ToString()
            {
                return "Result = " + result;
            }
        }
        public class RegisterPartnerRequest
        {
            public string clientID;  // TODO: TripThru needs to know who's making the call
            public string name;
            public string callback_url; // TODO: This should actually be a string, since we're not yet integrated I'm just using an object pointer
            public string accessToken; //Todo: Lets assume for now that in Gateway service we retrieve an access token after registering partner in DB

            public RegisterPartnerRequest(string clientID, string name, string callback_url, string accessToken)
            {
                this.clientID = clientID;
                this.name = name;
                this.callback_url = callback_url;
                this.accessToken = accessToken;
            }
            public override string ToString()
            {
                string s = "Partner = " + name;
                if (clientID != null)
                    s += ", ClientID = " + clientID;
                return s;
            }
        }
        public class RegisterPartnerResponse
        {
            public RegisterPartnerResponse(string partnerID = null, Result result = Result.OK)
            {
                this.result = result;
                this.partnerID = partnerID;
            }
            public override string ToString()
            {
                return "PartnerID = " + partnerID + ", Result = " + result;
            }
            public Result result;
            public string partnerID;
        }
        public class GetPartnerInfoRequest
        {
            public string clientID;  // TODO: TripThru needs to know who's making the call
            public List<Zone> coverage;
            public List<VehicleType> vehicleTypes;
            public List<string> fleets { get; set; }
            // DispatchTrip must be supported.
            // GetPartnerInfo is not request as TripThru can provide some of these details
            public bool supportsQuoting;
            public bool supportsGetTripStatus; // may or maynot support tracking
            public bool supportsTracking;
            public bool supportsUpdateTripStatus;
            public bool supportsPayments;
            public GetPartnerInfoRequest(string clientID, List<Zone> coverage = null, List<VehicleType> vehicleTypes = null, List<string> fleets = null)
            {
                this.clientID = clientID;
                this.coverage = coverage;
                this.vehicleTypes = vehicleTypes;
                this.fleets = fleets;
            }
            public override string ToString()
            {
                return "ClientID = " + clientID;
            }
        }
        public class GetPartnerInfoResponse
        {
            public Result result;
            public List<Fleet> fleets;
            public List<VehicleType> vehicleTypes;
            public GetPartnerInfoResponse(List<Fleet> fleets = null, List<VehicleType> vehicleTypes = null, Result result = Result.OK)
            {
                this.fleets = fleets;
                this.vehicleTypes = vehicleTypes;
                this.result = result;
            }
            public override string ToString()
            {
                return "Result = " + result;
            }
        }
        public class DispatchTripRequest
        {
            public string clientID;  // TODO: TripThru needs to know who's making the call
            public string passengerID;
            public string passengerName;
            public int? luggage;
            public int? persons;
            public Location pickupLocation;
            public DateTime pickupTime;
            public Location dropoffLocation;
            public List<Location> waypoints;
            public PaymentMethod? paymentMethod;
            public VehicleType? vehicleType;
            public double? maxPrice;
            public int? minRating;
            public string tripID;
            public string partnerID;
            public string fleetID;
            public string driverID;
            public DispatchTripRequest(string clientID, string tripID, Location pickupLocation, DateTime pickupTime, string passengerID = null, string passengerName = null,
                int? luggage = null, int? persons = null, Location dropoffLocation = null, List<Location> waypoints = null,
                PaymentMethod? paymentMethod = null, VehicleType? vehicleType = null, double? maxPrice = null, int? minRating = null, string partnerID = null,
                string fleetID = null, string driverID = null)
            {
                this.tripID = tripID;
                this.clientID = clientID;
                this.passengerID = passengerID;
                this.passengerName = passengerName;
                this.pickupLocation = pickupLocation;
                this.pickupTime = pickupTime;
                this.dropoffLocation = dropoffLocation;
                this.waypoints = waypoints;
                this.paymentMethod = paymentMethod;
                this.vehicleType = vehicleType;
                this.maxPrice = maxPrice;
                this.minRating = minRating;
                this.partnerID = partnerID;
                this.fleetID = fleetID;
                this.driverID = driverID;
            }
            public override string ToString()
            {
                return "ClientID = " + clientID + ", TripID = " + tripID;
            }
        }
        public class DispatchTripResponse
        {
            public DispatchTripResponse(Result result = Result.OK)
            {
                this.result = result;
            }
            public override string ToString()
            {
                return "Result = " + result;
            }
            public Result result;
        }
        public class QuoteTripRequest
        {
            public string clientID;  // TODO: TripThru needs to know who's making the call
            public string passengerID;
            public string passengerName;
            public int? luggage;
            public int? persons;
            public Location pickupLocation;
            public DateTime pickupTime;
            public Location dropoffLocation;
            public List<Location> waypoints;
            public PaymentMethod? paymentMethod;
            public VehicleType? vehicleType;
            public double? maxPrice;
            public int? minRating;
            public string partnerID;
            public string fleetID;
            public string driverID;
            public QuoteTripRequest(string clientID, Location pickupLocation, DateTime pickupTime, string passengerID = null, string passengerName = null,
                int? luggage = null, int? persons = null, Location dropoffLocation = null, List<Location> waypoints = null,
                PaymentMethod? paymentMethod = null, VehicleType? vehicleType = null, double? maxPrice = null, int? minRating = null, string partnerID = null,
                string fleetID = null, string driverID = null)
            {
                this.clientID = clientID;
                this.passengerID = passengerID;
                this.passengerName = passengerName;
                this.pickupLocation = pickupLocation;
                this.pickupTime = pickupTime;
                this.dropoffLocation = dropoffLocation;
                this.waypoints = waypoints;
                this.paymentMethod = paymentMethod;
                this.vehicleType = vehicleType;
                this.maxPrice = maxPrice;
                this.minRating = minRating;
                this.partnerID = partnerID;
                this.fleetID = fleetID;
                this.driverID = driverID;
            }
            public override string ToString()
            {
                return "ClientID = " + clientID;
            }
        }
        public class QuoteTripResponse
        {
                
            public Result result;
            public List<Quote> quotes;
            public QuoteTripResponse(List<Quote> quotes = null, Result result = Result.OK)
            {
                this.quotes = quotes;
                this.result = result;
            }
            public override string ToString()
            {
                return "Result = " + result;
            }
        }
        public class GetTripsRequest
        {
            public string clientID;  // TODO: TripThru needs to know who's making the call
            public Status? status;
            public GetTripsRequest(string clientID, Status? status = null)
            {
                this.clientID = clientID;
                this.status = status;
            }
            public override string ToString()
            {
                return "ClientID = " + clientID + ", Status = " + status;
            }
        }
        public class GetTripsResponse
        {
            public Result result;
            public List<string> tripIDs;
            public GetTripsResponse(List<string> tripIDs, Result result = Result.OK)
            {
                this.tripIDs = tripIDs;
            }
            public override string ToString()
            {
                string s = "Result = " + result;
                return s;
            }
        }
        public class GetTripStatusRequest
        {
            public string clientID;  // TODO: TripThru needs to know who's making the call
            public string tripID;
            public GetTripStatusRequest(string clientID, string tripID)
            {
                this.clientID = clientID;
                this.tripID = tripID;
            }
            public override string ToString()
            {
                return "ClientID = " + clientID;
            }
        }
        public class GetTripStatusResponse
        {
            public Result result;
            public string partnerID;
            public string partnerName;
            public string fleetID;
            public string fleetName;
            public string driverID;
            public string driverName;
            public Location driverLocation;
            public DateTime? pickupTime;
            public DateTime? dropoffTime;
            public VehicleType? vehicleType;
            public Status? status;
            public DateTime? ETA; // in minutes;
            public double? price;
            public double? distance;
            public GetTripStatusResponse(string partnerID = null, string partnerName = null, string fleetID = null, string fleetName = null,
                string driverID = null, string driverName = null, Location driverLocation = null, VehicleType? vehicleType = null,
                DateTime? ETA = null, Status? status = null, DateTime? pickupTime = null, DateTime? dropoffTime = null, double? price = null, double? distance = null, Result result = Result.OK)
            {
                this.partnerID = partnerID;
                this.partnerName = partnerName;
                this.fleetID = fleetID;
                this.fleetName = fleetName;
                this.driverID = driverID;
                this.driverName = driverName;
                this.driverLocation = driverLocation;
                this.vehicleType = vehicleType;
                this.ETA = ETA;
                this.pickupTime = pickupTime;
                this.dropoffTime = dropoffTime;
                this.price = price;
                this.distance = distance;
                this.result = result;
                this.status = status;
            }
            public override string ToString()
            {
                string s = "Result = " + result;
                if (status != null)
                    s += ", Status = " + status;
                if (ETA != null)
                    s += ", ETA = " + ETA;
                if (pickupTime != null)
                    s += ", PickupTime = " + pickupTime;
                if (driverName != null)
                    s += ", DriverName = " + driverName;
                if (driverLocation != null)
                    s += ", DriverLocation = " + driverLocation;
                if (dropoffTime != null)
                    s += ", DropoffTime = " + dropoffTime;
                if (price != null)
                    s += ", Price = " + dropoffTime;
                if (distance != null)
                    s += ", Distance = " + distance;
                return s;
            }
        }
        public class UpdateTripStatusRequest
        {
            public string clientID;  // TODO: TripThru needs to know who's making the call
            public string tripID;
            public Status status;
            public UpdateTripStatusRequest(string clientID, string tripID, Status status)
            {
                this.clientID = clientID;
                this.tripID = tripID;
                this.status = status;
            }
            public override string ToString()
            {
                return "ClientID = " + clientID;
            }
        }
        public class UpdateTripStatusResponse
        {
            public Result result;
            public UpdateTripStatusResponse(Result result = Result.OK)
            {
                this.result = result;
            }
            public override string ToString()
            {
                return "Result = " + result;
            }
        }

        virtual public RegisterPartnerResponse RegisterPartner(Gateway.RegisterPartnerRequest request)
        {
            throw new Exception("Not supported");

        }
        virtual public RegisterPartnerResponse RegisterPartner(Gateway partner)
        {
            throw new Exception("Not supported");
        }
        virtual public GetPartnerInfoResponse GetPartnerInfo(GetPartnerInfoRequest request)
        {
            throw new Exception("Not supported");
        }
        virtual public DispatchTripResponse DispatchTrip(DispatchTripRequest request)
        {
            throw new Exception("Not supported");
        }
        virtual public QuoteTripResponse QuoteTrip(QuoteTripRequest request)
        {
            throw new Exception("Not supported");
        }
        virtual public GetTripStatusResponse GetTripStatus(GetTripStatusRequest request)
        {
            throw new Exception("Not supported");
        }
        virtual public UpdateTripStatusResponse UpdateTripStatus(UpdateTripStatusRequest request)
        {
            throw new Exception("Not supported");
        }
        virtual public GetGatewayStatsResponse GetGatewayStats(GetGatewayStatsRequest request)
        {
            throw new Exception("Not supported");
        }
        virtual public GetTripsResponse GetTrips(GetTripsRequest request)
        {
            throw new Exception("Not supported");
        }
    }

    public class GatewayServer : Gateway
    {

        public class Stat
        {
            public class Counter
            {
                TimeSpan maxAge;
                Queue<Pair<DateTime, double>> counts;
                double count;
                public static implicit operator int(Counter c)
                {
                    return (int)c.count;
                }
                public static implicit operator long(Counter c)
                {
                    return (long)c.count;
                }
                public static implicit operator double(Counter c)
                {
                    return (int)c.count;
                }
                public Counter(TimeSpan maxAge)
                {
                    this.maxAge = maxAge;
                    counts = new Queue<Pair<DateTime, double>>();
                }
                void Cleanup()
                {
                    while (counts.Count > 0 && DateTime.UtcNow - counts.Peek().First > maxAge)
                    {
                        count -= counts.Peek().Second;
                        counts.Dequeue();
                    }
                }

                public static Counter operator ++(Counter c)
                {
                    // Increment this widget.
                    c.counts.Enqueue(new Pair<DateTime, double>(DateTime.UtcNow, 1));
                    c.count += 1;
                    c.Cleanup();
                    return c;
                }
                public static Counter operator +(Counter c, double d)
                {
                    // Increment this widget.
                    c.counts.Enqueue(new Pair<DateTime, double>(DateTime.UtcNow, d));
                    c.count += d;
                    c.Cleanup();
                    return c;
                }
                public override string ToString()
                {
                    return count.ToString();
                }
            }
            public double allTime;
            public Counter last24Hrs;
            public Counter lastHour;
            public Stat()
            {
                last24Hrs = new Counter(new TimeSpan(24, 0, 0));
                lastHour = new Counter(new TimeSpan(1, 0, 0));
            }
            public static Stat operator ++(Stat s)
            {
                // Increment this widget.
                s.allTime++;
                s.last24Hrs++;
                s.lastHour++;
                return s;
            }
            public static Stat operator +(Stat s, double d)
            {
                // Increment this widget.
                s.allTime += d;
                s.last24Hrs += d;
                s.lastHour += d;
                return s;
            }
            public override string ToString()
            {
                return "AllTime = " + allTime + ", Last24Hrs = " + ((long)last24Hrs) + ", LastHour = " + ((long)lastHour);
            }
        }
        public Stat exceptions;
        public Stat requests;
        public Stat rejects;
        public Stat cancels;
        public Stat distance;
        public Stat completes;
        public Stat fare;
        public HashSet<string> activeTrips;
        public GarbageCleanup<string> garbageCleanup;
        public override GetGatewayStatsResponse GetGatewayStats(Gateway.GetGatewayStatsRequest request)
        {
            try
            {
                GetGatewayStatsResponse resp = new GetGatewayStatsResponse(
                    activeTrips: activeTrips.Count,
                    requestsAllTime: (long)requests.allTime, requestsLast24Hrs: requests.last24Hrs, requestsLastHour: requests.lastHour,
                    rejectsAllTime: (long)rejects.allTime, rejectsLast24Hrs: rejects.last24Hrs, rejectsLastHour: rejects.lastHour,
                    cancelsAllTime: (long)cancels.allTime, cancelsLast24Hrs: cancels.last24Hrs, cancelsLastHour: cancels.lastHour,
                    exceptionsAllTime: (long)exceptions.allTime, exceptionsLast24Hrs: exceptions.last24Hrs, exceptionsLastHour: exceptions.lastHour,
                    tripsAllTime: (long)completes.allTime, tripsLast24Hrs: completes.last24Hrs, tripsLastHour: completes.lastHour,
                    distanceAllTime: distance.allTime, distanceLast24Hours: distance.last24Hrs, distanceLastHour: distance.lastHour,
                    fareAllTime: fare.allTime, fareLast24Hrs: fare.last24Hrs, fareLastHour: fare.lastHour);
                return resp;
            }
            catch (Exception e)
            {
                exceptions++;
                Logger.Log("Exception :" + e.Message);
                return new GetGatewayStatsResponse(result: Result.UnknownError);
            }
        }
        TimeSpan getGatewayStatsInterval = new TimeSpan(0, 2, 0);
        DateTime lastGetGatewayStats = DateTime.UtcNow;

        public GatewayServer(string ID, string name) : base(ID, name)
        {
            exceptions = new Stat();
            rejects = new Stat();
            requests = new Stat();
            cancels = new Stat();
            fare = new Stat();
            completes = new Stat();
            distance = new Stat();
            activeTrips = new HashSet<string>();
        }
        public void DeactivateTrip(string tripID, Status status, double? price = null, double? distance = null)
        {
            if (!activeTrips.Contains(tripID))
                return;

            activeTrips.Remove(tripID);
            switch (status)
            {
                case Status.Complete:
                {
                    completes++;
                    fare += (double)price;
                    this.distance += (double)distance;
                    break;
                }
                case Status.Cancelled: cancels++; break;
                case Status.Rejected: rejects++; break;
            }
            if (garbageCleanup != null)
                garbageCleanup.Add(tripID);
        }
        public void LogStats()
        {
            Logger.BeginRequest("", null);
            if ((DateTime.UtcNow - lastGetGatewayStats) > getGatewayStatsInterval)
            {
                Gateway.GetGatewayStatsResponse r = GetGatewayStats(new Gateway.GetGatewayStatsRequest());
                Logger.Log(name + " Stats: ActiveTrips = " + r.activeTrips);
                Logger.Tab();
                Logger.Log("Requests: AllTime = " + r.requestsAllTime + ", Last24Hrs = " + r.requestsLast24Hrs + ", LastHour = " + r.requestsLastHour);
                Logger.Log("Reject: AllTime = " + r.rejectsAllTime + ", Last24Hrs = " + r.rejectsLast24Hrs + ", LastHour = " + r.rejectsLastHour);
                Logger.Log("Cancel: AllTime = " + r.cancelsAllTime + ", Last24Hrs = " + r.cancelsLast24Hrs + ", LastHour = " + r.cancelsLastHour);
                Logger.Log("Exceptions: AllTime = " + r.exceptionsAllTime + ", Last24Hrs = " + r.exceptionsLast24Hrs + ", LastHour = " + r.exceptionsLastHour);
                Logger.Log("Completes: AllTime = " + r.tripsAllTime + ", Last24Hrs = " + r.tripsLast24Hrs + ", LastHour = " + r.tripsLastHour);
                Logger.Log("Distance: AllTime = " + r.distanceAllTime + ", Last24Hrs = " + r.distanceLast24Hrs + ", LastHour = " + r.distanceLastHour);
                Logger.Log("Fare: AllTime = " + r.fareAllTime + ", Last24Hrs = " + r.fareLast24Hrs + ", LastHour = " + r.fareLastHour);
                Logger.Log("Per Trip Averages: Distance = " + r.distanceAllTime / r.tripsAllTime + ", Fare = " + r.fareAllTime / r.tripsAllTime);
                Logger.Untab();
                lastGetGatewayStats = DateTime.UtcNow;
            }
            Logger.EndRequest(null);
        }
        public virtual void Simulate(DateTime until)
        {
            throw new Exception("Not supported");
        }
    }
}