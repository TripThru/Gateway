using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ServiceStack.Common.Utils;
using Utils;
using CustomIntegrations;
using System.IO;
using RestSharp;
using ServiceStack.Text;
using ServiceStack.Redis;

namespace TripThruCore
{
    public class TripThru : GatewayServer
    {
        static long nextID = 0;
        static public string GenerateUniqueID(string clientID) { nextID++; return nextID.ToString() + "@" + clientID; }
        void CleanUpTrip(string tripID)
        {
            originatingPartnerByTrip.Remove(tripID);
            servicingPartnerByTrip.Remove(tripID);
        }
        public Dictionary<string, Gateway> partners;

        public RedisDictionary<string, string> originatingPartnerByTrip;
        public RedisDictionary<string, string> servicingPartnerByTrip;
        private RedisDictionary<string, List<Zone>> partnerCoverage;
        public readonly TimeSpan missedBookingPeriod = new TimeSpan(0, 30, 0);

        List<Zone> GetPartnerCoverage(string partnerID)
        {
            if (!partnerCoverage.ContainsKey(partnerID))
            {

                Gateway partner = partners[partnerID];
                Gateway.GetPartnerInfoResponse resp = partner.GetPartnerInfo(new Gateway.GetPartnerInfoRequest(ID));
                List<Zone> coverage = new List<Zone>();
                foreach (Fleet f in resp.fleets)
                    coverage.AddRange(f.Coverage);
                partnerCoverage.Add(partner.ID, coverage);
            }
            return partnerCoverage[partnerID];
        }
        public TripThru()
            : base("TripThru", "TripThru")
        {
            //            partners = new RedisStore<string, Gateway>(redis, MemberInfoGetting.GetMemberName(() => partners));
            partners = new Dictionary<string, Gateway>();
            var redisClient = (RedisClient) redis.GetClient();
            originatingPartnerByTrip = new RedisDictionary<string, string>(redisClient, ID + ":" + MemberInfoGetting.GetMemberName(() => originatingPartnerByTrip));
            originatingPartnerByTrip.Clear();
            servicingPartnerByTrip = new RedisDictionary<string, string>(redisClient, ID + "." + MemberInfoGetting.GetMemberName(() => servicingPartnerByTrip));
            servicingPartnerByTrip.Clear();
            partnerCoverage = new RedisDictionary<string, List<Zone>>(redisClient, ID + "." + MemberInfoGetting.GetMemberName(() => partnerCoverage));
            partnerCoverage.Clear();
            partnerAccounts.Clear();
            clientIdByAccessToken.Clear();
            {
                PartnerAccount partnerAccount = new PartnerAccount
                {
                    UserName = "TripThru",
                    Password = "password",
                    Email = "tripthru@tripthru.com",
                    AccessToken = "jaosid1201231",
                    RefreshToken = "jaosid1201231",
                    ClientId = "TripThru",
                    ClientSecret = "23noiasdn2123"
                };
                partnerAccounts[partnerAccount.ClientId] = partnerAccount;
                clientIdByAccessToken[partnerAccount.AccessToken] = partnerAccount.ClientId;
            }
            {
                PartnerAccount partnerAccount = new PartnerAccount
                {
                    UserName = "Swagger Test Client",
                    Password = "password",
                    Email = "testclient@tripthru.com",
                    AccessToken = "demo12345",
                    RefreshToken = "demo12345",
                    ClientId = "testclient@tripthru.com",
                    ClientSecret = "demo12345"
                };
                partnerAccounts[partnerAccount.ClientId] = partnerAccount;
                clientIdByAccessToken[partnerAccount.AccessToken] = partnerAccount.ClientId;
            }

            {
                PartnerAccount partnerAccount = new PartnerAccount
                {
                    UserName = "Luxor Cab",
                    Password = "password",
                    Email = "partner1@tripthru.com",
                    AccessToken = "luxor23noiasdn2123",
                    RefreshToken = "23noiasdn2123",
                    ClientId = "luxor@tripthru.com",
                    ClientSecret = "23noiasdn2123"
                };
                partnerAccounts[partnerAccount.ClientId] = partnerAccount;
                clientIdByAccessToken[partnerAccount.AccessToken] = partnerAccount.ClientId;
            }
            {
                PartnerAccount partnerAccount = new PartnerAccount
                {
                    UserName = "Yellow Cab",
                    Password = "password",
                    Email = "yellowcab@tripthru.com",
                    AccessToken = "yellow12ondazazxx21",
                    RefreshToken = "12ondazazxx21",
                    ClientId = "yellow@tripthru.com",
                    ClientSecret = "12ondazazxx21"
                };
                partnerAccounts[partnerAccount.ClientId] = partnerAccount;
                clientIdByAccessToken[partnerAccount.AccessToken] = partnerAccount.ClientId;
            }

            {
                PartnerAccount partnerAccount = new PartnerAccount
                {
                    UserName = "Metro Cab of Boston",
                    Password = "password",
                    Email = "metro@tripthru.com",
                    AccessToken = "metro12ondazazxx21",
                    RefreshToken = "12ondazazxx21",
                    ClientId = "metro@tripthru.com",
                    ClientSecret = "12ondazazxx21"
                };
                partnerAccounts[partnerAccount.ClientId] = partnerAccount;
                clientIdByAccessToken[partnerAccount.AccessToken] = partnerAccount.ClientId;
            }

            {
                PartnerAccount partnerAccount = new PartnerAccount
                {
                    UserName = "Les Taxi Blues",
                    Password = "password",
                    Email = "lestaxi@tripthru.com",
                    AccessToken = "les12ondazazxx21",
                    RefreshToken = "12ondazazxx21",
                    ClientId = "les@tripthru.com",
                    ClientSecret = "12ondazazxx21"
                };
                partnerAccounts[partnerAccount.ClientId] = partnerAccount;
                clientIdByAccessToken[partnerAccount.AccessToken] = partnerAccount.ClientId;
            }

            {
                PartnerAccount partnerAccount = new PartnerAccount
                {
                    UserName = "Dubai Taxi Corporation",
                    Password = "password",
                    Email = "dubaitaxicorp@tripthru.com",
                    AccessToken = "dubai12ondazazxx21",
                    RefreshToken = "12ondazazxx21",
                    ClientId = "dubai@tripthru.com",
                    ClientSecret = "12ondazazxx21"
                };
                partnerAccounts[partnerAccount.ClientId] = partnerAccount;
                clientIdByAccessToken[partnerAccount.AccessToken] = partnerAccount.ClientId;
            }

            {
                PartnerAccount partnerAccount = new PartnerAccount
                {
                    UserName = "Test TDispatch",
                    Password = "password",
                    Email = "test_tdispatch@tripthru.com",
                    AccessToken = "test_tdispatch12ondazazxx21",
                    RefreshToken = "test_tdispatch12ondazazxx21",
                    ClientId = "test_tdispatch@tripthru.com",
                    ClientSecret = "test_tdispatch12ondazazxx21"
                };
                partnerAccounts[partnerAccount.ClientId] = partnerAccount;
                clientIdByAccessToken[partnerAccount.AccessToken] = partnerAccount.ClientId;
            }

            {
                PartnerAccount partnerAccount = new PartnerAccount
                {
                    UserName = "TripThruWeb",
                    Password = "password",
                    Email = "web@tripthru.com",
                    AccessToken = "webondazazxx21",
                    RefreshToken = "web12ondazazxx21",
                    ClientId = "web@tripthru.com",
                    ClientSecret = "web12ondazazxx21"
                };
                partnerAccounts[partnerAccount.ClientId] = partnerAccount;
                clientIdByAccessToken[partnerAccount.AccessToken] = partnerAccount.ClientId;
            }

            LoadTDispatchIntegrations();

            garbageCleanup = new GarbageCleanup<string>(new TimeSpan(0, 1, 0), CleanUpTrip);
        }
        public override string GetName(string clientID)
        {
            return partners[clientID].name;
        }
        public Gateway GetDestinationPartner(string tripID)
        {
            if (servicingPartnerByTrip.ContainsKey(tripID))
                return partners[servicingPartnerByTrip[tripID]];
            return null;
        }
        public Gateway GetDestinationPartner(string clientID, string tripID)
        {
            if (originatingPartnerByTrip.ContainsKey(tripID))
            {
                Gateway partner = partners[originatingPartnerByTrip[tripID]];
                if (partner.ID == clientID || (servicingPartnerByTrip.ContainsKey(tripID) && partners[servicingPartnerByTrip[tripID]].ID != clientID)) // who's asking?
                {
                    // the originating partner or third party is asking.
                    if (servicingPartnerByTrip.ContainsKey(tripID))
                    {
                        return partners[servicingPartnerByTrip[tripID]];
                    }
                    else
                        throw new Exception("Fatal Error: destination could not be found");
                }
                // the servicing partner must be asking.
                return partner;
            }
            throw new Exception("Fatal Error: destination could not be found");

        }

