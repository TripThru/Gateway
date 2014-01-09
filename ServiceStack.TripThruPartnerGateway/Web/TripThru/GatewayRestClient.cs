using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Web;
using RestSharp;
using RestSharp.Deserializers;
using ServiceStack.Text;

namespace ServiceStack.TripThruGateway.TripThru
{
    public class GatewayRestClient
    {

        private RestClient Client { get; set; }
        private string AccessToken { get; set; } //Directly assing access token until authentication is implemented
        private string RootUrl { get; set; }

        public GatewayRestClient(string accessToken, string rootUrl)
        {
            AccessToken = accessToken;
            RootUrl = rootUrl.EndsWith("/") ? rootUrl : rootUrl+"/";
        }

        public Gateway.GetPartnerInfo.Response GetPartnerInfo()
        {
            var r= Request("GET", "partners", null);

            if (r!= null)
            {
                var response = JsonSerializer.DeserializeFromString<GatewayService.PartnersResponse>(r);
                if (response.ResultCode == Gateway.Result.OK)
                {
                    return new Gateway.GetPartnerInfo.Response(
                        response.Fleets,
                        response.VehicleTypes,
                        response.ResultCode
                        );    
                }

                return new Gateway.GetPartnerInfo.Response(
                    result : response.ResultCode
                    );
            }

            return new Gateway.GetPartnerInfo.Response
            {
                result = Gateway.Result.UnknownError
            };
        }

        public Gateway.DispatchTrip.Response DispatchTrip(Gateway.DispatchTrip.Request request)
        {
            var partnerRequest = JsonSerializer.SerializeToString(new GatewayService.Dispatch
            {
                ForeignId = request.foreignID,
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
                Waypoints = request.waypoints
            });

            var r = Request("POST", "trip/dispatch", partnerRequest);

            if (r!= null)
            {
                var response = JsonSerializer.DeserializeFromString<GatewayService.DispatchResponse>(r);

                if (response.ResultCode == Gateway.Result.OK)
                {
                    return new Gateway.DispatchTrip.Response
                    {
                        result = Gateway.Result.OK, 
                        tripID = response.TripId
                    };
                }

                return new Gateway.DispatchTrip.Response
                {
                    result = response.ResultCode
                };
            }

            return new Gateway.DispatchTrip.Response
            {
                result = Gateway.Result.UnknownError
            };
        }

        public Gateway.QuoteTrip.Response QuoteTrip(Gateway.QuoteTrip.Request request)
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

            var r = Request("POST", "trip/quotes", partnerRequest);

            if (r!= null)
            {
                var response = JsonSerializer.DeserializeFromString<GatewayService.QuotesResponse>(r);

                if (response.ResultCode == Gateway.Result.OK)
                {
                    return new Gateway.QuoteTrip.Response
                    {
                        result = Gateway.Result.OK, 
                        quotes = response.Quotes
                    };
                }

                return new Gateway.QuoteTrip.Response
                {
                    result = response.ResultCode
                };
            }

            return new Gateway.QuoteTrip.Response
            {
                result = Gateway.Result.UnknownError
            };
            
        }

        public Gateway.GetTripStatus.Response GetTripStatus(Gateway.GetTripStatus.Request request)
        {
            var r = Request("GET", "trip/" + request.tripID + "/status", null);

            if (r != null)
            {
                var response = JsonSerializer.DeserializeFromString<GatewayService.TripResponse>(r);

                if (response.ResultCode == Gateway.Result.OK)
                {
                    return new Gateway.GetTripStatus.Response
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

                return new Gateway.GetTripStatus.Response
                {
                    result = response.ResultCode
                };
            }

            return new Gateway.GetTripStatus.Response
            {
                result = Gateway.Result.UnknownError
            };
        }

        public Gateway.UpdateTripStatus.Response UpdateTripStatus(Gateway.UpdateTripStatus.Request request)
        {
            var partnerRequest = JsonSerializer.SerializeToString(new GatewayService.Trip
            {
                Status = request.status,
                TripId = request.tripID
            });
            
            var r = Request("PUT", "trip/" + request.tripID + "/status", partnerRequest);

            if (r != null)
            {
                var response = JsonSerializer.DeserializeFromString<GatewayService.TripResponse>(r);

                if (response.ResultCode == Gateway.Result.OK)
                {
                    return new Gateway.UpdateTripStatus.Response
                    {
                        result = Gateway.Result.OK
                    };
                }

                return new Gateway.UpdateTripStatus.Response
                {
                    result = response.ResultCode
                };
            }

            return new Gateway.UpdateTripStatus.Response
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

            var response = Client.Execute(request);
            return response.Content;
        }

        
    }
}