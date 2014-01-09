using System;
using System.Linq;
using ServiceStack.Common;
using ServiceStack.TripThruGateway.TripThru;

namespace ServiceStack.TripThruGateway
{
    using System.Collections.Generic;
    using ServiceStack.OrmLite;
    using ServiceStack.ServiceHost;
    using ServiceStack.ServiceInterface;

    public class GatewayService
    {
        public static TripThru.Partner TripThruPartner; //gets initialized in InitPartners
        public static string TripThruAccessToken = "jaosid1201231";
        public static string TripThruId = "TripThru";

        [Api("Use GET to get a list of partners or POST to create search for partners meeting the filter criteria.")]
        [Route("/partners", "POST, GET")]
        public class Partners : IReturn<PartnersResponse>
        {
            public List<Zone> Zone { get; set; }
            public List<VehicleType> VehicleTypes { get; set; }
        }

        public class PartnersResponse
        {
            public string Result { get; set; }
            public Gateway.Result ResultCode { get; set; }
            public List<Fleet> Fleets { get; set; }
            public List<VehicleType> VehicleTypes { get; set; }
        }

        public class PartnersService : Service
        {

            public PartnersResponse Post(Partners request)
            {
                var accessToken = this.Request.QueryString.Get("access_token");
                if (accessToken.Equals(TripThruAccessToken))
                {
                    var response = TripThruPartner.getPartnerInfo.Get(new Gateway.GetPartnerInfo.Request(
                        clientID: TripThruId
                        ));

                    if (response.result == Gateway.Result.OK)
                    {
                        return new PartnersResponse
                        {
                            Result = "OK",
                            ResultCode = Gateway.Result.OK,
                            Fleets = response.fleets,
                            VehicleTypes = response.vehicleTypes,
                        };
                    }


                    return new PartnersResponse
                    {
                        Result = "Failed",
                        ResultCode = response.result
                    };
                }

                Logger.Log("POST /partners called with access token " + Request.RemoteIp + ": Response = Authentication failed");
                return new PartnersResponse
                {
                    Result = "Failed",
                    ResultCode = Gateway.Result.AuthenticationError
                };
            }

            public PartnersResponse Get(Partners request)
            {
                var accessToken = this.Request.QueryString.Get("access_token");
                if (accessToken.Equals(TripThruAccessToken))
                {
                    var response = TripThruPartner.getPartnerInfo.Get(new Gateway.GetPartnerInfo.Request(
                            TripThruId
                        ));

                    if (response.result == Gateway.Result.OK)
                    {
                        return new PartnersResponse
                        {
                            Result = "OK",
                            ResultCode = Gateway.Result.OK,
                            Fleets = response.fleets,
                            VehicleTypes = response.vehicleTypes
                        };
                    }

                    return new PartnersResponse
                    {
                        Result = "Failed",
                        ResultCode = response.result
                    };
                }

                Logger.Log("GET /partners called with access token " + Request.RemoteIp + ": Response = Authentication failed");
                return new PartnersResponse
                {
                    Result = "Failed",
                    ResultCode = Gateway.Result.AuthenticationError
                };
            }

        }

        [Api("Use POST to create search for quotes meeting the filter criteria.")]
        [Route("/trip/quotes", "POST")]
        public class Quotes : IReturn<QuotesResponse>
        {
            public string PassengerId { get; set; }
            public string PassengerName { get; set; }
            public int? Luggage { get; set; }
            public int? Persons { get; set; }
            public DateTime PickupTime { get; set; }
            public Location PickupLocation { get; set; }
            public Location DropoffLocation { get; set; }
            public List<Location> WayPoints { get; set; }
            public PaymentMethod? PaymentMethod { get; set; }
            public VehicleType? VehicleType { get; set; }
            public double? MaxPrice { get; set; }
            public int? MinRating { get; set; }
            public string PartnerId { get; set; }
            public string FleetId { get; set; }
            public string DriverId { get; set; }
        }