        public override RegisterPartnerResponse RegisterPartner(Gateway partner)
        {
            try
            {
                requests++;
                partners.Add(partner.ID, partner);
                RegisterPartnerResponse response = new RegisterPartnerResponse(partner.ID);
                return response;
            }
            catch (Exception e)
            {
                exceptions++;
                Logger.Log("Exception=" + e.Message);
                Logger.LogDebug("RegisterPartner=" + e.Message, e.ToString());
                return new RegisterPartnerResponse(result: Result.UnknownError);
            }
        }
        public override GetPartnerInfoResponse GetPartnerInfo(GetPartnerInfoRequest r)
        {
            try
            {
                if (r.fleets != null || r.vehicleTypes != null || r.coverage != null)
                    throw new Exception("Filters currently not supported");
                requests++;
                List<VehicleType> vehicleTypes = new List<VehicleType>();
                List<Fleet> fleets = new List<Fleet>();
                r.clientID = ID;
                foreach (Gateway p in partners.Values)
                {
                    GetPartnerInfoResponse response = p.GetPartnerInfo(r);
                    if (response.result == Result.OK)
                    {
                        fleets.AddRange(response.fleets);
                        vehicleTypes.AddRange(response.vehicleTypes);
                    }
                }
                GetPartnerInfoResponse resp = new GetPartnerInfoResponse(fleets, vehicleTypes);
                return resp;
            }
            catch (Exception e)
            {
                exceptions++;
                Logger.Log("Exception=" + e.Message);
                Logger.LogDebug("GetPartnerInfo=" + e.Message, e.ToString());
                return new GetPartnerInfoResponse(result: Result.UnknownError);
            }
        }
        public override DispatchTripResponse DispatchTrip(DispatchTripRequest r)
        {
            try
            {
                requests++;
                DispatchTripResponse response1;
                if (!activeTrips.Keys.Contains(r.tripID))
                {
                    // Note: GetTrip populates the foreignTripID
                    Gateway partner = null;
                    if (r.partnerID == null)
                    {
                        Logger.Log("Auto mode, so quote trip through all partners");
                        Logger.Tab();
                        // Dispatch to partner with shortest ETA
                        QuoteTripResponse response = QuoteTrip(new QuoteTripRequest(
                            clientID: r.clientID, // TODO: Daniel, fix this when you add authentication
                            pickupLocation: r.pickupLocation,
                            pickupTime: r.pickupTime,
                            passengerID: r.passengerID,
                            passengerName: r.passengerName,
                            luggage: r.luggage,
                            persons: r.persons,
                            dropoffLocation: r.dropoffLocation,
                            waypoints: r.waypoints,
                            paymentMethod: r.paymentMethod,
                            vehicleType: r.vehicleType,
                            maxPrice: r.maxPrice,
                            minRating: r.minRating,
                            partnerID: r.partnerID,
                            fleetID: r.fleetID,
                            driverID: r.driverID));
                        if (response.result == Result.Rejected || response.quotes.Count == 0)
                        {
                            Logger.Log("No partners are available that cover that area");
                            Logger.Untab();
                            rejects++;
                            return new DispatchTripResponse(result: Result.Rejected);
                        }
                        else if (response.result != Result.OK)
                        {
                            Logger.Log("QuoteTrip call failed");
                            Logger.Untab();
                            rejects++;
                            return new DispatchTripResponse(result: response.result);
                        }
                        else
                        {
                            Quote bestQuote = null;
                            DateTime bestETA = r.pickupTime + missedBookingPeriod;
                            // not more than 30 minues late
                            foreach (Quote q in response.quotes)
                            {
                                DateTime eta = (DateTime)q.ETA;
                                if (eta == null) // if no ETA is returned then we assum a certain lateness.
                                    eta = r.pickupTime + missedBookingPeriod - new TimeSpan(0, 1, 0);
                                if (eta.ToUniversalTime() < bestETA.ToUniversalTime())
                                {
                                    bestETA = (DateTime)q.ETA;
                                    bestQuote = q;
                                }
                            }
                            if (bestQuote != null)
                            {
                                partner = partners[bestQuote.PartnerId];
                                r.fleetID = bestQuote.FleetId;
                                Logger.Log("Best quote " + bestQuote + " from " + partner.name);
                            }
                            else
                                Logger.Log("There are no partners to handle this trip within an exceptable service time");

                        }
                        Logger.Untab();
                    }
                    else
                        partner = partners[r.partnerID];

                    if (partner != null)
                    {
                        Gateway client = partners[r.clientID];
                        originatingPartnerByTrip.Add(r.tripID, r.clientID);
                        Logger.Log("Originating partner = " + r.clientID);
                        servicingPartnerByTrip.Add(r.tripID, partner.ID);
                        Logger.Log("Servicing partner = " + partner.name);
                        r.clientID = ID;
                        response1 = partner.DispatchTrip(r);
                        if (response1.result != Result.OK)
                        {
                            Logger.Log("DispatchTrip to " + partner.name + " failed");
                        }
                        else
                        {
                            var trip = new Trip
                            {
                                Id = r.tripID,
                                OriginatingPartnerName = client.name,
                                OriginatingPartnerId = client.ID,
                                ServicingPartnerName = partner.name,
                                ServicingPartnerId = partner.ID,
                                Status = Status.Queued,
                                PickupLocation = r.pickupLocation,
                                PickupTime = r.pickupTime,
                                DropoffLocation = r.dropoffLocation,
                                PassengerName = r.passengerName,
                                VehicleType = r.vehicleType
                            };
                            activeTrips.Add(r.tripID, trip);
                            Logger.Log("Passenger= " + r.passengerName);
                            Logger.Log("Pickup time= " + r.pickupTime);
                            Logger.Log("Pickup location= " + r.pickupLocation);
                            Logger.Log("Dropoff location= " + r.dropoffLocation);
                        }
                    }
                    else
                    {
                        rejects++;
                        response1 = new DispatchTripResponse(result: Result.Rejected);
                    }
                }
                else
                {
                    rejects++;
                    response1 = new DispatchTripResponse(result: Result.Rejected);
                }
                return response1;
            }
            catch (Exception e)
            {
                exceptions++;
                Logger.Log("Exception=" + e.Message);
                Logger.LogDebug("DispatchTrip=" + e.Message, e.ToString());
                return new DispatchTripResponse(result: Result.UnknownError);
            }
        }
        public override QuoteTripResponse QuoteTrip(QuoteTripRequest r)
        {
            try
            {
                requests++;
                var quotes = new List<Quote>();

                foreach (Gateway p in partners.Values)
                {
                    //if (p.ID == r.clientID)
                    //    continue;

                    bool covered = false;
                    foreach (Zone z in GetPartnerCoverage(p.ID))
                    {
                        if (z.IsInside(r.pickupLocation))
                        {
                            covered = true;
                            break;
                        }
                    }

                    if (covered)
                    {
                        string clientID = r.clientID;
                        r.clientID = ID;
                        QuoteTripResponse response = p.QuoteTrip(r);
                        if (response.result == Result.OK)
                        {
                            if (response.quotes != null)
                                quotes.AddRange(response.quotes);
                        }
                        r.clientID = clientID;
                    }
                }
                QuoteTripResponse response1 = new QuoteTripResponse(quotes);
                return response1;
            }
            catch (Exception e)
            {
                exceptions++;
                Logger.Log("Exception=" + e.Message);
                Logger.LogDebug("QuoteTrip=" + e.Message, e.ToString());
                return new QuoteTripResponse(result: Result.UnknownError);
            }
        }
        public override GetTripsResponse GetTrips(GetTripsRequest r)
        {
            try
            {
                requests++;
                var trips = new List<Trip>();
                if (activeTrips.Count > 0)
                {
                    if (r.status != null)
                    {
                        trips.AddRange(activeTrips.Values.Where(trip => trip.Status == r.status).ToList());
                    }
                    else
                    {
                        trips.AddRange(activeTrips.Values);
                    }
                }
                return new GetTripsResponse(trips);
            }
            catch (Exception e)
            {
                exceptions++;
                Logger.LogDebug("GetTrips=" + e.Message, e.ToString());
                return new GetTripsResponse(new List<Trip>(), Result.UnknownError);
            }
        }
        public override GetTripStatusResponse GetTripStatus(GetTripStatusRequest r)
        {
            try
            {
                requests++;
                Gateway partner = GetDestinationPartner(r.clientID, r.tripID);
                if (partner != null)
                {
                    Logger.Log("Destination partner=" + partner.name);
                    r.clientID = ID;
                    GetTripStatusResponse response = partner.GetTripStatus(r);
                    if (response.result == Result.OK)
                    {
                        if (response.status == Status.Complete || response.status == Status.Cancelled ||
                            response.status == Status.Rejected)
                        {
                            DeactivateTrip(r.tripID, (Status) response.status, response.price, response.distance);
                        }
                        else
                        {
                            UpdateTrip(new Trip
                            {
                                Id = r.tripID,
                                FleetId = response.fleetID,
                                FleetName = response.fleetName,
                                DriverId = response.driverID,
                                DriverName = response.driverName,
                                Status = response.status,
                                ETA = response.ETA,
                                Price = response.price,
                                Distance = response.distance
                            });
                        }

                        Logger.Log("Passenger = " + response.passengerName);
                        Logger.Log("Pickup time = " + response.pickupTime);
                        Logger.Log("Pickup location = "+response.pickupLocation);
                        Logger.Log("Dropoff location = " + response.dropoffLocation);
                        Logger.Log("Status = " + response.status);
                        Logger.Log("ETA = " + response.ETA);

                        response.partnerID = partner.ID;
                        response.partnerName = partner.name;
                        response.originatingPartnerName = partners[originatingPartnerByTrip[r.tripID]].name;
                        response.servicingPartnerName = partners[servicingPartnerByTrip[r.tripID]].name;
                        Logger.Log("Originating partner = " + response.originatingPartnerName);
                        Logger.Log("Servicing partner = " + response.servicingPartnerName);
                    }
                    else
                    {
                        Logger.Log("Request to destination partner failed, Result=" + response.result);
                    }
                    return response;
                }
                Logger.Log("Destination partner trip not found, ClientId=" + r.clientID);
                return new GetTripStatusResponse(result: Result.NotFound);
            }
            catch (Exception e)
            {
                exceptions++;
                Logger.Log("Exception=" + e.Message);
                Logger.LogDebug("GetTripStatus = " + e.Message, e.ToString());
                return new GetTripStatusResponse(result: Result.UnknownError);
            }
        }
        public override UpdateTripStatusResponse UpdateTripStatus(UpdateTripStatusRequest r)
        {
            try
            {
                requests++;
                Gateway destPartner = GetDestinationPartner(r.clientID, r.tripID);
                if (destPartner != null)
                {
                    Logger.Log("Destination partner = " + destPartner.name);
                    string clientID = r.clientID;
                    r.clientID = ID;
                    UpdateTripStatusResponse response = destPartner.UpdateTripStatus(r);
                    r.clientID = clientID;
                    if (response.result == Result.OK)
                    {
                        activeTrips[r.tripID].Status = r.status;
                        if (r.status == Status.Complete)
                        {
                            Gateway origPartner = partners[r.clientID];
                            GetTripStatusResponse resp = origPartner.GetTripStatus(new GetTripStatusRequest(r.clientID, r.tripID));
                            r.clientID = clientID;
                            DeactivateTrip(r.tripID, Status.Complete, resp.price, resp.distance);
                        }
                        else if (r.status == Status.Cancelled || r.status == Status.Rejected)
                            DeactivateTrip(r.tripID, r.status);
                    }
                    else
                    {
                        Logger.Log("Request to destination partner failed, Result="+response.result);
                    }
                    return response;
                }
                Logger.Log("Destination partner trip not found, ClientId="+r.clientID);
                return new UpdateTripStatusResponse(result: Result.NotFound);
            }
            catch (Exception e)
            {
                exceptions++;
                Logger.Log("Exception = " + e.Message);
                Logger.LogDebug("UpdateTripStatus = " + e.Message, e.ToString());
                return new UpdateTripStatusResponse(result: Result.UnknownError);
            }
        }

