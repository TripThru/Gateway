using System;
using System.Linq;
using ServiceStack.Common;
using ServiceStack.Messaging.Rcon;
using EnumerableExtensions = ServiceStack.Common.Extensions.EnumerableExtensions;
using Utils;
using TripThruCore;

namespace ServiceStack.TripThruGateway
{
    using System.Collections.Generic;
    using ServiceStack.OrmLite;
    using ServiceStack.ServiceHost;
    using ServiceStack.ServiceInterface;

    public class GatewayService
    {
        public static Gateway gateway = null; //gets initialized in InitPartners



        [Api("GET latest log entries")]
        [Route("/log", "GET")]
        public class Log : IReturn<LogResponse>
        {

        }

        public class LogResponse
        {
            public string Result { get; set; }
            public Gateway.Result ResultCode { get; set; }
            public List<Logger.RequestLog> LogList { get; set; }
        }

        public class LogService : Service
        {
            public LogResponse Get(Log request)
            {
                var l = Logger.Queue.ToList();
                return new LogResponse
                {
                    Result = "OK",
                    ResultCode = Gateway.Result.OK,
                    LogList = Logger.Queue.ToList()
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
                var response = gateway.GetGatewayStats(new Gateway.GetGatewayStatsRequest());

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
        }
        
        [Api("Use POST to create a new Partner, GET to retrieve it and PUT to update name or callback url.")]
        [Route("/partner", "GET, PUT, POST, DELETE")]
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

        public class PartnerService : Service
        {
            public PartnerResponse Post(PartnerRequest request)
            {
                PartnerResponse partnerResponse;
                var accessToken = this.Request.QueryString.Get("access_token");
                var u = Db.Select<User>(x => x.AccessToken == accessToken);
                if (u.Count > 0 && !request.CallbackUrl.IsNullOrEmpty() && !request.Name.IsNullOrEmpty())
                {
                    Logger.BeginRequest("RegisterPartner received from " + u.First().UserName, request);
                    var p = Db.Select<Partner>(x => x.UserId == u.First().Id);
                    if (p.Count == 0)
                    {
                        Db.Insert(new Partner
                        {
                            UserId = u.First().Id,
                            Name = request.Name,
                            CallbackUrl = request.CallbackUrl,
                        });

                        gateway.RegisterPartner(new GatewayClient(u.First().ClientId, request.Name, "jaosid1201231", request.CallbackUrl));

                        partnerResponse = new PartnerResponse
                        {
                            Result = "OK",
                            ResultCode = Gateway.Result.OK,
                            Id = Db.GetLastInsertId()
                        };
                    }
                    else
                    {
                        partnerResponse = new PartnerResponse
                        {
                            Result = "Failed",
                            ResultCode = Gateway.Result.Rejected
                        };
                    }
                }
                else
                {
                    Logger.BeginRequest("RegisterPartner received from unknown user", request);
                    string msg = "POST /partner called with invalid access token, ip: " + Request.RemoteIp +
                                 ", Response = Authentication failed";
                    Logger.Log(msg);
                    partnerResponse = new PartnerResponse
                    {
                        Result = "Failed",
                        ResultCode = Gateway.Result.AuthenticationError
                    };
                }
                Logger.Log("RequestType=RegisterPartner");
                Logger.EndRequest(partnerResponse);
                return partnerResponse;
            }

            public PartnerResponse Get(PartnerRequest request)
            {
                PartnerResponse partnerResponse;
                var accessToken = this.Request.QueryString.Get("access_token");
                var u = Db.Select<User>(x => x.AccessToken == accessToken);
                if (u.Count > 0 && !request.CallbackUrl.IsNullOrEmpty() && !request.Name.IsNullOrEmpty())
                {
                    Logger.BeginRequest("RegisterPartner received from " + u.First().UserName, request);
                    var p = Db.Select<Partner>(x => x.UserId == u.First().Id);
                    if (p.Count == 0)
                    {
                        Db.Insert(new Partner
                        {
                            UserId = u.First().Id,
                            Name = request.Name,
                            CallbackUrl = request.CallbackUrl,
                        });

                        gateway.RegisterPartner(new GatewayClient(u.First().ClientId, request.Name, "jaosid1201231", request.CallbackUrl));

                        partnerResponse = new PartnerResponse
                        {
                            Result = "OK",
                            ResultCode = Gateway.Result.OK,
                            Id = Db.GetLastInsertId()
                        };
                    }
                    else
                    {
                        partnerResponse = new PartnerResponse
                        {
                            Result = "Failed",
                            ResultCode = Gateway.Result.Rejected
                        };
                    }
                }
                else
                {
                    Logger.BeginRequest("RegisterPartner received from unknown user", request);
                    string msg = "GET /partner called with invalid access token, ip: " + Request.RemoteIp +
                                 ", Response = Authentication failed";
                    Logger.Log(msg);
                    partnerResponse = new PartnerResponse
                    {
                        Result = "Failed",
                        ResultCode = Gateway.Result.AuthenticationError
                    };
                }
                Logger.Log("RequestType=RegisterPartner");
                Logger.EndRequest(partnerResponse);
                return partnerResponse;
            }
        }

        [Api("Use GET to get a list of partners or POST to create search for partners meeting the filter criteria.")]
        [Route("/partners", "POST, GET")]
        public class Partners : IReturn<PartnersResponse>
        {
            public List<Zone> Coverage { get; set; }
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
                PartnersResponse partnersResponse;
                var accessToken = this.Request.QueryString.Get("access_token");
                var u = Db.Select<User>(x => x.AccessToken == accessToken);
                var clientId = "none";
                try
                {
                    if (u.Count > 0)
                    {
                        var user = u.First();
                        clientId = user.ClientId;
                        Logger.BeginRequest("GetPartnerInfo received from " + user.UserName, request);
                        var response = gateway.GetPartnerInfo(new Gateway.GetPartnerInfoRequest(
                            user.ClientId
                            ));

                        if (response.result == Gateway.Result.OK)
                        {
                            partnersResponse = new PartnersResponse
                            {
                                Result = "OK",
                                ResultCode = Gateway.Result.OK,
                                Fleets = response.fleets,
                                VehicleTypes = response.vehicleTypes
                            };
                        }
                        else
                        {
                            partnersResponse = new PartnersResponse
                            {
                                Result = "Failed",
                                ResultCode = response.result
                            };
                        }
                    }
                    else
                    {
                        Logger.BeginRequest("GetPartnerInfo received from unknown user", request);
                        string msg = "POST /partners called with invalid access token, ip: " + Request.RemoteIp +
                                     ", Response = Authentication failed";
                        Logger.Log(msg);
                        partnersResponse = new PartnersResponse
                        {
                            Result = "Failed",
                            ResultCode = Gateway.Result.AuthenticationError
                        };
                    }
                }
                catch (Exception e)
                {
                    Logger.LogDebug("GetPartnerInfo="+e.Message, e.StackTrace);
                    Logger.Log("Exception=" + e.Message);
                    partnersResponse = new PartnersResponse
                    {
                        Result = "Failed",
                        ResultCode = Gateway.Result.UnknownError
                    };
                }
                Logger.Log("RequestType=GetPartnerInfo");
                Logger.Log("ClientId="+clientId);
                Logger.EndRequest(partnersResponse);
                return partnersResponse;
            }

            public PartnersResponse Get(Partners request)
            {
                PartnersResponse partnersResponse;
                var accessToken = this.Request.QueryString.Get("access_token");
                var u = Db.Select<User>(x => x.AccessToken == accessToken);
                var clientId = "none";
                try
                {
                    if (u.Count > 0)
                    {
                        var user = u.First();
                        clientId = user.ClientId;
                        Logger.BeginRequest("GetPartnerInfo received from " + user.UserName, request);
                        var response = gateway.GetPartnerInfo(new Gateway.GetPartnerInfoRequest(
                            user.ClientId
                            ));

                        if (response.result == Gateway.Result.OK)
                        {
                            partnersResponse = new PartnersResponse
                            {
                                Result = "OK",
                                ResultCode = Gateway.Result.OK,
                                Fleets = response.fleets,
                                VehicleTypes = response.vehicleTypes
                            };
                        }
                        else
                        {
                            partnersResponse = new PartnersResponse
                            {
                                Result = "Failed",
                                ResultCode = response.result
                            };
                        }
                    }
                    else
                    {
                        Logger.BeginRequest("GetPartnerInfo received from unknown user", request);
                        string msg = "POST /partners called with invalid access token, ip: " + Request.RemoteIp +
                                     ", Response = Authentication failed";
                        Logger.Log(msg);
                        partnersResponse = new PartnersResponse
                        {
                            Result = "Failed",
                            ResultCode = Gateway.Result.AuthenticationError
                        };
                    }
                }
                catch (Exception e)
                {
                    Logger.LogDebug("GetPartnerInfo=" + e.Message, e.StackTrace);
                    Logger.Log("Exception=" + e.Message);
                    partnersResponse = new PartnersResponse
                    {
                        Result = "Failed",
                        ResultCode = Gateway.Result.UnknownError
                    };
                }
                Logger.Log("RequestType=GetPartnerInfo");
                Logger.Log("ClientId=" + clientId);
                Logger.EndRequest(partnersResponse);
                return partnersResponse;
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
                QuotesResponse quotesResponse;
                var accessToken = this.Request.QueryString.Get("access_token");
                var u = Db.Select<User>(x => x.AccessToken == accessToken);
                var clientId = "none";
                try
                {
                    if (u.Count > 0)
                    {
                        var user = u.First();
                        clientId = user.ClientId;
                        Logger.BeginRequest("QuoteTrip received from " + user.UserName, request);
                        var response = gateway.QuoteTrip(new Gateway.QuoteTripRequest(
                            user.ClientId,
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
                            quotesResponse = new QuotesResponse
                            {
                                Count = response.quotes.Count,
                                Quotes = response.quotes,
                                ResultCode = response.result,
                                Result = "OK"
                            };
                        }
                        else
                        {
                            return new QuotesResponse
                            {
                                Result = "Failed",
                                ResultCode = response.result
                            };
                        }
                    }
                    else
                    {
                        Logger.BeginRequest("QuoteTrip received from unknown user", request);
                        string msg = "POST /quotes called with invalid access token, ip: " + Request.RemoteIp +
                                     ", Response = Authentication failed";
                        Logger.Log(msg);
                        quotesResponse = new QuotesResponse
                        {
                            Result = "Failed",
                            ResultCode = Gateway.Result.AuthenticationError
                        };
                    }
                }
                catch (Exception e)
                {
                    Logger.LogDebug("QuoteTrip=" + e.Message, e.StackTrace);
                    Logger.Log("Exception="+e.Message);
                    quotesResponse = new QuotesResponse
                    {
                        Result = "Failed",
                        ResultCode = Gateway.Result.UnknownError
                    };
                }
                Logger.Log("RequestType=QuoteTrip");
                Logger.Log("ClientId="+clientId);
                Logger.EndRequest(quotesResponse);
                return quotesResponse;
            }

            public QuotesResponse Get(Quotes request)
            {
                QuotesResponse quotesResponse;
                var accessToken = this.Request.QueryString.Get("access_token");
                var u = Db.Select<User>(x => x.AccessToken == accessToken);
                var clientId = "none";
                try
                {
                    if (u.Count > 0)
                    {
                        var user = u.First();
                        clientId = user.ClientId;
                        Logger.BeginRequest("QuoteTrip received from " + user.UserName, request);
                        var response = gateway.QuoteTrip(new Gateway.QuoteTripRequest(
                            user.ClientId,
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
                            quotesResponse = new QuotesResponse
                            {
                                Count = response.quotes.Count,
                                Quotes = response.quotes,
                                ResultCode = response.result,
                                Result = "OK"
                            };
                        }
                        else
                        {
                            return new QuotesResponse
                            {
                                Result = "Failed",
                                ResultCode = response.result
                            };
                        }
                    }
                    else
                    {
                        Logger.BeginRequest("QuoteTrip received from unknown user", request);
                        string msg = "POST /quotes called with invalid access token, ip: " + Request.RemoteIp +
                                     ", Response = Authentication failed";
                        Logger.Log(msg);
                        quotesResponse = new QuotesResponse
                        {
                            Result = "Failed",
                            ResultCode = Gateway.Result.AuthenticationError
                        };
                    }
                }
                catch (Exception e)
                {
                    Logger.LogDebug("QuoteTrip=" + e.Message, e.StackTrace);
                    Logger.Log("Exception=" + e.Message);
                    quotesResponse = new QuotesResponse
                    {
                        Result = "Failed",
                        ResultCode = Gateway.Result.UnknownError
                    };
                }
                Logger.Log("RequestType=QuoteTrip");
                Logger.Log("ClientId=" + clientId);
                Logger.EndRequest(quotesResponse);
                return quotesResponse;
            }
        }

        [Api("Use POST or GET to dispatch a trip to a fleet. Can be used in conjuction with /quotes")]
        [Route("/dispatch", "POST, GET")]
        public class Dispatch : IReturn<DispatchResponse>
        {
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
        }

        public class DispatchService : Service
        {
            public DispatchResponse Post(Dispatch request)
            {
                DispatchResponse dispatchResponse;
                var accessToken = this.Request.QueryString.Get("access_token");
                var u = Db.Select<User>(x => x.AccessToken == accessToken);
                var clientId = "none";
                try
                {
                    if (u.Count > 0)
                    {
                        var user = u.First();
                        clientId = user.ClientId;
                        Logger.BeginRequest("DispatchTrip received from " + user.UserName, request);
                        var response = gateway.DispatchTrip(new Gateway.DispatchTripRequest(
                            user.ClientId,
                            request.TripId,
                            request.PickupLocation,
                            request.PickupTime,
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
                            dispatchResponse = new DispatchResponse
                            {
                                Result = "OK",
                                ResultCode = response.result
                            };
                        }
                        else
                        {
                            dispatchResponse = new DispatchResponse
                            {
                                Result = "Failed",
                                ResultCode = response.result
                            };
                        }
                    }
                    else
                    {
                        Logger.BeginRequest("DispatchTrip received from unknown user", request);
                        string msg = "POST /dispatch called with invalid access token, ip: " + Request.RemoteIp +
                                     ", Response = Authentication failed";
                        Logger.Log(msg);
                        dispatchResponse = new DispatchResponse
                        {
                            Result = "Failed",
                            ResultCode = Gateway.Result.AuthenticationError
                        };
                    }
                }
                catch (Exception e)
                {
                    Logger.LogDebug("DispatchTrip=" + e.Message, e.StackTrace);
                    Logger.Log("Exception=" + e.Message);
                    dispatchResponse = new DispatchResponse
                    {
                        Result = "Failed",
                        ResultCode = Gateway.Result.UnknownError
                    };
                }
                Logger.Log("RequestType=DispatchTrip");
                Logger.Log("ClientId="+clientId);
                Logger.EndRequest(dispatchResponse);
                return dispatchResponse;
            }

            public DispatchResponse Get(Dispatch request)
            {
                DispatchResponse dispatchResponse;
                var accessToken = this.Request.QueryString.Get("access_token");
                var u = Db.Select<User>(x => x.AccessToken == accessToken);
                var clientId = "none";
                try
                {
                    if (u.Count > 0)
                    {
                        var user = u.First();
                        clientId = user.ClientId;
                        Logger.BeginRequest("DispatchTrip received from " + user.UserName, request);
                        var response = gateway.DispatchTrip(new Gateway.DispatchTripRequest(
                            user.ClientId,
                            request.TripId,
                            request.PickupLocation,
                            request.PickupTime,
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
                            dispatchResponse = new DispatchResponse
                            {
                                Result = "OK",
                                ResultCode = response.result
                            };
                        }
                        else
                        {
                            dispatchResponse = new DispatchResponse
                            {
                                Result = "Failed",
                                ResultCode = response.result
                            };
                        }
                    }
                    else
                    {
                        Logger.BeginRequest("DispatchTrip received from unknown user", request);
                        string msg = "POST /dispatch called with invalid access token, ip: " + Request.RemoteIp +
                                     ", Response = Authentication failed";
                        Logger.Log(msg);
                        dispatchResponse = new DispatchResponse
                        {
                            Result = "Failed",
                            ResultCode = Gateway.Result.AuthenticationError
                        };
                    }
                }
                catch (Exception e)
                {
                    Logger.LogDebug("DispatchTrip=" + e.Message, e.StackTrace);
                    Logger.Log("Exception=" + e.Message);
                    dispatchResponse = new DispatchResponse
                    {
                        Result = "Failed",
                        ResultCode = Gateway.Result.UnknownError
                    };
                }
                Logger.Log("RequestType=DispatchTrip");
                Logger.Log("ClientId=" + clientId);
                Logger.EndRequest(dispatchResponse);
                return dispatchResponse;
            }
        }

        [Api("Use GET /trip/{Id}/status for trip status and PUT /trip/{Id}/status to update a trip status. Use POST /trip/{Id}/rating to rate a trip.")]
        [Route("/trip/status/{TripId}", "GET, PUT")]
        [Route("/trip/rating/{TripId}", "POST")]
        public class TripRequest : IReturn<TripResponse>
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
            public string PassengerName { get; set; }
            public Location DriverLocation { get; set; }
            public DateTime? PickupTime { get; set; }
            public DateTime? DropoffTime { get; set; }
            public VehicleType? VehicleType { get; set; }
            public Status? Status { get; set; }
            public DateTime? ETA { get; set; } // in minutes;
            public double? Price { get; set; }
            public double? Distance { get; set; }
            public Location PickupLocation { get; set; }
            public Location DropoffLocation { get; set; }
            public string OriginatingPartnerName { get; set; }
            public string ServicingPartnerName { get; set; }
        }

        public class TripService : Service
        {
            public TripResponse Get(TripRequest request)
            {
                Logger.BeginRequest("GetTripStatus received", request);
                TripResponse tripResponse;
                var accessToken = this.Request.QueryString.Get("access_token");
                var u = Db.Select<User>(x => x.AccessToken == accessToken);
                var clientId = "none";
                try
                {
                    if (u.Count > 0)
                    {
                        var user = u.First();
                        clientId = user.ClientId;
                        Logger.BeginRequest("GetTripStatus received from " + user.UserName, request);
                        var response = gateway.GetTripStatus(new Gateway.GetTripStatusRequest(
                            user.ClientId,
                            request.TripId
                            ));

                        if (response.result == Gateway.Result.OK)
                        {
                            tripResponse = new TripResponse
                            {
                                PassengerName = response.passengerName,
                                DriverId = response.driverID,
                                DriverLocation = response.driverLocation,
                                DriverName = response.driverName,
                                DropoffTime = response.dropoffTime,
                                DropoffLocation = response.dropoffLocation,
                                ETA = response.ETA,
                                FleetId = response.fleetID,
                                FleetName = response.fleetName,
                                PartnerId = response.partnerID,
                                PartnerName = response.partnerName,
                                PickupTime = response.pickupTime,
                                PickupLocation = response.pickupLocation,
                                VehicleType = response.vehicleType,
                                Result = "OK",
                                ResultCode = response.result,
                                Status = response.status,
                                Price = response.price,
                                Distance = response.distance,
                                OriginatingPartnerName = response.originatingPartnerName,
                                ServicingPartnerName = response.servicingPartnerName
                            };
                        }
                        else
                        {
                            tripResponse = new TripResponse
                            {
                                Result = "Failed",
                                ResultCode = response.result
                            };
                        }
                    }
                    else
                    {
                        Logger.BeginRequest("GetTripStatus received from unknown user", request);
                        string msg = "GET /trip/status called with invalid access token, ip: " + Request.RemoteIp +
                                     ", Response = Authentication failed";
                        Logger.Log(msg);
                        tripResponse = new TripResponse
                        {
                            Result = "Failed",
                            ResultCode = Gateway.Result.AuthenticationError
                        };
                    }
                }
                catch (Exception e)
                {
                    Logger.LogDebug("GetTripStatus=" + e.Message, e.StackTrace);
                    Logger.Log("Exception=" + e.Message);
                    tripResponse = new TripResponse
                    {
                        Result = "Failed",
                        ResultCode = Gateway.Result.UnknownError
                    };
                }
                Logger.Log("RequestType=GetTripStatus");
                Logger.Log("ClientId="+clientId);
                Logger.EndRequest(tripResponse);
                return tripResponse;
            }

            public TripResponse Put(TripRequest request)
            {
                TripResponse tripResponse;
                var accessToken = this.Request.QueryString.Get("access_token");
                var u = Db.Select<User>(x => x.AccessToken == accessToken);
                var clientId = "none";
                try
                {
                    if (u.Count > 0)
                    {
                        var user = u.First();
                        Logger.BeginRequest("UpdateTripStatus received from " + user.UserName, request);
                        clientId = user.ClientId;
                        var response = gateway.UpdateTripStatus(new Gateway.UpdateTripStatusRequest(
                            user.ClientId,
                            request.TripId,
                            request.Status
                            ));

                        if (response.result == Gateway.Result.OK)
                        {
                            tripResponse = new TripResponse
                            {
                                Result = "OK",
                                ResultCode = response.result
                            };
                        }
                        else
                        {

                            tripResponse = new TripResponse
                            {
                                Result = "Failed",
                                ResultCode = response.result
                            };
                        }
                    }
                    else
                    {
                        Logger.BeginRequest("UpdateTripStatus received from unknown user", request);
                        string msg = "PUT /trip/status called with invalid access token, ip: " + Request.RemoteIp +
                                     ", Response = Authentication failed";
                        Logger.Log(msg);
                        tripResponse = new TripResponse
                        {
                            Result = "Failed",
                            ResultCode = Gateway.Result.AuthenticationError
                        };
                    }
                }
                catch (Exception e)
                {
                    Logger.LogDebug("UpdateTripStatus=" + e.Message, e.StackTrace);
                    Logger.Log("Exception=" + e.Message);
                    tripResponse = new TripResponse
                    {
                        Result = "Failed",
                        ResultCode = Gateway.Result.UnknownError
                    };
                }
                Logger.Log("RequestType=UpdateTripStatus");
                Logger.Log("ClientId=" + clientId);
                Logger.EndRequest(tripResponse);
                return tripResponse;
            }
        }

        [Api("Use GET /trips")]
        [Route("/trips", "GET")]
        public class Trips : IReturn<TripsResponse>
        {
            public Status Status { get; set; }
        }

        public class TripsResponse
        {
            public string Result { get; set; }
            public Gateway.Result ResultCode { get; set; }
            public List<Trip> Trips { get; set; }
        }

        public class TripsService : Service
        {
            public TripsResponse Get(Trips request)
            {
                Logger.BeginRequest("GetTrips received", request);
                TripsResponse tripResponse;
                try
                {
                    var response = gateway.GetTrips(new Gateway.GetTripsRequest(null, null));

                    if (response.result == Gateway.Result.OK)
                    {
                        tripResponse = new TripsResponse
                        {
                            Result = "OK",
                            ResultCode = response.result,
                            Trips = response.trips
                        };
                    }
                    else
                    {
                        tripResponse = new TripsResponse
                        {
                            Result = "Failed",
                            ResultCode = response.result
                        };
                    }
                }
                catch (Exception e)
                {

                    Logger.LogDebug("GetTrips=" + e.Message, e.StackTrace);
                    Logger.Log("Exception=" + e.Message);
                    tripResponse = new TripsResponse
                    {
                        Result = "Failed",
                        ResultCode = Gateway.Result.UnknownError
                    };
                }
                Logger.Log("RequestType=GetTrips");
                Logger.EndRequest(tripResponse);
                return tripResponse;
            }
        }

    }
}