﻿using System;
using System.ComponentModel.DataAnnotations;
using System.Data.Odbc;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using MongoDB.Bson;
using ServiceStack.Common;
using ServiceStack.Html;
using Utils;
using TripThruCore;
using TripThruCore.Storage;


namespace ServiceStack.TripThruGateway
{
    using System.Collections.Generic;
    using ServiceStack.ServiceHost;
    using ServiceStack.ServiceInterface;

    public class GatewayService
    {
        public static Gateway gateway; //gets initialized in InitPartners

        #region Log

        [Api("GET latest log entries")]
        [Route("/log", "GET")]
        [Restrict(VisibilityTo = EndpointAttributes.None)]
        public class Log : IReturn<LogResponse>
        {
            public string access_token { get; set; }
            public string tripID { get; set; }
        }

        public class LogResponse
        {
            public string Result { get; set; }
            public Gateway.Result ResultCode { get; set; }
            public string Message { get; set; }
            public List<Logger.RequestLog> LogList { get; set; }
        }

        public class LogService : Service
        {
            public LogResponse Get(Log request)
            {
                LogResponse logResponse = new LogResponse
                {
                    Result = "Unknown",
                    ResultCode = Gateway.Result.OK,
                    LogList = new List<Logger.RequestLog>()
                };
                var accessToken = request.access_token;
                request.access_token = null;
                PartnerAccount acct = gateway.GetPartnerAccountByAccessToken(accessToken);
                PartnerAccount user = StorageManager.GetPartnerAccountByAccessToken(accessToken);
                try
                {
                    if (!accessToken.IsNullOrEmpty() && acct != null || (user != null && user.Role == Storage.UserRole.admin)){
                        if(acct == null)
                            acct = user;
                        IEnumerable<Logger.RequestLog> logList = Logger.Queue;

                        if (acct.Role != Storage.UserRole.admin)
                            logList = logList.Where(log => log.originID == acct.ClientId ||
                                                         log.destinationID == acct.ClientId);
                        if (request.tripID != null)
                            logList = logList.Where(log => log.tripID == request.tripID);
                        
                        logList = logList.OrderBy(log => log.Time);

                        logResponse = new LogResponse
                        {
                            Result = "OK",
                            ResultCode = Gateway.Result.OK,
                            LogList = logList.ToList()
                        };
                    }
                    else
                    {
                        logResponse = new LogResponse
                        {
                            Result = "Failed",
                            ResultCode = Gateway.Result.AuthenticationError
                        };
                    }
                }
                catch (Exception e)
                {
                    Logger.LogDebug("GetLog = " + e.Message, e.ToString());
                }
                return logResponse;
            }
        }

        #endregion

        #region Stats

        [Api("Use GET /stats")]
        [Route("/stats", "GET")]
        [Restrict(VisibilityTo = EndpointAttributes.None)]
        public class Stats : IReturn<StatsResponse>
        {
            
        }

        public class StatsResponse
        {
            public string Result { get; set; }
            public Gateway.Result ResultCode { get; set; }
            public string Message { get; set; }
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

                StatsResponse statsResponse = new StatsResponse
                {
                    Result = "Unknown",
                    ResultCode = Gateway.Result.UnknownError
                };

                try
                {

                    var response = gateway.GetGatewayStats(new Gateway.GetGatewayStatsRequest());

                    if (response.result == Gateway.Result.OK)
                    {
                        statsResponse = new StatsResponse
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
                    else
                    {
                        statsResponse = new StatsResponse
                {
                    Result = "Failed",
                    ResultCode = response.result
                };
                    }
                }
                catch (Exception e)
                {
                    Logger.LogDebug("GetStats=" + e.Message, e.ToString());
                }

                return statsResponse;
            }
        }

        #endregion

        #region Partner

        [Api("Use POST to create a new Partner, GET to retrieve it and PUT to update name or callback url.")]
        [Route("/partner", "POST, OPTIONS", Summary = "Networks Service", Notes = "Register your network with TripThru")]
        public class PartnerRequest : IReturn<PartnerResponse>
        {
            [ApiMember(Name = "access_token", Description = "Access token acquired through OAuth2.0 authorization procedure.  Example: demo12345", ParameterType = "query", DataType = "string", IsRequired = true)]
            public string access_token { get; set; }
            [ApiMember(Name = "Name", Description = "The name of your fleet", ParameterType = "query", DataType = "string", IsRequired = true)]
            public string Name { get; set; }
            [ApiMember(Name = "CallbackUrl", Description = "This is the callback url where your support of our Gateway API is", ParameterType = "query", DataType = "string", IsRequired = true)]
            public string CallbackUrl { get; set; }
        }

        public class PartnerResponse
        {
            public string Result { get; set; }
            public Gateway.Result ResultCode { get; set; }
            public string Message { get; set; }
        }

        public class PartnerService : Service
        {
            public PartnerResponse Post(PartnerRequest request)
            {
                var accessToken = request.access_token;
                var message = ValidatePartner(request);
                request.access_token = null;
                PartnerResponse partnerResponse = new PartnerResponse
                {
                    Result = "Unknown",
                    ResultCode = Gateway.Result.AuthenticationError
                };
                PartnerAccount acct = gateway.GetPartnerAccountByAccessToken(accessToken);
                try
                {
                    if (acct != null && message == null)
                    {
                        Logger.BeginRequest("RegisterPartner received from " + acct.UserName, request);
                        acct.PartnerName = request.Name;
                        acct.CallbackUrl = request.CallbackUrl;
                        gateway.RegisterPartner(new GatewayClient(acct.ClientId, request.Name, request.CallbackUrl, acct.TripThruAccessToken));
                        StorageManager.RegisterPartner(acct, request.Name, request.CallbackUrl);

                        partnerResponse = new PartnerResponse
                        {
                            Result = "OK",
                            ResultCode = Gateway.Result.OK,
                            Message = "OK"
                        };
                    }
                    else
                    {
                        
                        string msg;
                        if (message == null)
                        {
                            Logger.BeginRequest("RegisterPartner received from unknown user", request);
                            msg = "POST /partner called with invalid access token, ip: " + Request.RemoteIp +
                                  ", Response = Authentication failed";
                            partnerResponse = new PartnerResponse
                            {
                                Result = "Failed",
                                ResultCode = Gateway.Result.AuthenticationError,
                                Message = "Failed"
                            };
                        }
                        else
                        {

                            Logger.BeginRequest("RegisterPartner received with wrong parameters", request);
                            msg = message;
                            partnerResponse.Message = message;
                        }
                        Logger.Log(msg);
                        
                    }
                }
                finally
                {
                    Logger.AddTag("RequestType", "RegisterPartner");
                    Logger.SetOriginatingId(acct.ClientId);
                    Logger.SetServicingId(gateway.ID);
                    Logger.EndRequest(partnerResponse);
                }
                return partnerResponse;
            }

            public PartnerResponse Options(PartnerRequest request)
            {
                return new PartnerResponse();
            }

            private string ValidatePartner(PartnerRequest request)
            {
                if (request.access_token.IsNullOrEmpty())
                    return "Access Token is Required";
                if (request.Name.IsNullOrEmpty())
                    return "Name is Required";
                if (request.CallbackUrl.IsNullOrEmpty())
                    return "CallbackUrl is Required";
                return null;
            }
        }

        #endregion

        #region Networks

        [Api("Use GET to get a list of partners or POST to create search for partners meeting the filter criteria.")]
        [Route("/networks", "GET,OPTIONS")]
        public class Networks : IReturn<NetworksResponse>
        {
            [ApiMember(Name = "access_token", Description = "Access token acquired through OAuth2.0 authorization procedure.  Example: demo12345", ParameterType = "query", DataType = "string", IsRequired = true)]
            public string access_token { get; set; }
        }

        public class NetworksResponse
        {
            public string Result { get; set; }
            public Gateway.Result ResultCode { get; set; }
            public string Message { get; set; }
            public List<Fleet> Fleets { get; set; }
            public List<VehicleType> VehicleTypes { get; set; }
        }

        public class NetworksService : Service
        {

