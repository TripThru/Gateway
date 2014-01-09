using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Simulator.Utils;
namespace Simulator.APIs
{
    public enum Status { Queued, Dispatched, Enroute, PickedUp, DroppedOff, Complete, Rejected, Cancelled };
    public enum VehicleType { Compact, Sedan };
    public enum PaymentMethod { Cash, Credit, Account };
    public class Zone
    {
        public Zone(Location center, double radius)
        {
            this.center = center;
            this.radius = radius;
        }
        public double DegreesToRadians(double angle)
        {
            return Math.PI * angle / 180.0;
        }
        public bool IsInside(Location l)
        {
            double lat1 = DegreesToRadians(center.lat);
            double lng1 = DegreesToRadians(center.lng);
            double lat2 = DegreesToRadians(l.lat);
            double lng2 = DegreesToRadians(l.lng);
            double dlon = lng2 - lng1;
            double dlat = lat2 - lat1;
            double a = Math.Pow(Math.Sin(dlat / 2.0), 2) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(dlon / 2.0), 2);
            double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1-a));
            double d = 3961.0 * c; // (where 3961 is the radius of the Earth in miles
            return d < radius;
        }
        public Location center;
        public double radius;
    }
    public class Location
    {
        public double lat;
        public double lng;
        public string name; // temp until we hookup with a geolocator serviced
        public Location()
        {
        }
        public Location(double lat, double lng, string name = null)
        {
            this.lng = lng;
            this.lat = lat;
            if (name == null)
                this.name = MapTools.GetReverseGeoLoc(this);
            else
                this.name = name;
        }
        public double DegreesToRadians(double angle)
        {
            return Math.PI * angle / 180.0;
        }
        public double GetDistance(Location l)
        {
            double lat1 = DegreesToRadians(lat);
            double lng1 = DegreesToRadians(lng);
            double lat2 = DegreesToRadians(l.lat);
            double lng2 = DegreesToRadians(l.lng);
            double dlon = lng2 - lng1;
            double dlat = lat2 - lat1;
            double a = Math.Pow(Math.Sin(dlat / 2.0), 2) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Pow(Math.Sin(dlon / 2.0), 2);
            double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            double d = 3961.0 * c; // (where 3961 is the radius of the Earth in miles
            return d;
        }
        public bool Equals(Location l)
        {
            double distance = Math.Sqrt(Math.Pow(l.lat - lat, 2) + Math.Pow(l.lng - lng, 2));
            return distance < .00001;
        }
        public string getID()
        {
            return "<" + lat + ":" + lng + ">";
        }
        public override string ToString()
        {
            if (name != null)
                return name;
            return "(" + lat + ", " + lng + ")";
        }
    }
    public class Gateway
    {
        public enum Result { OK = 100, MethodNotSupported = 200, Rejected = 300, UnknownError = 400, InvalidParameters = 500, NotFound = 600 };
        // Partners only call this method, they do not need to support it
        public class RegisterPartner
        {
            public class Request
            {
                public string clientID;  // TODO: TripThru needs to know who's making the call
                public string name;
                public Gateway callback_url; // TODO: This should actually be a string, since we're not yet integrated I'm just using an object pointer
                public Request(string clientID, string name, Gateway callback_url)
                {
                    this.clientID = clientID;
                    this.name = name;
                    this.callback_url = callback_url;
                }
                public override string ToString()
                {
                    string s = "Partner = " + name;
                    if (clientID != null)
                        s += ", ClientID = " + clientID;
                    return s;
                }
            }
            public class Response
            {
                public Response(string partnerID = null, Result result = Result.OK)
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
            public virtual Response Post(Request r)
            {
                throw new Exception("Not supported");
            }
        }
        public class GetPartnerInfo
        {
            public class Request
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
                public Request(string clientID, List<Zone> coverage = null, List<VehicleType> vehicleTypes = null, List<string> fleets = null)
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
            public class Response
            {
                public class Fleet
                {
                    IDName partner;
                    IDName fleet;
                    List<Zone> coverage;
                    public Fleet(IDName partner, IDName fleet, List<Zone> coverage)
                    {
                        this.partner = partner;
                        this.fleet = fleet;
                        this.coverage = coverage;
                    }
                }
                public Result result;
                public List<Fleet> fleets;
                public List<VehicleType> vehicleTypes;
                public Response(List<Fleet> fleets = null, List<VehicleType> vehicleTypes = null, Result result = Result.OK)
                {
                    this.fleets = fleets;
                    this.vehicleTypes = vehicleTypes;
                    this.result = result;
                }
                public override string ToString()
                {
                    return "Fleets = " + fleets + ", Result = " + result;
                }
            }
            public virtual Response Get(Request request)
            {
                throw new Exception("Not supported");
            }
        }
        public class DispatchTrip
        {
            public class Request
            {
                public string clientID;  // TODO: TripThru needs to know who's making the call
                public string foreignID;
                public string passengerID;
                public string passengerName;
                public int? luggage;
                public int? persons;
                public Location pickupLocation;
                public DateTime pickupTime;
                public Location dropoffLocation;
                public Location[] waypoints;
                public PaymentMethod? paymentMethod;
                public VehicleType? vehicleType;
                public double? maxPrice;
                public int? minRating;
                public string partnerID;
                public string fleetID;
                public string driverID;
                public Request(string clientID, Location pickupLocation, DateTime pickupTime, string foreignID = null, string passengerID = null, string passengerName = null,
                    int? luggage = null, int? persons = null, Location dropoffLocation = null, Location[] waypoints = null,
                    PaymentMethod? paymentMethod = null, VehicleType? vehicleType = null, double? maxPrice = null, int? minRating = null, string partnerID = null,
                    string fleetID = null, string driverID = null)
                {
                    this.clientID = clientID;
                    this.passengerID = passengerID;
                    this.passengerName = passengerName;
                    this.foreignID = foreignID;
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
            public class Response
            {
                public Response(string tripID = null, Result result = Result.OK)
                {
                    this.tripID = tripID;
                    this.result = result;
                }
                public override string ToString()
                {
                    if (result == Result.OK)
                        return "TripID = " + tripID + ", Result = " + result;
                    else
                        return "Result = " + result;
                }
                public Result result;
                public string tripID;
            }
            public virtual Response Post(Request request)
            {
                throw new Exception("Not supported");
            }
        }
        public class QuoteTrip
        {
            public class Request
            {
                public string clientID;  // TODO: TripThru needs to know who's making the call
                public string passengerID;
                public string passengerName;
                public int? luggage;
                public int? persons;
                public Location pickupLocation;
                public DateTime pickupTime;
                public Location dropoffLocation;
                public Location[] waypoints;
                public PaymentMethod? paymentMethod;
                public VehicleType? vehicleType;
                public double? maxPrice;
                public int? minRating;
                public string partnerID;
                public string fleetID;
                public string driverID;
                public Request(string clientID, Location pickupLocation, DateTime pickupTime, string passengerID = null, string passengerName = null,
                    int? luggage = null, int? persons = null, Location dropoffLocation = null, Location[] waypoints = null,
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
            public class Response
            {
                public class Quote
                {
                    public Quote(string partnerID, string partnerName = null, string fleetID = null, string fleetName = null, VehicleType? vehicleType = null, double? price = null, double? distance = null, TimeSpan? duration = null, DateTime? ETA = null)
                    {
                        this.partnerID = partnerID;
                        this.partnerName = partnerName;
                        this.fleetID = fleetID;
                        this.fleetName = fleetName;
                        this.vehicleType = vehicleType;
                        this.price = price;
                        this.duration = duration;
                        this.ETA = ETA;
                    }
                    public override string ToString()
                    {
                        string s = "Partner = " + (partnerName == null ?  partnerID : partnerName);
                        if (fleetName != null)
                            s += ", Fleet = " + fleetName;
                        else if (fleetID != null)
                            s += ", Fleet = " + fleetID;
                        if (vehicleType != null)
                            s += ", VehicleType = " + vehicleType;
                        if (price != null)
                            s += ", Price = " + String.Format("{0:C}", price);
                        if (ETA != null)
                            s += ", ETA = " + ETA;
                        return s;
                    }

                    public string partnerID; // partners don't need to supply this
                    public string partnerName; // partners don't need to supply this
                    public string fleetID;
                    public string fleetName;
                    public VehicleType? vehicleType;
                    public double? price; // in local currency
                    public double? distance; // in km
                    public TimeSpan? duration; // estimated duration of the trip
                    public DateTime? ETA; // estimated time of arrival
                }
                public Result result;
                public List<Quote> quotes;
                public Response(List<Quote> quotes = null, Result result = Result.OK)
                {
                    this.quotes = quotes;
                    this.result = result;
                }
                public override string ToString()
                {
                    return "Result = " + result;
                }
            }
            public virtual Response Get(Request r)
            {
                throw new Exception("Not supported");
            }
        }
        public class GetTripStatus
        {
            public class Request
            {
                public string clientID;  // TODO: TripThru needs to know who's making the call
                public string tripID;
                public Request(string clientID, string tripID)
                {
                    this.clientID = clientID;
                    this.tripID = tripID;
                }
                public override string ToString()
                {
                    return "ClientID = " + clientID;
                }
            }
            public class Response
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
                public Response(string partnerID = null, string partnerName = null, string fleetID = null, string fleetName = null,
                    string driverID = null, string driverName = null, Location driverLocation = null, VehicleType? vehicleType = null, 
                    DateTime? ETA = null, Status? status = null, DateTime? pickupTime = null, DateTime? dropoffTime = null, Result result = Result.OK)
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
                    return s;
                }
            }
            public virtual Response Get(Request r)
            {
                throw new Exception("Not supported");
            }
        }
        public class UpdateTripStatus
        {
            public class Request
            {
                public string clientID;  // TODO: TripThru needs to know who's making the call
                public string tripID;
                public Status status;
                public Request(string clientID, string tripID, Status status)
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
            public class Response
            {
                public Result result;
                public Response(Result result = Result.OK)
                {
                    this.result = result;
                }
                public override string ToString()
                {
                    return "Result = " + result;
                }
            }
            public virtual Response Post(Request r)
            {
                throw new Exception("Not supported");
            }
        }
        public virtual void Simulate(DateTime until)
        {
            throw new Exception("Not supported");
        }
        public RegisterPartner registerPartner;
        public GetPartnerInfo getPartnerInfo;
        public DispatchTrip dispatchTrip;
        public QuoteTrip quoteTrip;
        public GetTripStatus getTripStatus;
        public UpdateTripStatus updateTripStatus;
    }

}
