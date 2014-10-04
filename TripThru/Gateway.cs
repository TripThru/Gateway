using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using ServiceStack.Razor.Compilation.CodeTransformers;
using Utils;
using ServiceStack.Redis;
using ServiceStack;
using ServiceStack.Text;
using System.Runtime.Serialization;
using ServiceStack.ServiceModel;
using TripThruCore.Storage;
using System.Collections.Concurrent;
using MongoDB.Bson.Serialization.Attributes;
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
        public HostConfiguration host { get; set; }
        public string UrlsTrips { get; set; }

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
    }


    public class HostConfiguration
    {
        public string virtualPath { get; set; }
        public bool debug { get; set; }
    }

    public enum Status { New, Queued, Dispatched, Confirmed, Enroute, ArrivedAndWaiting, PickedUp, DroppedOff, Complete, Rejected, Cancelled };

    public enum VehicleType { Compact, Sedan };

    public enum PaymentMethod { Cash, Credit, Account };
    public enum QuoteStatus { New, InProgress, Complete, Sending, Sent };
    public enum TripState { New, Quoting, Dispatching, Dispatched, Local };

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

    [BsonIgnoreExtraElements]
    public class Location
    {
        public Double Lat { get; set; }
        public Double Lng { get; set; }
        //public string Address { get; set; } // temp until we hookup with a geolocator serviced
        public Location()
        {
        }
        public Location(double lat, double lng)
        {
            Lng = lng;
            Lat = lat;
        }
        public string getID()
        {
            return "<" + Lat + ":" + Lng + ">";
        }
        public override string ToString()
        {
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

    public class Shift
    {
        public DateTime DateTime { get; set; }
        public Location Location { get; set; }
    }

    public enum DriverStatus { Occupied, Enroute, Idle, OffDuty };

    public class MongoDBLocationIndex
    {
        public string type { get; set; }
        public Double[] coordinates { get; set; }
        public MongoDBLocationIndex(Double lat, Double lng)
        {
            this.type = "Point";
            this.coordinates = new Double[] { lng, lat };
        }
    }

    public class Trip
    {
        public string Id { get; set; }
        public long? IdNumber { get; set; }
        public string OriginatingPartnerName { get; set; }
        public string OriginatingPartnerId { get; set; }
        public string ServicingPartnerName { get; set; }
        public string ServicingPartnerId { get; set; }
        public string FleetId { get; set; }
        public string FleetName { get; set; }
        public string DriverId { get; set; }
        public string DriverName { get; set; }
        public string PassengerName { get; set; }
        public Location DriverLocation { get; set; }
        private Location _pickupLocation;
        public MongoDBLocationIndex loc;
        public Location PickupLocation 
        { 
            get
            {
                return this._pickupLocation;
            }
            set
            {
                this._pickupLocation = value;
                this.loc = new MongoDBLocationIndex(value.Lat, value.Lng);
            } 
        }
        public Location DropoffLocation { get; set; }
        public Location DriverInitialLocation { get; set; }
        public DateTime? PickupTime { get; set; }
        public DateTime? DropoffTime { get; set; }
        public VehicleType? VehicleType { get; set; }
        public Status? Status { get; set; }
        public DateTime? ETA { get; set; } // in minutes;
        public double? Price { get; set; }
        public TimeSpan OccupiedTime { get; set; }
        public TimeSpan EnrouteTime { get; set; }
        public TimeSpan IdleTime { get; set; }
        public double? OccupiedDistance { get; set; }
        public double? EnrouteDistance { get; set; }
        public double? DriverRouteDuration { get; set; }
        public DateTime Creation { get; set; }
        public DateTime? LastUpdate { get; set; }
        public DateTime? LastStatusChange;
        public bool ServiceGoalMet { get; set; }
        public TimeSpan Lateness { get; set; }
        public double LatenessMilliseconds { get; set; }
        public double SamplingPercentage { get; set; }
        public TripState State { get; set; }
        public bool IsDirty { get; set; }
        public string MadeDirtyById { get; set; }

        private List<Location> _historyEnrouteList = new List<Location>();
        private List<Location> _historyPickUpList = new List<Location>();

        public bool AddEnrouteLocationList(Location l)
        {
            _historyEnrouteList.Add(l);
            return true;
        }

        public bool AddPickUpLocationList(Location l)
        {
            _historyPickUpList.Add(l);
            return true;
        }

        public List<Location> GetEnrouteLocationList()
        {
            return _historyEnrouteList;
        }

        public List<Location> GetPickupLocationList()
        {
            return _historyPickUpList;
        }

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
            this.OccupiedDistance = trip.OccupiedDistance;

            this.DriverRouteDuration = trip.DriverRouteDuration;
        }

        public void SetCreation(DateTime time)
        {
            this.Creation = time;
        }
        public DateTime GetCreation()
        {
            return this.Creation;
        }
    }

    public class TripQuotes
    {
        public string Id { get; set; }
        public QuoteStatus Status { get; set; }
        public Gateway.QuoteTripRequest QuoteRequest { get; set; }
        public List<Quote> ReceivedQuotes { get; set; }
        public int PartnersThatServe { get; set; }
        public int ReceivedUpdatesCount { get; set; }
        public bool Autodispatch { set; get; }

        public TripQuotes Clone()
        {
            return new TripQuotes()
            {
                Id = Id,
                Status = Status,
                QuoteRequest = CopyQuoteTripRequest(QuoteRequest),
                ReceivedQuotes = ReceivedQuotes.ToList(),
                PartnersThatServe = PartnersThatServe,
                ReceivedUpdatesCount = ReceivedUpdatesCount,
                Autodispatch = Autodispatch
            };
        }
        public static Gateway.QuoteTripRequest CopyQuoteTripRequest(Gateway.QuoteTripRequest r)
        {
            return new Gateway.QuoteTripRequest(
                clientID: r.clientID, id: r.tripId, pickupLocation: r.pickupLocation, pickupTime: r.pickupTime,
                passengerName: r.passengerName, dropoffLocation: r.dropoffLocation, vehicleType: r.vehicleType
            );
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
        public class Request
        {

        }
        public class Response
        {
            public Result result { get; set; }

            public Response(Result result)
            {
                this.result = result;
            }

        }
        public class GetGatewayStatsRequest : Request
        {
            public override string ToString()
            {
                return "stats";
            }
        }
        public class GetGatewayStatsResponse : Response
        {
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
                : base(result)
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
            }
            public GetGatewayStatsResponse(Result result = Result.UnknownError)
                : base(result)
            {

            }
            public override string ToString()
            {
                return "Result = " + result;
            }
        }
        public class RegisterPartnerRequest : Request
        {
            public string name { get; set; }
            public string callback_url { get; set; } 
            public string accessToken { get; set; } 
            public List<Zone> coverage { get; set; }

            public RegisterPartnerRequest(string name, string callback_url, string accessToken, List<Zone> coverage)
            {
                this.name = name;
                this.callback_url = callback_url;
                this.accessToken = accessToken;
                this.coverage = coverage;
            }
            public override string ToString()
            {
                string s = "Partner = " + name;
                s += ", Callback = " + callback_url;
                s += ", Access token = " + accessToken;
                return s;
            }
        }
        public class RegisterPartnerResponse : Response
        {
            public RegisterPartnerResponse(string partnerID = null, Result result = Result.OK)
                : base(result)
            {
                this.partnerID = partnerID;
            }
            public override string ToString()
            {
                return "PartnerID = " + partnerID + ", Result = " + result;
            }
            public string partnerID { get; set; }
        }
        public class GetPartnerInfoRequest : Request
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
        public class GetPartnerInfoResponse : Response
        {
            public List<Fleet> fleets { get; set; }
            public List<VehicleType> vehicleTypes { get; set; }
            public GetPartnerInfoResponse(List<Fleet> fleets = null, List<VehicleType> vehicleTypes = null, Result result = Result.OK)
                : base(result)
            {
                this.fleets = fleets;
                this.vehicleTypes = vehicleTypes;
            }
            public override string ToString()
            {
                return "Result = " + result;
            }
        }
        public class DispatchTripRequest : Request
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
        public class DispatchTripResponse : Response
        {
            public DispatchTripResponse(Result result = Result.OK)
                : base(result)
            {

            }
            public override string ToString()
            {
                return "Result = " + result;
            }
        }
        public class QuoteTripRequest : Request
        {
            public string clientID { get; set; }  // TODO: TripThru needs to know who's making the call
            public string tripId { get; set; }
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
            public QuoteTripRequest(string clientID, string id, Location pickupLocation, DateTime pickupTime, string passengerID = null, string passengerName = null,
                int? luggage = null, int? persons = null, Location dropoffLocation = null, List<Location> waypoints = null,
                PaymentMethod? paymentMethod = null, VehicleType? vehicleType = null, double? maxPrice = null, int? minRating = null, string partnerID = null,
                string fleetID = null, string driverID = null)
            {
                this.clientID = clientID;
                this.tripId = id;
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
        public class QuoteTripResponse : Response
        {
            public QuoteTripResponse(Result result = Result.OK)
                : base(result)
            {

            }
            public override string ToString()
            {
                return "Result = " + result;
            }
        }
        public class UpdateQuoteRequest : Request
        {
            public string clientID { get; set; }  // TODO: TripThru needs to know who's making the call
            public string tripId { get; set; }
            public int count { get; set; }
            public List<Quote> quotes { get; set; }
            public UpdateQuoteRequest(string clientID, string tripId, List<Quote> quotes)
            {
                this.clientID = clientID;
                this.tripId = tripId;
                this.count = quotes.Count;
                this.quotes = quotes;
            }
            public override string ToString()
            {
                return "ClientID = " + clientID;
            }
        }
        public class UpdateQuoteResponse : Response
        {
            public UpdateQuoteResponse(Result result = Result.OK)
                : base(result)
            {

            }
            public override string ToString()
            {
                return "Result = " + result;
            }
        }
        public class GetQuoteRequest : Request
        {
            public string clientID { get; set; }  // TODO: TripThru needs to know who's making the call
            public string tripId { get; set; }
            public GetQuoteRequest(string clientID, string tripId)
            {
                this.clientID = clientID;
                this.tripId = tripId;
            }
            public override string ToString()
            {
                return "ClientID = " + clientID + ", TripId = " + tripId;
            }
        }
        public class GetQuoteResponse : Response
        {
            public List<Quote> quotes { get; set; }
            public QuoteStatus? status { get; set; }
            public GetQuoteResponse(QuoteStatus? status = null, List<Quote> quotes = null, Result result = Result.OK)
                : base(result)
            {
                this.status = status;
                this.quotes = quotes;
            }
            public override string ToString()
            {
                return "Result = " + result;
            }
        }
        public class GetTripsRequest : Request
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
        public class GetTripsResponse : Response
        {
            public List<Trip> trips { get; set; }
            public GetTripsResponse(List<Trip> trips, Result result = Result.OK)
                : base(result)
            {
                this.trips = trips;
            }
            public override string ToString()
            {
                string s = "Result = " + result;
                return s;
            }
        }
        public class GetRouteTripRequest : Request
        {
            public string tripID { get; set; }

            public GetRouteTripRequest(string tripID)
            {
                this.tripID = tripID;
            }
        }
        public class GetRouteTripResponse : Response
        {
            public string OriginatingPartnerId { get; set; }
            public string ServicingPartnerId { get; set; }
            public List<Location> HistoryEnrouteList { get; set; }
            public List<Location> HistoryPickupList { get; set; }

            public GetRouteTripResponse(Result result = Result.OK, string originatingPartnerId = null, string servicingPartnerId = null, List<Location> historyEnrouteList = null, List<Location> historyPickupList = null)
                : base(result)
            {
                this.OriginatingPartnerId = originatingPartnerId;
                this.ServicingPartnerId = servicingPartnerId;
                this.HistoryEnrouteList = historyEnrouteList;
                this.HistoryPickupList = historyPickupList;
            }
        }
        public class GetTripStatusRequest : Request
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
        public class GetTripStatusResponse : Response
        {
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
                : base(result)
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
        public class UpdateTripStatusRequest : Request
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
        public class UpdateTripStatusResponse : Response
        {
            public UpdateTripStatusResponse(Result result = Result.OK)
                : base(result)
            {

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
        virtual public RegisterPartnerResponse RegisterPartner(Gateway partner, List<Zone> coverage)
        {
            throw new Exception("Not supported");
        }
        virtual public GetPartnerInfoResponse GetPartnerInfo(GetPartnerInfoRequest request)
        {
            throw new Exception("Not supported");
        }
        virtual public void DispatchTripAsync(DispatchTripRequest request, Action<DispatchTripResponse> callback)
        {
            throw new Exception("Not supported");
        }
        virtual public DispatchTripResponse DispatchTrip(DispatchTripRequest request)
        {
            throw new Exception("Not supported");
        }
        virtual public void QuoteTripAsync(QuoteTripRequest request, Action<QuoteTripResponse> callback)
        {
            throw new Exception("Not supported");
        }
        virtual public QuoteTripResponse QuoteTrip(QuoteTripRequest request)
        {
            throw new Exception("Not supported");
        }
        virtual public void UpdateQuoteAsync(UpdateQuoteRequest request, Action<UpdateQuoteResponse> callback)
        {
            throw new Exception("Not supported");
        }
        virtual public UpdateQuoteResponse UpdateQuote(UpdateQuoteRequest request)
        {
            throw new Exception("Not supported");
        }
        virtual public GetQuoteResponse GetQuote(GetQuoteRequest request)
        {
            throw new Exception("Not supported");
        }
        virtual public GetTripStatusResponse GetTripStatus(GetTripStatusRequest request)
        {
            throw new Exception("Not supported");
        }
        virtual public void UpdateTripStatusAsync(UpdateTripStatusRequest request, Action<UpdateTripStatusResponse> callback)
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
        virtual public GetRouteTripResponse GetRouteTrip(GetRouteTripRequest request)
        {
            throw new Exception("Not supported");
        }
    }

    public class GatewayWithCallbackUrl : Gateway
    {
        private string _rootUrl;
        public string RootUrl
        {
            get
            {
                return this._rootUrl;
            }
            set
            {
                this._rootUrl = value.EndsWith("/") ? value : value + "/";
            }
        }

        public GatewayWithCallbackUrl(string ID, string name, string rootUrl)
            : base(ID, name)
        {
            this.RootUrl = rootUrl.EndsWith("/") ? rootUrl : rootUrl + "/";
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

        public abstract class ActiveValuesManager<T>
        {
            protected ConcurrentDictionary<string, T> dict;
            public bool IsEmpty { get { return dict.IsEmpty; } }
            public int Count { get { return dict.Count; } }
            public IEnumerable<T> Values { get { return dict.Values; } }
            public ActiveValuesManager()
            {
                dict = new ConcurrentDictionary<string, T>();
                dict.Clear();
            }
            public bool ContainsKey(string id)
            {
                return dict.Keys.Contains(id);
            }
            public T this[string id]
            {
                get
                {
                    T value;
                    dict.TryGetValue(id, out value);
                    return value;
                }
            }
            public abstract void Insert(string id, T value);
            public abstract void Remove(string id);
            public abstract void Update(T value);
        }
        public class ActiveTrips : ActiveValuesManager<Trip>
        {
            public long lastID { get; set; }
            public ActiveTrips() 
                : base()
            {
                var lastDbTripId = StorageManager.GetLastTripId();
                this.lastID = lastDbTripId;
            }

            public override void Insert(string id, Trip value)
            {
                dict.TryAdd(id, value);
                value.LastUpdate = DateTime.UtcNow;
                StorageManager.InsertTrip(value);
            }
            public override void Remove(string id)
            {
                if (dict.ContainsKey(id)) 
                { 
                    Logger.Log("Removing active trip " + id);
                    Trip trip;
                    dict.TryRemove(id, out trip);
                    if (trip != null)
                    {
                        StorageManager.UpdateTrip(trip);
                    }
                    else
                    {
                        Logger.Log("Remove trip exception. " + id + " must have been tried to remove multiple times");
                    }
                }
                else
                {
                    throw new Exception("Trip " + id + " doesn't exist");
                    Logger.Log("Remove trip exception. " + id + " doesn't exist");
                }
            }
            public override void Update(Trip trip)
            {
                if (!ContainsKey(trip.Id)) return;
                this[trip.Id].FleetId = trip.FleetId;
                this[trip.Id].FleetName = trip.FleetName;
                this[trip.Id].DriverId = trip.DriverId;
                this[trip.Id].DriverName = trip.DriverName;
                this[trip.Id].DriverLocation = trip.DriverLocation;
                this[trip.Id].ETA = trip.ETA;
                this[trip.Id].Price = trip.Price;
                this[trip.Id].OccupiedDistance = trip.OccupiedDistance;
                if (trip.Status == Status.PickedUp)
                {
                    var lastStatusChange = this[trip.Id].LastStatusChange;
                    if (lastStatusChange != null)
                        this[trip.Id].OccupiedTime = DateTime.UtcNow - (DateTime)lastStatusChange;
                }
                if (trip.Status != this[trip.Id].Status)
                {
                    this[trip.Id].Status = trip.Status;
                    this[trip.Id].LastStatusChange = DateTime.UtcNow;
                }
                this[trip.Id].LastUpdate = DateTime.UtcNow;
                this[trip.Id].IsDirty = trip.IsDirty;
                this[trip.Id].MadeDirtyById = trip.MadeDirtyById;
                StorageManager.UpdateTrip(trip);
            }
            public List<Trip> GetTripsByState(TripState state)
            {
                return dict.Values.Where(t => t.State == state).ToList();
            }
            public List<Trip> GetDirtyTrips()
            {
                return dict.Values.Where(t => t.IsDirty).ToList();
            }
        }
        public class ActiveQuotes : ActiveValuesManager<TripQuotes>
        {
            public long lastID { get; set; }
            public ActiveQuotes()
                : base()
            {

            }

            public override void Insert(string id, TripQuotes value)
            {
                dict.TryAdd(id, value);
                StorageManager.InsertQuote(value);
            }
            public override void Remove(string id)
            {
                if (dict.ContainsKey(id))
                {
                    Logger.Log("Removing active quote " + id);
                    TripQuotes quote;
                    dict.TryRemove(id, out quote);
                    if (quote != null)
                    {
                        StorageManager.UpdateQuote(quote);
                    }
                    else
                    {
                        Logger.Log("Remove quote exception. " + id + " must have been tried to remove multiple times");
                    }
                }
                else
                {
                    throw new Exception("Quote " + id + " doesn't exist");
                    Logger.Log("Remove quote exception. " + id + " doesn't exist");
                }
            }
            public override void Update(TripQuotes quote)
            {
                if (!ContainsKey(quote.Id)) return;
                this[quote.Id].Autodispatch = quote.Autodispatch;
                this[quote.Id].Id = quote.Id;
                this[quote.Id].PartnersThatServe = quote.PartnersThatServe;
                this[quote.Id].QuoteRequest = quote.QuoteRequest;
                this[quote.Id].ReceivedQuotes = quote.ReceivedQuotes;
                this[quote.Id].ReceivedUpdatesCount = quote.ReceivedUpdatesCount;
                this[quote.Id].Status = quote.Status;
                StorageManager.UpdateQuote(quote.Clone());
            }
            public List<TripQuotes> GetQuotesByStatus(QuoteStatus status)
            {
                return dict.Values.Where(q => q.Status == status).ToList();
            }
        }

        public ActiveTrips activeTrips;
        public ActiveQuotes activeQuotes;

        public GarbageCleanup<string> garbageCleanup;
        public RedisDictionary<string, string> clientIdByAccessToken;
        public RedisDictionary<string, PartnerAccount> partnerAccounts;
        public override PartnerAccount GetPartnerAccountByAccessToken(string accessToken)
        {
            if (accessToken == null || !clientIdByAccessToken.ContainsKey(accessToken))
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

            activeTrips = new ActiveTrips();
            activeQuotes = new ActiveQuotes();
            partnerAccounts = new RedisDictionary<string, PartnerAccount>(redisClient, ID + ":" + MemberInfoGetting.GetMemberName(() => partnerAccounts));
            clientIdByAccessToken = new RedisDictionary<string, string>(redisClient, ID + ":" + MemberInfoGetting.GetMemberName(() => clientIdByAccessToken));
        }
        public override Gateway.RegisterPartnerResponse RegisterPartner(Gateway partner, List<Zone> coverage)
        {
            return MakeRejectRegisterPartnerResponse();
        }
        protected RegisterPartnerResponse MakeRejectRegisterPartnerResponse()
        {
            RegisterPartnerResponse response;
            rejects++;
            response = new RegisterPartnerResponse(result: Result.Rejected);
            return response;
        }
        override public DispatchTripResponse DispatchTrip(DispatchTripRequest request)
        {

            throw new Exception("not supported");
        }
        protected DispatchTripResponse MakeRejectDispatchResponse(DispatchTripRequest r, Gateway client, Gateway partner)
        {
            DispatchTripResponse response;
            rejects++;
            response = new DispatchTripResponse(result: Result.Rejected);

            var trip = new Trip
            {
                Id = r.tripID,
                OriginatingPartnerName = client.name,
                OriginatingPartnerId = client.ID,
                ServicingPartnerName = partner == null ? null : partner.name,
                ServicingPartnerId = partner == null ? null : partner.ID,
                Status = Status.Rejected,
                PickupLocation = r.pickupLocation,
                PickupTime = r.pickupTime,
                DropoffLocation = r.dropoffLocation,
                PassengerName = r.passengerName,
                VehicleType = r.vehicleType,
                Creation = DateTime.UtcNow,
                LatenessMilliseconds = 0,
                SamplingPercentage = 1
            };
            activeTrips.Update(trip); // Hack: save trip should be moved somewhere else.

            return response;
        }
        public void DeactivateTripAndUpdateStats(string tripID, Status status, double? price = null, double? distance = null)
        {
            if (!activeTrips.ContainsKey(tripID))
                return;
            Logger.Log("Deactivating trip " + tripID + " from " + name);

            var trip = activeTrips[tripID];
            trip.Status = status;
            switch (status)
            {
                case Status.Complete:
                    {
                        completes++;
                        fare += (double)price;
                        this.distance += (double)distance;
                        if (trip.LastStatusChange != null)
                            trip.OccupiedTime = DateTime.UtcNow - (DateTime)trip.LastStatusChange;
                        trip.OccupiedDistance = distance;
                        break;
                    }
                case Status.Cancelled: cancels++; break;
                case Status.Rejected: rejects++; break;
            }
            activeTrips.Update(trip);
            activeTrips.Remove(tripID);
            if (garbageCleanup != null)
                garbageCleanup.Add(tripID);
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