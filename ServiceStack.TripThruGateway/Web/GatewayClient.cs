using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Web;
using ServiceStack.Text;
using Utils;
using TripThruCore;
using ServiceStack.ServiceClient.Web;
using ServiceStack.ServiceModel;
namespace ServiceStack.TripThruGateway
{
    public class GatewayClient : Gateway
    {

        public string AccessToken { get; set; } //Directly assing access token until authentication is implemented
        public string RootUrl { get; set; }
        private TimeSpan? timeout;

        public GatewayClient(string ID, string name, string accessToken, string rootUrl) : base(ID, name)
        {
            AccessToken = accessToken;
            RootUrl = rootUrl.EndsWith("/") ? rootUrl : rootUrl + "/";
            timeout = new TimeSpan(0, 5, 0);
        }
        public override Gateway.RegisterPartnerResponse RegisterPartner(Gateway.RegisterPartnerRequest request)
        {
            Uri uri;
            if (!Uri.TryCreate(RootUrl, UriKind.Absolute, out uri))
                return new Gateway.RegisterPartnerResponse
                {
                    result = Result.InvalidParameters
                };

            JsonServiceClient client = new JsonServiceClient(RootUrl);
            client.Timeout = timeout;
            GatewayService.PartnerResponse resp = client.Post<GatewayService.PartnerResponse>(new GatewayService.PartnerRequest
            {
                access_token = AccessToken,
                Name = request.name,
                CallbackUrl = request.callback_url
            });
            
            return new Gateway.RegisterPartnerResponse
            {
                result = resp.ResultCode
            };
        }

        public override Gateway.GetPartnerInfoResponse GetPartnerInfo(Gateway.GetPartnerInfoRequest request)
        {
            Uri uri;
            if (!Uri.TryCreate(RootUrl, UriKind.Absolute, out uri))
                return new Gateway.GetPartnerInfoResponse
                {
                    result = Result.InvalidParameters
                };

            Logger.BeginRequest("GetPartnerInfo sent to " + name, request);
            //Logger.Log("RootURL: " + RootUrl);

            JsonServiceClient client = new JsonServiceClient(RootUrl);
            client.Timeout = timeout;
            GatewayService.PartnersResponse resp = client.Get<GatewayService.PartnersResponse>(new GatewayService.Partners
            {
                access_token = AccessToken,
            });

            Gateway.GetPartnerInfoResponse response = new Gateway.GetPartnerInfoResponse
            {
                fleets = resp.Fleets,
                vehicleTypes = resp.VehicleTypes,
                result = resp.ResultCode
            };
            Logger.EndRequest(response);
            return response;
        }

        public override Gateway.DispatchTripResponse DispatchTrip(Gateway.DispatchTripRequest request)
        {
            Uri uri;
            if (!Uri.TryCreate(RootUrl, UriKind.Absolute, out uri))
                return new Gateway.DispatchTripResponse
                {
                    result = Result.InvalidParameters
                };
            Logger.BeginRequest("DispatchTrip sent to " + name, request);
            //Logger.Log("RootURL: " + RootUrl);
            GatewayService.Trip dispatch = new GatewayService.Trip
            {
                access_token = AccessToken,
                PassengerId = request.passengerID,
                PassengerName = request.passengerName,
                Luggage = request.luggage,
                Persons = request.persons,
                PickupLat = request.pickupLocation.Lat,
                PickupLng = request.pickupLocation.Lng,
                PickupTime = request.pickupTime,
                DropoffLat = request.dropoffLocation == null ? (double?) null : request.dropoffLocation.Lat,
                DropoffLng = request.dropoffLocation == null ? (double?) null : request.dropoffLocation.Lng,
                PaymentMethod = request.paymentMethod,
                VehicleType = request.vehicleType,
                MaxPrice = request.maxPrice,
                MinRating = request.minRating,
                PartnerId = request.partnerID,
                FleetId = request.fleetID,
                DriverId = request.driverID,
                TripId = request.tripID
            };
            JsonServiceClient client = new JsonServiceClient(RootUrl);
            client.Timeout = timeout;
            GatewayService.TripResponse resp = client.Get<GatewayService.TripResponse>(dispatch);
            Gateway.DispatchTripResponse response = new Gateway.DispatchTripResponse
            {
                result = resp.ResultCode,
            };
            Logger.EndRequest(response);
            return response;
        }