        public class Office
        {
            public bool enabled { get; set; }
            public string ID { get; set; }
            public string name { get; set; }
            public string api_key { get; set; }
            public string fleetAuthorizationCode { get; set; }
            public string fleetAccessToken { get; set; }
            public string fleetRefreshToken { get; set; }
            public string passengerAuthorizationCode { get; set; }
            public string passengerAccessToken { get; set; }
            public string passengerRefreshToken { get; set; }
            public string passengerProxyPK { get; set; }
            public List<Zone> coverage { get; set; }
        }

        public class TDispatchOfficeConfigs
        {

            public List<Office> offices { get; set; }
        }


        public void LoadTDispatchIntegrations()
        {
            string officeConfigsStr = File.ReadAllText("~/Custom Integrations/TDispatchOffices.txt".MapHostAbsolutePath());
            TDispatchOfficeConfigs config = null;
            try
            {
                config = JsonSerializer.DeserializeFromString<TDispatchOfficeConfigs>(officeConfigsStr);
            }
            catch (Exception e)
            {
                Console.WriteLine("TDispatch integration not available");
            }
            if (config != null)
            {
                Gateway tripThru = new GatewayLocalClient(new GatewayLocalServer(this));
                foreach (Office o in config.offices)
                {
                    if (!o.enabled)
                        continue;
                    List<Fleet> fleets = new List<Fleet>();
                    List<Zone> coverage = o.coverage;
                    fleets.Add(new Fleet("TDispatch", "TDispatch", o.name, o.name, coverage));
                    List<VehicleType> vehicleTypes = new List<VehicleType>();

                    TDispatchIntegration partner = new TDispatchIntegration(tripThru, apiKey: o.api_key,
                        fleetAuth: o.fleetAuthorizationCode, fleetAccessToken: o.fleetAccessToken,
                        fleetRefreshToken: o.fleetRefreshToken,
                        passengerAuth: o.passengerAuthorizationCode, passengerAccessToken: o.passengerAccessToken,
                        passengerRefreshToken: o.passengerRefreshToken,
                        passengerProxyPK: o.passengerProxyPK, fleets: fleets, vehicleTypes: vehicleTypes);
                    o.fleetAccessToken = partner.api.FLEET_ACCESS_TOKEN;
                    o.fleetRefreshToken = partner.api.FLEET_REFRESH_TOKEN;
                    o.passengerAccessToken = partner.api.PASSENGER_ACCESS_TOKEN;
                    o.passengerRefreshToken = partner.api.PASSENGER_REFRESH_TOKEN;
                    o.ID = partner.ID;
                    o.name = partner.name;
                    RegisterPartner(new GatewayLocalClient(partner)); // the local client is not necessary, it just encloses the call inside a Begin/End request (for logging)
                }
            }

            //string configStr = JsonSerializer.SerializeToString<TDispatchOfficeConfigs>(config);
            //File.WriteAllText("../../Custom Integrations/TDispatchOffices.txt", configStr);   // We don't need this while testing.  The main purpose is to save the tokens so token reresh not constantly needed

            //TDispatchIntegration sanFranOffice = (TDispatchIntegration) partnersByID["52b4053948efcb7ac1137d41"];
            //DispatchTripResponse response = sanFranOffice.DispatchTrip(new DispatchTripRequest(clientID: "test",
            //    tripID: "test", pickupLocation: new Location(37.78906, -122.402127), pickupTime: DateTime.UtcNow,
            //    passengerID: "test", passengerName: "test-ed", luggage: 1, persons: 1, dropoffLocation: new Location(37.78906, -122.402127), vehicleType: VehicleType.Sedan));

            //TDispatchAPI.Booking booking = sanFranOffice.activeTrips["test"];
            //sanFranOffice.api.RejectBooking(booking.pk);
        }

