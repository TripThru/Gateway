using System;
using System.ComponentModel.DataAnnotations;
using System.Data.Odbc;
using System.Linq;
using ServiceStack.Common;
using ServiceStack.Html;
using Utils;
using TripThruCore;


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
                try
                {
                    List<Logger.RequestLog> logList =
                        request.tripID != null
                            ? Logger.Queue.Where(log => log.tripID == request.tripID).ToList().OrderBy(log => log.Time).ToList()
                            : Logger.Queue.ToList();
                    logResponse = new LogResponse
                {
                    Result = "OK",
                    ResultCode = Gateway.Result.OK,
                    LogList = logList
                };
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
        [Authenticate]
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
        [Route("/partner", "GET, PUT, POST, DELETE", Summary = "Partners Service", Notes = "Register your network with TripThru")]
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
            public long? Id { get; set; }
        }

        public class PartnerService : Service
        {
            public PartnerResponse Get(PartnerRequest request) // this
            {
                return Post(request);
            }

            public PartnerResponse Post(PartnerRequest request)
            {
                var accessToken = request.access_token;
                PartnerResponse partnerResponse = new PartnerResponse
                {
                    Result = "Unknown",
                    ResultCode = Gateway.Result.AuthenticationError
                };
                try
                {
                    PartnerAccount acct = gateway.GetPartnerAccountByAccessToken(accessToken);
                    var message = ValidatePartner(request);
                    if (acct != null && message == null)
                    {
                        Logger.BeginRequest("RegisterPartner received from " + acct.UserName, request);
                        acct.Name = request.Name;
                        acct.CallbackUrl = request.CallbackUrl;
                        gateway.RegisterPartner(new GatewayClient(acct.ClientId, request.Name, "jaosid1201231", request.CallbackUrl));

                        partnerResponse = new PartnerResponse
                        {
                            Result = "OK",
                            ResultCode = Gateway.Result.OK,
                            Message = "OK",
                            Id = -1 // what is this used for
                        };
                    }
                    else
                    {
                        Logger.BeginRequest("RegisterPartner received from unknown user", request);
                        string msg;
                        if (message == null)
                        {
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
                            msg = message;
                            partnerResponse.Message = message;
                        }
                        Logger.Log(msg);
                        
                    }
                }
                finally
                {
                    Logger.AddTag("RequestType", "RegisterPartner");
                    Logger.EndRequest(partnerResponse);
                }
                return partnerResponse;
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

        #region Partners

        [Api("Use GET to get a list of partners or POST to create search for partners meeting the filter criteria.")]
        [Route("/partners", "POST, GET")]
        public class Partners : IReturn<PartnersResponse>
        {
            [ApiMember(Name = "access_token", Description = "Access token acquired through OAuth2.0 authorization procedure.  Example: demo12345", ParameterType = "query", DataType = "string", IsRequired = true)]
            public string access_token { get; set; }
        }

        public class PartnersResponse
        {
            public string Result { get; set; }
            public Gateway.Result ResultCode { get; set; }
            public string Message { get; set; }
            public List<Fleet> Fleets { get; set; }
            public List<VehicleType> VehicleTypes { get; set; }
        }

        public class PartnersService : Service
        {

            public PartnersResponse Get(Partners request)
            {
                var accessToken = request.access_token;
                PartnerAccount acct = gateway.GetPartnerAccountByAccessToken(accessToken);
                PartnersResponse partnersResponse = new PartnersResponse
                {
                    Result = "Unknown",
                    ResultCode = Gateway.Result.UnknownError
                };
                var message = ValidatePartners(request);
                var clientId = "none";
                try
                {
                    if (acct != null && message == null)
                    {
                        clientId = acct.ClientId;
                        Logger.BeginRequest("GetPartnerInfo received from " + acct.UserName, request);
                        var response = gateway.GetPartnerInfo(new Gateway.GetPartnerInfoRequest(
                            acct.ClientId
                            ));

                        if (response.result == Gateway.Result.OK)
                        {
                            partnersResponse = new PartnersResponse
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
                            partnersResponse = new PartnersResponse
                            {
                                Result = "Failed",
                                ResultCode = response.result,
                                Message = "Failed"
                            };
                        }
                    }
                    else
                    {
                        Logger.BeginRequest("GetPartnerInfo received from unknown user", request);
                        string msg;
                        if (message == null)
                        {
                            msg = "GET /partners called with invalid access token, ip: " + Request.RemoteIp +
                                  ", Response = Authentication failed";
                            partnersResponse = new PartnersResponse
                            {
                                Result = "Failed",
                                ResultCode = Gateway.Result.AuthenticationError,
                                Message = "Failed"
                            };
                        }
                        else
                        {
                            msg = message;
                            partnersResponse.Message = msg;
                        }
                        Logger.Log(msg);
                        
                    }
                }
                catch (Exception e)
                {
                    Logger.LogDebug("GetPartnerInfo=" + e.Message, e.ToString());
                    partnersResponse = new PartnersResponse
                    {
                        Result = "Failed",
                        ResultCode = Gateway.Result.UnknownError
                    };
                }
                finally
                {
                    Logger.AddTag("RequestType", "GetPartnerInfo");
                    Logger.AddTag("ClientId", clientId);
                    Logger.EndRequest(partnersResponse);
                }
                return partnersResponse;
            }

            private string ValidatePartners(Partners partners)
            {
                if (partners.access_token.IsNullOrEmpty())
                    return "Access Token is Required";
                return null;
            }
        }

        #endregion

        #region Quotes

        [Api(Description = "Use GET to get quotes for a possible trip.")]
        [Route("/quotes", Verbs = "GET", Summary = @"get quotes for a possible trip", Notes = "The standard usage is to first get quotes for a planned trip and then dispatch the trip to your selected fleet and/or driver")]
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
            public List<Quote> Quotes { get; set; }
        }

        public class QuotesService : Service
        {
            public QuotesResponse Get(Quotes request)
            {
                var message = ValidateQuote(request);
                QuotesResponse quotesResponse = new QuotesResponse
                {
                    Result = "Unknown",
                    ResultCode = Gateway.Result.UnknownError
                };
                
                var accessToken = request.access_token;
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
                        Logger.BeginRequest("QuoteTrip received from unknown user", request);
                        string msg;
                        if (message == null)
                        {
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
                    Logger.EndRequest(quotesResponse);
                }
                return quotesResponse;
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

        #region Dispatch

        [Api("Use POST or GET to dispatch a trip to a fleet. Can be used in conjuction with /quotes")]
        [Route("/dispatch", "POST, GET")]
        public class Dispatch : IReturn<DispatchResponse>
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

        public class DispatchResponse
        {
            public string Result { get; set; }
            public Gateway.Result ResultCode { get; set; }
            public string Message { get; set; }
        }

        public class DispatchService : Service
        {
            public DispatchResponse Get(Dispatch request)
            {
                return Post(request);
            }

            public DispatchResponse Post(Dispatch request)
            {
                DispatchResponse dispatchResponse = new DispatchResponse
                {
                    Result = "Unknown",
                    ResultCode = Gateway.Result.UnknownError
                };
                var accessToken = request.access_token;
                var message = ValidateDispatch(request);
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
                            dispatchResponse = new DispatchResponse
                            {
                                Result = "OK",
                                ResultCode = response.result,
                                Message = "OK"
                            };
                        }
                        else
                        {
                            dispatchResponse = new DispatchResponse
                            {
                                Result = "Failed",
                                ResultCode = response.result,
                                Message = "Failed"
                            };
                        }
                    }
                    else
                    {

                        Logger.BeginRequest("DispatchTrip received from unknown user", request, request.TripId);
                        string msg;
                        if (message == null)
                        {
                            msg = "POST /dispatch called with invalid access token, ip: " + Request.RemoteIp +
                                  ", Response = Authentication failed";
                            dispatchResponse = new DispatchResponse
                            {
                                Result = "Failed",
                                ResultCode = Gateway.Result.AuthenticationError,
                                Message = "Failed"
                            };
                        }
                        else
                        {
                            msg = message;
                            dispatchResponse.Message = message;
                        }
                        Logger.Log(msg);
                        
                    }
                }
                catch (Exception e)
                {
                    Logger.LogDebug("DispatchTrip=" + e.Message, e.ToString());
                    dispatchResponse = new DispatchResponse
                    {
                        Result = "Failed",
                        ResultCode = Gateway.Result.UnknownError,
                        Message = "Failed"
                    };
                }
                finally
                {
                    Logger.AddTag("RequestType", "DispatchTrip");
                    Logger.AddTag("ClientId", clientId);
                    Logger.EndRequest(dispatchResponse);
                }
                return dispatchResponse;
            }

            private string ValidateDispatch(Dispatch dispatch)
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
        [Route("/tripstatus", "GET, PUT")]
        public class TripStatus : IReturn<TripStatusResponse>
        {
            [ApiMember(Name = "access_token", Description = "Access token acquired through OAuth2.0 authorization procedure.  Example: demo12345", ParameterType = "query", DataType = "string", IsRequired = true)]
            public string access_token { get; set; }

            [ApiMember(Name = "TripId", Description = "Partner scope unique identifier of the trip (the same as you passed into /dispatch).  Note: it only has to be unique to you.  TripThru will handle any cross-network uniqueness issues.", ParameterType = "query", DataType = "string", IsRequired = true)]
            public string TripId { get; set; }

            [ApiAllowableValues("Status", typeof(Status))]
            [ApiMember(Name = "Status", Description = "Trip status code", ParameterType = "query", DataType = "Status", IsRequired = false, Verb = "PUT")]
            public Status Status { get; set; }

            [ApiMember(Name = "DriverLocationLat", Description = "GPS coordinate latitude of the driver location. Example: 37.786956", ParameterType = "query", DataType = "double", IsRequired = false)]
            public double? DriverLocationLat { get; set; }

            [ApiMember(Name = "DriverLocationLng", Description = "GPS coordinate longitude of the driver location. Example: -122.440279", ParameterType = "query", DataType = "double", IsRequired = false)]
            public double? DriverLocationLng { get; set; }

            // Can we remove this?  I want to prevent partners from passing locations in this form.
            [ApiMember(Name = "DriverLocationAddress", Description = "Address of the driver location", ParameterType = "query", DataType = "string", IsRequired = false)]
            public string DriverLocationAddress { get; set; }

            [ApiMember(Name = "ETA", Description = "Time that driver will arrive at destination. Either pickup location or dropoff location", ParameterType = "query", DataType = "DateTime", IsRequired = false)]
            public DateTime? ETA { get; set; }


            [ApiAllowableValues("Rating", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10")]
            [ApiMember(Name = "Rating", Description = "Rating of the trip from driver's or passenger's perspective", ParameterType = "query", DataType = "int", IsRequired = false)]
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
                Logger.BeginRequest("GetTripStatus received", request);
                TripStatusResponse tripStatusResponse = new TripStatusResponse
                {
                    Result = "Unknown",
                    ResultCode = Gateway.Result.UnknownError
                };

                var accessToken = request.access_token;
                PartnerAccount acct = gateway.GetPartnerAccountByAccessToken(accessToken);
                var message = ValidateTripStatusGet(request);
                var clientId = "none";
                try
                {
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
                        Logger.BeginRequest("GetTripStatus received from unknown user", request, request.TripId);
                        string msg;
                        if (message == null)
                        {
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
                            msg = message;
                            tripStatusResponse.Message = message;
                        }
                        Logger.Log(msg);
                        
                    }
                }
                catch (Exception e)
                {
                    Logger.LogDebug("GetTripStatus=" + e.Message, e.ToString());
                    tripStatusResponse = new TripStatusResponse
                    {
                        Result = "Failed",
                        ResultCode = Gateway.Result.UnknownError,
                        Message = "Failed"
                    };
                }
                finally
                {
                    Logger.AddTag("RequestType", "GetTripStatus");
                    Logger.AddTag("ClientId", clientId);
                    Logger.EndRequest(tripStatusResponse);
                    Logger.Enable();
                }
                return tripStatusResponse;
            }

            public TripStatusResponse Put(TripStatus request)
            {
                TripStatusResponse tripStatusResponse = new TripStatusResponse
                {
                    Result = "Unknown",
                    ResultCode = Gateway.Result.UnknownError
                };
                var accessToken = request.access_token;
                PartnerAccount acct = gateway.GetPartnerAccountByAccessToken(accessToken);
                var message = ValidateTripStatusPut(request);
                var clientId = "none";
                try
                {
                    if (acct != null && message == null)
                    {
                        clientId = acct.ClientId;
                        Logger.BeginRequest("UpdateTripStatus(" + request.Status + ") received from " + acct.UserName, request, request.TripId);

                        Location driverLocation = null;
                        if (request.DriverLocationLat != null && request.DriverLocationLng != null && request.DriverLocationAddress != null)
                        {
                            driverLocation = new Location((double)request.DriverLocationLat, (double)request.DriverLocationLng, request.DriverLocationAddress);
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
                        Logger.BeginRequest("UpdateTripStatus received from unknown user", request, request.TripId);
                        string msg;
                        if (message == null)
                        {
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
                            msg = message;
                            tripStatusResponse.Message = msg;
                        }
                        Logger.Log(msg);
                        
                    }
                }
                catch (Exception e)
                {
                    Logger.LogDebug("UpdateTripStatus=" + e.Message, e.ToString());
                    tripStatusResponse = new TripStatusResponse
                    {
                        Result = "Failed",
                        ResultCode = Gateway.Result.UnknownError
                    };
                }
                finally
                {
                    Logger.AddTag("RequestType", "UpdateTripStatus");
                    Logger.AddTag("ClientId", clientId);
                    Logger.EndRequest(tripStatusResponse);
                }
                return tripStatusResponse;
            }

            private string ValidateTripStatusGet(TripStatus tripStatus)
            {
                if (tripStatus.access_token.IsNullOrEmpty())
                    return "Access Token is Required";
                if (tripStatus.TripId.IsNullOrEmpty())
                    return "Trip Id is Required";
                return null;
            }

            private string ValidateTripStatusPut(TripStatus tripStatus)
            {
                if (tripStatus.access_token.IsNullOrEmpty())
                    return "Access Token is Required";
                if (tripStatus.TripId.IsNullOrEmpty())
                    return "Trip Id is Required";
                if (tripStatus.Status == null)
                    return "Trip Id is Required";
                if (tripStatus.DriverLocationLat == null)
                    return "DriverLocationLat is Required";
                if (tripStatus.DriverLocationLng == null)
                    return "DriverLocationLng is Required";
                if (tripStatus.DriverLocationAddress == null)
                    return "DriverLocationAddress is Required";
                if (tripStatus.ETA == null)
                    return "ETA is Required";
                if (tripStatus.Rating == null)
                    return "Rating is Required";
                return null;
            }

        }

        #endregion

        #region Trips

        [Api("Use GET /trips")]
        [Route("/trips", "GET")]
        public class Trips : IReturn<TripsResponse>
        {
            [ApiAllowableValues("Status", typeof(Status))]
            [ApiMember(Name = "Status", Description = "Get a list of trips with given status", ParameterType = "query", DataType = "Status", IsRequired = false)]
            public Status? Status { get; set; }
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
                Logger.Disable();
                Logger.BeginRequest("GetTrips received", request);
                TripsResponse tripsResponse = new TripsResponse
                {
                    Result = "Unknown",
                    ResultCode = Gateway.Result.UnknownError
                };
                try
                {
                    var response = gateway.GetTrips(new Gateway.GetTripsRequest(null, request.Status));

                    if (response.result == Gateway.Result.OK)
                    {
                        tripsResponse = new TripsResponse
                        {
                            Result = "OK",
                            ResultCode = response.result,
                            Trips = response.trips
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
                    Logger.EndRequest(tripsResponse);
                    Logger.Enable();
                }
                return tripsResponse;
            }

        }

        #endregion

    }
}