            public NetworksResponse Get(Networks request)
            {
                var accessToken = request.access_token;
                var message = ValidatePartners(request);
                request.access_token = null;
                NetworksResponse networksResponse = new NetworksResponse
                {
                    Result = "Unknown",
                    ResultCode = Gateway.Result.UnknownError
                };
                var clientId = "none";
                PartnerAccount acct = gateway.GetPartnerAccountByAccessToken(accessToken);
                PartnerAccount user = StorageManager.GetPartnerAccountByAccessToken(accessToken);
                try
                {
                    if (accessToken != null && (acct != null || user != null))
                    {
                        if (acct == null)
                            acct = user;
                        clientId = acct.ClientId;
                        Logger.BeginRequest("GetPartnerInfo received from " + acct.UserName, request);
                        var response = gateway.GetPartnerInfo(new Gateway.GetPartnerInfoRequest(
                            acct.ClientId
                            ));

                        if (response.result == Gateway.Result.OK)
                        {
                            networksResponse = new NetworksResponse
                            {
                                Result = "OK",
                                ResultCode = Gateway.Result.OK,
                                Fleets = response.fleets,
                                VehicleTypes = response.vehicleTypes,
                                Message = "OK"
                            };
                        }
                        else
                        {
                            networksResponse = new NetworksResponse
                            {
                                Result = "Failed",
                                ResultCode = response.result,
                                Message = "Failed"
                            };
                        }
                    }
                    else
                    {
                        
                        string msg;
                        if (message == null)
                        {
                            Logger.BeginRequest("GetPartnerInfo received from unknown user", request);
                            msg = "GET /Networks called with invalid access token, ip: " + Request.RemoteIp +
                                  ", Response = Authentication failed";
                            networksResponse = new NetworksResponse
                            {
                                Result = "Failed",
                                ResultCode = Gateway.Result.AuthenticationError,
                                Message = "Failed"
                            };
                        }
                        else
                        {
                            Logger.BeginRequest("GetPartnerInfo received with wrong parameters", request);
                            msg = message;
                            networksResponse.Message = msg;
                        }
                        Logger.Log(msg);
                        
                    }
                }
                catch (Exception e)
                {
                    Logger.LogDebug("GetPartnerInfo=" + e.Message, e.ToString());
                    networksResponse = new NetworksResponse
                    {
                        Result = "Failed",
                        ResultCode = Gateway.Result.UnknownError
                    };
                }
                finally
                {
                    Logger.AddTag("RequestType", "GetPartnerInfo");
                    Logger.AddTag("ClientId", clientId);
                    if (acct != null) Logger.SetOriginatingId(acct.ClientId);
                    Logger.EndRequest(networksResponse);
                }
                return networksResponse;
            }

            public NetworksResponse Options(Networks request)
            {
                return new NetworksResponse();
            }

            private string ValidatePartners(Networks networks)
            {
                if (networks.access_token.IsNullOrEmpty())
                    return "Access Token is Required";
                return null;
            }
        }

        #endregion

        #region Quotes

        [Api(Description = "Use GET to get quotes for a possible trip.")]
        [Route("/quotes", Verbs = "GET, OPTIONS", Summary = @"get quotes for a possible trip", Notes = "The standard usage is to first get quotes for a planned trip and then dispatch the trip to your selected fleet and/or driver")]
        [Restrict(VisibilityTo = EndpointAttributes.None)]
        public class Quotes : IReturn<QuotesResponse>
        {
            [ApiMember(Name = "access_token", Description = "Access token acquired through OAuth2.0 authorization procedure.  Example: demo12345", ParameterType = "query", DataType = "string", IsRequired = true)]
            public string access_token { get; set; }
            [ApiMember(Name = "PickupTime", Description = "Time that the taxi should arrive. Format (yyyy-MM-ddTHH:mm:ss) GMT.  Example: 2014-02-25T23:30:00", ParameterType = "query", DataType = "DateTime", IsRequired = true)]
            public DateTime PickupTime { get; set; }
            [ApiMember(Name = "PickupLat", Description = "GPS coordinate latitude of where the passenger should be picked up. Example: 37.782551", ParameterType = "query", DataType = "double", IsRequired = true)]
            public double PickupLat { get; set; }
            [ApiMember(Name = "PickupLng", Description = "GPS coordinate longitude of where the passenger should be picked up. Example: -122.445368", ParameterType = "query", DataType = "double", IsRequired = true)]
            public double PickupLng { get; set; }
            [ApiMember(Name = "PassengerName", Description = "Name of passenger", ParameterType = "query", DataType = "string", IsRequired = false)]
            public string PassengerName { get; set; }
            [ApiAllowableValues("Luggage", "1", "2", "3", "4", "5", "6", "7")]
            [ApiMember(Name = "Luggage", Description = "Number of pieces of luggage", ParameterType = "query", DataType = "int", IsRequired = false)]
            public int? Luggage { get; set; }
            [ApiAllowableValues("Persons", "1", "2", "3", "4", "5", "6", "7")]
            [ApiMember(Name = "Persons", Description = "Number of people that will be in the vehicle", ParameterType = "query", DataType = "int", IsRequired = false)]
            public int? Persons { get; set; }
            [ApiMember(Name = "DropoffLat", Description = "GPS coordinate latitude of where the passenger should be dropped off. Example: 37.786956", ParameterType = "query", DataType = "double", IsRequired = true)]
            public double? DropoffLat { get; set; }
            [ApiMember(Name = "DropoffLng", Description = "GPS coordinate longitude of where the passenger should be dropped off. Example: -122.440279", ParameterType = "query", DataType = "double", IsRequired = true)]
            public double? DropoffLng { get; set; }
            [ApiAllowableValues("PaymentMethod", typeof(PaymentMethod))]
            [ApiMember(Name = "PaymentMethod", Description = "How does customer plan to pay", ParameterType = "query", DataType = "PaymentMethod", IsRequired = false)]
            public PaymentMethod? PaymentMethod { get; set; }
            [ApiAllowableValues("VehicleType", typeof(VehicleType))]
            [ApiMember(Name = "VehicleType", Description = "What type of vehicle", ParameterType = "query", DataType = "VehicleType", IsRequired = false)]
            public VehicleType? VehicleType { get; set; }
            [ApiMember(Name = "MaxPrice", Description = "Maximum price passenger is willing to pay", ParameterType = "query", DataType = "double", IsRequired = false)]
            public double? MaxPrice { get; set; }
            [ApiAllowableValues("MinRating", "1", "2", "3", "4", "5", "6", "7")]
            [ApiMember(Name = "MinRating", Description = "Minimum driver rating", ParameterType = "query", DataType = "int", IsRequired = false)]
            public int? MinRating { get; set; }
            [ApiMember(Name = "PartnerId", Description = "Unique identifier of partner you wish to receive quotes from.  Use this field only if you have a specific partner in mind.", ParameterType = "query", DataType = "string", IsRequired = false)]
            public string PartnerId { get; set; }
            [ApiMember(Name = "FleetId", Description = "Unique identifier of fleet you wish to receive quotes from. Use this field only if you have a specific partner in mind.", ParameterType = "query", DataType = "string", IsRequired = false)]
            public string FleetId { get; set; }
            [ApiMember(Name = "DriverId", Description = "Unique identifier of driver you wish to receive quotes from. Use this field only if you have a specific driver in mind.", ParameterType = "query", DataType = "string", IsRequired = false)]
            public string DriverId { get; set; }
            [ApiMember(Name = "PassengerId", Description = "In case there's a specific passenger ID.  Not normally needed", ParameterType = "query", DataType = "string", IsRequired = false)]
            public string PassengerId { get; set; }
        }

        public class QuotesResponse
        {
            public string Result { get; set; }
            public Gateway.Result ResultCode { get; set; }
            public string Message { get; set; }
            public int? Count { get; set; }
            public List<TripThruCore.Quote> Quotes { get; set; }
        }

