using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Utils;
using ServiceStack.Redis;
using ServiceStack;
using ServiceStack.Text;
using System.Runtime.Serialization;
using ServiceStack.ServiceModel;
using TripThruCore.Storage;
namespace TripThruCore
{
    public class PartnerConfiguration
    {
        public ConfigPartner Partner { get; set; }
        public List<Fleet> Fleets { get; set; }
        public string TripThruUrl { get; set; }
        public string TripThruUrlMono { get; set; }
        public int SimInterval { get; set; }
        public List<PartnerFleet> partnerFleets;
        public string preferedPartnerId { get; set; }
        public Boolean Enabled { get; set; }
        public Host host { get; set; }

        public class ConfigPartner
        {
            public string Name { get; set; }
            public string ClientId { get; set; }
            public string AccessToken { get; set; }
            public string CallbackUrl { get; set; }
            public string CallbackUrlMono { get; set; }
            public string WebUrl { get; set; }
            public string WebUrlRelative { get; set; }
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

        public class Host
        {
            public string virtualPath { get; set; }
        }
    }

    public enum Status { New, Queued, Dispatched, Confirmed, Enroute, ArrivedAndWaiting, PickedUp, DroppedOff, Complete, Rejected, Cancelled };

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
        public string Address { get; set; } // temp until we hookup with a geolocator serviced
        public Location()
        {
        }
        public Location(double lat, double lng, string address = null)
        {
            this.Lng = lng;
            this.Lat = lat;
            if (address == null)
                this.Address = MapTools.GetReverseGeoLoc(this);
            else
                this.Address = address;
        }
        public string getID()
        {
            return "<" + Lat + ":" + Lng + ">";
        }
        public override string ToString()
        {
            if (Address != null)
                return Address;
            return "(" + Lat + ", " + Lng + ")";
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
        public bool Equals(Location l, double tolerance = .005)
        {
            double distance = GetDistance(l);
            return distance < tolerance;
        }
    }


