using System;
using System.Collections.Generic;
using RestSharp;
using ServiceStack.ServiceClient.Web;
using Utils;
using TripThruCore;
using System.Globalization;
using ServiceStack.Common.ServiceClient.Web;
using ServiceStack.Text;
using System.Threading;
using ServiceStack;


namespace CustomIntegrations
{

    public class TDispatchAPI
    {
        public string passengerProxyPK = "52ed292649d9433a78e5016c"; // SanFran: 52ef0f1b48efcb3da44adf21
        string API_KEY;
        string FLEET_AUTH_CODE;
        public string FLEET_REFRESH_TOKEN;
        public string FLEET_ACCESS_TOKEN;
        string FLEET_API_ROOT_URL;

        string PASSENGER_AUTH_CODE;
        public string PASSENGER_REFRESH_TOKEN;
        public string PASSENGER_ACCESS_TOKEN;
        string PASSENGER_API_ROOT_URL;


        string CLIENT_ID;
        string CLIENT_SECRET;

        string AUTH_URI;
        string TOKEN_URI;
        string REVOKE_URI;
        bool authorized;

        // API_KEY(SanFran) = "ec1a72432c504ed470695943e3dfccd8"
        // Passenger(SanFran) AUTH_CODE = "52ef10ba49d94303bc62aa25"
        // Fleet(SanFran)  AUTH_CODE = "52b4dd8149d943327b19c269"
        // API_KEY(TripThru) = "926d24e1c76e2ea3c098a843b6693d9f"
        // Passenger(TripThru) AUTH_CODE = "52ef117c49d94303bc62aa2b"
        // Fleet(TripThru)  AUTH_CODE = "529b610e49d9431cd4d74ed7"
        public TDispatchAPI(string apiKey,
            string fleetAuth, string fleetAccessToken, string fleetRefreshToken,
            string passengerAuth, string passengerAccessToken, string passengerRefreshToken, string passengerProxyPK)
        {
            this.API_KEY = apiKey; // "24eba1dd5fe7580af0d571e5e6b0e88a";
            FLEET_AUTH_CODE = fleetAuth; //52b4dd8149d943327b19c269"; //"52a7ff0749d9431698a04a84";
            FLEET_REFRESH_TOKEN = fleetRefreshToken; // "Si3zx6XmsBgv9c3cQ31bGIGZImYwI3ps";// "FRNqMMP5oamdqasiYeTokPwv89FNXFo9";
            FLEET_ACCESS_TOKEN = fleetAccessToken; // "52ea8daa49d94350c6b2d53f";// "52eb73ee49d9433369cd3562";

            PASSENGER_AUTH_CODE = passengerAuth; // "52eda97049d94364d1ad91f4"; // For Passenger TripThru -- edward.orourke.hamilton@gmail.com
            PASSENGER_REFRESH_TOKEN = passengerRefreshToken; //"lbUD2i2aNVvzwlDGHw59jKMF5lDnoknG";
            PASSENGER_ACCESS_TOKEN = passengerAccessToken; //"52edbeef49d94364d1ad92dd";
            this.passengerProxyPK = passengerProxyPK;

            CLIENT_ID = "XU3PSNDBWP@tdispatch.com";
            CLIENT_SECRET = "yBuFN4Pmfxfm5kQ3YKvCIa8NkV1Psrhc";

            FLEET_API_ROOT_URL = "https://api.tdispatch.com/fleet/v1";
            PASSENGER_API_ROOT_URL = "https://api.tdispatch.com/passenger/v1";

            AUTH_URI = "oauth2/auth";
            TOKEN_URI = "oauth2/token";
            REVOKE_URI = "oauth2/revoke";
            Authorize();

        }
        public bool Authorize()
        {
            authorized = false;
            try
            {
                APIInfoResponse apiInfoResponse = GetFleetAPI_Info();
            }
            catch (Exception)
            {
                try
                {
                    GetTokensRequest request = new GetTokensRequest { code = FLEET_REFRESH_TOKEN, client_id = CLIENT_ID, client_secret = CLIENT_SECRET, grant_type = "refresh_token" };
                    JsonServiceClient client = new JsonServiceClient("https://api.tdispatch.com/fleet");
                    GetTokensResponse response = client.Post<GetTokensResponse>("oauth2/token", request);
                    FLEET_ACCESS_TOKEN = response.access_token;
                }
                catch (Exception)
                {
                    GetTokensRequest request = new GetTokensRequest { code = FLEET_AUTH_CODE, client_id = CLIENT_ID, client_secret = CLIENT_SECRET, grant_type = "authorization_code" };
                    JsonServiceClient client = new JsonServiceClient("https://api.tdispatch.com/fleet");
                    GetTokensResponse response = client.Post<GetTokensResponse>("oauth2/token", request);
                    FLEET_ACCESS_TOKEN = response.access_token;
                    FLEET_REFRESH_TOKEN = response.refresh_token;
                }
            }
            try
            {
                APIInfoResponse apiInfoResponse = GetPassengerAPI_Info();
            }
            catch (Exception)
            {
                try
                {
                    GetTokensRequest request = new GetTokensRequest { code = PASSENGER_REFRESH_TOKEN, client_id = CLIENT_ID, client_secret = CLIENT_SECRET, grant_type = "refresh_token" };
                    JsonServiceClient client = new JsonServiceClient("https://api.tdispatch.com/passenger");
                    GetTokensResponse response = client.Post<GetTokensResponse>("oauth2/token", request);
                    PASSENGER_ACCESS_TOKEN = response.access_token;
                }
                catch (Exception)
                {
                    GetTokensRequest request = new GetTokensRequest { code = PASSENGER_AUTH_CODE, client_id = CLIENT_ID, client_secret = CLIENT_SECRET, grant_type = "authorization_code" };
                    JsonServiceClient client = new JsonServiceClient("https://api.tdispatch.com/passenger");
                    GetTokensResponse response = client.Post<GetTokensResponse>("oauth2/token", request);
                    PASSENGER_ACCESS_TOKEN = response.access_token;
                    PASSENGER_REFRESH_TOKEN = response.refresh_token;
                }
            }
            authorized = true;
            return true;
        }