        public class QuotesService : Service
        {
            public QuotesResponse Get(Quotes request)
            {
                var accessToken = request.access_token;
                var message = ValidateQuote(request);
                request.access_token = null;
                QuotesResponse quotesResponse = new QuotesResponse
                {
                    Result = "Unknown",
                    ResultCode = Gateway.Result.UnknownError
                };
                var acct = gateway.GetPartnerAccountByAccessToken(accessToken);
                var user = StorageManager.GetPartnerAccountByAccessToken(accessToken);
                var clientId = "none";
                try
                {
                    if ((!accessToken.IsNullOrEmpty() && acct != null || (user != null && user.Role == Storage.UserRole.admin)) && message == null){
                        if(acct == null)
                            acct = user;
                        clientId = acct.ClientId;
                        Logger.BeginRequest("QuoteTrip received from " + acct.UserName, request);
                        var response = gateway.QuoteTrip(new Gateway.QuoteTripRequest(
                            clientID: acct.ClientId,
                            pickupLocation: new Location(request.PickupLat, request.PickupLng),
                            pickupTime: request.PickupTime,
                            passengerID: request.PassengerId,
                            passengerName: request.PassengerName,
                            luggage: request.Luggage,
                            persons: request.Persons,
                            dropoffLocation: request.DropoffLat == null ? null : new Location((double)request.DropoffLat, (double)request.DropoffLng),
                            paymentMethod: request.PaymentMethod,
                            vehicleType: request.VehicleType,
                            maxPrice: request.MaxPrice,
                            minRating: request.MinRating,
                            partnerID: request.PartnerId,
                            fleetID: request.FleetId,
                            driverID: request.DriverId
                            ));
                        if (response.result == Gateway.Result.OK)
                        {
                            quotesResponse = new QuotesResponse
                            {
                                Count = response.quotes.Count,
                                Quotes = response.quotes,
                                ResultCode = response.result,
                                Result = "OK",
                                Message = "OK"
                            };
                        }
                        else
                        {
                            quotesResponse = new QuotesResponse
                            {
                                Result = "Failed",
                                ResultCode = response.result,
                                Message = "Failed"
                            };
                        }
                    }
                    else
                    {
                        string msg;
                        if (message == null)
                        {
                            Logger.BeginRequest("QuoteTrip received from unknown user", request);
                            msg = "POST /quotes called with invalid access token, ip: " + Request.RemoteIp +
                                  ", Response = Authentication failed";
                            quotesResponse = new QuotesResponse
                            {
                                Result = "Failed",
                                ResultCode = Gateway.Result.AuthenticationError,
                                Message = "Acces Token Invalid"
                            };
                        }
                        else
                        {
                            Logger.BeginRequest("QuoteTrip received with wrong parameters", request);
                            msg = message;
                            quotesResponse.Message = message;
                        }
                        Logger.Log(msg);
                        
                    }
                }
                catch (Exception e)
                {
                    Logger.LogDebug("QuoteTrip=" + e.Message, e.ToString());
                    quotesResponse = new QuotesResponse
                    {
                        Result = "Failed",
                        ResultCode = Gateway.Result.UnknownError,
                        Message = "Failed"
                    };
                }
                finally
                {
                    Logger.AddTag("RequestType", "QuoteTrip");
                    Logger.AddTag("ClientId", clientId);
                    Logger.SetOriginatingId(acct.ClientId);
                    Logger.SetServicingId(gateway.ID); //Should we have a list of servicing partners for this case?
                    Logger.EndRequest(quotesResponse);
                }
                return quotesResponse;
            }

            public QuotesResponse Options(Quotes request)
            {
                return new QuotesResponse();
            }

            private string ValidateQuote(Quotes quote)
            {
                if (quote.access_token.IsNullOrEmpty())
                    return "Access Token is Required.";
                if (quote.PickupTime == null)
                    return "PickupTime is Required.";
                if (quote.PickupLat == null)
                    return "PickupLat is Required.";
                if (quote.PickupLng == null)
                    return "PickupLng is Required.";
                if (quote.DropoffLat == null)
                    return "DropoffLat is Required.";
                if (quote.DropoffLng == null)
                    return "DropoffLng is Required.";
                return null;
            }

        }

        #endregion

        #region Quote

        [Api(Description = "Use GET to get quote for a possible trip.")]
        [Route("/quote", Verbs = "GET, PUT, POST, OPTIONS", Summary = @"get quote for a possible trip", Notes = "The standard usage is to first get quote for a planned trip and then dispatch the trip to your selected fleet and/or driver")]
        public class Quote : IReturn<QuoteResponse>
        {
            [ApiMember(Name = "access_token", Description = "Access token acquired through OAuth2.0 authorization procedure.  Example: demo12345", ParameterType = "query", DataType = "string", IsRequired = true)]
            public string access_token { get; set; }
            [ApiMember(Name = "PickupTime", Description = "Time that the taxi should arrive. Format (yyyy-MM-ddTHH:mm:ss) GMT.  Example: 2014-02-25T23:30:00", ParameterType = "query", DataType = "DateTime", IsRequired = true, Verb = "POST")]
            [ApiMember(Name = "PickupTime", Description = "Time that the taxi should arrive. Format (yyyy-MM-ddTHH:mm:ss) GMT.  Example: 2014-02-25T23:30:00", ParameterType = "query", DataType = "DateTime", IsRequired = true, Verb = "PUT")]
            public DateTime PickupTime { get; set; }
            [ApiMember(Name = "PickupLat", Description = "GPS coordinate latitude of where the passenger should be picked up. Example: 37.782551", ParameterType = "query", DataType = "double", IsRequired = true, Verb = "POST")]
            [ApiMember(Name = "PickupLat", Description = "GPS coordinate latitude of where the passenger should be picked up. Example: 37.782551", ParameterType = "query", DataType = "double", IsRequired = true, Verb = "PUT")]
            public double PickupLat { get; set; }
            [ApiMember(Name = "PickupLng", Description = "GPS coordinate longitude of where the passenger should be picked up. Example: -122.445368", ParameterType = "query", DataType = "double", IsRequired = true, Verb = "POST")]
            [ApiMember(Name = "PickupLng", Description = "GPS coordinate longitude of where the passenger should be picked up. Example: -122.445368", ParameterType = "query", DataType = "double", IsRequired = true, Verb = "PUT")]
            public double PickupLng { get; set; }
            [ApiMember(Name = "PassengerName", Description = "Name of passenger", ParameterType = "query", DataType = "string", IsRequired = false, Verb = "POST")]
            [ApiMember(Name = "PassengerName", Description = "Name of passenger", ParameterType = "query", DataType = "string", IsRequired = false, Verb = "PUT")]
            public string PassengerName { get; set; }
            [ApiAllowableValues("Luggage", "1", "2", "3", "4", "5", "6", "7")]
            [ApiMember(Name = "Luggage", Description = "Number of pieces of luggage", ParameterType = "query", DataType = "int", IsRequired = false, Verb = "POST")]
            [ApiMember(Name = "Luggage", Description = "Number of pieces of luggage", ParameterType = "query", DataType = "int", IsRequired = false, Verb = "PUT")]
            public int? Luggage { get; set; }
            [ApiAllowableValues("Persons", "1", "2", "3", "4", "5", "6", "7")]
            [ApiMember(Name = "Persons", Description = "Number of people that will be in the vehicle", ParameterType = "query", DataType = "int", IsRequired = false, Verb = "POST")]
            [ApiMember(Name = "Persons", Description = "Number of people that will be in the vehicle", ParameterType = "query", DataType = "int", IsRequired = false, Verb = "PUT")]
            public int? Persons { get; set; }
            [ApiMember(Name = "DropoffLat", Description = "GPS coordinate latitude of where the passenger should be dropped off. Example: 37.786956", ParameterType = "query", DataType = "double", IsRequired = true, Verb = "POST")]
            [ApiMember(Name = "DropoffLat", Description = "GPS coordinate latitude of where the passenger should be dropped off. Example: 37.786956", ParameterType = "query", DataType = "double", IsRequired = true, Verb = "PUT")]
            public double? DropoffLat { get; set; }
            [ApiMember(Name = "DropoffLng", Description = "GPS coordinate longitude of where the passenger should be dropped off. Example: -122.440279", ParameterType = "query", DataType = "double", IsRequired = true, Verb = "POST")]
            [ApiMember(Name = "DropoffLng", Description = "GPS coordinate longitude of where the passenger should be dropped off. Example: -122.440279", ParameterType = "query", DataType = "double", IsRequired = true, Verb = "PUT")]
            public double? DropoffLng { get; set; }
            [ApiAllowableValues("PaymentMethod", typeof(PaymentMethod))]
            [ApiMember(Name = "PaymentMethod", Description = "How does customer plan to pay", ParameterType = "query", DataType = "PaymentMethod", IsRequired = false, Verb = "POST")]
            [ApiMember(Name = "PaymentMethod", Description = "How does customer plan to pay", ParameterType = "query", DataType = "PaymentMethod", IsRequired = false, Verb = "PUT")]
            public PaymentMethod? PaymentMethod { get; set; }
            [ApiAllowableValues("VehicleType", typeof(VehicleType))]
            [ApiMember(Name = "VehicleType", Description = "What type of vehicle", ParameterType = "query", DataType = "VehicleType", IsRequired = false, Verb = "POST")]
            [ApiMember(Name = "VehicleType", Description = "What type of vehicle", ParameterType = "query", DataType = "VehicleType", IsRequired = false, Verb = "PUT")]
            public VehicleType? VehicleType { get; set; }
            [ApiMember(Name = "MaxPrice", Description = "Maximum price passenger is willing to pay", ParameterType = "query", DataType = "double", IsRequired = false, Verb = "POST")]
            [ApiMember(Name = "MaxPrice", Description = "Maximum price passenger is willing to pay", ParameterType = "query", DataType = "double", IsRequired = false, Verb = "PUT")]
            public double? MaxPrice { get; set; }
            [ApiMember(Name = "Price", Description = "Price", ParameterType = "query", DataType = "double", IsRequired = true, Verb = "PUT")]
            public double? Price { get; set; }
            [ApiMember(Name = "ETA", Description = "Estimated time of arrival. Format (yyyy-MM-ddTHH:mm:ss) GMT.  Example: 2014-02-25T23:30:00", ParameterType = "query", DataType = "DateTime", IsRequired = true, Verb = "PUT")]
            public DateTime? ETA { get; set; }
            [ApiAllowableValues("MinRating", "1", "2", "3", "4", "5", "6", "7")]
            [ApiMember(Name = "MinRating", Description = "Minimum driver rating", ParameterType = "query", DataType = "int", IsRequired = false, Verb = "POST")]
            [ApiMember(Name = "MinRating", Description = "Minimum driver rating", ParameterType = "query", DataType = "int", IsRequired = false, Verb = "PUT")]
            public int? MinRating { get; set; }
            [ApiMember(Name = "PartnerId", Description = "Unique identifier of partner you wish to receive quote from.  Use this field only if you have a specific partner in mind.", ParameterType = "query", DataType = "string", IsRequired = false, Verb = "POST")]
            [ApiMember(Name = "PartnerId", Description = "Unique identifier of partner.", ParameterType = "query", DataType = "string", IsRequired = false, Verb = "PUT")]
            public string PartnerId { get; set; }
            [ApiMember(Name = "FleetId", Description = "Unique identifier of fleet you wish to receive quote from. Use this field only if you have a specific partner in mind.", ParameterType = "query", DataType = "string", IsRequired = false, Verb = "POST")]
            [ApiMember(Name = "FleetId", Description = "Unique identifier of fleet.", ParameterType = "query", DataType = "string", IsRequired = false, Verb = "PUT")]
            public string FleetId { get; set; }
            [ApiMember(Name = "DriverId", Description = "Unique identifier of driver you wish to receive quote from. Use this field only if you have a specific driver in mind.", ParameterType = "query", DataType = "string", IsRequired = false, Verb = "POST")]
            [ApiMember(Name = "DriverId", Description = "Unique identifier of driver.", ParameterType = "query", DataType = "string", IsRequired = false, Verb = "PUT")]
            public string DriverId { get; set; }
            [ApiMember(Name = "PassengerId", Description = "In case there's a specific passenger ID.  Not normally needed", ParameterType = "query", DataType = "string", IsRequired = false, Verb = "POST")]
            [ApiMember(Name = "PassengerId", Description = "In case there's a specific passenger ID.  Not normally needed", ParameterType = "query", DataType = "string", IsRequired = false, Verb = "PUT")]
            public string PassengerId { get; set; }
            [ApiMember(Name = "TripId", Description = "Partner scope unique identifier of the trip that you will use to make queries about the trip.  Note: it only has to be unique to you.  TripThru will handle any cross-network uniqueness issues.", ParameterType = "query", DataType = "string", IsRequired = true)]
            public string TripId { get; set; }
        }