    public class Trip
    {
        public string Id { get; set; }
        public string OriginatingPartnerName { get; set; }
        public string OriginatingPartnerId { get; set; }
        public string ServicingPartnerName { get; set; }
        public string ServicingPartnerId { get; set; }
        public string FleetId { get; set; }
        public string FleetName { get; set; }
        public string DriverId { get; set; }
        public string DriverName { get; set; }
        public string PassengerName { get; set; }
        public Location DriverLocation { get; set; } // no get/set so servicestack will ignore during serialization
        public Location PickupLocation { get; set; } // no get/set so servicestack will ignore during serialization
        public Location DropoffLocation { get; set; }  // no get/set so servicestack will ignore during serialization
        public Location DriverInitiaLocation { get; set; }
        public DateTime? PickupTime { get; set; }
        public DateTime? DropoffTime { get; set; }
        public VehicleType? VehicleType { get; set; }
        public Status? Status { get; set; }
        public DateTime? ETA { get; set; } // in minutes;
        public double? Price { get; set; }
        public double? Distance { get; set; }
        public double? DriverRouteDuration { get; set; }
        public void Update(Trip trip)
        {
            this.FleetId = trip.FleetId;
            this.FleetName = trip.FleetName;
            this.DriverId = trip.DriverId;
            this.DriverName = trip.DriverName;
            this.DriverLocation = trip.DriverLocation;
            this.Status = trip.Status;
            this.ETA = trip.ETA;
            this.Price = trip.Price;
            this.Distance = trip.Distance;
            this.DriverRouteDuration = trip.DriverRouteDuration;
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
        public string ID { get; set; }
        public string name { get; set; }
        public enum Result { OK = 100, MethodNotSupported = 200, Rejected = 300, UnknownError = 400, InvalidParameters = 500, NotFound = 600, AuthenticationError = 700 };
        public Gateway(string ID, string name)
        {
            this.ID = ID;
            this.name = name;
        }
        virtual public PartnerAccount GetPartnerAccountByAccessToken(string accessToken)
        {
            throw new Exception("not supported");
            return null;
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
            public string clientID { get; set; }  // TODO: TripThru needs to know who's making the call
            public string name { get; set; }
            public string callback_url { get; set; } // TODO: This should actually be a string, since we're not yet integrated I'm just using an object pointer
            public string accessToken { get; set; } //Todo: Lets assume for now that in Gateway service we retrieve an access token after registering partner in DB

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
            public Result result { get; set; }
            public string partnerID { get; set; }
        }
        public class GetPartnerInfoRequest
        {
            public string clientID { get; set; }  // TODO: TripThru needs to know who's making the call
            public List<Zone> coverage { get; set; }
            public List<VehicleType> vehicleTypes { get; set; }
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
            public Result result { get; set; }
            public List<Fleet> fleets { get; set; }
            public List<VehicleType> vehicleTypes { get; set; }
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
            public string clientID { get; set; }  // TODO: TripThru needs to know who's making the call
            public string passengerID { get; set; }
            public string passengerName { get; set; }
            public int? luggage { get; set; }
            public int? persons { get; set; }
            public Location pickupLocation { get; set; }
            public DateTime pickupTime { get; set; }
            public Location dropoffLocation { get; set; }
            public List<Location> waypoints { get; set; }
            public PaymentMethod? paymentMethod { get; set; }
            public VehicleType? vehicleType { get; set; }
            public double? maxPrice { get; set; }
            public int? minRating { get; set; }
            public string tripID { get; set; }
            public string partnerID { get; set; }
            public string fleetID { get; set; }
            public string driverID { get; set; }
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
            public Result result { get; set; }
        }
        public class QuoteTripRequest
        {
            public string clientID { get; set; }  // TODO: TripThru needs to know who's making the call
            public string passengerID { get; set; }
            public string passengerName { get; set; }
            public int? luggage { get; set; }
            public int? persons { get; set; }
            public Location pickupLocation { get; set; }
            public DateTime pickupTime { get; set; }
            public Location dropoffLocation { get; set; }
            public List<Location> waypoints { get; set; }
            public PaymentMethod? paymentMethod { get; set; }
            public VehicleType? vehicleType { get; set; }
            public double? maxPrice { get; set; }
            public int? minRating { get; set; }
            public string partnerID { get; set; }
            public string fleetID { get; set; }
            public string driverID { get; set; }
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

            public Result result { get; set; }
            public List<Quote> quotes { get; set; }
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
            public string clientID { get; set; }  // TODO: TripThru needs to know who's making the call
            public Status? status { get; set; }
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
            public Result result { get; set; }
            public List<Trip> trips { get; set; }
            public GetTripsResponse(List<Trip> trips, Result result = Result.OK)
            {
                this.trips = trips;
                this.result = result;
            }
            public override string ToString()
            {
                string s = "Result = " + result;
                return s;
            }
        }
        public class GetTripStatusRequest
        {
            public string clientID { get; set; }  // TODO: TripThru needs to know who's making the call
            public string tripID { get; set; }
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
            public Result result { get; set; }
            public string partnerID { get; set; }
            public string partnerName { get; set; }
            public string fleetID { get; set; }
            public string fleetName { get; set; }
            public string passengerName { get; set; }
            public string driverID { get; set; }
            public string driverName { get; set; }
            public Location driverLocation { get; set; }
            public Location driverInitialLocation { get; set; }
            public DateTime? pickupTime { get; set; }
            public Location pickupLocation { get; set; }
            public DateTime? dropoffTime { get; set; }
            public Location dropoffLocation { get; set; }
            public VehicleType? vehicleType { get; set; }
            public Status? status { get; set; }
            public DateTime? ETA { get; set; } // in minutes;
            public double? price { get; set; }
            public double? distance { get; set; }
            public double? driverRouteDuration { get; set; }
            public string originatingPartnerName { get; set; }
            public string servicingPartnerName { get; set; }
            public GetTripStatusResponse(string partnerID = null, string partnerName = null, string fleetID = null, string fleetName = null, string originatingPartnerName = null,
                string servicingPartnerName = null, string driverID = null, string driverName = null, Location driverLocation = null, Location driverInitialLocation = null, VehicleType? vehicleType = null, string passengerName = null,
                DateTime? ETA = null, Status? status = null, DateTime? pickupTime = null, Location pickupLocation = null, DateTime? dropoffTime = null, Location dropoffLocation = null,
                double? price = null, double? distance = null, double? driverRouteDuration = null, Result result = Result.OK
                 )
            {
                this.partnerID = partnerID;
                this.partnerName = partnerName;
                this.fleetID = fleetID;
                this.fleetName = fleetName;
                this.passengerName = passengerName;
                this.driverID = driverID;
                this.driverName = driverName;
                this.driverLocation = driverLocation;
                this.driverInitialLocation = driverInitialLocation;
                this.vehicleType = vehicleType;
                this.ETA = ETA;
                this.pickupTime = pickupTime;
                this.pickupLocation = pickupLocation;
                this.dropoffLocation = dropoffLocation;
                this.dropoffTime = dropoffTime;
                this.price = price;
                this.distance = distance;
                this.driverRouteDuration = driverRouteDuration;
                this.result = result;
                this.status = status;
                this.originatingPartnerName = originatingPartnerName;
                this.servicingPartnerName = servicingPartnerName;
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
            public string clientID { get; set; }  // TODO: TripThru needs to know who's making the call
            public string tripID { get; set; }
            public Status status { get; set; }
            public Location driverLocation { get; set; }
            public DateTime? eta { get; set; }
            public UpdateTripStatusRequest(string clientID, string tripID, Status status, Location driverLocation = null, DateTime? eta = null)
            {
                this.clientID = clientID;
                this.tripID = tripID;
                this.status = status;
                this.driverLocation = driverLocation;
                this.eta = eta;
            }
            public override string ToString()
            {
                return "ClientID = " + clientID;
            }
        }
        public class UpdateTripStatusResponse
        {
            public Result result { get; set; }
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
        virtual public void Update()
        {
            throw new Exception("Not supported");
        }
        virtual public string GetName(string clientID)
        {
            throw new Exception("Not supported");
        }
        virtual public void Log()
        {
        }
    }

    public class GatewayWithStats : Gateway
    {
        public GatewayWithStats(string ID, string name)
            : base(ID, name)
        {
            JsConfig.AssumeUtc = true;
            redisClient = (RedisClient)redis.GetClient();
            exceptions = new RedisStat(redisClient, ID + ":" + MemberInfoGetting.GetMemberName(() => exceptions));
            rejects = new RedisStat(redisClient, ID + ":" + MemberInfoGetting.GetMemberName(() => rejects));
            requests = new RedisStat(redisClient, ID + ":" + MemberInfoGetting.GetMemberName(() => requests));
            cancels = new RedisStat(redisClient, ID + ":" + MemberInfoGetting.GetMemberName(() => cancels));
            fare = new RedisStat(redisClient, ID + ":" + MemberInfoGetting.GetMemberName(() => fare));
            completes = new RedisStat(redisClient, ID + ":" + MemberInfoGetting.GetMemberName(() => completes));
            distance = new RedisStat(redisClient, ID + ":" + MemberInfoGetting.GetMemberName(() => distance));
        }

        public class RedisStat
        {
            public RedisObject<double> allTime;
            public RedisExpiryCounter last24Hrs;
            public RedisExpiryCounter lastHour;
            public RedisStat(RedisClient redis, string id)
            {
                allTime = new RedisObject<double>(redis, id + ":" + MemberInfoGetting.GetMemberName(() => allTime));
                last24Hrs = new RedisExpiryCounter(redis, id + ":" + MemberInfoGetting.GetMemberName(() => last24Hrs), new TimeSpan(24, 0, 0));
                lastHour = new RedisExpiryCounter(redis, id + ":" + MemberInfoGetting.GetMemberName(() => lastHour), new TimeSpan(1, 0, 0));
            }
            public static RedisStat operator ++(RedisStat s)
            {
                // Increment this widget.
                s.allTime++;
                s.last24Hrs++;
                s.lastHour++;
                return s;
            }
            public static RedisStat operator +(RedisStat s, double d)
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
        public RedisStat exceptions;
        public RedisStat requests;
        public RedisStat rejects;
        public RedisStat cancels;
        public RedisStat distance;
        public RedisStat completes;
        public RedisStat fare;
        protected RedisClient redisClient;
        public readonly PooledRedisClientManager redis = new PooledRedisClientManager("localhost:6379")
        {
            ConnectTimeout = 5000,
            IdleTimeOutSecs = 30000
        };
    }

    public class GatewayServer : GatewayWithStats
    {
        public class ActiveTrips
        {
            private RedisDictionary<string, Trip> dict;
            public bool IsEmpty { get { return dict.IsEmpty; } }
            public int Count { get { return dict.Count; } }
            public IEnumerable<Trip> Values { get { return dict.Values; } }
            public ActiveTrips(RedisClient redisClient, string id)
            {
                dict = new RedisDictionary<string, Trip>(redisClient, id);
                dict.Clear();

            }
            public bool ContainsKey(string id)
            {
                return dict.Keys.Contains(id);
            }
            public void Add(string id, Trip trip)
            {
                dict.Add(id, trip);
            }
            public void Remove(string id)
            {
                Logger.Log("Removing active trip " + id);
                dict.Remove(id);
            }
            public Trip this[string id]
            {
                get { return dict[id]; }
            }
        }

        public ActiveTrips activeTrips;

        public GarbageCleanup<string> garbageCleanup;
        public RedisDictionary<string, string> clientIdByAccessToken;
        public RedisDictionary<string, PartnerAccount> partnerAccounts;
        public override PartnerAccount GetPartnerAccountByAccessToken(string accessToken)
        {
            if (accessToken == null) // TODO: this is to get swagger working and is temporary.  We need to add swagger authentication support
                accessToken = "metro12ondazazxx21";
            if (!clientIdByAccessToken.ContainsKey(accessToken))
                return null;
            string clientID = clientIdByAccessToken[accessToken];
            return partnerAccounts[clientID];
        }

        public override GetGatewayStatsResponse GetGatewayStats(Gateway.GetGatewayStatsRequest request)
        {
            try
            {
                GetGatewayStatsResponse resp = new GetGatewayStatsResponse(
                    activeTrips: activeTrips.Count,
                    requestsAllTime: (long)requests.allTime.value, requestsLast24Hrs: (long)requests.last24Hrs.Count, requestsLastHour: (long)requests.lastHour.Count,
                    rejectsAllTime: (long)rejects.allTime.value, rejectsLast24Hrs: (long)rejects.last24Hrs.Count, rejectsLastHour: (long)rejects.lastHour.Count,
                    cancelsAllTime: (long)cancels.allTime.value, cancelsLast24Hrs: (long)cancels.last24Hrs.Count, cancelsLastHour: (long)cancels.lastHour.Count,
                    exceptionsAllTime: (long)exceptions.allTime.value, exceptionsLast24Hrs: (long)exceptions.last24Hrs.Count, exceptionsLastHour: (long)exceptions.lastHour.Count,
                    tripsAllTime: (long)completes.allTime.value, tripsLast24Hrs: (long)completes.last24Hrs.Count, tripsLastHour: (long)completes.lastHour.Count,
                    distanceAllTime: distance.allTime.value, distanceLast24Hours: distance.last24Hrs.Value, distanceLastHour: distance.lastHour.Value,
                    fareAllTime: fare.allTime.value, fareLast24Hrs: fare.last24Hrs.Value, fareLastHour: fare.lastHour.Value);
                return resp;
            }
            catch (Exception e)
            {
                exceptions++;
                Logger.LogDebug("GatewayStats=" + e.Message, e.ToString());
                return new GetGatewayStatsResponse(result: Result.UnknownError);
            }
        }
        readonly TimeSpan getGatewayStatsInterval = new TimeSpan(0, 2, 0);
        DateTime lastGetGatewayStats = DateTime.UtcNow;
        public GatewayServer(string ID, string name)
            : base(ID, name)
        {
            JsConfig.AssumeUtc = true;
            activeTrips = new ActiveTrips(redisClient, ID + ":" + MemberInfoGetting.GetMemberName(() => activeTrips));
            partnerAccounts = new RedisDictionary<string, PartnerAccount>(redisClient, ID + ":" + MemberInfoGetting.GetMemberName(() => partnerAccounts));
            clientIdByAccessToken = new RedisDictionary<string, string>(redisClient, ID + ":" + MemberInfoGetting.GetMemberName(() => clientIdByAccessToken));
        }
        public override Gateway.RegisterPartnerResponse RegisterPartner(Gateway partner)
        {
            return MakeRejectRegisterPartnerResponse();
        }
        protected RegisterPartnerResponse MakeRejectRegisterPartnerResponse(){
            RegisterPartnerResponse response;
            rejects++;
            response = new RegisterPartnerResponse(result: Result.Rejected);
            return response;
        }
        override public DispatchTripResponse DispatchTrip(DispatchTripRequest request)
        {
            return MakeRejectDispatchResponse();
        }
        protected DispatchTripResponse MakeRejectDispatchResponse()
        {
            DispatchTripResponse response;
            rejects++;
            response = new DispatchTripResponse(result: Result.Rejected);
            return response;
        }
        public void DeactivateTripAndUpdateStats(string tripID, Status status, double? price = null, double? distance = null)
        {
            if (!activeTrips.ContainsKey(tripID))
                return;
            Logger.Log("Deactivating trip " + tripID + " from " + name);

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

        public void UpdateActiveTrip(Trip trip)
        {
            if (activeTrips.ContainsKey(trip.Id))
            {
                activeTrips[trip.Id].FleetId = trip.FleetId;
                activeTrips[trip.Id].FleetName = trip.FleetName;
                activeTrips[trip.Id].DriverId = trip.DriverId;
                activeTrips[trip.Id].DriverName = trip.DriverName;
                activeTrips[trip.Id].Status = trip.Status;
                activeTrips[trip.Id].ETA = trip.ETA;
                activeTrips[trip.Id].Price = trip.Price;
                activeTrips[trip.Id].Distance = trip.Distance;
            }
        }
        public void LogStats()
        {
            if ((DateTime.UtcNow - lastGetGatewayStats) > getGatewayStatsInterval)
            {
                Gateway.GetGatewayStatsResponse r = GetGatewayStats(new Gateway.GetGatewayStatsRequest());
                Logger.BeginRequest(name + " Stats: ActiveTrips = " + r.activeTrips, null);
                Logger.Log("Requests: AllTime = " + r.requestsAllTime + ", Last24Hrs = " + r.requestsLast24Hrs + ", LastHour = " + r.requestsLastHour);
                Logger.Log("Reject: AllTime = " + r.rejectsAllTime + ", Last24Hrs = " + r.rejectsLast24Hrs + ", LastHour = " + r.rejectsLastHour);
                Logger.Log("Cancel: AllTime = " + r.cancelsAllTime + ", Last24Hrs = " + r.cancelsLast24Hrs + ", LastHour = " + r.cancelsLastHour);
                Logger.Log("Exceptions: AllTime = " + r.exceptionsAllTime + ", Last24Hrs = " + r.exceptionsLast24Hrs + ", LastHour = " + r.exceptionsLastHour);
                Logger.Log("Completes: AllTime = " + r.tripsAllTime + ", Last24Hrs = " + r.tripsLast24Hrs + ", LastHour = " + r.tripsLastHour);
                Logger.Log("Distance: AllTime = " + r.distanceAllTime + ", Last24Hrs = " + r.distanceLast24Hrs + ", LastHour = " + r.distanceLastHour);
                Logger.Log("Fare: AllTime = " + r.fareAllTime + ", Last24Hrs = " + r.fareLast24Hrs + ", LastHour = " + r.fareLastHour);
                Logger.Log("Per Trip Averages: Distance = " + r.distanceAllTime / r.tripsAllTime + ", Fare = " + r.fareAllTime / r.tripsAllTime);
                lastGetGatewayStats = DateTime.UtcNow;
                Logger.EndRequest(null);
            }
        }
    }
}