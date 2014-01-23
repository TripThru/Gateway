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

        public class PartnerRequest : IReturn<PartnerResponse>
        {
            public string Name { get; set; }
            public string CallbackUrl { get; set; }
        }

        public class PartnerResponse
        {
            public string Result { get; set; }
            public Gateway.Result ResultCode { get; set; }
            public long? Id { get; set; }
        }

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

                string msg = "POST /partners called with invalid access token, ip: " + Request.RemoteIp +
                             ", Response = Authentication failed";
                Logger.Log(msg);
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

                string msg = "GET /partners called with invalid access token, ip: " + Request.RemoteIp +
                             ", Response = Authentication failed";
                Logger.Log(msg);
                return new PartnersResponse
                {
                    Result = "Failed",
                    ResultCode = Gateway.Result.AuthenticationError
                };
            }

        }

        [Api("Use POST or GET to create search for quotes meeting the filter criteria.")]
        [Route("/quotes", "POST, GET")]
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

                string msg = "POST /quotes called with invalid access token, ip: " + Request.RemoteIp +
                             ", Response = Authentication failed";
                Logger.Log(msg);
                return new QuotesResponse
                {
                    Result = "Failed",
                    ResultCode = Gateway.Result.AuthenticationError
                };
            }

            public QuotesResponse Get(Quotes request)
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

                string msg = "GET /quotes called with invalid access token, ip: " + Request.RemoteIp +
                             ", Response = Authentication failed";
                Logger.Log(msg);
                return new QuotesResponse
                {
                    Result = "Failed",
                    ResultCode = Gateway.Result.AuthenticationError
                };
            }

        }

        [Api("Use POST to dispatch a trip to a fleet. Can be used in conjuction with /quotes")]
        [Route("/dispatch", "POST, GET")]
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
            public string TripId { get; set; }
            public string PartnerId { get; set; }
            public string FleetId { get; set; }
            public string DriverId { get; set; }
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
                        request.TripId,
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
                            ResultCode = response.result
                        };
                    }

                    return new DispatchResponse
                    {
                        Result = "Failed",
                        ResultCode = response.result
                    };
                }

                string msg = "POST /dispatch called with invalid access token, ip: " + Request.RemoteIp +
                             ", Response = Authentication failed";
                Logger.Log(msg);
                return new DispatchResponse
                {
                    Result = "Failed",
                    ResultCode = Gateway.Result.AuthenticationError
                };
            }

            public DispatchResponse Get(Dispatch request)
            {
                var accessToken = this.Request.QueryString.Get("access_token");
                if (accessToken.Equals(TripThruAccessToken))
                {
                    var response = TripThruPartner.dispatchTrip.Post(new Gateway.DispatchTrip.Request(
                        TripThruId,
                        request.TripId,
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
                            ResultCode = response.result
                        };
                    }

                    return new DispatchResponse
                    {
                        Result = "Failed",
                        ResultCode = response.result
                    };
                }

                string msg = "GET /dispatch called with invalid access token, ip: " + Request.RemoteIp +
                             ", Response = Authentication failed";
                Logger.Log(msg);
                return new DispatchResponse
                {
                    Result = "Failed",
                    ResultCode = Gateway.Result.AuthenticationError
                };
            }
        }

        [Api("Use GET /trip/{Id}/status for trip status and PUT /trip/{Id}/status to update a trip status. Use POST /trip/{Id}/rating to rate a trip.")]
        [Route("/trip/status/{TripId}", "GET, PUT")]
        [Route("/trip/rating/{TripId}", "POST")]
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
            public double? Price { get; set; }
            public double? Distance { get; set; }
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
                            Status = response.status,
                            Price = response.price,
                            Distance = response.distance
                        };
                    }

                    return new TripResponse
                    {
                        Result = "Failed",
                        ResultCode = response.result
                    };

                }

                string msg = "GET /trip/status called with invalid access token, ip: " + Request.RemoteIp +
                             ", Response = Authentication failed";
                Logger.Log(msg);
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

                string msg = "PUT /trip/status called with invalid access token, ip: " + Request.RemoteIp +
                             ", Response = Authentication failed";
                Logger.Log(msg);
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

        [Api("GET latest log entries")]
        [Route("/log", "GET")]
        public class Log : IReturn<LogResponse>
        {

        }

        public class LogResponse
        {
            public string Result { get; set; }
            public Gateway.Result ResultCode { get; set; }
            public List<Tuple<DateTime, int, string>> LogList { get; set; }
        }

        public class LogService : Service
        {
            public LogResponse Get(Log request)
            {
                return new LogResponse
                {
                    Result = "OK",
                    ResultCode = Gateway.Result.OK,
                    LogList = Logger.LogQueue.Queue.ToList()
                };
            }
        }

        [Api("Use GET /stats")]
        [Route("/stats", "GET")]
        public class Stats : IReturn<StatsResponse>
        {

        }

        public class StatsResponse
        {
            public string Result { get; set; }
            public Gateway.Result ResultCode { get; set; }
            public long ActiveTrips { get; set; }
            public long RejectsAllTime { get; set; }
            public long RejectsLast24Hrs { get; set; }
            public long RejectsLastHour { get; set; }
            public long CancelsAllTime { get; set; }
            public long CancelsLast24Hrs { get; set; }
            public long CancelsLastHour { get; set; }
            public long RequestsAllTime { get; set; }
            public long RequestsLast24Hrs { get; set; }
            public long RequestsLastHour { get; set; }
            public double ExceptionsAllTime { get; set; }
            public double ExceptionsLast24Hrs { get; set; }
            public double ExceptionsLastHour { get; set; }
            public long TripsAllTime { get; set; }
            public double DistanceAllTime { get; set; }
            public double FareAllTime { get; set; }
            public long TripsLast24Hrs { get; set; }
            public double DistanceLast24Hrs { get; set; }
            public double FareLast24Hrs { get; set; }
            public long TripsLastHour { get; set; }
            public double DistanceLastHour { get; set; }
            public double FareLastHour { get; set; }
        }

        public class StatsService : Service
        {
            public StatsResponse Get(Stats request)
            {
                var accessToken = this.Request.QueryString.Get("access_token");
                if (accessToken.Equals(TripThruAccessToken))
                {
                    var response = TripThruPartner.getGatewayStats.Get(new Gateway.GetGatewayStats.Request(
                        TripThruId
                        ));

                    if (response.result == Gateway.Result.OK)
                    {
                        return new StatsResponse
                        {
                            Result = "OK",
                            ResultCode = response.result,
                            ActiveTrips = response.activeTrips,
                            CancelsAllTime = response.cancelsAllTime,
                            CancelsLast24Hrs = response.cancelsLast24Hrs,
                            CancelsLastHour = response.cancelsLastHour,
                            DistanceAllTime = response.distanceAllTime,
                            DistanceLast24Hrs = response.distanceLast24Hrs,
                            DistanceLastHour = response.distanceLastHour,
                            ExceptionsAllTime = response.exceptionsAllTime,
                            ExceptionsLast24Hrs = response.exceptionsLast24Hrs,
                            ExceptionsLastHour = response.exceptionsLastHour,
                            FareAllTime = response.fareAllTime,
                            FareLast24Hrs = response.fareLast24Hrs,
                            FareLastHour = response.fareLastHour,
                            RejectsAllTime = response.rejectsAllTime,
                            RejectsLast24Hrs = response.rejectsLast24Hrs,
                            RejectsLastHour = response.rejectsLastHour,
                            RequestsAllTime = response.requestsAllTime,
                            RequestsLast24Hrs = response.requestsLast24Hrs,
                            RequestsLastHour = response.requestsLastHour,
                            TripsAllTime = response.tripsAllTime,
                            TripsLast24Hrs = response.tripsLast24Hrs,
                            TripsLastHour = response.tripsLastHour
                        };
                    }

                    return new StatsResponse
                    {
                        Result = "Failed",
                        ResultCode = response.result
                    };

                }

                string msg = "GET /stats called with invalid access token, ip: " + Request.RemoteIp +
                             ", Response = Authentication failed";
                Logger.Log(msg);
                return new StatsResponse
                {
                    Result = "Failed",
                    ResultCode = Gateway.Result.AuthenticationError
                };
            }
        }

        [Api("Use GET /trips")]
        [Route("/trips", "GET")]
        public class Trips : IReturn<TripsResponse>
        {
            public Status? Status { get; set; }
        }

        public class TripsResponse
        {
            public string Result { get; set; }
            public Gateway.Result ResultCode { get; set; }
            public List<string> TripsIDs { get; set; }
        }

        public class TripsService : Service
        {
            public TripsResponse Get(Trips request)
            {
                var accessToken = this.Request.QueryString.Get("access_token");
                if (accessToken.Equals(TripThruAccessToken))
                {
                    var response = TripThruPartner.getTrips.Get(new Gateway.GetTrips.Request(
                        TripThruId,
                        request.Status
                        ));

                    if (response.result == Gateway.Result.OK)
                    {
                        return new TripsResponse
                        {
                            Result = "OK",
                            ResultCode = response.result,
                            TripsIDs = response.tripIDs
                        };
                    }

                    return new TripsResponse
                    {
                        Result = "Failed",
                        ResultCode = response.result
                    };

                }

                string msg = "GET /trips called with invalid access token, ip: " + Request.RemoteIp +
                             ", Response = Authentication failed";
                Logger.Log(msg);
                return new TripsResponse
                {
                    Result = "Failed",
                    ResultCode = Gateway.Result.AuthenticationError
                };
            }
        }
    }
}