        public class GetAuthorizationRequest
        {
            public string key { get; set; }
            public string client_id { get; set; }
            public string response_type { get { return "code"; } }
        }
        public class GetAuthorizationResponse
        {
            public string status { get; set; }
            public int status_code { get; set; }
            public string message { get; set; }
            public string code { get; set; }
            public string authorization { get; set; }
        }


        public class GetTokensRequest
        {
            public string code { get; set; }
            public string client_id { get; set; }
            public string client_secret { get; set; }
            public string grant_type { get; set; }
            public string redirect_uri { get { return ""; } }
        }
        public class GetTokensResponse
        {
            public string status { get; set; }
            public int status_code { get; set; }
            public string message { get; set; }
            public string refresh_token { get; set; }
            public string access_token { get; set; }
            public string token_type { get; set; }
            public int expires_in { get; set; }
        }

        public class Fleet
        {
            public string pk { get; set; }
            public string slug { get; set; }
            public string name { get; set; }
        }

        public class GetFleetResponse
        {
            public string status { get; set; }
            public int status_code { get; set; }
            public string message { get; set; }
            public Fleet fleet { get; set; }
        }
        public GetFleetResponse GetFleet()
        {
            JsonServiceClient client = new JsonServiceClient(FLEET_API_ROOT_URL);
            GetFleetResponse response = client.Get<GetFleetResponse>("fleet" + "?access_token=" + FLEET_ACCESS_TOKEN);
            return response;
        }


        public class APIInfoResponse
        {
            public class Application
            {
                public string client_id { get; set; }
                public string email { get; set; }
                public string name { get; set; }
            }
            public class Passenger
            {
                public string name { get; set; }
            }
            public class Session
            {
                public string access_token { get; set; }
                public DateTime create { get; set; }
                public int expires_in { get; set; }
            }
            public Passenger passenger { get; set; }
            public Fleet fleet { get; set; }
            public Application application { get; set; }
            public Session session { get; set; }
            public string api { get; set; }
            public string version { get; set; }
            public string status { get; set; }
            public int status_code { get; set; }
            public string message { get; set; }
        }
        //Info
        public APIInfoResponse GetFleetAPI_Info()
        {
            JsonServiceClient client = new JsonServiceClient(FLEET_API_ROOT_URL);
            APIInfoResponse response = client.Get<APIInfoResponse>("api-info" + "?access_token=" + FLEET_ACCESS_TOKEN);
            return response;
        }
        public APIInfoResponse GetPassengerAPI_Info()
        {
            JsonServiceClient client = new JsonServiceClient(PASSENGER_API_ROOT_URL);
            APIInfoResponse response = client.Get<APIInfoResponse>("api-info" + "?access_token=" + PASSENGER_ACCESS_TOKEN);
            return response;
        }