        public override void Update()
        {
            foreach (Gateway p in partners.Values)
                p.Update();
        }

    }


    public class GatewayLocalClient : Gateway
    {
        Gateway server;

        public GatewayLocalClient(Gateway server)
            : base(server.ID, server.name)
        {
            this.server = server;
        }
        public override Gateway.RegisterPartnerResponse RegisterPartner(Gateway.RegisterPartnerRequest request)
        {
            return server.RegisterPartner(request);
        }

        public override Gateway.GetPartnerInfoResponse GetPartnerInfo(Gateway.GetPartnerInfoRequest request)
        {
            Logger.BeginRequest("GetPartnerInfo sent to " + server.name, request);
            Gateway.GetPartnerInfoResponse response = server.GetPartnerInfo(request);
            Logger.EndRequest(response);
            return response;
        }

        public override Gateway.DispatchTripResponse DispatchTrip(Gateway.DispatchTripRequest request)
        {
            Logger.BeginRequest("DispatchTrip sent to " + server.name, request);
            Gateway.DispatchTripResponse response = server.DispatchTrip(request);
            Logger.EndRequest(response);
            return response;
        }

        public override Gateway.QuoteTripResponse QuoteTrip(Gateway.QuoteTripRequest request)
        {
            Logger.BeginRequest("QuoteTrip sent to " + server.name, request);
            Gateway.QuoteTripResponse response = server.QuoteTrip(request);
            Logger.EndRequest(response);
            return response;

        }