        public override Gateway.QuoteTripResponse QuoteTrip(Gateway.QuoteTripRequest request)
        {
            Uri uri;
            if (!Uri.TryCreate(RootUrl, UriKind.Absolute, out uri))
                return new Gateway.QuoteTripResponse
                {
                    result = Result.InvalidParameters
                };
            Logger.BeginRequest("QuoteTrip sent to " + name, request);
            //Logger.Log("RootURL: " + RootUrl);
            GatewayService.Quotes quotes = new GatewayService.Quotes
            {
                access_token = AccessToken,
                PassengerId = request.passengerID,
                PassengerName = request.passengerName,
                Luggage = request.luggage,
                Persons = request.persons,
                PickupLat = request.pickupLocation.Lat,
                PickupLng = request.pickupLocation.Lng,
                PickupTime = request.pickupTime,
                DropoffLat = request.dropoffLocation == null ? (double?) null : request.dropoffLocation.Lat,
                DropoffLng = request.dropoffLocation == null ? (double?) null : request.dropoffLocation.Lng,
                PaymentMethod = request.paymentMethod,
                VehicleType = request.vehicleType,
                MaxPrice = request.maxPrice,
                MinRating = request.minRating,
                FleetId = request.fleetID,
                DriverId = request.driverID,
            };
            JsonServiceClient client = new JsonServiceClient(RootUrl);
            client.Timeout = timeout;
            GatewayService.QuotesResponse resp = client.Get<GatewayService.QuotesResponse>(quotes);
            Gateway.QuoteTripResponse response = new Gateway.QuoteTripResponse
            {
                result = resp.ResultCode, 
                quotes = resp.Quotes
            };
            Logger.EndRequest(response);
            return response;
            
        }

        public override Gateway.GetTripStatusResponse GetTripStatus(Gateway.GetTripStatusRequest request)
        {
            Uri uri;
            if (!Uri.TryCreate(RootUrl, UriKind.Absolute, out uri))
                return new Gateway.GetTripStatusResponse
                {
                    result = Result.InvalidParameters
                };
            Logger.BeginRequest("GetTripStatus sent to " + name, request);
            //Logger.Log("RootURL: " + RootUrl);
            JsonServiceClient client = new JsonServiceClient(RootUrl);
            client.Timeout = timeout;
            GatewayService.TripStatusResponse resp = client.Get<GatewayService.TripStatusResponse>(new GatewayService.TripStatus
            {
                access_token = AccessToken,
                TripId = request.tripID
            });
            GetTripStatusResponse response;
            if (resp.ResultCode == Result.OK)
            {
                response = new Gateway.GetTripStatusResponse
                {
                    result = Gateway.Result.OK,
                    ETA = resp.ETA,
                    passengerName = resp.PassengerName,
                    driverID = resp.DriverId,
                    driverLocation = resp.DriverLocation,
                    driverInitialLocation = resp.DriverInitialLocation,
                    driverName = resp.DriverName,
                    dropoffTime = resp.DropoffTime,
                    dropoffLocation = resp.DropoffLocation,
                    fleetName = resp.FleetName,
                    fleetID = resp.FleetId,
                    vehicleType = resp.VehicleType,
                    status = resp.Status,
                    partnerName = resp.PartnerName,
                    partnerID = resp.PartnerId,
                    pickupTime = resp.PickupTime,
                    pickupLocation = resp.PickupLocation,
                    distance = resp.Distance,
                    driverRouteDuration = resp.DriverRouteDuration,
                    price = resp.Price
                };
            }
            else
            {
                response = new Gateway.GetTripStatusResponse
                {
                        result = resp.ResultCode
                };
            }
            Logger.EndRequest(response);
            return response;
        }

        public override Gateway.UpdateTripStatusResponse UpdateTripStatus(Gateway.UpdateTripStatusRequest request)
        {
            Uri uri;
            if (!Uri.TryCreate(RootUrl, UriKind.Absolute, out uri))
                return new Gateway.UpdateTripStatusResponse
                {
                    result = Result.InvalidParameters
                };
            Logger.BeginRequest("UpdateTripStatus sent to " + name, request);
            //Logger.Log("RootURL: " + RootUrl);
            JsonServiceClient client = new JsonServiceClient(RootUrl);
            client.Timeout = timeout;
            GatewayService.TripStatusResponse resp = client.Put<GatewayService.TripStatusResponse>(new GatewayService.TripStatus
            {
                access_token = AccessToken,
                Status = request.status,
                TripId = request.tripID,
                DriverLocationLat = request.driverLocation != null ? (double?)request.driverLocation.Lat : null,
                DriverLocationLng = request.driverLocation != null ? (double?)request.driverLocation.Lng : null,
                ETA = request.eta
            });
            Gateway.UpdateTripStatusResponse response;
            response = new Gateway.UpdateTripStatusResponse
            {
                result = resp.ResultCode
            };
            Logger.EndRequest(response);
            return response;
        }
    }
}