        public class AccountCreateResponse
        {
            public class Account
            {
                public string pk { get; set; }
            }
            public string status { get; set; }
            public int status_code { get; set; }
            public Account account { get; set; }
        }

        //Passengers
        public class CreatePassengerRequest// : ServiceStack.ServiceHost.IReturn<BookingCreateResponse>
        {
            public string username { get; set; }
            public string name { get; set; }
            public string phone { get; set; }
        }
        public class CreatePassengerResponse// : ServiceStack.ServiceHost.IReturn<BookingCreateResponse>
        {
            public class Passenger
            {
                public string pk { get; set; }
            }
            public Passenger passenger { get; set; }
            public string status { get; set; }
            public int status_code { get; set; }
            public string message { get; set; }
        }
        public CreatePassengerResponse createPassenger(CreatePassengerRequest request)
        {
            JsonServiceClient client = new JsonServiceClient(FLEET_API_ROOT_URL);
            CreatePassengerResponse response = client.Post<CreatePassengerResponse>("passengers" + "?access_token=" + FLEET_ACCESS_TOKEN, request);
            return response;
        }
        public class Location
        {
            public double? lat { get; set; }
            public double? lng { get; set; }
        }

        public class Address
        {
            public string address { get; set; }
            public Location location { get; set; }
            public string postcode { get; set; }
        }
        public class Cost
        {
            public string currency { get; set; }
            public double value { get; set; }
        }
        public class Office
        {
            public string slug { get; set; }
            public string name { get; set; }
        }
        public class Driver
        {
            public string pk { get; set; }
            public Location location { get; set; }
            public string name { get; set; }
        }
        public class VehicleType
        {
            public string pk { get; set; }
            public string name { get; set; }
        }
        public class Booking
        {
            public class SubStatus
            {
                bool is_drop { get; set; }
                bool is_coa { get; set; }
                bool is_passenger_on_board { get; set; }
                bool is_on_way_to_job { get; set; }
                bool is_arrived_waiting { get; set; }
            }
            public string status { get; set; }
            public SubStatus sub_status { get; set; }
            public Office office { get; set; }
            public string payment_method { get; set; }
            public DateTime? pickup_time { get; set; }
            public DateTime? dropoff_time { get; set; }
            public Address pickup_Location { get; set; }
            public Address dropoff_Location { get; set; }
            public List<Address> way_points { get; set; }
            public Cost cost { get; set; }
            public Cost total_cost { get; set; }
            public string pk { get; set; }
            public string key { get; set; }
            public Driver driver { get; set; }
            public VehicleType vehicleType { get; set; }
            public void Merge(Booking b)
            {
                if (status == null)
                    status = b.status;
                if (sub_status == null)
                    sub_status = b.sub_status;
                if (office == null)
                    office = b.office;
                if (pickup_time == null)
                    pickup_time = b.pickup_time;
                if (pickup_Location == null)
                    pickup_Location = b.pickup_Location;
                if (dropoff_Location == null)
                    dropoff_Location = b.dropoff_Location;
                if (way_points == null)
                    way_points = b.way_points;
                if (payment_method == null)
                    payment_method = b.payment_method;
                if (cost == null)
                    cost = b.cost;
                if (total_cost == null)
                    total_cost = b.total_cost;
                if (pk == null)
                    pk = b.pk;
                if (key == null)
                    key = b.key;
                if (driver == null)
                    driver = b.driver;
                if (vehicleType == null)
                    vehicleType = b.vehicleType;
            }
        }