        public override Gateway.GetTripStatusResponse GetTripStatus(Gateway.GetTripStatusRequest request)
        {
            Logger.BeginRequest("GetTripStatus sent to " + server.name, request);
            Gateway.GetTripStatusResponse response = server.GetTripStatus(request);
            Logger.EndRequest(response);
            return response;
        }

        public override Gateway.UpdateTripStatusResponse UpdateTripStatus(Gateway.UpdateTripStatusRequest request)
        {
            Logger.BeginRequest("UpdateTripStatus sent to " + server.name, request);
            UpdateTripStatusResponse response = server.UpdateTripStatus(request);
            Logger.EndRequest(response);
            return response;
        }
        public override void Update()
        {
            server.Update();
        }
        public override void Log()
        {
            server.Log();
        }
    }


    public class GatewayLocalServer : Gateway
    {
        Gateway gateway;

        public GatewayLocalServer(Gateway gateway)
            : base(gateway.ID, gateway.name)
        {
            this.gateway = gateway;
        }
        public override Gateway.RegisterPartnerResponse RegisterPartner(Gateway.RegisterPartnerRequest request)
        {
            return gateway.RegisterPartner(request);
        }

        public override Gateway.GetPartnerInfoResponse GetPartnerInfo(Gateway.GetPartnerInfoRequest request)
        {
            Logger.BeginRequest("GetPartnerInfo received from " + gateway.GetName(request.clientID), request);
            Gateway.GetPartnerInfoResponse response = gateway.GetPartnerInfo(request);
            Logger.EndRequest(response);
            return response;
        }

