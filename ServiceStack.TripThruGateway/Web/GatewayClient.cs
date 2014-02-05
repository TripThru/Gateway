using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Web;
using RestSharp;
using RestSharp.Deserializers;
using ServiceStack.Text;
using Utils;
using TripThruCore;

namespace ServiceStack.TripThruGateway
{
    public class GatewayClient : Gateway
    {

        private RestClient Client { get; set; }
        private string AccessToken { get; set; } //Directly assing access token until authentication is implemented
        private string RootUrl { get; set; }

        public GatewayClient(string ID, string name, string accessToken, string rootUrl) : base(ID, name)
        {
            AccessToken = accessToken;
            RootUrl = rootUrl.EndsWith("/") ? rootUrl : rootUrl + "/";
            Client = new RestClient(RootUrl);
        }
        public override Gateway.RegisterPartnerResponse RegisterPartner(Gateway.RegisterPartnerRequest request)
        {
            var partnerRequest = JsonSerializer.SerializeToString(new GatewayService.PartnerRequest()
            {
                Name = request.name,
                CallbackUrl = request.callback_url
            });

            var r = Request("POST", "partner", partnerRequest);

            if (r != null && !r.Equals(""))
            {
                var response = JsonSerializer.DeserializeFromString<GatewayService.PartnerResponse>(r);

                if (response.ResultCode == Gateway.Result.OK)
                {
                    return new Gateway.RegisterPartnerResponse
                    {
                        result = Gateway.Result.OK
                    };
                }

                return new Gateway.RegisterPartnerResponse
                {
                    result = response.ResultCode
                };
            }

            return new Gateway.RegisterPartnerResponse
            {
                result = Gateway.Result.UnknownError
            };

        }

        public override Gateway.GetPartnerInfoResponse GetPartnerInfo(Gateway.GetPartnerInfoRequest request)
        {
            var partnerRequest = JsonSerializer.SerializeToString(new GatewayService.Partners
            {
                VehicleTypes = request.vehicleTypes,
                Coverage = request.coverage
            });

            var r = Request("GET", "partners", partnerRequest);

            if (r != null)
            {
                var response = JsonSerializer.DeserializeFromString<GatewayService.PartnersResponse>(r);
                if (response.ResultCode == Gateway.Result.OK)
                {
                    return new Gateway.GetPartnerInfoResponse(
                        response.Fleets,
                        response.VehicleTypes,
                        response.ResultCode
                        );    
                }

                return new Gateway.GetPartnerInfoResponse(
                    result : response.ResultCode
                    );
            }

            return new Gateway.GetPartnerInfoResponse
            {
                result = Gateway.Result.UnknownError
            };
        }

        public override Gateway.DispatchTripResponse DispatchTrip(Gateway.DispatchTripRequest request)
        {
            var partnerRequest = JsonSerializer.SerializeToString(new GatewayService.Dispatch
            {
                PassengerId = request.passengerID,
                PassengerName = request.passengerName,
                Luggage = request.luggage,
                Persons = request.persons,
                PickupLocation = request.pickupLocation,
                PickupTime = request.pickupTime,
                DropoffLocation = request.dropoffLocation,
                PaymentMethod = request.paymentMethod,
                VehicleType = request.vehicleType,
                MaxPrice = request.maxPrice,
                MinRating = request.minRating,
                PartnerId = request.partnerID,
                FleetId = request.fleetID,
                DriverId = request.driverID,
                Waypoints = request.waypoints,
                TripId = request.tripID
            });

            var r = Request("POST", "dispatch", partnerRequest);

            if (r!= null)
            {
                var response = JsonSerializer.DeserializeFromString<GatewayService.DispatchResponse>(r);

                if (response.ResultCode == Gateway.Result.OK)
                {
                    return new Gateway.DispatchTripResponse
                    {
                        result = Gateway.Result.OK
                    };
                }

                return new Gateway.DispatchTripResponse
                {
                    result = response.ResultCode
                };
            }

            return new Gateway.DispatchTripResponse
            {
                result = Gateway.Result.UnknownError
            };
        }