        public class CreateBookingRequest// : ServiceStack.ServiceHost.IReturn<BookingCreateResponse>
        {
            public string passenger { get; set; }
            public string customer_name { get; set; }
            public string customer_phone { get; set; }
            public string customer_email { get; set; }
            public int? distance { get; set; }
            public int? duration { get; set; }
            public List<Address> way_points { get; set; }
            public string extra_instructions { get; set; }
            public string status { get; set; }
            public string pickup_time { get; set; }
            public Address pickup_location { get; set; }
            public Address dropoff_location { get; set; }
            public string dropoff_time { get; set; }
            public string payment_method { get; set; }
            public int luggage { get; set; }
            public int passengers { get; set; }
            public bool pre_paid { get; set; }
        }
        public class CreateBookingResponse
        {
            public string status { get; set; }
            public int status_code { get; set; }
            public string message { get; set; }
            public Booking booking { get; set; }
        }

        public CreateBookingResponse CreateBooking(CreateBookingRequest request)
        {
            string test = FLEET_API_ROOT_URL + "bookings" + "?access_token=" + FLEET_ACCESS_TOKEN;
            JsonServiceClient client = new JsonServiceClient(FLEET_API_ROOT_URL);
            CreateBookingResponse response = client.Post<CreateBookingResponse>("bookings" + "?access_token=" + FLEET_ACCESS_TOKEN, request);
            return response;

        }
        public class Distance
        {
            public float miles { get; set; }
            public float km { get; set; }
        }
        public class Duration
        {
            public int raw_duration { get; set; }
            public int hours { get; set; }
            public int minutes { get; set; }
        }
        public class Fare
        {
            public string formatted_total_cost { get; set; }
            public Distance distance { get; set; }
            public Duration duration { get; set; }
            public int time_to_wait { get; set; }

        }

        public class GetFareRequest// : ServiceStack.ServiceHost.IReturn<BookingCreateResponse>
        {
            public List<Address> way_points { get; set; }
            public string pickup_time { get; set; }
            public Location pickup_location { get; set; }
            public Location dropoff_location { get; set; }
            public string dropoff_time { get; set; }
            public string payment_method { get; set; }
        }
        public class GetFareResponse
        {
            public string status { get; set; }
            public int status_code { get; set; }
            public string message { get; set; }
            public Fare fare { get; set; }
        }

        public GetFareResponse GetFare(GetFareRequest request)
        {
            JsonServiceClient client = new JsonServiceClient(PASSENGER_API_ROOT_URL);
            GetFareResponse response = client.Post<GetFareResponse>("locations/fare" + "?access_token=" + PASSENGER_ACCESS_TOKEN, request);
            return response;

        }


        public class CancelBookingRequest// : ServiceStack.ServiceHost.IReturn<BookingCancelResponse>
        {
            public string description { get; set; }
        }
        public class CancelBookingResponse
        {
            public string status { get; set; }
            public int status_code { get; set; }
            public string message { get; set; }
        }

        public CancelBookingResponse CancelBooking(string bookingPK, CancelBookingRequest request)
        {
            JsonServiceClient client = new JsonServiceClient(PASSENGER_API_ROOT_URL);
            CancelBookingResponse response = client.Post<CancelBookingResponse>("bookings/" + bookingPK + "/cancel?access_token=" + PASSENGER_ACCESS_TOKEN, request);
            return response;
        }

        public class RejectBookingRequest// : ServiceStack.ServiceHost.IReturn<BookingRejectResponse>
        {
        }
        public class RejectBookingResponse
        {
            public string status { get; set; }
            public int status_code { get; set; }
            public string message { get; set; }
        }

        public RejectBookingResponse RejectBooking(string bookingPK)
        {
            JsonServiceClient client = new JsonServiceClient(FLEET_API_ROOT_URL);
            RejectBookingResponse response = client.Post<RejectBookingResponse>("bookings/" + bookingPK + "/Reject?access_token=" + FLEET_ACCESS_TOKEN, null);
            return response;
        }

        public class GetBookingResponse
        {
            public string status { get; set; }
            public int status_code { get; set; }
            public string message { get; set; }
            public Booking booking { get; set; }
        }

