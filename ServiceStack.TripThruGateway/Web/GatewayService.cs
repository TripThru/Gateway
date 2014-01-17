using System;
using System.Linq;
using ServiceStack.Common;
using ServiceStack.TripThruGateway.TripThru;
using EnumerableExtensions = ServiceStack.Common.Extensions.EnumerableExtensions;

namespace ServiceStack.TripThruGateway
{
    using System.Collections.Generic;
    using ServiceStack.OrmLite;
    using ServiceStack.ServiceHost;
    using ServiceStack.ServiceInterface;

    public class GatewayService
    {
        public static TripThru.TripThru TripThru; //gets initialized in InitPartners
        
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
                var accessToken = this.Request.QueryString.Get("access_token");
                var u = Db.Select<User>(x => x.AccessToken == accessToken);
                if (u.Count > 0 && !request.CallbackUrl.IsNullOrEmpty() && !request.Name.IsNullOrEmpty())
                {
                    var p = Db.Select<Partner>(x => x.UserId == u.First().Id);
                    if (p.Count == 0)
                    {
                        Db.Insert(new Partner
                        {
                            UserId = u.First().Id,
                            Name = request.Name,
                            CallbackUrl = request.CallbackUrl + "/gateway/v1/",
                        });

                        TripThru.AddPartner(new TripThru.TripThru.Partner(
                            request.Name,
                            request.CallbackUrl,
                            u.First().ClientId,
                            "klmaspdo1p2om" //We need to authenticate with the partner
                            ));

                        return new PartnerResponse
                        {
                            Result = "OK",
                            ResultCode = Gateway.Result.OK,
                            Id = Db.GetLastInsertId()
                        };
                    }

                    return new PartnerResponse
                    {
                        Result = "Failed",
                        ResultCode = Gateway.Result.Rejected
                    };
                }

                string msg = "POST /partner called with invalid access token, ip: " + Request.RemoteIp +
                             ", Response = Authentication failed";
                Logger.Log(msg);
                return new PartnerResponse
                {
                    Result = "Failed",
                    ResultCode = Gateway.Result.AuthenticationError
                };
            }

            public PartnerResponse Get(PartnerRequest request)
            {
                var accessToken = this.Request.QueryString.Get("access_token");
                var u = Db.Select<User>(x => x.AccessToken == accessToken);
                return new PartnerResponse
                {
                    Result = "InvalidParameters",
                    ResultCode = Gateway.Result.InvalidParameters
                };
            }

            //public PartnerResponse Put(PartnerRequest request)
            //{
            //    var accessToken = this.Request.QueryString.Get("access_token");
            //    var u = Db.Select<User>(x => x.AccessToken == accessToken);
            //    return new PartnerResponse
            //    {
            //        Result = "InvalidParameters",
            //        ResultCode = Gateway.Result.InvalidParameters
            //    };
            //}

            //public PartnerResponse Delete(PartnerRequest request)
            //{
            //    var accessToken = this.Request.QueryString.Get("access_token");
            //    var u = Db.Select<User>(x => x.AccessToken == accessToken);
            //    return new PartnerResponse
            //    {
            //        Result = "InvalidParameters",
            //        ResultCode = Gateway.Result.InvalidParameters
            //    };
            //}

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
                var u = Db.Select<User>(x => x.AccessToken == accessToken);
                if (u.Count > 0)
                {
                    var user = u.First();
                    var response = TripThru.getPartnerInfo.Get(new Gateway.GetPartnerInfo.Request(
                            user.ClientId
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
                var u = Db.Select<User>(x => x.AccessToken == accessToken);
                if (u.Count > 0)
                {
                    var user = u.First();
                    var response = TripThru.getPartnerInfo.Get(new Gateway.GetPartnerInfo.Request(
                            user.ClientId
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

        [Api("Use POST to create search for quotes meeting the filter criteria.")]
        [Route("/quotes", "POST")]
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
                var u = Db.Select<User>(x => x.AccessToken == accessToken);
                if (u.Count > 0)
                {
                    var user = u.First();
                    var response = TripThru.quoteTrip.Get(new Gateway.QuoteTrip.Request(
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

        }

        [Api("Use POST to dispatch a trip to a fleet. Can be used in conjuction with /quotes")]
        [Route("/dispatch", "POST")]
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
                var u = Db.Select<User>(x => x.AccessToken == accessToken);
                if (u.Count > 0)
                {
                    var user = u.First();
                    var response = TripThru.dispatchTrip.Post(new Gateway.DispatchTrip.Request(
                        user.ClientId,
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

                string msg = "POST /dispatch called with invalid access token, ip: " + Request.RemoteIp +
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
        }

        public class TripService : Service
        {
            public TripResponse Get(Trip request)
            {
                var accessToken = this.Request.QueryString.Get("access_token");
                var u = Db.Select<User>(x => x.AccessToken == accessToken);
                if (u.Count > 0)
                {
                    var user = u.First();
                    var response = TripThru.getTripStatus.Get(new Gateway.GetTripStatus.Request(
                        user.ClientId,
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
                var u = Db.Select<User>(x => x.AccessToken == accessToken);
                if (u.Count > 0)
                {
                    var user = u.First();
                    var response = TripThru.updateTripStatus.Post(new Gateway.UpdateTripStatus.Request(
                        user.ClientId,
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
    }
}