        public class QuotesResponse
        {
            public string Result { get; set; }
            public Gateway.Result ResultCode { get; set; }
            public int? Count { get; set; }
            public List<Quote> Quotes { get; set; }
        }

        public class QuotesService : Service
        {

            public QuotesResponse Post(Quotes request)
            {
                var accessToken = this.Request.QueryString.Get("access_token");
                if (accessToken.Equals(TripThruAccessToken))
                {
                    var response = TripThruPartner.quoteTrip.Get(new Gateway.QuoteTrip.Request(
                        TripThruId,
                        request.PickupLocation,
                        request.PickupTime,
                        request.PassengerId,
                        request.PassengerName,
                        request.Luggage,
                        request.Persons,
                        request.DropoffLocation,
                        request.WayPoints,
                        request.PaymentMethod,
                        request.VehicleType,
                        request.MaxPrice,
                        request.MinRating,
                        request.PartnerId,
                        request.FleetId,
                        request.DriverId
                        ));

                    if (response.result == Gateway.Result.OK)
                    {
                        return new QuotesResponse
                        {
                            Count = response.quotes.Count,
                            Quotes = response.quotes,
                            ResultCode = response.result,
                            Result = "OK"
                        };
                    }

                    return new QuotesResponse
                    {
                        Result = "Failed",
                        ResultCode = response.result
                    };
                }

                Logger.Log("POST /trip/quotes called with access token " + Request.RemoteIp + ": Response = Authentication failed");
                return new QuotesResponse
                {
                    Result = "Failed",
                    ResultCode = Gateway.Result.AuthenticationError
                };
            }

        }

        [Api("Use POST to dispatch a trip to a fleet. Can be used in conjuction with /quotes")]
        [Route("/trip/dispatch", "POST")]
        public class Dispatch : IReturn<DispatchResponse>
        {
            public string ForeignId { get; set; }
            public string PassengerId { get; set; }
            public string PassengerName { get; set; }
            public int? Luggage { get; set; }
            public int? Persons { get; set; }
            public Location PickupLocation { get; set; }
            public DateTime PickupTime { get; set; }
            public Location DropoffLocation { get; set; }
            public List<Location> Waypoints { get; set; }
            public PaymentMethod? PaymentMethod { get; set; }
            public VehicleType? VehicleType { get; set; }
            public double? MaxPrice { get; set; }
            public int? MinRating { get; set; }
            public string PartnerId { get; set; }
            public string FleetId { get; set; }
            public string DriverId { get; set; }
            public string QuoteId { get; set; }
            public string ExtraInstructions { get; set; }
            public string QuotedPrice { get; set; }
        }

        public class DispatchResponse
        {
            public string Result { get; set; }
            public Gateway.Result ResultCode { get; set; }
            public string TripId { get; set; }
        }

        public class DispatchService : Service
        {
            public DispatchResponse Post(Dispatch request)
            {
                var accessToken = this.Request.QueryString.Get("access_token");
                if (accessToken.Equals(TripThruAccessToken))
                {
                    var response = TripThruPartner.dispatchTrip.Post(new Gateway.DispatchTrip.Request(
                        TripThruId,
                        request.PickupLocation,
                        request.PickupTime,
                        request.ForeignId,
                        request.PassengerId,
                        request.PassengerName,
                        request.Luggage,
                        request.Persons,
                        request.DropoffLocation,
                        request.Waypoints,
                        request.PaymentMethod,
                        request.VehicleType,
                        request.MaxPrice,
                        request.MinRating,
                        request.PartnerId,
                        request.FleetId,
                        request.DriverId
                        ));

                    if (response.result == Gateway.Result.OK)
                    {
                        return new DispatchResponse
                        {
                            Result = "OK",
                            ResultCode = response.result,
                            TripId = response.tripID
                        };
                    }

                    return new DispatchResponse
                    {
                        Result = "Failed",
                        ResultCode = response.result
                    };
                }

                Logger.Log("POST /trip/dispatch called with access token " + Request.RemoteIp + ": Response = Authentication failed");
                return new DispatchResponse
                {
                    Result = "Failed",
                    ResultCode = Gateway.Result.AuthenticationError
                };
            }
        }