        public GetBookingResponse GetBooking(string bookingPK)
        {
            JsonServiceClient client = new JsonServiceClient(PASSENGER_API_ROOT_URL);
            GetBookingResponse response = client.Get<GetBookingResponse>("bookings/" + bookingPK + "?access_token=" + PASSENGER_ACCESS_TOKEN);
            return response;
        }



        public class GetBookingStatusResponse
        {
            public string status { get; set; }
            public int status_code { get; set; }
            public string message { get; set; }
            public Booking booking { get; set; }
        }

        public GetBookingStatusResponse GetBookingStatus(string bookingPK)
        {
            JsonServiceClient client = new JsonServiceClient(FLEET_API_ROOT_URL);
            GetBookingStatusResponse response = client.Get<GetBookingStatusResponse>("bookings/" + bookingPK + "/status?access_token=" + FLEET_ACCESS_TOKEN);
            return response;
        }
    }

    public class TDispatchIntegration : Gateway
    {
        List<VehicleType> vehicleTypes;
        List<Fleet> fleets;
        public TDispatchAPI api;
        public Dictionary<string, TDispatchAPI.Booking> activeTrips = new Dictionary<string, TDispatchAPI.Booking>();
        public Gateway tripthru;
        TimeSpan tripStatusRefreshRate = new TimeSpan(0, 1, 0);


        public TDispatchIntegration(Gateway tripthru, string apiKey,
            string fleetAuth, string fleetAccessToken, string fleetRefreshToken,
            string passengerAuth, string passengerAccessToken, string passengerRefreshToken, string passengerProxyPK,
            List<Fleet> fleets, List<VehicleType> vehicleTypes)
            : base("TDispatch", "TDispatch")
        {
            this.tripthru = tripthru;
            this.fleets = fleets;
            this.vehicleTypes = vehicleTypes;
            api = new TDispatchAPI(apiKey: apiKey,
                fleetAuth: fleetAuth, fleetAccessToken: fleetAccessToken, fleetRefreshToken: fleetRefreshToken,
                passengerAuth: passengerAuth, passengerAccessToken: passengerAccessToken, passengerRefreshToken: passengerRefreshToken, passengerProxyPK: passengerProxyPK);
            TDispatchAPI.GetFleetResponse getFleetResponse = api.GetFleet();
            name = getFleetResponse.fleet.name;
            ID = getFleetResponse.fleet.pk;
        }
        TimeSpan checkStatusInterval = new TimeSpan(0, 0, 30);
        DateTime lastCheckStatus = DateTime.UtcNow;
        public override void Update()
        {
            if (DateTime.UtcNow < lastCheckStatus + checkStatusInterval)
                return;
            List<string> checkTrips = new List<string>(activeTrips.Keys);
            foreach (string tripID in checkTrips)
            {
                string bookingPK;
                bookingPK = activeTrips[tripID].pk;

                TDispatchAPI.GetBookingStatusResponse getBookingStatusResponse = api.GetBookingStatus(bookingPK);

                TDispatchAPI.GetBookingResponse getBookingResponse = api.GetBooking(bookingPK);

                bool update = false;
                update = activeTrips[tripID].status != getBookingStatusResponse.booking.status;
                if (getBookingResponse.booking.pk == null)
                    getBookingResponse.booking.pk = bookingPK;
                if (!update)
                    continue;
                Logger.Log("BookingStatus changed ", getBookingStatusResponse.booking);
                activeTrips[tripID] = getBookingResponse.booking;
                Gateway.UpdateTripStatusRequest request = new Gateway.UpdateTripStatusRequest(
                    clientID: ID,
                    tripID: tripID,
                    status: ConvertTDispatchStatusToTripThruStatus(getBookingStatusResponse.booking.status));
                tripthru.UpdateTripStatus(request);
                if (activeTrips[tripID].status == "completed" || activeTrips[tripID].status == "cancelled" || activeTrips[tripID].status == "rejected" || activeTrips[tripID].status == "missed")
                    activeTrips.Remove(tripID);
            }
            lastCheckStatus = DateTime.UtcNow;

        }
        Status ConvertTDispatchStatusToTripThruStatus(string value)
        {
            switch (value)
            {
                case "missed": return Status.Cancelled;
                case "dispatched": return Status.Queued;
                case "incoming": return Status.Queued;
                case "completed": return Status.Complete;
                case "rejected": return Status.Rejected;
                case "cancelled": return Status.Cancelled;
                case "confirmed": return Status.Dispatched;
                case "active": return Status.Enroute;
                default: throw new Exception("fatal error");
            }
        }


