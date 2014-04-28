using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using ServiceStack.Common.Utils;
using Utils;
using CustomIntegrations;
using System.IO;
using RestSharp;
using ServiceStack.Text;
using ServiceStack.Redis;
using TripThruCore.Storage;

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
                if (resp.result == Result.OK) 
                    foreach (Fleet f in resp.fleets)
                        coverage.AddRange(f.Coverage);
                partnerCoverage.Add(partner.ID, coverage);
            }
            return partnerCoverage[partnerID];
        }
        public TripThru(bool enableTDispatch = true)
            : base("TripThru", "TripThru")
        {
            InitializePersistantDataObjects();
            garbageCleanup = new GarbageCleanup<string>(new TimeSpan(0, 1, 0), CleanUpTrip);

            LoadUserAccounts();

            if (enableTDispatch)
                LoadTDispatchIntegrations();

            new PartnersUpdateThread(partners);
            new HealthCheckThread(this);
        }

        private void InitializePersistantDataObjects()
        {
            partners = new Dictionary<string, Gateway>();
            var redisClient = (RedisClient)redis.GetClient();
            originatingPartnerByTrip = new RedisDictionary<string, string>(redisClient, ID + ":" + MemberInfoGetting.GetMemberName(() => originatingPartnerByTrip));
            originatingPartnerByTrip.Clear();
            servicingPartnerByTrip = new RedisDictionary<string, string>(redisClient, ID + "." + MemberInfoGetting.GetMemberName(() => servicingPartnerByTrip));
            servicingPartnerByTrip.Clear();
            partnerCoverage = new RedisDictionary<string, List<Zone>>(redisClient, ID + "." + MemberInfoGetting.GetMemberName(() => partnerCoverage));
            partnerCoverage.Clear();
            partnerAccounts.Clear();
            clientIdByAccessToken.Clear();
        }

        private void LoadUserAccounts()
        {
            var accounts = StorageManager.GetPartnerAccounts();
            if(accounts != null)
                foreach (PartnerAccount account in accounts)
                {
                    if (Storage.Storage.UserRole.partner != account.Role)
                        continue;
                    if (!partnerAccounts.ContainsKey(account.ClientId))
                        partnerAccounts[account.ClientId] = account;
                    if (!clientIdByAccessToken.ContainsKey(account.AccessToken))
                        clientIdByAccessToken[account.AccessToken] = account.ClientId;
                }
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
            if (OriginationHasBeenEstablished(tripID))
            {
                if (TripOriginatedWithClient(clientID, tripID) || (ServiceHasBeenEstablished(tripID) && ClientIsNotServicingTheTrip(clientID, tripID)))
                {
                    if (ServiceHasBeenEstablished(tripID))
                        return ServicingPartner(tripID);
                    else
                        throw new Exception("Fatal Error: destination could not be found");
                }
                return OriginatingPartner(tripID);
            }
            throw new Exception("Fatal Error: destination could not be found");

        }

        private Gateway ServicingPartner(string tripID)
        {
            return partners[servicingPartnerByTrip[tripID]];
        }

        private bool OriginationHasBeenEstablished(string tripID)
        {
            return originatingPartnerByTrip.ContainsKey(tripID);
        }

        private Gateway OriginatingPartner(string tripID)
        {
            return partners[originatingPartnerByTrip[tripID]];
        }

        private bool ServiceHasBeenEstablished(string tripID)
        {
            return servicingPartnerByTrip.ContainsKey(tripID);
        }

        private bool TripOriginatedWithClient(string clientID, string tripID)
        {
            return partners[originatingPartnerByTrip[tripID]].ID == clientID;
        }

        private bool ClientIsNotServicingTheTrip(string clientID, string tripID)
        {
            return partners[servicingPartnerByTrip[tripID]].ID != clientID;
        }

        public override RegisterPartnerResponse RegisterPartner(Gateway partner)
        {
            requests++;
            if (!partners.ContainsKey(partner.ID))
                partners.Add(partner.ID, partner);
            RegisterPartnerResponse response = new RegisterPartnerResponse(partner.ID);
            return response;
        }
        public override GetPartnerInfoResponse GetPartnerInfo(GetPartnerInfoRequest r)
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
        public override DispatchTripResponse DispatchTrip(DispatchTripRequest r)
        {
            requests++;
            DispatchTripResponse response;
            if (TripIsNotAlreadyActive(r))
            {
                // Note: GetTrip populates the foreignTripID
                Gateway partner = null;
                if (PartnerHasNotBeenSpecified(r))
                    response = AutoDispatchTrip(r, ref partner);
                else
                    partner = SelectedPartner(r);

                if (PartnerHasBeenSelected(partner))
                {
                    RecordTripOriginatingAndServicingPartner(r, partner);
                    var partnerClientId = r.clientID; 
                    ChangeTheClientIDToTripThru(r);
                    response = partner.DispatchTrip(r);
                    r.clientID = partnerClientId;
                    if (response.result != Result.OK)
                        Logger.Log("DispatchTrip to " + partner.name + " failed");
                    else
                        MakeTripAndAddItToActive(r, partner);
                }
                else
                    response = MakeRejectDispatchResponse();
            }
            else
                response = MakeRejectDispatchResponse();
            return response;
        }

        private void MakeTripAndAddItToActive(DispatchTripRequest r, Gateway partner)
        {
            Gateway client = partners[r.clientID];
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
            Logger.AddTag("Passenger", r.passengerName);
            Logger.AddTag("Pickup_time", r.pickupTime.ToString());
            Logger.AddTag("Pickup_location,", r.pickupLocation.ToString());
            Logger.AddTag("Dropoff_location", r.dropoffLocation.ToString());
        }

        private void RecordTripOriginatingAndServicingPartner(DispatchTripRequest r, Gateway partner)
        {
            originatingPartnerByTrip.Add(r.tripID, r.clientID);
            Logger.AddTag("Originating partner", r.clientID);
            servicingPartnerByTrip.Add(r.tripID, partner.ID);
            Logger.AddTag("Servicing partner", partner.name);
            Logger.SetServicingId(partner.ID);
        }

        private void ChangeTheClientIDToTripThru(DispatchTripRequest r)
        {
            r.clientID = ID;
        }

        private bool TripIsNotAlreadyActive(DispatchTripRequest r)
        {
            return !activeTrips.ContainsKey(r.tripID);
        }

        private static bool PartnerHasBeenSelected(Gateway partner)
        {
            return partner != null;
        }

        private Gateway SelectedPartner(DispatchTripRequest r)
        {
            return partners[r.partnerID];
        }

        private static bool PartnerHasNotBeenSpecified(DispatchTripRequest r)
        {
            return r.partnerID == null;
        }

        private DispatchTripResponse AutoDispatchTrip(DispatchTripRequest r, ref Gateway partner)
        {
            DispatchTripResponse response = new DispatchTripResponse(result: Result.UnknownError);
            Logger.Log("Auto mode, so quote trip through all partners");
            Logger.Tab();
            // Dispatch to partner with shortest ETA
            QuoteTripResponse quoteTripResponse = BroadcastQuoteRequestsToAllPartners(r);
            if (BroadcastQuoteWasRejected(quoteTripResponse))
                response = HandleRejectDispatchResponse(response);
            else if (quoteTripResponse.result != Result.OK)
                response = HandleQuoteBroadcastFailedResponse(response, quoteTripResponse);
            else
                partner = SelectThePartnerWithBestQuote(r, partner, quoteTripResponse);
            Logger.Untab();
            return response;
        }

        private static bool BroadcastQuoteWasRejected(QuoteTripResponse response)
        {
            return response.result == Result.Rejected || response.quotes.Count == 0;
        }

        private DispatchTripResponse HandleQuoteBroadcastFailedResponse(DispatchTripResponse response1, QuoteTripResponse response)
        {
            Logger.Log("QuoteTrip call failed");
            Logger.Untab();
            rejects++;
            response1 = new DispatchTripResponse(result: response.result); return response1;
        }

        private DispatchTripResponse HandleRejectDispatchResponse(DispatchTripResponse response1)
        {
            Logger.Log("No partners are available that cover that area");
            Logger.Untab();
            rejects++;
            response1 = new DispatchTripResponse(result: Result.Rejected); return response1;
        }

        private QuoteTripResponse BroadcastQuoteRequestsToAllPartners(DispatchTripRequest r)
        {
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
            return response;
        }

        private Gateway SelectThePartnerWithBestQuote(DispatchTripRequest r, Gateway partner, QuoteTripResponse response)
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
                Logger.Log("There are no partners to handle this trip within an acceptable service time"); 
            return partner;
        }
        public override QuoteTripResponse QuoteTrip(QuoteTripRequest request)
        {
            requests++;
            var quotes = new List<Quote>();

            foreach (Gateway partner in partners.Values)
            {
                if (partner.ID == request.clientID)
                    continue;
                if (PickupLocationIsServedByPartner(request, partner))
                    RequestQuotesFromPartnerAndAdd(request, quotes, partner);
            }
            QuoteTripResponse response1 = new QuoteTripResponse(quotes);
            return response1;
        }

        private void RequestQuotesFromPartnerAndAdd(QuoteTripRequest request, List<Quote> quotes, Gateway partner)
        {
            string savedClientID = request.clientID;
            request.clientID = ID;
            QuoteTripResponse response = partner.QuoteTrip(request);
            if (response.result == Result.OK)
            {
                if (response.quotes != null)
                    quotes.AddRange(response.quotes);
            }
            request.clientID = savedClientID;
        }

        private bool PickupLocationIsServedByPartner(QuoteTripRequest r, Gateway p)
        {
            bool covered = false;
            foreach (Zone z in GetPartnerCoverage(p.ID))
            {
                if (z.IsInside(r.pickupLocation))
                {
                    covered = true;
                    break;
                }
            }
            return covered;
        }
        public override GetTripsResponse GetTrips(GetTripsRequest r)
        {
            var trips = new List<Trip>();
            if (activeTrips.Count > 0)
            {
                if (r.status != null)
                    trips.AddRange(activeTrips.Values.Where(trip => trip.Status == r.status).ToList());
                else
                    trips.AddRange(activeTrips.Values);
            }
            return new GetTripsResponse(trips);
        }
        public override GetTripStatusResponse GetTripStatus(GetTripStatusRequest r)
        {
            requests++;
            Gateway partner = GetDestinationPartner(r.clientID, r.tripID);
            if (partner != null)
            {
                Logger.AddTag("Destination_partner", partner.name);
                Logger.SetServicingId(partner.ID);
                r.clientID = ID;
                GetTripStatusResponse response = partner.GetTripStatus(r);
                if (response.result == Result.OK)
                {
                    if (TripHasNonActiveStatus(response))
                        DeactivateTripAndUpdateStats(r.tripID, (Status)response.status, response.price, response.distance);
                    else
                        UpdateActiveTripWithNewTripStatus(r, response);
                    MakeGetTripStatusResponse(r, partner, response);
                }
                else
                {
                    Logger.Log("Request to destination partner failed, Result=" + response.result);
                }
                return response;
            }
            Logger.Log("Destination partner trip not found");
            Logger.AddTag("ClientId", r.clientID);
            return new GetTripStatusResponse(result: Result.NotFound);
        }

        public override GetRouteTripResponse GetRouteTrip(GetRouteTripRequest request)
        {
            requests++;
            GetRouteTripResponse getRouteTripResponse = new GetRouteTripResponse
            {
                result = Result.NotFound
            };
            if(activeTrips.ContainsKey(request.tripID))
            getRouteTripResponse = new GetRouteTripResponse
            {
                result = Result.OK,
                OriginatingPartnerId = activeTrips[request.tripID].OriginatingPartnerId,
                ServicingPartnerId = activeTrips[request.tripID].ServicingPartnerId,
                HistoryEnrouteList = activeTrips[request.tripID].GetEnrouteLocatinList(),
                HistoryPickUpList = activeTrips[request.tripID].GetPickUpLocatinList()
            };
            return getRouteTripResponse;
        }

        private void UpdateActiveTripWithNewTripStatus(GetTripStatusRequest r, GetTripStatusResponse response)
        {
            UpdateActiveTrip(new Trip
            {
                Id = r.tripID,
                FleetId = response.fleetID,
                FleetName = response.fleetName,
                DriverId = response.driverID,
                DriverName = response.driverName,
                Status = response.status,
                ETA = response.ETA,
                Price = response.price,
                Distance = response.distance,
                DriverRouteDuration = response.driverRouteDuration
            });
        }

        private void MakeGetTripStatusResponse(GetTripStatusRequest r, Gateway partner, GetTripStatusResponse response)
        {
            Logger.AddTag("Passenger", response.passengerName);
            Logger.AddTag("Pickup time", response.pickupTime.ToString());
            Logger.AddTag("Pickup location", response.pickupLocation.ToString());
            Logger.AddTag("Dropoff location", response.dropoffLocation.ToString());
            Logger.AddTag("Status", response.status.ToString());
            Logger.AddTag("ETA", response.ETA.ToString());

            response.partnerID = partner.ID;
            response.partnerName = partner.name;
            response.originatingPartnerName = partners[originatingPartnerByTrip[r.tripID]].name;
            response.servicingPartnerName = partners[servicingPartnerByTrip[r.tripID]].name;
            Logger.AddTag("Originating partner", response.originatingPartnerName);
            Logger.AddTag("Servicing partner", response.servicingPartnerName);
        }

        private static bool TripHasNonActiveStatus(GetTripStatusResponse response)
        {
            return response.status == Status.Complete || response.status == Status.Cancelled || response.status == Status.Rejected;
        }
        public override UpdateTripStatusResponse UpdateTripStatus(UpdateTripStatusRequest r)
        {
            requests++;
            Gateway destPartner = GetDestinationPartner(r.clientID, r.tripID);
            if (destPartner != null)
            {
                Logger.AddTag("Destination partner", destPartner.name);
                Logger.SetServicingId(destPartner.ID);
                string originClientID = r.clientID;
                ChangeClientIDToTripThru(r);
                UpdateTripStatusResponse response = destPartner.UpdateTripStatus(r);
                r.clientID = originClientID;
                if (SuccesAndTripStillActive(r, response))
                {
                    if (r.driverLocation != null)
                    {
                        if (r.status == Status.Dispatched || r.status == Status.Confirmed)
                            activeTrips[r.tripID].DriverInitiaLocation = r.driverLocation;
                    }
                    activeTrips[r.tripID].Status = r.status;
                    switch (r.status)
                    {
                        case Status.Enroute:
                            activeTrips[r.tripID].AddEnrouteLocationList(r.driverLocation);
                            break;
                        case Status.PickedUp:
                            activeTrips[r.tripID].AddPickUpLocationList(r.driverLocation);
                            break;
                        case Status.Complete:
                        {
                            GetTripStatusResponse resp = GetPriceAndDistanceDetailsFromClient(r);
                            DeactivateTripAndUpdateStats(r.tripID, Status.Complete, resp.price, resp.distance);
                        }
                            break;
                        case Status.Rejected:
                        case Status.Cancelled:
                            DeactivateTripAndUpdateStats(r.tripID, r.status);
                            break;
                    }
                }
                else
                    Logger.Log("Request to destination partner failed, Result=" + response.result);
                return response;
            }
            Logger.Log("Destination partner trip not found");
            Logger.AddTag("ClientId", r.clientID);
            return new UpdateTripStatusResponse(result: Result.NotFound);
        }

        private bool SuccesAndTripStillActive(UpdateTripStatusRequest r, UpdateTripStatusResponse response)
        {
            return response.result == Result.OK && activeTrips.ContainsKey(r.tripID);
        }

        private GetTripStatusResponse GetPriceAndDistanceDetailsFromClient(UpdateTripStatusRequest r)
        {
            GetTripStatusResponse resp = partners[r.clientID].GetTripStatus(new GetTripStatusRequest(r.clientID, r.tripID));
            return resp;
        }

        private void ChangeClientIDToTripThru(UpdateTripStatusRequest r)
        {
            r.clientID = ID;
        }

        public void HealthCheck()
        {
            var tags = new Dictionary<string, string>();
            tags["ActiveTrips"] = this.activeTrips.Count.ToString();
            tags["OriginatingPartnerByTrip"] = this.originatingPartnerByTrip.Count.ToString();
            tags["ServicingPartnerByTrip"] = this.servicingPartnerByTrip.Count.ToString();
            tags["Routes"] = MapTools.routes.Count.ToString();
            tags["LocationAddresses"] = MapTools.locationAddresses.Count.ToString();
            tags["LocationNames"] = MapTools.locationNames.Count.ToString();
            tags["Garbage"] = this.garbageCleanup.garbage.Count.ToString();
            tags["LoggerQueue"] = Logger.Queue.Count.ToString();
            if(Logger.splunkEnabled)
                tags["SplunkQueue"] = Logger.splunkClient.queue.Count.ToString();
            

            foreach (Trip trip in activeTrips.Values)
            {
                if (!originatingPartnerByTrip.ContainsKey(trip.Id))
                    tags["Bad Health"] = "Active trip " + trip + " has no originating partner";
                if (!servicingPartnerByTrip.ContainsKey(trip.Id))
                    tags["Bad Health"] = "Active trip " + trip + " has no servicing partner";
            }

            Logger.LogDebug("Health check", null, tags);
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
                    if (o.enabled)
                        RegisterOfficeConfiguration(tripThru, o);
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

        private void RegisterOfficeConfiguration(Gateway tripThru, Office o)
        {
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
            Logger.BeginRequest("RegisterPartner sent to " + server.name, request);
            Gateway.RegisterPartnerResponse response = server.RegisterPartner(request);
            Logger.EndRequest(response);
            return response;
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

    public class HealthCheckThread : IDisposable
    {
        private TripThru _tripthruGateway;
        private TimeSpan _heartbeat = new TimeSpan(0, 1, 0);
        private Thread _worker;
        private volatile bool _workerTerminateSignal = false;

        public HealthCheckThread(TripThru tripthruGateway)
        {
            this._tripthruGateway = tripthruGateway;
            _worker = new Thread(StartThread);
            _worker.Start();
        }

        private void StartThread()
        {
            try
            {
                while (true)
                {
                    _tripthruGateway.HealthCheck();
                    System.Threading.Thread.Sleep(_heartbeat);
                }
            }
            catch (Exception e)
            {
                Logger.LogDebug("TripThru health check error :" + e.Message, e.StackTrace);
            }
        }

        public void Dispose()
        {
            Logger.LogDebug("HealthCheckThread disposed");
        }
    }

    public class PartnersUpdateThread : IDisposable
    {
        private Dictionary<string, Gateway> _partners;
        private TimeSpan _heartbeat = new TimeSpan(0, 0, 30);
        private Thread _worker;
        private volatile bool _workerTerminateSignal = false;

        public PartnersUpdateThread(Dictionary<string, Gateway> partners)
        {
            _partners = partners;
            _worker = new Thread(StartThread);
            _worker.Start();
        }

        private void StartThread()
        {
            try
            {
                while (true)
                {
                    try
                    {
                        foreach (var partner in _partners.Values)
                        {
                            lock (partner)
                            {
                                partner.Update();
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        if (e.Message != "Not supported")
                        {
                            Logger.LogDebug("PartnersUpdateThread error :" + e.Message, e.StackTrace);
                        }
                    }
                    System.Threading.Thread.Sleep(_heartbeat);
                }
            }
            catch (Exception e)
            {
                Logger.LogDebug("PartnersUpdateThread initialization error :" + e.Message, e.StackTrace);
            }
        }

        public void Dispose()
        {
            Logger.LogDebug("PartnersUpdateThread disposed");
        }
    }
}
