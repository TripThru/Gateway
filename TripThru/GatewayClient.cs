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
    public class GatewayClient : GatewayWithCallbackUrl
    {

        public string AccessToken { get; set; } //Directly assing access token until authentication is implemented
        private TimeSpan? timeout;

        public GatewayClient(string ID, string name, string rootUrl, string accessToken)
            : base(ID, name, rootUrl)
        {
            AccessToken = accessToken;
            timeout = new TimeSpan(0, 5, 0);
        }
        private JsonServiceClient GetClient()
        {
            return new JsonServiceClient(RootUrl) { Timeout = this.timeout };
        }
        public override Gateway.RegisterPartnerResponse RegisterPartner(Gateway.RegisterPartnerRequest request)
        {
            Uri uri;
            if (!Uri.TryCreate(RootUrl, UriKind.Absolute, out uri))
                throw new Exception("Invalid callback url: " + RootUrl);
            var client = GetClient();
            var resp = client.Post<GatewayService.PartnerResponse>(new GatewayService.PartnerRequest
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
                throw new Exception("Invalid callback url: " + RootUrl);

            Logger.BeginRequest("GetPartnerInfo sent to " + name, request);

            var client = GetClient();
            GatewayService.NetworksResponse resp = client.Get<GatewayService.NetworksResponse>(new GatewayService.Networks
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

        public override void DispatchTripAsync(Gateway.DispatchTripRequest request, Action<Gateway.DispatchTripResponse> callback)
        {
            Uri uri;
            if (!Uri.TryCreate(RootUrl, UriKind.Absolute, out uri))
                throw new Exception("Invalid callback url: " + RootUrl);

            var tripId = request.tripID;
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
                DropoffLat = request.dropoffLocation == null ? (double?)null : request.dropoffLocation.Lat,
                DropoffLng = request.dropoffLocation == null ? (double?)null : request.dropoffLocation.Lng,
                PaymentMethod = request.paymentMethod,
                VehicleType = request.vehicleType,
                MaxPrice = request.maxPrice,
                MinRating = request.minRating,
                PartnerId = request.partnerID,
                FleetId = request.fleetID,
                DriverId = request.driverID,
                TripId = request.tripID
            };
            var client = GetClient();
            Logger.BeginRequest("Async DispatchTrip sent to " + name + ", trip: " + tripId, request);
            client.PostAsync<GatewayService.TripResponse>(dispatch, 
                r => {
                    Logger.BeginRequest("Successful async DispatchTrip response received for trip: " + tripId, r);
                    var result = new Gateway.DispatchTripResponse(r.ResultCode);
                    Logger.Log("Invoking callback");
                    callback(result);
                },
                (r, ex) =>
                {
                    Logger.BeginRequest("Exception ocurred async DispatchTrip. Trip: " + tripId + ", Result: " + (r != null ? r.ResultCode.ToString() : "null"), ex);
                    Gateway.DispatchTripResponse result = null;
                    if (r != null)
                        result = new Gateway.DispatchTripResponse(r.ResultCode);
                    else
                        result = new Gateway.DispatchTripResponse(Result.UnknownError);
                    Logger.Log("Invoking callback");
                    callback(result);
                }
            );
            Logger.EndRequest(null);
        }

        public override Gateway.DispatchTripResponse DispatchTrip(Gateway.DispatchTripRequest request)
        {
            Uri uri;
            if (!Uri.TryCreate(RootUrl, UriKind.Absolute, out uri))
                throw new Exception("Invalid callback url: " + RootUrl);

            Logger.BeginRequest("DispatchTrip sent to " + name, request);
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
                DropoffLat = request.dropoffLocation == null ? (double?)null : request.dropoffLocation.Lat,
                DropoffLng = request.dropoffLocation == null ? (double?)null : request.dropoffLocation.Lng,
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
            GatewayService.TripResponse resp = client.Post<GatewayService.TripResponse>(dispatch);
            Gateway.DispatchTripResponse response = new Gateway.DispatchTripResponse
            {
                result = resp.ResultCode,
            };
            Logger.EndRequest(response);
            return response;
        }

        public override void QuoteTripAsync(Gateway.QuoteTripRequest request, Action<Gateway.QuoteTripResponse> callback)
        {
            Uri uri;
            if (!Uri.TryCreate(RootUrl, UriKind.Absolute, out uri))
                throw new Exception("Invalid callback url: " + RootUrl);

            var tripId = request.tripId;
            GatewayService.Quote quotes = new GatewayService.Quote
            {
                access_token = AccessToken,
                PassengerId = request.passengerID,
                PassengerName = request.passengerName,
                Luggage = request.luggage,
                Persons = request.persons,
                PickupLat = request.pickupLocation.Lat,
                PickupLng = request.pickupLocation.Lng,
                PickupTime = request.pickupTime,
                DropoffLat = request.dropoffLocation == null ? (double?)null : request.dropoffLocation.Lat,
                DropoffLng = request.dropoffLocation == null ? (double?)null : request.dropoffLocation.Lng,
                PaymentMethod = request.paymentMethod,
                VehicleType = request.vehicleType,
                MaxPrice = request.maxPrice,
                MinRating = request.minRating,
                FleetId = request.fleetID,
                DriverId = request.driverID,
                TripId = request.tripId
            };
            var client = GetClient();

            Logger.BeginRequest("Async QuoteTrip sent to " + name + ". Trip: " + tripId, request);
            client.PostAsync<GatewayService.QuoteResponse>(quotes,
                r =>
                {
                    Logger.BeginRequest("Successful async QuoteTrip response received for trip: " + tripId, r);
                    var result = new Gateway.QuoteTripResponse(r.ResultCode);
                    Logger.Log("Invoking callback");
                    callback(result);
                },
                (r, ex) =>
                {
                    Logger.BeginRequest("Exception ocurred async QuoteTrip. Trip: " + tripId + ", Result: " + (r != null ? r.ResultCode.ToString() : "null"), ex);
                    Gateway.QuoteTripResponse result = null;
                    if (r != null)
                        result = new Gateway.QuoteTripResponse(r.ResultCode);
                    else
                        result = new Gateway.QuoteTripResponse(Result.UnknownError);
                    Logger.Log("Invoking callback");
                    callback(result);
                }
            );
            Logger.EndRequest(null);
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
            GatewayService.Quote quotes = new GatewayService.Quote
            {
                access_token = AccessToken,
                PassengerId = request.passengerID,
                PassengerName = request.passengerName,
                Luggage = request.luggage,
                Persons = request.persons,
                PickupLat = request.pickupLocation.Lat,
                PickupLng = request.pickupLocation.Lng,
                PickupTime = request.pickupTime,
                DropoffLat = request.dropoffLocation == null ? (double?)null : request.dropoffLocation.Lat,
                DropoffLng = request.dropoffLocation == null ? (double?)null : request.dropoffLocation.Lng,
                PaymentMethod = request.paymentMethod,
                VehicleType = request.vehicleType,
                MaxPrice = request.maxPrice,
                MinRating = request.minRating,
                FleetId = request.fleetID,
                DriverId = request.driverID,
            };
            JsonServiceClient client = new JsonServiceClient(RootUrl);
            client.Timeout = timeout;
            GatewayService.QuoteResponse resp = client.Get<GatewayService.QuoteResponse>(quotes);
            Gateway.QuoteTripResponse response = new Gateway.QuoteTripResponse
            {
                result = resp.ResultCode
            };
            Logger.EndRequest(response);
            return response;

        }

        public override void UpdateQuoteAsync(Gateway.UpdateQuoteRequest request, Action<Gateway.UpdateQuoteResponse> callback)
        {
            Uri uri;
            if (!Uri.TryCreate(RootUrl, UriKind.Absolute, out uri))
                throw new Exception("Invalid callback url: " + RootUrl);

            var tripId = request.tripId;
            GatewayService.Quote quotes = new GatewayService.Quote
            {
                access_token = AccessToken,
                TripId = request.tripId,
                Count = request.quotes.Count,
                Quotes = request.quotes
            };
            var client = GetClient();

            Logger.BeginRequest("Async UpdateQuote sent to " + name + ". Trip: " + tripId, request);
            client.PutAsync<GatewayService.QuoteResponse>(quotes,
                r =>
                {
                    Logger.BeginRequest("Successful async UpdateQuote response received for trip: " + tripId, r);
                    var result = new Gateway.UpdateQuoteResponse(r.ResultCode);
                    Logger.Log("Invoking callback");
                    callback(result);
                },
                (r, ex) =>
                {
                    Logger.BeginRequest("Exception ocurred async UpdateQuote. Trip: " + tripId + ", Result: " + (r != null ? r.ResultCode.ToString() : "null"), ex);
                    Gateway.UpdateQuoteResponse result = null;
                    if (r != null)
                        result = new Gateway.UpdateQuoteResponse(r.ResultCode);
                    else
                        result = new Gateway.UpdateQuoteResponse(Result.UnknownError);
                    Logger.Log("Invoking callback");
                    callback(result);
                }
            );
            Logger.EndRequest(null);
        }

        public override Gateway.UpdateQuoteResponse UpdateQuote(Gateway.UpdateQuoteRequest request)
        {
            Uri uri;
            if (!Uri.TryCreate(RootUrl, UriKind.Absolute, out uri))
                throw new Exception("Invalid callback url: " + RootUrl);

            var tripId = request.tripId;
            GatewayService.Quote quotes = new GatewayService.Quote
            {
                access_token = AccessToken,
                TripId = request.tripId,
                Count = request.quotes.Count,
                Quotes = request.quotes
            };
            var client = GetClient();

            Logger.BeginRequest("UpdateQuote sent to " + name + ". Trip: " + tripId, request);
            var response = client.Put<GatewayService.QuoteResponse>(quotes);
            var result = new Gateway.UpdateQuoteResponse(response.ResultCode);
            Logger.EndRequest(result);
            return result;
        }

        public override Gateway.GetQuoteResponse GetQuote(Gateway.GetQuoteRequest request)
        {
            Uri uri;
            if (!Uri.TryCreate(RootUrl, UriKind.Absolute, out uri))
                throw new Exception("Invalid callback url: " + RootUrl);
            Logger.BeginRequest("GetQuote sent to " + name + ". Trip: " + request.tripId, request);

            var client = GetClient();
            GatewayService.QuoteResponse resp = client.Get<GatewayService.QuoteResponse>(new GatewayService.Quote
            {
                access_token = AccessToken,
                TripId = request.tripId
            });

            GetQuoteResponse response;
            if (resp.ResultCode == Result.OK)
            {
                response = new Gateway.GetQuoteResponse(
                    status: resp.Status,
                    quotes: resp.Quotes,
                    result: Gateway.Result.OK
                );
            }
            else
            {
                response = new Gateway.GetQuoteResponse(result: Result.UnknownError);
            }
            Logger.EndRequest(response);
            return response;
        }

        public override Gateway.GetTripStatusResponse GetTripStatus(Gateway.GetTripStatusRequest request)
        {

            Uri uri;
            if (!Uri.TryCreate(RootUrl, UriKind.Absolute, out uri))
                throw new Exception("Invalid callback url: " + RootUrl);
            Logger.BeginRequest("GetTripStatus sent to " + name, request);
            var client = GetClient();
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
                throw new Exception("Invalid callback url: " + RootUrl);
            Logger.BeginRequest("UpdateTripStatus sent to " + name, request);
            var client = GetClient();
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