        public void UpdateBooking(string tripID, TDispatchAPI.Booking value)
        {
            if (activeTrips[tripID].status == value.status)
                return;
            Logger.Log("Notify originating partner through TripThru");
            Logger.Tab();
            Gateway.UpdateTripStatusRequest request = new Gateway.UpdateTripStatusRequest(
                clientID: ID,
                tripID: tripID,
                status: ConvertTDispatchStatusToTripThruStatus(value.status));
            tripthru.UpdateTripStatus(request);
            Logger.Untab();
            if (value.status == "Complete")
                activeTrips.Remove(tripID);
        }

        public override Gateway.GetPartnerInfoResponse GetPartnerInfo(GetPartnerInfoRequest request)
        {
            Logger.BeginRequest("GetPartnerInfo received from " + tripthru.name, request);
            Gateway.GetPartnerInfoResponse response = new GetPartnerInfoResponse(fleets, vehicleTypes);
            Logger.EndRequest(request);
            return response;
        }
        public override Gateway.DispatchTripResponse DispatchTrip(Gateway.DispatchTripRequest request)
        {

            Logger.BeginRequest("DispatchTrip recieved from " + tripthru.name, request);
            Gateway.DispatchTripResponse response = null;
            {

                // TDispatch requires that we supply the string address and postal code in addition to the Lng/Lat coordinates
                Pair<string, string> pickup_address = MapTools.GetReverseGeoLocAddress(request.pickupLocation);
                Pair<string, string> dropoff_address = MapTools.GetReverseGeoLocAddress(request.dropoffLocation);

                TDispatchAPI.CreateBookingRequest createRequest = new TDispatchAPI.CreateBookingRequest
                {
                    passenger = api.passengerProxyPK,
                    customer_name = request.passengerName,
                    luggage = 1,
                    passengers = 1,
                    payment_method = "cash",
                    pickup_location = new TDispatchAPI.Address
                    {
                        address = pickup_address.First,
                        location = new TDispatchAPI.Location { lat = request.pickupLocation.Lat, lng = request.pickupLocation.Lng },
                        postcode = pickup_address.Second
                    },
                    pickup_time = request.pickupTime.ToString("yyyy-MM-dd'T'HH:mm:ssK", DateTimeFormatInfo.InvariantInfo),
                    dropoff_location = new TDispatchAPI.Address
                    {
                        address = dropoff_address.First,
                        location = new TDispatchAPI.Location { lat = request.dropoffLocation.Lat, lng = request.dropoffLocation.Lng },
                        postcode = dropoff_address.Second
                    },
                    status = "incoming",
                    pre_paid = false
                };
                TDispatchAPI.CreateBookingResponse createResponse = api.CreateBooking(createRequest);
                if (createResponse.booking.pk == null)
                    throw new Exception("Fatal Error: null booking pk");
                activeTrips.Add(request.tripID, createResponse.booking); // TODO: need to clean these up later
                response = new Gateway.DispatchTripResponse(createResponse.status_code == 200 ? Result.OK : Result.UnknownError);
            }
            Logger.EndRequest(response);
            return response;
        }
        public override Gateway.QuoteTripResponse QuoteTrip(Gateway.QuoteTripRequest request)
        {
            Logger.BeginRequest("QuoteTrip received from " + tripthru.name, request);

            Gateway.QuoteTripResponse response = null;
            {

                // TDispatch requires that we supply the string address and postal code in addition to the Lng/Lat coordinates
                Pair<string, string> pickup_address = MapTools.GetReverseGeoLocAddress(request.pickupLocation);
                Pair<string, string> dropoff_address = MapTools.GetReverseGeoLocAddress(request.dropoffLocation);

                TDispatchAPI.GetFareRequest createRequest = new TDispatchAPI.GetFareRequest
                {
                    payment_method = "cash",
                    pickup_location = new TDispatchAPI.Location { lat = request.pickupLocation.Lat, lng = request.pickupLocation.Lng },
                    pickup_time = request.pickupTime.ToString("yyyy-MM-dd'T'HH:mm:ssK", DateTimeFormatInfo.InvariantInfo),
                    dropoff_location = new TDispatchAPI.Location { lat = request.dropoffLocation.Lat, lng = request.dropoffLocation.Lng }
                };
                // TODO: replace with POST /locations/fare
                TDispatchAPI.GetFareResponse createResponse = api.GetFare(createRequest);
                List<Quote> quotes = new List<Quote>();
                var price = 0.0;
                try
                {
                    price = double.Parse(createResponse.fare.formatted_total_cost.Replace("$",""));
                }
                catch (Exception e) { }
                quotes.Add(new Quote(partnerID: ID, partnerName: name, fleetID: ID, fleetName: name, price: price, ETA: DateTime.UtcNow + new TimeSpan(0, 0, createResponse.fare.time_to_wait)));
                response = new Gateway.QuoteTripResponse(quotes, Result.OK);
            }
            Logger.EndRequest(response);
            return response;
        }

