using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
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
        public Dictionary<string, Gateway> partners;

        public RedisDictionary<string, string> originatingPartnerByTrip;
        public RedisDictionary<string, string> servicingPartnerByTrip;
        private RedisDictionary<string, List<Zone>> partnerCoverage;
        public readonly TimeSpan missedBookingPeriod = new TimeSpan(0, 30, 0);
        private TripsManager tripsManager;

        public List<Zone> GetPartnerCoverage(string partnerID)
        {
            if (partnerCoverage.ContainsKey(partnerID))
                return partnerCoverage[partnerID];
            else
                return null;
        }
        public TripThru(bool enableTDispatch = true, bool async = false)
            : base("TripThru", "TripThru")
        {
            InitializePersistantDataObjects();
            //garbageCleanup = new GarbageCleanup<string>(new TimeSpan(0, 1, 0), CleanUpTrip);
            LoadUserAccounts();
            if (enableTDispatch)
                LoadTDispatchIntegrations();
            this.tripsManager = async == true ? new TripsManagerAsync(this) : new TripsManager(this);

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
            if (accounts != null)
            {
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
                        return null;
                }
                return OriginatingPartner(tripID);
            }
            throw new Exception("Fatal Error: Origination has not been established for tripID = " + tripID);

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

        public override RegisterPartnerResponse RegisterPartner(Gateway partner, List<Zone> coverage)
        {
            requests++;
            if (!partners.ContainsKey(partner.ID))
                partners.Add(partner.ID, partner);
            else
                partners[partner.ID] = partner;
            if (partnerCoverage.ContainsKey(partner.ID))
                this.partnerCoverage[partner.ID].Clear();
            else
                this.partnerCoverage[partner.ID] = new List<Zone>();
            this.partnerCoverage[partner.ID].AddRange(coverage);
            
            RegisterPartnerResponse response = new RegisterPartnerResponse(partner.ID);
            return response;
        }
        public override GetPartnerInfoResponse GetPartnerInfo(GetPartnerInfoRequest r)
        {
            requests++;
            if (!partners.ContainsKey(r.clientID))
            {
                Logger.Log("Partner " + r.clientID + " not found.");
                return new GetPartnerInfoResponse(result: Result.NotFound);
            }
            if (r.fleets != null || r.vehicleTypes != null || r.coverage != null)
                throw new Exception("Filters currently not supported");
            List<VehicleType> vehicleTypes = new List<VehicleType>();
            List<Fleet> fleets = new List<Fleet>();
            r.clientID = ID;
            foreach (Gateway p in partners.Values)
            {
                try
                {
                    GetPartnerInfoResponse response = p.GetPartnerInfo(r);
                    if (response.result == Result.OK)
                    {
                        fleets.AddRange(response.fleets);
                        vehicleTypes.AddRange(response.vehicleTypes);
                    }
                }
                catch (Exception e)
                {
                    Logger.LogDebug("Exception getting partner info from " + p.name + ": " + e.ToString());
                }
            }
            GetPartnerInfoResponse resp = new GetPartnerInfoResponse(fleets, vehicleTypes);
            return resp;
        }
        public override DispatchTripResponse DispatchTrip(DispatchTripRequest request)
        {
            requests++;
            if (!partners.ContainsKey(request.clientID))
            {
                Logger.Log("Partner " + request.clientID + " not found.");
                return new DispatchTripResponse(result: Result.NotFound);
            }
            var response = this.tripsManager.CreateTrip(request);
            if (response.result == Result.Rejected)
                rejects++;
            return response;
        }
        public DispatchTripResponse MakeRejectDispatchResponse(DispatchTripRequest r, Gateway client, Gateway partner)
        {
            return base.MakeRejectDispatchResponse(r, client, partner);
        }

        public override QuoteTripResponse QuoteTrip(QuoteTripRequest request)
        {
            requests++;
            if (!partners.ContainsKey(request.clientID))
            {
                Logger.Log("Partner " + request.clientID + " not found.");
                return new QuoteTripResponse(result: Result.NotFound);
            }
            var response = this.tripsManager.CreateQuote(request);
            if (response.result == Result.Rejected)
                rejects++;
            return response;
        }
        public override Gateway.UpdateQuoteResponse UpdateQuote(Gateway.UpdateQuoteRequest request)
        {
            requests++;
            if (!partners.ContainsKey(request.clientID))
            {
                Logger.Log("Partner " + request.clientID + " not found.");
                return new UpdateQuoteResponse(result: Result.NotFound);
            }
            return this.tripsManager.UpdateQuote(request);
        }
        public override Gateway.GetQuoteResponse GetQuote(Gateway.GetQuoteRequest request)
        {
            requests++;
            if (!partners.ContainsKey(request.clientID))
            {
                Logger.Log("Partner " + request.clientID + " not found.");
                return new GetQuoteResponse(result: Result.NotFound);
            }
            var quote = StorageManager.GetQuote(request.tripId);
            if (quote != null)
            {
                return new GetQuoteResponse(status: quote.Status, quotes: quote.ReceivedQuotes);
            }
            Logger.Log("Quote " + request.tripId + " not found");
            Logger.AddTag("ClientId", request.clientID);
            return new GetQuoteResponse(result: Result.NotFound);
        }

        public override GetTripsResponse GetTrips(GetTripsRequest r)
        {
            if (!partners.ContainsKey(r.clientID))
            {
                Logger.Log("Partner " + r.clientID + " not found.");
                return new GetTripsResponse(null, Result.NotFound);
            }
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
            if (!partners.ContainsKey(r.clientID))
            {
                Logger.Log("Partner " + r.clientID + " not found.");
                return new GetTripStatusResponse(result: Result.NotFound);
            }
            var trip = StorageManager.GetTrip(r.tripID);
            if (trip != null)
            {
                return MakeGetTripStatusResponse(trip);
            }
            Logger.Log("Trip " + r.tripID + " not found");
            Logger.AddTag("ClientId", r.clientID);
            return new GetTripStatusResponse(result: Result.NotFound);
        }
        private GetTripStatusResponse MakeGetTripStatusResponse(Trip trip)
        {
            return new GetTripStatusResponse(
                partnerID: trip.OriginatingPartnerId,
                partnerName: trip.OriginatingPartnerName,
                fleetID: trip.FleetId,
                fleetName: trip.FleetName,
                pickupTime: trip.PickupTime,
                pickupLocation: trip.PickupLocation,
                driverID: trip.DriverId,
                driverName: trip.DriverName,
                driverLocation: trip.DriverLocation,
                driverInitialLocation: trip.DriverInitialLocation,
                dropoffTime: trip.DropoffTime,
                dropoffLocation: trip.DropoffLocation,
                vehicleType: trip.VehicleType,
                ETA: trip.ETA,
                distance: trip.EnrouteDistance,
                driverRouteDuration: trip.DriverRouteDuration,
                price: trip.Price,
                status: trip.Status,
                passengerName: trip.PassengerName
            );
        }

        public override GetRouteTripResponse GetRouteTrip(GetRouteTripRequest request)
        {
            GetRouteTripResponse getRouteTripResponse = new GetRouteTripResponse
            {
                result = Result.NotFound
            };
            if (activeTrips.ContainsKey(request.tripID))
            {
                getRouteTripResponse = new GetRouteTripResponse
                {
                    result = Result.OK,
                    OriginatingPartnerId = activeTrips[request.tripID].OriginatingPartnerId,
                    ServicingPartnerId = activeTrips[request.tripID].ServicingPartnerId,
                    HistoryEnrouteList = activeTrips[request.tripID].GetEnrouteLocationList(),
                    HistoryPickupList = activeTrips[request.tripID].GetPickupLocationList()
                };
            }
            return getRouteTripResponse;
        }

        public override UpdateTripStatusResponse UpdateTripStatus(UpdateTripStatusRequest request)
        {
            requests++;
            if (!partners.ContainsKey(request.clientID))
            {
                Logger.Log("Partner " + request.clientID + " not found.");
                return new UpdateTripStatusResponse(result: Result.NotFound);
            }
            return this.tripsManager.UpdateTrip(request);
        }

        public void HealthCheck()
        {
            var tags = new Dictionary<string, string>();
            tags["ActiveTrips"] = this.activeTrips.Count.ToString();
            tags["ActiveQuotes"] = this.activeQuotes.Count.ToString();
            tags["OriginatingPartnerByTrip"] = this.originatingPartnerByTrip.Count.ToString();
            tags["ServicingPartnerByTrip"] = this.servicingPartnerByTrip.Count.ToString();
            tags["LocationAddresses"] = MapTools.locationAddresses.Count.ToString();
            tags["LocationNames"] = MapTools.locationNames.Count.ToString();
            //tags["Garbage"] = this.garbageCleanup.garbage.Count.ToString();
            tags["LoggerQueue"] = Logger.Queue.Count.ToString();
            tags["Memory"] = (System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / 1048576).ToString() + "Mb";
            tags["RunningThreads"] = Process.GetCurrentProcess().Threads.Count.ToString();
            if (Logger.splunkEnabled)
                tags["SplunkQueue"] = Logger.splunkClient.queue.Count.ToString();

            Logger.LogDebug("Health check TripThru", null, tags);
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
            RegisterPartner(new GatewayLocalClient(partner), coverage); // the local client is not necessary, it just encloses the call inside a Begin/End request (for logging)
        }

        public override void Update()
        {
            foreach (Gateway p in partners.Values)
                p.Update();
        }

    }

    public class TripsManager
    {

        private TimeSpan monitorsInteval;
        private TripThru tripthru;
        public TripsManager(TripThru tripthru)
        {
            this.monitorsInteval = new TimeSpan(0, 0, 5);
            this.tripthru = tripthru;
            new TripDispatcherThread(this, monitorsInteval);
            new TripUpdaterThread(this, monitorsInteval);
            new NewQuotesHandlerThread(this, monitorsInteval);
            new CompleteQuotesHandlerThread(this, monitorsInteval);
        }

        public Gateway.DispatchTripResponse CreateTrip(Gateway.DispatchTripRequest r)
        {
            Gateway.DispatchTripResponse response;
            if (TripIsNotAlreadyActive(r))
            {
                Gateway partner = null;
                bool isLocal = false;
                if (PartnerHasBeenSpecified(r))
                {
                    partner = SelectedPartner(r);
                    RecordTripServicingPartner(r, partner);
                    isLocal = partner.ID == r.clientID;
                }
                MakeTripAndAddItToActive(r, partner, isLocal);
                RecordTripOriginatingPartner(r);
                response = new Gateway.DispatchTripResponse();
            }
            else
            {
                response = tripthru.MakeRejectDispatchResponse(r, tripthru.partners[r.clientID], null);
                Logger.Log("DispatchTrip failed: Trip already active");
            }
            return response;
        }
        private Gateway SelectedPartner(Gateway.DispatchTripRequest r)
        {
            return tripthru.partners[r.partnerID];
        }
        private bool PartnerHasBeenSpecified(Gateway.DispatchTripRequest r)
        {
            return r.partnerID != null;
        }
        private bool TripIsNotAlreadyActive(Gateway.DispatchTripRequest r)
        {
            return !tripthru.activeTrips.ContainsKey(r.tripID);
        }
        private static bool PartnerHasNotBeenSpecified(Gateway.DispatchTripRequest r)
        {
            return r.partnerID == null;
        }
        private void MakeTripAndAddItToActive(Gateway.DispatchTripRequest r, Gateway partner, bool isLocal = false)
        {
            Gateway client = tripthru.partners[r.clientID];
            var trip = new Trip
            {
                Id = r.tripID,
                OriginatingPartnerName = client.name,
                OriginatingPartnerId = client.ID,
                ServicingPartnerName = partner != null ? partner.name : null,
                ServicingPartnerId = partner != null ? partner.ID : null,
                Status = Status.Queued,
                PickupLocation = r.pickupLocation,
                PickupTime = r.pickupTime,
                DropoffLocation = r.dropoffLocation,
                PassengerName = r.passengerName,
                VehicleType = r.vehicleType,
                IsDirty = false,
                State = isLocal ? TripState.Dispatched : TripState.New,
                SamplingPercentage = 1
            };
            trip.SetCreation(DateTime.UtcNow);
            tripthru.activeTrips.Insert(r.tripID, trip);
            Logger.AddTag("Passenger", r.passengerName);
            Logger.AddTag("Pickup_time", r.pickupTime.ToString());
            Logger.AddTag("Pickup_location,", r.pickupLocation.ToString());
            Logger.AddTag("Dropoff_location", r.dropoffLocation.ToString());
        }
        private void RecordTripOriginatingPartner(Gateway.DispatchTripRequest r)
        {
            Logger.Log("RecordTripOriginatingPartner: Request=" + r + ", partner=" + r.clientID);
            tripthru.originatingPartnerByTrip.Add(r.tripID, r.clientID);
            Logger.AddTag("Originating partner", r.clientID);
        }
        private void RecordTripServicingPartner(Gateway.DispatchTripRequest r, Gateway partner)
        {
            Logger.Log("RecordTripServicingPartner: Request=" + r + ", partner=" + partner.name);
            tripthru.servicingPartnerByTrip.Add(r.tripID, partner.ID);
            Logger.AddTag("Servicing partner", partner.ID);
        }

        public Gateway.UpdateTripStatusResponse UpdateTrip(Gateway.UpdateTripStatusRequest r)
        {
            if (!tripthru.activeTrips.ContainsKey(r.tripID))
            {
                Logger.AddTag("ClientId", r.clientID);
                Logger.Log("Trip id not found");
                return new Gateway.UpdateTripStatusResponse(result: TripThruCore.Gateway.Result.NotFound);
            }

            Gateway destPartner = tripthru.GetDestinationPartner(r.clientID, r.tripID);
            if (destPartner == null)
            {
                if (r.status == Status.Cancelled && tripthru.originatingPartnerByTrip.ContainsKey(r.tripID) && r.clientID == tripthru.originatingPartnerByTrip[r.tripID])
                {
                    var trip = tripthru.activeTrips[r.tripID];
                    trip.Status = r.status;
                    tripthru.activeTrips.Update(trip);
                    return new Gateway.UpdateTripStatusResponse();
                }
                else
                { 
                    Logger.AddTag("ClientId", r.clientID);
                    Logger.Log("Destination partner trip not found");
                    return new Gateway.UpdateTripStatusResponse(result: TripThruCore.Gateway.Result.NotFound);
                }
            }
            else
            {
                var trip = tripthru.activeTrips[r.tripID];
                Gateway.UpdateTripStatusResponse response = null;
                Logger.AddTag("Destination partner", destPartner.name);
                Logger.SetServicingId(destPartner.ID);
                trip.Status = r.status;
                trip.DriverLocation = r.driverLocation;
                trip.ETA = r.eta;
                trip.IsDirty = true;
                trip.MadeDirtyById = r.clientID;
                if (trip.DriverInitialLocation == null)
                    trip.DriverInitialLocation = r.driverLocation;
                switch (r.status)
                {
                    case Status.Enroute:
                        trip.AddEnrouteLocationList(r.driverLocation);
                        break;
                    case Status.PickedUp:
                        trip.AddPickUpLocationList(r.driverLocation);
                        break;
                }
                tripthru.activeTrips.Update(trip);
                return new Gateway.UpdateTripStatusResponse(result: TripThruCore.Gateway.Result.OK);
            }
        }
        private bool ShouldForwardTripUpdate(Gateway.UpdateTripStatusRequest r, Gateway destinationPartner)
        {
            return r.clientID != destinationPartner.ID;
        }
        private void ChangeClientIDToTripThru(Gateway.UpdateTripStatusRequest r)
        {
            r.clientID = tripthru.ID;
        }

        public Gateway.QuoteTripResponse CreateQuote(Gateway.QuoteTripRequest r, bool autodispatch = false)
        {
            Logger.Log("Creating new quote for " + r.tripId);
            Gateway.QuoteTripResponse response;
            if (!QuoteExists(r))
            {
                SaveNewQuoteToStorage(r, autodispatch);
                response = new Gateway.QuoteTripResponse();
            }
            else
            {
                response = new Gateway.QuoteTripResponse(TripThruCore.Gateway.Result.Rejected);
                Logger.Log("QuoteTrip failed: already exists");
            }
            return response;
        }
        private bool QuoteExists(Gateway.QuoteTripRequest r)
        {
            return tripthru.activeQuotes.ContainsKey(r.tripId);
        }
        private void SaveNewQuoteToStorage(Gateway.QuoteTripRequest r, bool autodispatch = false)
        {
            var tripQuotes = new TripQuotes()
            {
                Id = r.tripId,
                Status = QuoteStatus.New,
                PartnersThatServe = 0,
                QuoteRequest = r,
                Autodispatch = autodispatch,
                ReceivedQuotes = new List<Quote>(),
                ReceivedUpdatesCount = 0
            };
            tripthru.activeQuotes.Insert(tripQuotes.Id, tripQuotes);
        }

        public Gateway.UpdateQuoteResponse UpdateQuote(Gateway.UpdateQuoteRequest r)
        {
            Gateway.UpdateQuoteResponse response;
            var quotes = tripthru.activeQuotes.ContainsKey(r.tripId) ? tripthru.activeQuotes[r.tripId].Clone() : null;
            if (quotes != null)
            {
                if (quotes.ReceivedQuotes == null)
                    quotes.ReceivedQuotes = new List<Quote>();
                quotes.ReceivedQuotes.AddRange(r.quotes);
                quotes.ReceivedUpdatesCount++;
                if (quotes.ReceivedUpdatesCount == quotes.PartnersThatServe)
                    quotes.Status = QuoteStatus.Complete;
                tripthru.activeQuotes.Update(quotes);
                response = new Gateway.UpdateQuoteResponse();
            }
            else
            {
                response = new Gateway.UpdateQuoteResponse(TripThruCore.Gateway.Result.NotFound);
                Logger.Log("UpdateQuote failed: Unknown quote id");
            }
            return response;
        }

        protected virtual void DispatchTrip(Trip t, Gateway partner, Gateway.DispatchTripRequest request, Action<Trip, Gateway.DispatchTripResponse> responseHandler)
        {
            var response = partner.DispatchTrip(request);
            if(!tripthru.servicingPartnerByTrip.ContainsKey(t.Id))
                RecordTripServicingPartner(request, partner);
            responseHandler(t, response);
        }
        protected void DispatchTripResponseHandler(Trip t, Gateway.DispatchTripResponse response)
        {
            Logger.BeginRequest("DispatchTrip response received. Trip: " + t.Id, null, t.Id);
            if (response.result == Gateway.Result.OK || response.result == Gateway.Result.Rejected)
            {
                Logger.Log("Successful request, changing trip state to Dispatched. Result: " + response.result);
                t.State = TripState.Dispatched;
                t.IsDirty = true;
                t.MadeDirtyById = tripthru.ID;
                Logger.Log("Activating isDirtyFlag and setting MadeDirtyBy TripThru");
                if (response.result == Gateway.Result.Rejected)
                    t.Status = Status.Rejected;
                    Logger.Log("Changing trip state to " + t.Status);
            }
            else
            {
                Logger.Log("Unsuccessful request, so setting trip to New state to retry dispatch. Result: " + response.result);
                t.State = TripState.New; //Try to dispatch again
            }
            Logger.EndRequest(response);
            tripthru.activeTrips.Update(t);
        }
        protected Gateway.DispatchTripRequest MakeDispatchRequest(Trip t)
        {
            return new Gateway.DispatchTripRequest(
                clientID: tripthru.ID, tripID: t.Id, pickupLocation: t.PickupLocation, pickupTime: (DateTime)t.PickupTime,
                passengerName: t.PassengerName, dropoffLocation: t.DropoffLocation, vehicleType: t.VehicleType,
                partnerID: t.ServicingPartnerId, fleetID: t.FleetId, driverID: t.DriverId
            );
        }

        protected virtual void ForwardTripUpdate(Trip t, Gateway partner, Gateway.UpdateTripStatusRequest request, Action<Trip, Gateway.UpdateTripStatusResponse> responseHandler)
        {
            var response = partner.UpdateTripStatus(request);
            responseHandler(t, response);
        }
        protected void UpdateTripStatusResponseHandler(Trip t, Gateway.UpdateTripStatusResponse response)
        {
            Logger.BeginRequest("UpdateTripStatus response received. Trip: " + t.Id, null, t.Id);
            if (response.result == Gateway.Result.OK)
            {
                Logger.Log("Successful request. Result: " + response.result);
            }
            else
            {
                Logger.Log("Unsuccesful request, rectivating isDirty flag. Result: " + response.result);
                t.IsDirty = true;
                tripthru.activeTrips.Update(t);
            }
            Logger.EndRequest(response);
        }

        protected virtual void ForwardNewQuote(TripQuotes q, Gateway partner, Gateway.QuoteTripRequest request, Action<TripQuotes, Gateway.QuoteTripResponse> responseHandler)
        {
            var response = partner.QuoteTrip(request);
            responseHandler(q, response);
        }
        protected void QuoteTripResponseHandler(TripQuotes q, Gateway.QuoteTripResponse response)
        {
            Logger.BeginRequest("QuoteTrip response received. Trip: " + q.Id, null, q.Id);
            if (q.Status != QuoteStatus.InProgress)
            {
                Logger.Log("Changing quote status to InProgress");
                q.Status = QuoteStatus.InProgress;
                tripthru.activeQuotes.Update(q);
            }
            if (response.result == Gateway.Result.OK)
            {
                Logger.Log("Successful request. Result: " + response.result);
            }
            else
            {
                Logger.Log("Unsuccessful request. Result: " + response.result);
            }
            Logger.EndRequest(response);
        }

        protected virtual void ForwardCompleteQuote(TripQuotes q, Gateway partner, Gateway.UpdateQuoteRequest request, Action<TripQuotes, Gateway.UpdateQuoteResponse> responseHandler)
        {
            var response = partner.UpdateQuote(request);
            responseHandler(q, response);
        }
        private void SelectBestQuoteAndSetServicingPartnerToTrip(TripQuotes q)
        {
            Logger.Log("Selecting best quote for autodispatch. Quote: " + q.Id);
            var bestQuote = SelectBestQuote(q.QuoteRequest, q.ReceivedQuotes);
            Trip t = tripthru.activeTrips[q.Id];
            if (bestQuote != null)
            {
                Logger.Log("Best quote found from partner " + bestQuote.PartnerId + " with ETA " + bestQuote.ETA);
                tripthru.activeTrips[q.Id].ServicingPartnerId = bestQuote.PartnerId;
                tripthru.activeTrips[q.Id].ServicingPartnerName = bestQuote.PartnerName;
                RecordTripServicingPartner(MakeDispatchRequest(t), tripthru.partners[bestQuote.PartnerId]);
                tripthru.activeTrips[q.Id].FleetId = bestQuote.FleetId;
                tripthru.activeTrips[q.Id].FleetName = bestQuote.FleetName;
                tripthru.activeTrips[q.Id].State = TripState.New;
                tripthru.activeTrips.Update(tripthru.activeTrips[q.Id]);
            }
            else
            {
                Logger.Log("No quote within acceptable ETA was found. Changing trip status to rejected trip " + q.Id);
                t.State = TripState.Dispatched;
                t.Status = Status.Rejected;
                t.IsDirty = true;
                t.MadeDirtyById = tripthru.ID;
                tripthru.activeTrips.Update(t);
            }
        }
        protected void UpdateQuoteResponseHandler(TripQuotes q, Gateway.UpdateQuoteResponse response)
        {
            Logger.BeginRequest("UpdateQuote response received. Trip: " + q.Id, null, q.Id);
            if (response.result == Gateway.Result.OK)
            {
                Logger.Log("Successful request, changing quote status to Sent. Result: " + response.result);
                q.Status = QuoteStatus.Sent;
                tripthru.activeQuotes.Update(q);
                tripthru.activeQuotes.Remove(q.Id);
            }
            else
            {
                Logger.Log("Unsuccessful request, changing quote status to Complete to retry. Result: " + response.result);
                q.Status = QuoteStatus.Complete;
                tripthru.activeQuotes.Update(q);
            }
            Logger.EndRequest(response);
        }
        private Quote SelectBestQuote(Gateway.QuoteTripRequest r, List<Quote> quotes)
        {
            Quote bestQuote = null;
            DateTime bestETA = r.pickupTime + tripthru.missedBookingPeriod;
            // not more than 30 minues late
            if (quotes != null)
            {
                foreach (Quote q in quotes)
                {
                    DateTime eta = (DateTime)q.ETA;
                    if (eta == null) // if no ETA is returned then we assume a certain lateness.
                        eta = r.pickupTime + tripthru.missedBookingPeriod - new TimeSpan(0, 1, 0);
                    if (eta.ToUniversalTime() < bestETA.ToUniversalTime())
                    {
                        bestETA = (DateTime)q.ETA;
                        bestQuote = q;
                    }
                }
            }
            return bestQuote;
        }

        private void NewTripHandler(Trip t)
        {
            if (TripIsAutodispatch(t))
            {
                Logger.Log("Trip is autodispatch, create quote and change state to Quoting");
                CreateQuote(MakeQuoteTripRequest(t), true);
                t.State = TripState.Quoting;
                tripthru.activeTrips.Update(t);
            }
            else
            {
                Logger.Log("Dispatching trip");
                Action<Trip, Gateway.DispatchTripResponse> responseHandler = DispatchTripResponseHandler;
                var request = MakeDispatchRequest(t);
                t.State = TripState.Dispatching;
                tripthru.activeTrips.Update(t);
                DispatchTrip(t, tripthru.partners[request.partnerID], request, responseHandler);
            }
        }
        private bool TripIsAutodispatch(Trip t)
        {
            return t.ServicingPartnerId == null;
        }
        private Gateway.QuoteTripRequest MakeQuoteTripRequest(Trip t)
        {
            return new Gateway.QuoteTripRequest(
                clientID: t.OriginatingPartnerId, id: t.Id, pickupLocation: t.PickupLocation, pickupTime: (DateTime)t.PickupTime,
                passengerName: t.PassengerName, dropoffLocation: t.DropoffLocation, vehicleType: t.VehicleType
            );
        }

        private void DirtyTripHandler(Trip t)
        {
            if (!TripIsLocal(t))
            {
                Action<Trip, Gateway.UpdateTripStatusResponse> responseHandler = UpdateTripStatusResponseHandler;
                var partnerId = t.MadeDirtyById == t.OriginatingPartnerId ? t.ServicingPartnerId : t.OriginatingPartnerId;
                Logger.Log("Notifying update to partner " + partnerId + ". Trip: " + t.Id);
                t.IsDirty = false;
                tripthru.activeTrips.Update(t);
                Logger.Log("Deactivating isDirty flag");
                ForwardTripUpdate(t, tripthru.partners[partnerId], MakeUpdateTripStatusRequest(t), responseHandler);
            }
            else
            {
                Logger.Log("Trip " + t.Id + " is local so no need to notify partner");
                UpdateTripStatusResponseHandler(t, new Gateway.UpdateTripStatusResponse());
                t.IsDirty = false;
                tripthru.activeTrips.Update(t);
            }
            if (TripHasNonActiveStatus(t))
            {
                Logger.Log("Deactivating " + t.Status +" trip");
                t.IsDirty = false;
                tripthru.activeTrips.Update(t);
                DeactivateTripAndUpdateStats(t);
            }
        }
        private bool TripIsLocal(Trip t)
        {
            return t.OriginatingPartnerId == t.ServicingPartnerId;
        }
        private bool TripHasNonActiveStatus(Trip t)
        {
            return t.Status == Status.Complete || t.Status == Status.Cancelled || t.Status == Status.Rejected;
        }
        protected Gateway.UpdateTripStatusRequest MakeUpdateTripStatusRequest(Trip t)
        {
            return new Gateway.UpdateTripStatusRequest(
                    clientID: tripthru.ID, tripID: t.Id, status: (Status)t.Status, driverLocation: t.DriverLocation, eta: t.ETA);
        }
        public void DeactivateTripAndUpdateStats(Trip t)
        {
            Gateway.GetTripStatusResponse tripStatus = null;
            double? price = 0;
            double? distance = 0;
            if (t.Status == Status.Complete)
            {
                var partner = tripthru.partners[t.ServicingPartnerId];
                Logger.Log("Getting trip stats from servicing partner " + partner.ID);
                tripStatus = partner.GetTripStatus(MakeGetTripStatusRequest(t));
                price = tripStatus.price != null ? tripStatus.price : 0;
                distance =  tripStatus.distance != null ? tripStatus.distance : 0;
                Logger.Log("Stats received. Price: " + price + ", Distance: " + distance);
            }
            CleanUpTrip(t.Id);
            tripthru.DeactivateTripAndUpdateStats(t.Id, (Status)t.Status, price, distance);
        }
        void CleanUpTrip(string tripID)
        {
            Logger.LogDebug("Cleaning up trip " + tripID);
            tripthru.originatingPartnerByTrip.Remove(tripID);
            tripthru.servicingPartnerByTrip.Remove(tripID);
        }
        private Gateway.GetTripStatusResponse GetPriceAndDistanceDetailsFromClient(Gateway.UpdateTripStatusRequest r)
        {
            var resp = tripthru.partners[r.clientID].GetTripStatus(new Gateway.GetTripStatusRequest(r.clientID, r.tripID));
            return resp;
        }
        private Gateway.GetTripStatusRequest MakeGetTripStatusRequest(Trip t)
        {
            return new Gateway.GetTripStatusRequest(clientID: tripthru.ID, tripID: t.Id);
        }

        private void NewQuoteHandler(TripQuotes q)
        {
            var request = q.QuoteRequest;
            q.Status = QuoteStatus.InProgress;
            tripthru.activeQuotes.Update(q);
            foreach (TripThruCore.Gateway partner in tripthru.partners.Values.Where(p => p.ID != request.clientID))
            {
                try
                {
                    if (PickupLocationIsServedByPartner(request, partner))
                    {
                        Logger.Log("Sending quote request to partner " + partner.ID);
                        Action<TripQuotes, Gateway.QuoteTripResponse> responseHandler = QuoteTripResponseHandler;
                        ForwardNewQuote(q, partner, MakeQuoteTripRequest(q), responseHandler);
                        q.PartnersThatServe++;
                    }
                }
                catch (Exception e)
                {
                    Logger.Log("Exception quoting " + partner.name + ": " + e.ToString());
                }
            }
            if (q.PartnersThatServe == 0)
            {
                Logger.Log("No partners available. Changing quote to complete.");
                q.Status = QuoteStatus.Complete;
            }
            tripthru.activeQuotes.Update(q);
        }
        private bool PickupLocationIsServedByPartner(Gateway.QuoteTripRequest r, Gateway p)
        {
            bool covered = false;
            
            var coverage = tripthru.GetPartnerCoverage(p.ID);
            if (coverage == null)
            {
                Logger.Log("Partner " + p.ID + " doesn't have any coverage zones");
                return false;
            }
            foreach (Zone z in coverage)
            {
                if (z.IsInside(r.pickupLocation))
                {
                    covered = true;
                    break;
                }
            }
            return covered;
        }
        private Gateway.QuoteTripRequest MakeQuoteTripRequest(TripQuotes q){
            var r = q.QuoteRequest;
            var request = new Gateway.QuoteTripRequest(
                    clientID: tripthru.ID, id: r.tripId, pickupLocation: r.pickupLocation, pickupTime: r.pickupTime,
                    passengerName: r.passengerName, dropoffLocation: r.dropoffLocation, vehicleType: r.vehicleType
                );
            return request;
        }

        private void CompleteQuoteHandler(TripQuotes q)
        {
            if (q.Autodispatch)
            {
                Logger.Log("Selecting best quote and changing quote state to sent");
                q.Status = QuoteStatus.Sent;
                tripthru.activeQuotes.Update(q);
                tripthru.activeQuotes.Remove(q.Id);
                SelectBestQuoteAndSetServicingPartnerToTrip(q);
            }
            else
            {
                var partner = tripthru.partners[q.QuoteRequest.clientID];
                Logger.Log("Sending Complete UpdateQuote request to partner " + partner.ID);
                Action<TripQuotes, Gateway.UpdateQuoteResponse> responseHandler = UpdateQuoteResponseHandler;
                q.Status = QuoteStatus.Sending;
                tripthru.activeQuotes.Update(q);
                ForwardCompleteQuote(q, partner, MakeUpdateQuoteRequest(q), responseHandler);
            }
        }
        private Gateway.UpdateQuoteRequest MakeUpdateQuoteRequest(TripQuotes q)
        {
            return new Gateway.UpdateQuoteRequest(
                clientID: tripthru.ID,
                tripId: q.Id,
                quotes: q.ReceivedQuotes
            );
        }

        public class TripDispatcherThread : IDisposable
        {
            private TripsManager _tripManager;
            private TimeSpan _heartbeat;
            private Thread _worker;
            private volatile bool _workerTerminateSignal = false;

            public TripDispatcherThread(TripsManager tripManager, TimeSpan heartbeat)
            {
                this._tripManager = tripManager;
                this._heartbeat = heartbeat;
                _worker = new Thread(StartThread);
                _worker.IsBackground = true;
                _worker.Start();
            }

            private void StartThread()
            {
                try
                {
                    while (true)
                    {
                        var trips = _tripManager.tripthru.activeTrips.GetTripsByState(TripState.New);
                        foreach (var trip in trips.ToList())
                        {
                            var t = _tripManager.tripthru.activeTrips[trip.Id];
                            new Thread( () => {
                                try
                                {
                                    Logger.BeginRequest("Processing trip " + t.Id + (t.ServicingPartnerId == null ? " for autodispatch quoting" : " for dispatch"), null, t.Id);
                                    this._tripManager.NewTripHandler(t);
                                    Logger.EndRequest(null);
                                }
                                catch (Exception e)
                                {
                                    Logger.LogDebug("New trip processing exception (" + t.Id + ")", e.ToString());
                                }
                            }).Start();
                        }
                        System.Threading.Thread.Sleep(_heartbeat);
                    }
                }
                catch (Exception e)
                {
                    Logger.LogDebug("TripDispatcherThread exception");
                }
            }

            public void Dispose()
            {
                Logger.LogDebug("TripDispatcher disposed");
            }
        }

        public class TripUpdaterThread : IDisposable
        {
            private TripsManager _tripManager;
            private TimeSpan _heartbeat;
            private Thread _worker;
            private volatile bool _workerTerminateSignal = false;

            public TripUpdaterThread(TripsManager tripManager, TimeSpan heartbeat)
            {
                this._tripManager = tripManager;
                this._heartbeat = heartbeat;
                _worker = new Thread(StartThread);
                _worker.IsBackground = true;
                _worker.Start();
            }

            private void StartThread()
            {
                try
                {
                    while (true)
                    {
                        var trips = _tripManager.tripthru.activeTrips.GetDirtyTrips();
                        foreach (var trip in trips.ToList())
                        {
                            var t = _tripManager.tripthru.activeTrips[trip.Id];
                            new Thread(() =>
                            {
                                try
                                {
                                    Logger.BeginRequest("Processing dirty trip " + "(" + t.Status + ", MadeDirtyBy: " + t.MadeDirtyById + ") " + t.Id, null, t.Id);
                                    this._tripManager.DirtyTripHandler(t);
                                    Logger.EndRequest(null);
                                }
                                catch (Exception e)
                                {
                                    Logger.LogDebug("Dirty trip processing exception (" + t.Id + ")", e.ToString());
                                }
                            }).Start();
                        }
                        System.Threading.Thread.Sleep(_heartbeat);
                    }
                }
                catch (Exception e)
                {
                    Logger.LogDebug("TripUpdaterThread exception");
                }
            }

            public void Dispose()
            {
                Logger.LogDebug("TripUpdater disposed");
            }
        }

        public class NewQuotesHandlerThread : IDisposable
        {
            private TripsManager _tripManager;
            private TimeSpan _heartbeat;
            private Thread _worker;
            private volatile bool _workerTerminateSignal = false;

            public NewQuotesHandlerThread(TripsManager tripManager, TimeSpan heartbeat)
            {
                this._tripManager = tripManager;
                this._heartbeat = heartbeat;
                _worker = new Thread(StartThread);
                _worker.IsBackground = true;
                _worker.Start();
            }

            private void StartThread()
            {
                while (true)
                {
                    try
                    {
                        var quotes = _tripManager.tripthru.activeQuotes.GetQuotesByStatus(QuoteStatus.New);
                        foreach (var quote in quotes.ToList())
                        {
                            var q = _tripManager.tripthru.activeQuotes[quote.Id];
                            new Thread( () => {
                                try { 
                                    Logger.BeginRequest("Processing new quote " + q.Id, null , q.Id);
                                    this._tripManager.NewQuoteHandler(q);
                                    Logger.EndRequest(null);
                                }
                                catch (Exception e)
                                {
                                    Logger.LogDebug("New quote processing exception (" + q.Id + ")", e.ToString());
                                }
                            }).Start();
                        }
                        System.Threading.Thread.Sleep(_heartbeat);
                    }
                    catch (Exception e)
                    {
                        Logger.LogDebug("NewQuotesHandlerThread exception");
                    }
                }
            }

            public void Dispose()
            {
                Logger.LogDebug("NewQuotesHandler disposed");
            }
        }

        public class CompleteQuotesHandlerThread : IDisposable
        {
            private TripsManager _tripManager;
            private TimeSpan _heartbeat;
            private Thread _worker;
            private volatile bool _workerTerminateSignal = false;

            public CompleteQuotesHandlerThread(TripsManager tripManager, TimeSpan heartbeat)
            {
                this._tripManager = tripManager;
                this._heartbeat = heartbeat;
                _worker = new Thread(StartThread);
                _worker.IsBackground = true;
                _worker.Start();
            }

            private void StartThread()
            {
                while (true)
                {
                    try
                    {
                        var quotes = _tripManager.tripthru.activeQuotes.GetQuotesByStatus(QuoteStatus.Complete);
                        foreach (var quote in quotes.ToList())
                        {
                            var q = _tripManager.tripthru.activeQuotes[quote.Id];
                            new Thread( () => {
                                try
                                {
                                    Logger.BeginRequest("Processing completed quote " + q.Id, null, q.Id);
                                    this._tripManager.CompleteQuoteHandler(q);
                                    Logger.EndRequest(null);
                                }
                                catch (Exception e)
                                {
                                    Logger.LogDebug("Complete quote processing exception (" + q.Id + ")", e.ToString());
                                }

                            }).Start();
                        }
                        System.Threading.Thread.Sleep(_heartbeat);
                    }
                    catch (Exception e)
                    {
                        Logger.LogDebug("CompleteQuotesHandlerThread exception");
                    }
                }
            }

            public void Dispose()
            {
                Logger.LogDebug("CompleteQuotesHandler disposed");
            }
        }

    }

    public class TripsManagerAsync : TripsManager
    {
        public TripsManagerAsync(TripThru tripthru)
            : base(tripthru)
        {
            Logger.LogDebug("Starting async trip manager");
        }
        protected override void DispatchTrip(Trip t, Gateway partner, Gateway.DispatchTripRequest request, Action<Trip, Gateway.DispatchTripResponse> responseHandler)
        {
            partner.DispatchTripAsync(request,
                response =>
                {
                    responseHandler(t, response);
                }
            );
        }
        protected override void ForwardTripUpdate(Trip t, Gateway partner, Gateway.UpdateTripStatusRequest request, Action<Trip, Gateway.UpdateTripStatusResponse> responseHandler)
        {
            partner.UpdateTripStatusAsync(request,
                response =>
                {
                    responseHandler(t, response);
                }
            );
        }

        protected override void ForwardNewQuote(TripQuotes q, Gateway partner, Gateway.QuoteTripRequest request, Action<TripQuotes, Gateway.QuoteTripResponse> responseHandler)
        {
            partner.QuoteTripAsync(request,
                response =>
                {
                    responseHandler(q, response);
                }
            );
        }

        protected override void ForwardCompleteQuote(TripQuotes q, Gateway partner, Gateway.UpdateQuoteRequest request, Action<TripQuotes, Gateway.UpdateQuoteResponse> responseHandler)
        {
            partner.UpdateQuoteAsync(request,
                response =>
                {
                    responseHandler(q, response);
                }
            );
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
            _worker.IsBackground = true;
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
            _worker.IsBackground = true;
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