        public override Gateway.DispatchTripResponse DispatchTrip(Gateway.DispatchTripRequest request)
        {
            Logger.BeginRequest("DispatchTrip received from " + gateway.GetName(request.clientID), request);
            Gateway.DispatchTripResponse response = gateway.DispatchTrip(request);
            Logger.EndRequest(response);
            return response;
        }

        public override Gateway.QuoteTripResponse QuoteTrip(Gateway.QuoteTripRequest request)
        {
            Logger.BeginRequest("QuoteTrip received from " + gateway.GetName(request.clientID), request);
            Gateway.QuoteTripResponse response = gateway.QuoteTrip(request);
            Logger.EndRequest(response);
            return response;

        }

        public override Gateway.GetTripStatusResponse GetTripStatus(Gateway.GetTripStatusRequest request)
        {
            Logger.BeginRequest("GetTripStatus received from " + gateway.GetName(request.clientID), request);
            Gateway.GetTripStatusResponse response = gateway.GetTripStatus(request);
            Logger.EndRequest(response);
            return response;
        }

        public override Gateway.UpdateTripStatusResponse UpdateTripStatus(Gateway.UpdateTripStatusRequest request)
        {
            Logger.BeginRequest("UpdateTripStatus received from " + gateway.GetName(request.clientID), request);
            UpdateTripStatusResponse response = gateway.UpdateTripStatus(request);
            Logger.EndRequest(response);
            return response;
        }
        public override void Update()
        {
            gateway.Update();
        }
        public override void Log()
        {
            gateway.Log();
        }
    }
}