        public override Gateway.GetTripStatusResponse GetTripStatus(Gateway.GetTripStatusRequest request)
        {
            Logger.BeginRequest("GetTripStatus received from " + tripthru.name, request);
            string bookingPK = activeTrips[request.tripID].pk;

            TDispatchAPI.GetBookingStatusResponse getBookingStatusResponse = api.GetBookingStatus(bookingPK);

            TDispatchAPI.GetBookingResponse getBookingResponse = api.GetBooking(bookingPK);

            TDispatchAPI.Booking booking = getBookingStatusResponse.booking;
            booking.Merge(getBookingResponse.booking);

            TDispatchAPI.GetFareResponse fare = api.GetFare(new TDispatchAPI.GetFareRequest
            {
                way_points = booking.way_points,
                pickup_time = booking.pickup_time != null ? ((DateTime)booking.pickup_time).ToString("yyyy-MM-dd'T'HH:mm:ssK", DateTimeFormatInfo.InvariantInfo) : null,
                pickup_location = booking.pickup_Location.location,
                dropoff_location = booking.dropoff_Location.location,
                dropoff_time = booking.dropoff_time != null ? ((DateTime)booking.dropoff_time).ToString("yyyy-MM-dd'T'HH:mm:ssK", DateTimeFormatInfo.InvariantInfo) : null,
                payment_method = booking.payment_method
            });

            Gateway.GetTripStatusResponse response = new Gateway.GetTripStatusResponse(
                partnerID: ID, partnerName: name,
                fleetID: booking.office.slug, fleetName: booking.office.name,
                driverID: booking.driver != null ? booking.driver.pk : null,
                driverName: booking.driver != null ? booking.driver.name : null,
                driverLocation: booking.driver != null ? new Location((double)booking.driver.location.lat,
                    (double)booking.driver.location.lng) : null, price: booking.cost.value, distance: fare.fare.distance.km,
                    result: Result.OK, status:this.ConvertTDispatchStatusToTripThruStatus(booking.status)
                    );
            if (booking.pk == null)
                booking.pk = bookingPK;
            activeTrips[request.tripID] = booking;
            Logger.EndRequest(response);
            return response;
        }
        public override Gateway.UpdateTripStatusResponse UpdateTripStatus(Gateway.UpdateTripStatusRequest request)
        {
            Logger.BeginRequest("UpdateTripStatus received from " + tripthru.name, request);
            Gateway.UpdateTripStatusResponse response;
            if (request.status == Status.Cancelled)
            {
                TDispatchAPI.CancelBookingResponse resp = api.CancelBooking(activeTrips[request.tripID].pk, new TDispatchAPI.CancelBookingRequest { description = "abandoned" });
                response = new Gateway.UpdateTripStatusResponse(result: Result.OK);
            }
            else
                response = new Gateway.UpdateTripStatusResponse(result: Result.MethodNotSupported);
            Logger.EndRequest(request);
            return response;
        }
    }


}