        public class QuoteResponse
        {
            public string Result { get; set; }
            public Gateway.Result ResultCode { get; set; }
            public string Message { get; set; }
            public int? Count { get; set; }
            public List<TripThruCore.Quote> Quotes { get; set; }
        }

        public class quoteService : Service
        {
            public QuoteResponse Post(Quote request)
            {
                var accessToken = request.access_token;
                var message = ValidateQuote(request);
                request.access_token = null;
                QuoteResponse quotesResponse = new QuoteResponse
                {
                    Result = "Unknown",
                    ResultCode = Gateway.Result.UnknownError
                };

                var acct = gateway.GetPartnerAccountByAccessToken(accessToken);
                var clientId = "none";
                try
                {
                    if (acct != null && message == null)
                    {
                        clientId = acct.ClientId;
                        Logger.BeginRequest("QuoteTrip received from " + acct.UserName, request);
                        var response = gateway.QuoteTrip(new Gateway.QuoteTripRequest(
                            clientID: acct.ClientId,
                            pickupLocation: new Location(request.PickupLat, request.PickupLng),
                            pickupTime: request.PickupTime,
                            passengerID: request.PassengerId,
                            passengerName: request.PassengerName,
                            luggage: request.Luggage,
                            persons: request.Persons,
                            dropoffLocation: request.DropoffLat == null ? null : new Location((double)request.DropoffLat, (double)request.DropoffLng),
                            paymentMethod: request.PaymentMethod,
                            vehicleType: request.VehicleType,
                            maxPrice: request.MaxPrice,
                            minRating: request.MinRating,
                            partnerID: request.PartnerId,
                            fleetID: request.FleetId,
                            driverID: request.DriverId
                            ));
                        if (response.result == Gateway.Result.OK)
                        {
                            quotesResponse = new QuoteResponse
                            {
                                Count = response.quotes.Count,
                                Quotes = response.quotes,
                                ResultCode = response.result,
                                Result = "OK",
                                Message = "OK"
                            };
                        }
                        else
                        {
                            quotesResponse = new QuoteResponse
                            {
                                Result = "Failed",
                                ResultCode = response.result,
                                Message = "Failed"
                            };
                        }
                    }
                    else
                    {
                        string msg;
                        if (message == null)
                        {
                            Logger.BeginRequest("QuoteTrip received from unknown user", request);
                            msg = "POST /quotes called with invalid access token, ip: " + Request.RemoteIp +
                                  ", Response = Authentication failed";
                            quotesResponse = new QuoteResponse
                            {
                                Result = "Failed",
                                ResultCode = Gateway.Result.AuthenticationError,
                                Message = "Acces Token Invalid"
                            };
                        }
                        else
                        {
                            Logger.BeginRequest("QuoteTrip received with wrong parameters", request);
                            msg = message;
                            quotesResponse.Message = message;
                        }
                        Logger.Log(msg);

                    }
                }
                catch (Exception e)
                {
                    Logger.LogDebug("QuoteTrip=" + e.Message, e.ToString());
                    quotesResponse = new QuoteResponse
                    {
                        Result = "Failed",
                        ResultCode = Gateway.Result.UnknownError,
                        Message = "Failed"
                    };
                }
                finally
                {
                    Logger.AddTag("RequestType", "QuoteTrip");
                    Logger.AddTag("ClientId", clientId);
                    Logger.SetOriginatingId(acct.ClientId);
                    Logger.SetServicingId(gateway.ID); //Should we have a list of servicing partners for this case?
                    Logger.EndRequest(quotesResponse);
                }
                return quotesResponse;
            }

            public QuoteResponse Get(Quote request)
            {
                QuoteResponse quoteResponse = new QuoteResponse
                {
                    Result = "OK",
                    ResultCode = Gateway.Result.OK
                };
                return quoteResponse;
            }
            public QuoteResponse Put(Quote request)
            {
                QuoteResponse quoteResponse = new QuoteResponse
                {
                    Result = "OK",
                    ResultCode = Gateway.Result.OK
                };
                return quoteResponse;
            }

