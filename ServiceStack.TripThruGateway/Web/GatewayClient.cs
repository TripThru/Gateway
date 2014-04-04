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

        public GatewayClient(string ID, string name, string accessToken, string rootUrl) : base(ID, name)
        {
            AccessToken = accessToken;
            RootUrl = rootUrl.EndsWith("/") ? rootUrl : rootUrl + "/";
        }
        public override Gateway.RegisterPartnerResponse RegisterPartner(Gateway.RegisterPartnerRequest request)
        {
            JsonServiceClient client = new JsonServiceClient(RootUrl);
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
            Logger.BeginRequest("GetPartnerInfo sent to " + name, request);

            JsonServiceClient client = new JsonServiceClient(RootUrl);
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
            Logger.BeginRequest("DispatchTrip sent to " + name, request);
            GatewayService.Dispatch dispatch = new GatewayService.Dispatch
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
            GatewayService.DispatchResponse resp = client.Get<GatewayService.DispatchResponse>(dispatch);
            Gateway.DispatchTripResponse response = new Gateway.DispatchTripResponse
            {
                result = resp.ResultCode,
            };
            Logger.EndRequest(response);
            return response;
        }

        public override Gateway.QuoteTripResponse QuoteTrip(Gateway.QuoteTripRequest request)
        {
            Logger.BeginRequest("QuoteTrip sent to " + name, request);
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
            Logger.BeginRequest("GetTripStatus sent to " + name, request);
            JsonServiceClient client = new JsonServiceClient(RootUrl);
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
            Logger.BeginRequest("UpdateTripStatus sent to " + name, request);
            JsonServiceClient client = new JsonServiceClient(RootUrl);
            GatewayService.TripStatusResponse resp = client.Put<GatewayService.TripStatusResponse>(new GatewayService.TripStatus
            {
                access_token = AccessToken,
                Status = request.status,
                TripId = request.tripID,
                DriverLocationLat = request.driverLocation != null ? (double?)request.driverLocation.Lat : null,
                DriverLocationLng = request.driverLocation != null ? (double?)request.driverLocation.Lng : null,
                DriverLocationAddress = request.driverLocation != null ? request.driverLocation.Address : null
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