        public override Gateway.QuoteTripResponse QuoteTrip(Gateway.QuoteTripRequest request)
        {
            var partnerRequest = JsonSerializer.SerializeToString(new GatewayService.Quotes
            {
                PassengerId = request.passengerID,
                PassengerName = request.passengerName,
                Luggage = request.luggage,
                Persons = request.persons,
                PickupLocation = request.pickupLocation,
                PickupTime = request.pickupTime,
                DropoffLocation = request.dropoffLocation,
                PaymentMethod = request.paymentMethod,
                VehicleType = request.vehicleType,
                MaxPrice = request.maxPrice,
                MinRating = request.minRating,
                FleetId = request.fleetID,
                DriverId = request.driverID,
                WayPoints = request.waypoints,
            });

            var r = Request("POST", "quotes", partnerRequest);

            if (r != null)
            {
                var response = JsonSerializer.DeserializeFromString<GatewayService.QuotesResponse>(r);

                if (response.ResultCode == Gateway.Result.OK)
                {
                    return new Gateway.QuoteTripResponse
                    {
                        result = Gateway.Result.OK, 
                        quotes = response.Quotes
                    };
                }

                return new Gateway.QuoteTripResponse
                {
                    result = response.ResultCode
                };
            }

            return new Gateway.QuoteTripResponse
            {
                result = Gateway.Result.UnknownError
            };
            
        }

        public override Gateway.GetTripStatusResponse GetTripStatus(Gateway.GetTripStatusRequest request)
        {
            var r = Request("GET", "trip/status/" + request.tripID, null);

            if (r != null)
            {
                var response = JsonSerializer.DeserializeFromString<GatewayService.TripResponse>(r);

                if (response.ResultCode == Gateway.Result.OK)
                {
                    return new Gateway.GetTripStatusResponse
                    {
                        result = Gateway.Result.OK,
                        ETA = response.ETA,
                        driverID = response.DriverId,
                        driverLocation = response.DriverLocation,
                        driverName = response.DriverName,
                        dropoffTime = response.DropoffTime,
                        fleetName = response.FleetName,
                        fleetID = response.FleetId,
                        vehicleType = response.VehicleType,
                        status = response.Status,
                        partnerName = response.PartnerName,
                        partnerID = response.PartnerId,
                        pickupTime = response.PickupTime
                    };
                }

                return new Gateway.GetTripStatusResponse
                {
                    result = response.ResultCode
                };
            }

            return new Gateway.GetTripStatusResponse
            {
                result = Gateway.Result.UnknownError
            };
        }

        public override Gateway.UpdateTripStatusResponse UpdateTripStatus(Gateway.UpdateTripStatusRequest request)
        {
            var partnerRequest = JsonSerializer.SerializeToString(new GatewayService.Trip
            {
                Status = request.status,
                TripId = request.tripID
            });

            var r = Request("PUT", "trip/status/" + request.tripID, partnerRequest);

            if (r != null)
            {
                var response = JsonSerializer.DeserializeFromString<GatewayService.TripResponse>(r);

                if (response.ResultCode == Gateway.Result.OK)
                {
                    return new Gateway.UpdateTripStatusResponse
                    {
                        result = Gateway.Result.OK
                    };
                }

                return new Gateway.UpdateTripStatusResponse
                {
                    result = response.ResultCode
                };
            }

            return new Gateway.UpdateTripStatusResponse
            {
                result = Gateway.Result.UnknownError
            };
        }

        private string Request(String method, String url, String data)
        {
            url += "?access_token=" + AccessToken;

            RestRequest request = null;
            if (method.Equals("POST"))
            {
                request = new RestRequest(url, Method.POST);
                if (data != null)
                {
                    request.AddParameter(
                        "application/json",
                        data,
                        ParameterType.RequestBody
                        );
                }
            }
            else if (method.Equals("PUT"))
            {
                request = new RestRequest(url, Method.PUT);
                if (data != null)
                {
                    request.AddParameter(
                        "application/json",
                        data,
                        ParameterType.RequestBody);
                }
            }
            else if (method.Equals("DELETE"))
            {
                request = new RestRequest(url, Method.DELETE);
            }
            else
            {
                request = new RestRequest(url, Method.GET);
                if (data != null)
                {
                    foreach (var entry in JsonSerializer.DeserializeFromString<Dictionary<string,object>>(data))
                    {
                        request.AddParameter(entry.Key, entry.Value, ParameterType.UrlSegment);
                    }
                }
            }

            request.AddHeader("content-type", "application/json");

            var response = Client.Execute(request);

            return (response == null || response.Content == null || response.Content.Equals("") || !response.ContentType.Contains("application/json")) ? null : response.Content;
        }

        
    }
}