            public QuoteResponse Options(Quote request)
            {
                return new QuoteResponse();
            }
            private string ValidateQuote(Quote quote)
            {
                if (quote.access_token.IsNullOrEmpty())
                    return "Access Token is Required.";
                if (quote.PickupTime == null)
                    return "PickupTime is Required.";
                if (quote.PickupLat == null)
                    return "PickupLat is Required.";
                if (quote.PickupLng == null)
                    return "PickupLng is Required.";
                if (quote.DropoffLat == null)
                    return "DropoffLat is Required.";
                if (quote.DropoffLng == null)
                    return "DropoffLng is Required.";
                return null;
            }
        }

        #endregion

        #region Trip

        [Api("Use POST to add trip to a fleet. Can be used in conjuction with /quotes")]
        [Route("/trip", "POST, OPTIONS")]
        public class Trip : IReturn<TripResponse>
        {
            [ApiMember(Name = "access_token", Description = "Access token acquired through OAuth2.0 authorization procedure.  Example: demo12345", ParameterType = "query", DataType = "string", IsRequired = true)]
            public string access_token { get; set; }
            [ApiMember(Name = "TripId", Description = "Partner scope unique identifier of the trip that you will use to make queries about the trip.  Note: it only has to be unique to you.  TripThru will handle any cross-network uniqueness issues.", ParameterType = "query", DataType = "string", IsRequired = true)]
            public string TripId { get; set; }
            [ApiMember(Name = "PickupTime", Description = "Time that the taxi should arrive. Format (yyyy-MM-ddTHH:mm:ss) GMT.  Example: 2014-02-25T23:30:00", ParameterType = "query", DataType = "DateTime", IsRequired = true)]
            public DateTime PickupTime { get; set; }
            [ApiMember(Name = "PickupLat", Description = "GPS coordinate latitude of where the passenger should be picked up. Example: 37.782551", ParameterType = "query", DataType = "double", IsRequired = true)]
            public double PickupLat { get; set; }
            [ApiMember(Name = "PickupLng", Description = "GPS coordinate longitude of where the passenger should be picked up. Example: -122.445368", ParameterType = "query", DataType = "double", IsRequired = true)]
            public double PickupLng { get; set; }
            [ApiMember(Name = "PassengerName", Description = "Name of passenger", ParameterType = "query", DataType = "string", IsRequired = false)]
            public string PassengerName { get; set; }
            [ApiAllowableValues("Luggage", "1", "2", "3", "4", "5", "6", "7")]
            [ApiMember(Name = "Luggage", Description = "Number of pieces of luggage", ParameterType = "query", DataType = "int", IsRequired = false)]
            public int? Luggage { get; set; }
            [ApiAllowableValues("Persons", "1", "2", "3", "4", "5", "6", "7")]
            [ApiMember(Name = "Persons", Description = "Number of people that will be in the vehicle", ParameterType = "query", DataType = "int", IsRequired = false)]
            public int? Persons { get; set; }
            [ApiMember(Name = "DropoffLat", Description = "GPS coordinate latitude of where the passenger should be dropped off. Example: 37.786956", ParameterType = "query", DataType = "double", IsRequired = false)]
            public double? DropoffLat { get; set; }
            [ApiMember(Name = "DropoffLng", Description = "GPS coordinate longitude of where the passenger should be dropped off. Example: -122.440279", ParameterType = "query", DataType = "double", IsRequired = false)]
            public double? DropoffLng { get; set; }
            [ApiMember(Name = "PaymentMethod", Description = "How does customer plan to pay", ParameterType = "query", DataType = "PaymentMethod", IsRequired = false)]
            [ApiAllowableValues("PaymentMethod", typeof(PaymentMethod))]
            public PaymentMethod? PaymentMethod { get; set; }
            [ApiAllowableValues("VehicleType", typeof(VehicleType))]
            [ApiMember(Name = "VehicleType", Description = "What type of vehicle", ParameterType = "query", DataType = "VehicleType", IsRequired = false)]
            public VehicleType? VehicleType { get; set; }
            [ApiMember(Name = "MaxPrice", Description = "Maximum price passenger is willing to pay", ParameterType = "query", DataType = "double", IsRequired = false)]
            public double? MaxPrice { get; set; }
            [ApiAllowableValues("MinRating", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10")]
            [ApiMember(Name = "MinRating", Description = "Minimum driver rating", ParameterType = "query", DataType = "int", IsRequired = false)]
            public int? MinRating { get; set; }
            [ApiMember(Name = "PartnerId", Description = "Unique identifier of partner you wish to dispatch to.  If blank then TripThru will select the best option.", ParameterType = "query", DataType = "string", IsRequired = false)]
            public string PartnerId { get; set; }
            [ApiMember(Name = "FleetId", Description = "Unique identifier of fleet you wish to dispatch to.  If blank then TripThru will select the best option.", ParameterType = "query", DataType = "string", IsRequired = false)]
            public string FleetId { get; set; }
            [ApiMember(Name = "DriverId", Description = "Unique identifier of driver you wish to dispatch to.  If blank then TripThru will select the best option.", ParameterType = "query", DataType = "string", IsRequired = false)]
            public string DriverId { get; set; }
            [ApiMember(Name = "PassengerId", Description = "In case there's a specific passenger ID.  Not normally needed", ParameterType = "query", DataType = "string", IsRequired = false)]
            public string PassengerId { get; set; }
        }

        public class TripResponse
        {
            public string Result { get; set; }
            public Gateway.Result ResultCode { get; set; }
            public string Message { get; set; }
        }

        public class DispatchService : Service
        {
            public TripResponse Post(Trip request)
            {
                var accessToken = request.access_token;
                var message = ValidateTrip(request);
                request.access_token = null;
                TripResponse dispatchResponse = new TripResponse
                {
                    Result = "Unknown",
                    ResultCode = Gateway.Result.UnknownError
                };
                PartnerAccount acct = gateway.GetPartnerAccountByAccessToken(accessToken);
                var clientId = "none";
                try
                {
                    if (acct != null && message == null)
                    {
                        clientId = acct.ClientId;
                        Logger.BeginRequest("DispatchTrip received from " + acct.UserName, request, request.TripId);
                        var response = gateway.DispatchTrip(new Gateway.DispatchTripRequest(
                            clientID: acct.ClientId,
                            tripID: request.TripId,
                            pickupLocation: new Location(request.PickupLat, request.PickupLng),
                            pickupTime: request.PickupTime,
                            passengerID: request.PassengerId,
                            passengerName: request.PassengerName,
                            luggage: request.Luggage,
                            persons: request.Persons,
                            dropoffLocation: request.DropoffLat == null ? null : new Location((double)request.DropoffLat, (double)request.DropoffLng),
                            paymentMethod: request.PaymentMethod,
                            vehicleType: request.VehicleType,
                            maxPrice: request.MaxPrice,
                            minRating: request.MinRating,
                            partnerID: request.PartnerId,
                            fleetID: request.FleetId,
                            driverID: request.DriverId
                            ));

                        if (response.result == Gateway.Result.OK)
                        {
                            dispatchResponse = new TripResponse
                            {
                                Result = "OK",
                                ResultCode = response.result,
                                Message = "OK"
                            };
                        }
                        else
                        {
                            dispatchResponse = new TripResponse
                            {
                                Result = "Failed",
                                ResultCode = response.result,
                                Message = "Failed"
                            };
                        }
                    }
                    else
                    {

                        string msg;
                        if (message == null)
                        {
                            Logger.BeginRequest("DispatchTrip received from unknown user", request, request.TripId);
                            msg = "POST /dispatch called with invalid access token, ip: " + Request.RemoteIp +
                                  ", Response = Authentication failed";
                            dispatchResponse = new TripResponse
                            {
                                Result = "Failed",
                                ResultCode = Gateway.Result.AuthenticationError,
                                Message = "Failed"
                            };
                        }
                        else
                        {
                            Logger.BeginRequest("DispatchTrip received with wrong parameters", request, request.TripId);
                            msg = message;
                            dispatchResponse.Message = message;
                        }
                        Logger.Log(msg);
                        
                    }
                }
                catch (Exception e)
                {
                    Logger.LogDebug("DispatchTrip=" + e.Message, e.ToString(),
                        new Dictionary<string, string>() { { "TripID", request.TripId }, {"ClientId", clientId},{"Remote Ip", Request.RemoteIp} });
                    dispatchResponse = new TripResponse
                    {
                        Result = "Failed",
                        ResultCode = Gateway.Result.UnknownError,
                        Message = "Failed"
                    };
                }
                finally
                {
                    Logger.AddTag("Remote Ip", Request.RemoteIp);
                    Logger.AddTag("RequestType", "DispatchTrip");
                    Logger.AddTag("ClientId", clientId);
                    Logger.AddTag("TripID", request.TripId);
                    Logger.SetOriginatingId(acct.ClientId);
                    Logger.EndRequest(dispatchResponse);
                }
                return dispatchResponse;
            }

            public TripResponse Options(Trip request)
            {
                return new TripResponse();
            }

            private string ValidateTrip(Trip dispatch)
            {
                if (dispatch.access_token.IsNullOrEmpty())
                    return "Access Token is Required.";
                if (dispatch.TripId.IsNullOrEmpty())
                    return "Trip Id is requiered";
                if (dispatch.PickupTime == null)
                    return "PickupTime is Required.";
                if (dispatch.PickupLat == null)
                    return "PickupLat is Required.";
                if (dispatch.PickupLng == null)
                    return "PickupLng is Required.";
                if (dispatch.DropoffLat == null)
                    return "DropoffLat is Required.";
                if (dispatch.DropoffLng == null)
                    return "DropoffLng is Required.";
                return null;
            }
        }

        #endregion

        #region TripStatus

        [Api("Use GET /tripstatus to get the trip status and PUT /tripstatus to update a trip status")]
        [Route("/tripstatus", "GET, PUT, OPTIONS")]
        public class TripStatus : IReturn<TripStatusResponse>
        {
            [ApiMember(Name = "access_token", Description = "Access token acquired through OAuth2.0 authorization procedure.  Example: demo12345", ParameterType = "query", DataType = "string", IsRequired = true)]
            public string access_token { get; set; }

            [ApiMember(Name = "TripId", Description = "Partner scope unique identifier of the trip (the same as you passed into /dispatch).  Note: it only has to be unique to you.  TripThru will handle any cross-network uniqueness issues.", ParameterType = "query", DataType = "string", IsRequired = true)]
            public string TripId { get; set; }

            [ApiAllowableValues("Status", typeof(Status))]
            [ApiMember(Name = "Status", Description = "Trip status code", ParameterType = "query", DataType = "Status", IsRequired = false, Verb = "PUT")]
            public Status Status { get; set; }

            [ApiMember(Name = "DriverLocationLat", Description = "GPS coordinate latitude of the driver location. Example: 37.786956", ParameterType = "query", DataType = "double", IsRequired = false, Verb = "PUT")]
            public double? DriverLocationLat { get; set; }

            [ApiMember(Name = "DriverLocationLng", Description = "GPS coordinate longitude of the driver location. Example: -122.440279", ParameterType = "query", DataType = "double", IsRequired = false, Verb = "PUT")]
            public double? DriverLocationLng { get; set; }

            [ApiMember(Name = "ETA", Description = "Time that driver will arrive at destination. Either pickup location or dropoff location", ParameterType = "query", DataType = "DateTime", IsRequired = false, Verb = "PUT")]
            public DateTime? ETA { get; set; }

            [ApiAllowableValues("Rating", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10")]
            [ApiMember(Name = "Rating", Description = "Rating of the trip from driver's or passenger's perspective", ParameterType = "query", DataType = "int", IsRequired = false, Verb = "PUT")]
            public int? Rating { get; set; }
        }

        public class TripStatusResponse
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
            public Location DriverInitialLocation { get; set; }
            public DateTime? PickupTime { get; set; }
            public DateTime? DropoffTime { get; set; }
            public VehicleType? VehicleType { get; set; }
            public Status? Status { get; set; }
            public DateTime? ETA { get; set; } // in minutes;
            public double? Price { get; set; }
            public double? Distance { get; set; }
            public double? DriverRouteDuration { get; set; }
            public Location PickupLocation { get; set; }
            public Location DropoffLocation { get; set; }
            public string OriginatingPartnerName { get; set; }
            public string ServicingPartnerName { get; set; }
            public string Message { get; set; }
        }

        public class TripService : Service
        {
            public TripStatusResponse Get(TripStatus request)
            {
                Logger.Disable();
                var accessToken = request.access_token;
                var message = ValidateTripStatus(request);
                request.access_token = null;
                Logger.BeginRequest("GetTripStatus received", request);
                TripStatusResponse tripStatusResponse = new TripStatusResponse
                {
                    Result = "Unknown",
                    ResultCode = Gateway.Result.UnknownError
                };

                PartnerAccount acct = gateway.GetPartnerAccountByAccessToken(accessToken);
                var clientId = "none";
                try
                {
                    if (!accessToken.IsNullOrEmpty() && acct == null) {
                        PartnerAccount user = StorageManager.GetPartnerAccountByAccessToken(accessToken);
                        if (user != null && user.Role == Storage.UserRole.admin)
                            acct = user;
                    }

                    if (acct != null && message == null)
                    {
                        clientId = acct.ClientId;
                        Logger.BeginRequest("GetTripStatus received from " + acct.UserName, request, request.TripId);
                        var response = gateway.GetTripStatus(new Gateway.GetTripStatusRequest(
                            acct.ClientId,
                            request.TripId
                            ));

                        if (response.result == Gateway.Result.OK)
                        {
                            tripStatusResponse = new TripStatusResponse
                            {
                                PassengerName = response.passengerName,
                                DriverId = response.driverID,
                                DriverLocation = response.driverLocation,
                                DriverInitialLocation = response.driverInitialLocation,
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
                                DriverRouteDuration = response.driverRouteDuration,
                                OriginatingPartnerName = response.originatingPartnerName,
                                ServicingPartnerName = response.servicingPartnerName,
                                Message = "OK"
                            };
                        }
                        else
                        {
                            tripStatusResponse = new TripStatusResponse
                            {
                                Result = "Failed",
                                ResultCode = response.result,
                                Message = "Failed"
                            };
                        }
                    }
                    else
                    {
                        string msg;
                        if (message == null)
                        {
                            Logger.BeginRequest("GetTripStatus received from unknown user", request, request.TripId);
                            msg = "GET /trip/status called with invalid access token, ip: " + Request.RemoteIp +
                                  ", Response = Authentication failed";
                            tripStatusResponse = new TripStatusResponse
                            {
                                Result = "Failed",
                                ResultCode = Gateway.Result.AuthenticationError,
                                Message = "Failed"
                            };
                        }
                        else
                        {
                            Logger.BeginRequest("GetTripStatus received with wrong parameters", request, request.TripId);
                            msg = message;
                            tripStatusResponse.Message = message;
                        }
                        Logger.Log(msg);
                        
                    }
                }
                catch (Exception e)
                {
                    Logger.LogDebug("GetTripStatus=" + e.Message, e.ToString(),
                        new Dictionary<string, string>() { { "TripID", request.TripId }, { "ClientId", clientId }, { "RemoteIp", Request.RemoteIp } });
                    tripStatusResponse = new TripStatusResponse
                    {
                        Result = "Failed",
                        ResultCode = Gateway.Result.UnknownError,
                        Message = "Failed"
                    };
                }
                finally
                {
                    Logger.AddTag("RemoteIp", Request.RemoteIp);
                    Logger.AddTag("RequestType", "GetTripStatus");
                    Logger.AddTag("ClientId", clientId);
                    Logger.AddTag("TripID", request.TripId);
                    Logger.SetOriginatingId(acct.ClientId);
                    Logger.EndRequest(tripStatusResponse);
                    Logger.Enable();
                }
                return tripStatusResponse;
            }

            public TripStatusResponse Put(TripStatus request)
            {
                var accessToken = request.access_token;
                var message = ValidateTripStatus(request);
                request.access_token = null;
                TripStatusResponse tripStatusResponse = new TripStatusResponse
                {
                    Result = "Unknown",
                    ResultCode = Gateway.Result.UnknownError
                };
                PartnerAccount acct = gateway.GetPartnerAccountByAccessToken(accessToken);
                var clientId = "none";
                try
                {
                    if (acct != null && message == null)
                    {
                        clientId = acct.ClientId;
                        Logger.BeginRequest("UpdateTripStatus(" + request.Status + ") received from " + acct.UserName, request, request.TripId);
                        Location driverLocation = null;
                        if (request.DriverLocationLat != null && request.DriverLocationLng != null)
                        {
                            driverLocation = new Location((double)request.DriverLocationLat, (double)request.DriverLocationLng);
                        }
                        var response = gateway.UpdateTripStatus(new Gateway.UpdateTripStatusRequest(
                            acct.ClientId,
                            request.TripId,
                            request.Status,
                            driverLocation,
                            request.ETA
                            ));

                        if (response.result == Gateway.Result.OK)
                        {
                            tripStatusResponse = new TripStatusResponse
                            {
                                Result = "OK",
                                ResultCode = response.result,
                                Message = "OK"
                            };
                        }
                        else
                        {

                            tripStatusResponse = new TripStatusResponse
                            {
                                Result = "Failed",
                                ResultCode = response.result,
                                Message = "Failed"
                            };
                        }
                    }
                    else
                    {
                        string msg;
                        if (message == null)
                        {
                            Logger.BeginRequest("UpdateTripStatus received from unknown user", request, request.TripId);
                            msg = "PUT /trip/status called with invalid access token, ip: " + Request.RemoteIp +
                                  ", Response = Authentication failed";
                            tripStatusResponse = new TripStatusResponse
                            {
                                Result = "Failed",
                                ResultCode = Gateway.Result.AuthenticationError,
                                Message = "Failed"
                            };
                        }
                        else
                        {
                            Logger.BeginRequest("UpdateTripStatus received with wrong parameters", request);
                            msg = message;
                            tripStatusResponse.Message = msg;
                        }
                        Logger.Log(msg);
                        
                    }
                }
                catch (Exception e)
                {
                    Logger.LogDebug("UpdateTripStatus=" + e.Message, e.ToString(),
                        new Dictionary<string, string>() { { "TripID", request.TripId }, { "ClientId", clientId }, { "RemoteIp", Request.RemoteIp } });
                    tripStatusResponse = new TripStatusResponse
                    {
                        Result = "Failed",
                        ResultCode = Gateway.Result.UnknownError
                    };
                }
                finally
                {
                    Logger.AddTag("RemoteIp", Request.RemoteIp);
                    Logger.AddTag("RequestType", "UpdateTripStatus");
                    Logger.AddTag("ClientId", clientId);
                    Logger.AddTag("TripID", request.TripId);
                    Logger.SetOriginatingId(acct.ClientId);
                    Logger.EndRequest(tripStatusResponse);
                }
                return tripStatusResponse;
            }

            public TripStatusResponse Options(TripStatus request)
            {
                return new TripStatusResponse();
            }

            private string ValidateTripStatus(TripStatus tripStatus)
            {
                if (tripStatus.access_token.IsNullOrEmpty())
                    return "Access Token is Required";
                if (tripStatus.TripId.IsNullOrEmpty())
                    return "Trip Id is Required";
                return null;
            }

        }

        #endregion

        #region Trips

        [Api("Use GET /trips")]
        [Route("/trips", "GET, OPTIONS")]
        public class Trips : IReturn<TripsResponse>
        {
            [ApiMember(Name = "access_token", Description = "Access token acquired through OAuth2.0 authorization procedure.  Example: demo12345", ParameterType = "query", DataType = "string", IsRequired = true)]
            public string access_token { get; set; }
            [ApiAllowableValues("Status", typeof(Status))]
            [ApiMember(Name = "Status", Description = "Get a list of trips with given status", ParameterType = "query", DataType = "Status", IsRequired = false)]
            public Status? Status { get; set; }
        }

        public class TripsResponse
        {
            public string Result { get; set; }
            public Gateway.Result ResultCode { get; set; }
            public List<TripThruCore.Trip> Trips { get; set; }
        }

        public class TripsService : Service
        {
            public TripsResponse Get(Trips request)
            {
                Logger.Disable();
                var accessToken = request.access_token;
                request.access_token = null;
                Logger.BeginRequest("GetTrips received", request);
                TripsResponse tripsResponse = new TripsResponse
                {
                    Result = "Unknown",
                    ResultCode = Gateway.Result.UnknownError
                };
                PartnerAccount acct = gateway.GetPartnerAccountByAccessToken(accessToken);
                PartnerAccount user = StorageManager.GetPartnerAccountByAccessToken(accessToken);
                var clientId = "none";
                try
                {
                    if (accessToken != null && acct != null || (user != null && user.Role == Storage.UserRole.admin))
                    {
                        if (acct == null)
                            acct = user;
                        clientId = acct.ClientId;
                        var response = gateway.GetTrips(new Gateway.GetTripsRequest(null, request.Status));

                        if (response.result == Gateway.Result.OK)
                        {
                            List<TripThruCore.Trip> trips = response.trips;
                            if (acct.Role != Storage.UserRole.admin)
                                trips = trips.Where(
                                            t => t.OriginatingPartnerId == acct.ClientId ||
                                                 t.ServicingPartnerId == acct.ClientId).ToList();
                            tripsResponse = new TripsResponse
                            {
                                Result = "OK",
                                ResultCode = response.result,
                                Trips = trips
                            };
                        }
                        else
                        {
                            tripsResponse = new TripsResponse
                            {
                                Result = "Failed",
                                ResultCode = response.result
                            };
                        }
                    }
                    else
                    {
                        tripsResponse = new TripsResponse
                        {
                            Result = "Failed",
                            ResultCode = Gateway.Result.AuthenticationError
                        };
                    }
                }
                catch (Exception e)
                {
                    Logger.LogDebug("GetTrips=" + e.Message, e.ToString());
                    tripsResponse = new TripsResponse
                    {
                        Result = "Failed",
                        ResultCode = Gateway.Result.UnknownError
                    };
                }
                finally
                {
                    Logger.AddTag("RequestType", "GetTrips");
                    Logger.SetOriginatingId(clientId);
                    Logger.SetServicingId(gateway.ID);
                    Logger.EndRequest(tripsResponse);
                    Logger.Enable();
                }
                return tripsResponse;
            }

            public TripResponse Options(Trips request)
            {
                return new TripResponse();
            }
        }

        #endregion

        #region RouteTrip

        [Api("Use GET /routetrip")]
        [Route("/routetrip", "GET, OPTIONS")]
        [Restrict(VisibilityTo = EndpointAttributes.None)]
        public class RouteTrip : IReturn<RouteTripResponse>
        {
            [ApiMember(Name = "access_token", Description = "Access token acquired through OAuth2.0 authorization procedure.  Example: demo12345", ParameterType = "query", DataType = "string", IsRequired = true)]
            public string access_token { get; set; }
            [ApiMember(Name = "tripId", Description = "Trip ID", DataType = "string", IsRequired = true)]
            public string tripId { get; set; }
        }

        public class RouteTripResponse
        {
            public Gateway.Result Result { get; set; }
            public List<Location> HistoryEnrouteList { get; set; }
            public List<Location> HistoryPickUpList { get; set; }
        }

        public class RouteTripService : Service
        {
            public RouteTripResponse Get(RouteTrip request)
            {
                var accessToken = request.access_token;
                request.access_token = null;
                PartnerAccount acct = gateway.GetPartnerAccountByAccessToken(accessToken);
                PartnerAccount user = StorageManager.GetPartnerAccountByAccessToken(accessToken);
                var routeTripResponse = new RouteTripResponse
                {
                    Result = Gateway.Result.UnknownError
                };
                try
                {
                    if (acct == null)
                        acct = user;

                    var response = gateway.GetRouteTrip(new Gateway.GetRouteTripRequest(request.tripId));
                    if (acct.Role != Storage.UserRole.admin && acct.ClientId != response.OriginatingPartnerId &&
                        acct.ClientId != response.ServicingPartnerId)
                        return routeTripResponse;
                    routeTripResponse.Result = response.result;
                    routeTripResponse.HistoryEnrouteList = response.HistoryEnrouteList;
                    routeTripResponse.HistoryPickUpList = response.HistoryPickUpList;
                }
                catch (Exception e)
                {
                    routeTripResponse = new RouteTripResponse
                    {
                        Result = Gateway.Result.UnknownError
                    };
                }
                return routeTripResponse;
            }
            public RouteTripResponse Options(RouteTrip request)
            {
                return new RouteTripResponse();
            }
        }

        #endregion

        #region Driver
        [Api("Use POST /driver")]
        [Route("/driver", "POST, OPTIONS")]
        public class Driver : IReturn<DriverResponse>
        {
            [ApiMember(Name = "access_token", Description = "Access token acquired through OAuth2.0 authorization procedure.  Example: demo12345", ParameterType = "query", DataType = "string", IsRequired = true)]
            public string access_token { get; set; }
            [ApiMember(Name = "Name", Description = "The driver's name.", ParameterType = "query", DataType = "string", IsRequired = true)]
            public string Name { get; set; }
            [ApiMember(Name = "Id", Description = "The driver's identifier.", ParameterType = "query", DataType = "string", IsRequired = true)]
            public string Id { get; set; }
            [ApiMember(Name = "NetworkId", Description = "Id of the network the driver belongs to", ParameterType = "query", DataType = "string", IsRequired = true)]
            public string NetworkId { get; set; }
            [ApiMember(Name = "FleetId", Description = "Id of the fleet the driver belongs to.", ParameterType = "query", DataType = "string", IsRequired = true)]
            public string FleetId { get; set; }
            public List<Shift> ShiftSchedule { get; set; }
            [ApiMember(Name = "ShiftStartLocationLat", Description = "GPS coordinate latitude of where the driver's shift starts. Example: -122.445368", ParameterType = "query", DataType = "double", IsRequired = true)]
            public double ShiftStartLocationLat { get; set; }
            [ApiMember(Name = "ShiftStartLocationLng", Description = "GPS coordinate longitude of where the driver's shift starts. Example: -122.445368", ParameterType = "query", DataType = "double", IsRequired = true)]
            public double ShiftStartLocationLng { get; set; }
            [ApiMember(Name = "ShiftStartTime", Description = "Time that the shift will start. Format (yyyy-MM-ddTHH:mm:ss) GMT.  Example: 2014-02-25T23:30:00", ParameterType = "query", DataType = "DateTime", IsRequired = true)]
            public DateTime ShiftStartTime { get; set; }
            [ApiMember(Name = "ShiftEndLocationLat", Description = "GPS coordinate latitude of where the driver's shift ends. Example: -122.445368", ParameterType = "query", DataType = "double", IsRequired = true)]
            public double ShiftEndLocationLat { get; set; }
            [ApiMember(Name = "ShiftEndLocationLng", Description = "GPS coordinate longitude of where the driver's shift ends. Example: -122.445368", ParameterType = "query", DataType = "double", IsRequired = true)]
            public double ShiftEndLocationLng { get; set; }
            [ApiMember(Name = "ShiftEndTime", Description = "Time that the shift will end. Format (yyyy-MM-ddTHH:mm:ss) GMT.  Example: 2014-02-25T23:30:00", ParameterType = "query", DataType = "DateTime", IsRequired = true)]
            public DateTime ShiftEndTime { get; set; }
        }

        public class DriverResponse
        {
            public string Result { get; set; }
            public Gateway.Result ResultCode { get; set; }
        }

        public class DriverService : Service
        {
            public DriverResponse Post(Driver request)
            {
                var accessToken = request.access_token;
                request.access_token = null;
                Logger.BeginRequest("GetDriver received", request);
                DriverResponse driverResponse = new DriverResponse
                {
                    Result = "Unknown",
                    ResultCode = Gateway.Result.UnknownError
                };
                PartnerAccount acct = gateway.GetPartnerAccountByAccessToken(accessToken);
                PartnerAccount user = StorageManager.GetPartnerAccountByAccessToken(accessToken);
                var clientId = "none";
                try
                {
                    if (accessToken != null && acct != null || (user != null && user.Role == Storage.UserRole.admin))
                    {
                        driverResponse = new DriverResponse
                        {
                            Result = "OK",
                            ResultCode = Gateway.Result.OK
                        };
                    }
                    else
                    {
                        driverResponse = new DriverResponse
                        {
                            Result = "Failed",
                            ResultCode = Gateway.Result.AuthenticationError
                        };
                    }
                }
                catch (Exception e)
                {
                    Logger.LogDebug("GetDriver=" + e.Message, e.ToString());
                    driverResponse = new DriverResponse
                    {
                        Result = "Failed",
                        ResultCode = Gateway.Result.UnknownError
                    };
                }
                finally
                {
                    Logger.AddTag("RequestType", "GetDriver");
                    Logger.SetOriginatingId(clientId);
                    Logger.SetServicingId(gateway.ID);
                    Logger.EndRequest(driverResponse);
                }
                return driverResponse;
            }

            public DriverResponse Options(Driver request)
            {
                return new DriverResponse();
            }

        }

        #endregion

        #region DriverStatus

        [Api("Use POST /driverstatus")]
        [Route("/driverstatus", "POST, OPTIONS")]
        public class DriverStatus : IReturn<DriverStatusResponse>
        {
            [ApiMember(Name = "access_token", Description = "Access token acquired through OAuth2.0 authorization procedure.  Example: demo12345", ParameterType = "query", DataType = "string", IsRequired = true)]
            public string access_token { get; set; }
            [ApiAllowableValues("Status", typeof(TripThruCore.DriverStatus))]
            [ApiMember(Name = "Status", Description = "Driver status code", ParameterType = "query", DataType = "Status", IsRequired = true, Verb = "POST")]
            public TripThruCore.DriverStatus Status { get; set; }
            [ApiMember(Name = "LocationLat", Description = "GPS coordinate latitude of where the driver is. Example: -122.445368", ParameterType = "query", DataType = "double", IsRequired = true)]
            public double LocationLat { get; set; }
            [ApiMember(Name = "LocationLng", Description = "GPS coordinate longitude of where the driver is. Example: -122.445368", ParameterType = "query", DataType = "double", IsRequired = true)]
            public double LocationLng { get; set; }
            [ApiMember(Name = "TripId", Description = "Id of trip currently served by the driver", ParameterType = "query", DataType = "string", IsRequired = false)]
            public string TripId { get; set; }
        }

        public class DriverStatusResponse
        {
            public string Result { get; set; }
            public Gateway.Result ResultCode { get; set; }
        }

        public class DriverStatusService : Service
        {
            public DriverStatusResponse Post(DriverStatus request)
            {
                var accessToken = request.access_token;
                request.access_token = null;
                Logger.BeginRequest("GetDriverStatus received", request);
                DriverStatusResponse driverStatusResponse = new DriverStatusResponse
                {
                    Result = "Unknown",
                    ResultCode = Gateway.Result.UnknownError
                };
                PartnerAccount acct = gateway.GetPartnerAccountByAccessToken(accessToken);
                PartnerAccount user = StorageManager.GetPartnerAccountByAccessToken(accessToken);
                var clientId = "none";
                try
                {
                    if (accessToken != null && acct != null || (user != null && user.Role == Storage.UserRole.admin))
                    {
                        driverStatusResponse = new DriverStatusResponse
                        {
                            Result = "OK",
                            ResultCode = Gateway.Result.OK
                        };
                    }
                    else
                    {
                        driverStatusResponse = new DriverStatusResponse
                        {
                            Result = "Failed",
                            ResultCode = Gateway.Result.AuthenticationError
                        };
                    }
                }
                catch (Exception e)
                {
                    Logger.LogDebug("GetDriverStatus=" + e.Message, e.ToString());
                    driverStatusResponse = new DriverStatusResponse
                    {
                        Result = "Failed",
                        ResultCode = Gateway.Result.UnknownError
                    };
                }
                finally
                {
                    Logger.AddTag("RequestType", "GetDriverStatus");
                    Logger.SetOriginatingId(clientId);
                    Logger.SetServicingId(gateway.ID);
                    Logger.EndRequest(driverStatusResponse);
                }
                return driverStatusResponse;
            }

            public DriverStatusResponse Options(DriverStatus request)
            {
                return new DriverStatusResponse();
            }

        }

        #endregion
    }
}