        [Api("Use GET /trip/{Id}/status for trip status and PUT /trip/{Id}/status to update a trip status. Use POST /trip/{Id}/rating to rate a trip.")]
        [Route("/trip/{Id}/status", "GET, PUT")]
        [Route("/trip/{Id}/rating", "POST")]
        public class Trip : IReturn<TripResponse>
        {
            public string TripId { get; set; }
            public Status Status { get; set; }
            public int? Rating { get; set; }
        }

        public class TripResponse
        {
            public string Result { get; set; }
            public Gateway.Result ResultCode { get; set; }
            public string PartnerName { get; set; }
            public string PartnerId { get; set; }
            public string FleetId { get; set; }
            public string FleetName { get; set; }
            public string DriverId { get; set; }
            public string DriverName { get; set; }
            public Location DriverLocation { get; set; }
            public DateTime? PickupTime { get; set; }
            public DateTime? DropoffTime { get; set; }
            public VehicleType? VehicleType { get; set; }
            public Status? Status { get; set; }
            public DateTime? ETA { get; set; } // in minutes;
        }

        public class TripService : Service
        {
            public TripResponse Get(Trip request)
            {
                var accessToken = this.Request.QueryString.Get("access_token");
                if (accessToken.Equals(TripThruAccessToken))
                {
                    var response = TripThruPartner.getTripStatus.Get(new Gateway.GetTripStatus.Request(
                        TripThruId,
                        request.TripId
                        ));

                    if (response.result == Gateway.Result.OK)
                    {
                        return new TripResponse
                        {
                            DriverId = response.driverID,
                            DriverLocation = response.driverLocation,
                            DriverName = response.driverName,
                            DropoffTime = response.dropoffTime,
                            ETA = response.ETA,
                            FleetId = response.fleetID,
                            FleetName = response.fleetName,
                            PartnerId = response.partnerID,
                            PartnerName = response.partnerName,
                            PickupTime = response.pickupTime,
                            VehicleType = response.vehicleType,
                            Result = "OK",
                            ResultCode = response.result,
                            Status = response.status
                        };
                    }

                    return new TripResponse
                    {
                        Result = "Failed",
                        ResultCode = response.result
                    };

                }

                Logger.Log("GET /trip//status called with access token " + Request.RemoteIp + ": Response = Authentication failed");
                return new TripResponse
                {
                    Result = "Failed",
                    ResultCode = Gateway.Result.AuthenticationError
                };
            }

            public TripResponse Put(Trip request)
            {
                var accessToken = this.Request.QueryString.Get("access_token");
                if (accessToken.Equals(TripThruAccessToken))
                {
                    var response = TripThruPartner.updateTripStatus.Post(new Gateway.UpdateTripStatus.Request(
                        TripThruId,
                        request.TripId,
                        request.Status
                        ));

                    if (response.result == Gateway.Result.OK)
                    {
                        return new TripResponse
                        {
                            Result = "OK",
                            ResultCode = response.result
                        };
                    }

                    return new TripResponse
                    {
                        Result = "Failed",
                        ResultCode = response.result
                    };
                }

                Logger.Log("PUT /trip//status called with access token " + Request.RemoteIp + ": Response = Authentication failed");
                return new TripResponse
                {
                    Result = "Failed",
                    ResultCode = Gateway.Result.AuthenticationError
                };
            }

            //public TripResponse Post(Trip request)
            //{
            //    var accessToken = this.Request.QueryString.Get("access_token");
            //    return new TripResponse
            //    {
            //        Result = "InvalidParameters",
            //        ResultCode = Gateway.Result.InvalidParameters
            //    };
            //}
        }
    }
}