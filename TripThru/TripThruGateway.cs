﻿using System;
using System.Collections.Generic;
using System.Data;
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
        private TripsManager tripsManager;

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
        public TripThru(bool enableTDispatch = true, bool async = false)
            : base("TripThru", "TripThru")
        {
            InitializePersistantDataObjects();
            garbageCleanup = new GarbageCleanup<string>(new TimeSpan(0, 1, 0), CleanUpTrip);
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
                        throw new Exception("Fatal Error: Service has not been establish for tripID = " + tripID + ", TripOriginatedWithClient = " + TripOriginatedWithClient(clientID, tripID));
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

        public override RegisterPartnerResponse RegisterPartner(Gateway partner)
        {
            requests++;
            if (!partners.ContainsKey(partner.ID))
                partners.Add(partner.ID, partner);
            else
                partners[partner.ID] = partner;
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
        public List<Zone> GetPartnerCoverage(string partnerID)
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
        public override DispatchTripResponse DispatchTrip(DispatchTripRequest request)
        {
            requests++;
            return this.tripsManager.CreateTrip(request);
        }
        public DispatchTripResponse MakeRejectDispatchResponse(DispatchTripRequest r, Gateway client, Gateway partner)
        {
            return base.MakeRejectDispatchResponse(r, client, partner);
        }

        public override QuoteTripResponse QuoteTrip(QuoteTripRequest request)
        {
            requests++;
            return this.tripsManager.CreateQuote(request);
        }
        public override Gateway.UpdateQuoteResponse UpdateQuote(Gateway.UpdateQuoteRequest request)
        {
            requests++;
            return this.tripsManager.UpdateQuote(request);
        }
        public override Gateway.GetQuoteResponse GetQuote(Gateway.GetQuoteRequest request)
        {
            requests++;
            var quote = StorageManager.GetQuote(request.tripId);
            if (quote != null)
            {
                return new GetQuoteResponse(status: quote.Status, quotes: quote.ReceivedQuotes);
            }
            Logger.Log("Quote id not found");
            Logger.AddTag("ClientId", request.clientID);
            return new GetQuoteResponse(result: Result.NotFound);
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
                    {
                        if (TripHasDriverInitialLocation(r.tripID))
                            AddDriverInitialLocation(response, r.tripID);
                        UpdateActiveTripWithNewTripStatus(r, response);
                    }
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
        private bool TripHasDriverInitialLocation(string tripId)
        {
            return activeTrips[tripId].DriverInitiaLocation != null;
        }
        private void AddDriverInitialLocation(GetTripStatusResponse response, string tripId)
        {
            response.driverInitialLocation = activeTrips[tripId].DriverInitiaLocation;
        }

        public override GetRouteTripResponse GetRouteTrip(GetRouteTripRequest request)
        {
            requests++;
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

        private void UpdateActiveTripWithNewTripStatus(GetTripStatusRequest r, GetTripStatusResponse response)
        {

            Trip trip = new Trip
            {
                Id = r.tripID,
                FleetId = response.fleetID,
                FleetName = response.fleetName,
                DriverId = response.driverID,
                DriverName = response.driverName,
                Status = response.status,
                ETA = response.ETA,
                Price = response.price,

                DriverRouteDuration = response.driverRouteDuration,
                SamplingPercentage = 1

            };
            if (response.status == Status.PickedUp)
                trip.EnrouteDistance = response.distance;
            activeTrips.UpdateTrip(trip);
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
        public override UpdateTripStatusResponse UpdateTripStatus(UpdateTripStatusRequest request)
        {
            requests++;
            return this.tripsManager.UpdateTrip(request);
        }

        public void HealthCheck()
        {
            var tags = new Dictionary<string, string>();
            tags["ActiveTrips"] = this.activeTrips.Count.ToString();
            tags["OriginatingPartnerByTrip"] = this.originatingPartnerByTrip.Count.ToString();
            tags["ServicingPartnerByTrip"] = this.servicingPartnerByTrip.Count.ToString();
            tags["LocationAddresses"] = MapTools.locationAddresses.Count.ToString();
            tags["LocationNames"] = MapTools.locationNames.Count.ToString();
            tags["Garbage"] = this.garbageCleanup.garbage.Count.ToString();
            tags["LoggerQueue"] = Logger.Queue.Count.ToString();
            if (Logger.splunkEnabled)
                tags["SplunkQueue"] = Logger.splunkClient.queue.Count.ToString();


            foreach (Trip trip in activeTrips.Values)
            {
                if (!originatingPartnerByTrip.ContainsKey(trip.Id))
                    Logger.LogDebug("Active trip " + trip + " has no originating partner");
                if (!servicingPartnerByTrip.ContainsKey(trip.Id))
                    Logger.LogDebug("Active trip " + trip + " has no servicing partner");
            }

            Logger.LogDebug("Health check (tripthru latest 2)", null, tags);
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
                if (PartnerHasBeenSpecified(r))
                    partner = SelectedPartner(r);
                MakeTripAndAddItToActive(r, partner);
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
        private void MakeTripAndAddItToActive(Gateway.DispatchTripRequest r, Gateway partner)
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
                State = TripState.New,
                SamplingPercentage = 1
            };
            trip.SetCreation(DateTime.UtcNow);
            tripthru.activeTrips.Add(r.tripID, trip);
            Logger.AddTag("Passenger", r.passengerName);
            Logger.AddTag("Pickup_time", r.pickupTime.ToString());
            Logger.AddTag("Pickup_location,", r.pickupLocation.ToString());
            Logger.AddTag("Dropoff_location", r.dropoffLocation.ToString());
        }

        public Gateway.UpdateTripStatusResponse UpdateTrip(Gateway.UpdateTripStatusRequest r)
        {
            Gateway destPartner = tripthru.GetDestinationPartner(r.clientID, r.tripID);
            if (destPartner == null)
            { 
                Logger.AddTag("ClientId", r.clientID);
                Logger.Log("Destination partner trip not found");
                return new Gateway.UpdateTripStatusResponse(result: TripThruCore.Gateway.Result.NotFound);
            } 
            else if (tripthru.activeTrips.ContainsKey(r.tripID))
            {
                Logger.AddTag("ClientId", r.clientID);
                Logger.Log("Trip id already exists");
                return new Gateway.UpdateTripStatusResponse(result: TripThruCore.Gateway.Result.Rejected);
            }
            else
            {
                Gateway.UpdateTripStatusResponse response = null;
                Logger.AddTag("Destination partner", destPartner.name);
                Logger.SetServicingId(destPartner.ID);
                tripthru.activeTrips[r.tripID].Status = r.status;
                tripthru.activeTrips[r.tripID].IsDirty = true;
                if (tripthru.activeTrips[r.tripID].DriverInitiaLocation == null)
                    tripthru.activeTrips[r.tripID].DriverInitiaLocation = r.driverLocation;
                switch (r.status)
                {
                    case Status.Enroute:
                        tripthru.activeTrips[r.tripID].AddEnrouteLocationList(r.driverLocation);
                        break;
                    case Status.PickedUp:
                        tripthru.activeTrips[r.tripID].AddPickUpLocationList(r.driverLocation);
                        break;
                }
                tripthru.activeTrips.SaveTrip(tripthru.activeTrips[r.tripID]);
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
            return StorageManager.GetQuote(r.tripId) != null;
        }
        private void SaveNewQuoteToStorage(Gateway.QuoteTripRequest r, bool autodispatch = false)
        {
            var tripQuotes = new TripQuotes()
            {
                Id = r.tripId,
                Status = QuoteStatus.New,
                PartnersThatServe = 0,
                QuoteRequest = r,
                autodispatch = autodispatch
            };
            StorageManager.InsertQuote(tripQuotes);
        }

        public Gateway.UpdateQuoteResponse UpdateQuote(Gateway.UpdateQuoteRequest r)
        {
            Gateway.UpdateQuoteResponse response;
            if (StorageManager.GetQuote(r.tripId) != null)
            {
                var quotes = StorageManager.GetQuote(r.tripId);
                quotes.ReceivedQuotes.AddRange(r.quotes);
                quotes.ReceivedUpdatesCount++;
                if (quotes.ReceivedUpdatesCount == quotes.PartnersThatServe)
                    quotes.Status = QuoteStatus.Complete;
                StorageManager.SaveQuote(quotes);
                response = new Gateway.UpdateQuoteResponse();
            }
            else
            {
                response = new Gateway.UpdateQuoteResponse(TripThruCore.Gateway.Result.Rejected);
                Logger.Log("UpdateQuote failed: Unknown quote id");
            }
            return response;
        }

        protected virtual void DispatchTrip(Trip t, Gateway partner, Gateway.DispatchTripRequest request, Action<Trip, Gateway.DispatchTripResponse> responseHandler)
        {
            var response = partner.DispatchTrip(request);
            responseHandler(t, response);
        }
        protected void DispatchTripResponseHandler(Trip t, Gateway.DispatchTripResponse response)
        {
            if (response.result == Gateway.Result.OK)
            {
                t.State = TripState.Dispatched;
                t.IsDirty = true;
                t.MadeDirtyById = tripthru.ID;
            }
            tripthru.activeTrips.SaveTrip(t);
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
            if (response.result == Gateway.Result.OK)
            {
                t.IsDirty = false;
                tripthru.activeTrips.SaveTrip(t);
                if (t.Status == Status.Complete)
                    DeactivateTripAndUpdateStats(t);
            }
        }
        private void DeactivateTripAndUpdateStats(Trip t)
        {
            var tripStatus = tripthru.partners[t.ServicingPartnerId].GetTripStatus(MakeGetTripStatusRequest(t));
            tripthru.DeactivateTripAndUpdateStats(t.Id, (Status)t.Status, tripStatus.price, tripStatus.distance);
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

        protected virtual void ForwardNewQuote(TripQuotes q, Gateway partner, Gateway.QuoteTripRequest request, Action<TripQuotes, Gateway.QuoteTripResponse> responseHandler)
        {
            var response = partner.QuoteTrip(request);
            responseHandler(q, response);
        }
        protected void QuoteTripResponseHandler(TripQuotes q, Gateway.QuoteTripResponse response)
        {
            if (response.result == Gateway.Result.OK)
            {
                q.Status = QuoteStatus.InProgress;
                StorageManager.SaveQuote(q);
            }
        }

        protected virtual void ForwardCompleteQuote(TripQuotes q, Gateway partner, Gateway.UpdateQuoteRequest request, Action<TripQuotes, Gateway.UpdateQuoteResponse> responseHandler)
        {
            var response = partner.UpdateQuote(request);
            responseHandler(q, response);
        }
        private void DispatchAutodispatchTrip(TripQuotes q)
        {
            var bestQuote = SelectBestQuote(q.QuoteRequest, q.ReceivedQuotes);
            var quoteRequest = q.QuoteRequest;
            tripthru.activeTrips[q.Id].ServicingPartnerId = bestQuote.PartnerId;
            tripthru.activeTrips[q.Id].ServicingPartnerName = bestQuote.PartnerName;
            tripthru.activeTrips[q.Id].FleetId = bestQuote.FleetId;
            tripthru.activeTrips[q.Id].FleetName = bestQuote.FleetName;
            tripthru.activeTrips[q.Id].State = TripState.New;
            tripthru.activeTrips.SaveTrip(tripthru.activeTrips[q.Id]);

            Action<Trip, Gateway.DispatchTripResponse> dispatchResponseHandler = DispatchTripResponseHandler;
            Trip t = tripthru.activeTrips[q.Id];
            Gateway.DispatchTripRequest request = MakeDispatchRequest(t);
            DispatchTrip(tripthru.activeTrips[q.Id], tripthru.partners[request.partnerID], request, dispatchResponseHandler);
        }
        protected void UpdateQuoteResponseHandler(TripQuotes q, Gateway.UpdateQuoteResponse response)
        {
            if (response.result == Gateway.Result.OK)
            {
                q.Status = QuoteStatus.Sent;
                StorageManager.SaveQuote(q);
            }
        }
        private Quote SelectBestQuote(Gateway.QuoteTripRequest r, List<Quote> quotes)
        {
            Quote bestQuote = null;
            DateTime bestETA = r.pickupTime + tripthru.missedBookingPeriod;
            // not more than 30 minues late
            foreach (Quote q in quotes)
            {
                DateTime eta = (DateTime)q.ETA;
                if (eta == null) // if no ETA is returned then we assum a certain lateness.
                    eta = r.pickupTime + tripthru.missedBookingPeriod - new TimeSpan(0, 1, 0);
                if (eta.ToUniversalTime() < bestETA.ToUniversalTime())
                {
                    bestETA = (DateTime)q.ETA;
                    bestQuote = q;
                }
            }
            return bestQuote;
        }

        private void NewTripHandler(Trip t)
        {
            if (TripIsAutodispatch(t))
            {
                CreateQuote(MakeQuoteTripRequest(t), true);
                t.State = TripState.Quoting;
                tripthru.activeTrips.SaveTrip(t);
            }
            else
            {
                Action<Trip, Gateway.DispatchTripResponse> responseHandler = DispatchTripResponseHandler;
                var request = MakeDispatchRequest(t);
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
                var partnerId = t.MadeDirtyById == t.ServicingPartnerId ? t.OriginatingPartnerId : t.ServicingPartnerId;
                ForwardTripUpdate(t, tripthru.partners[partnerId], MakeUpdateTripStatusRequest(t), responseHandler);
            }
            else
            {
                UpdateTripStatusResponseHandler(t, new Gateway.UpdateTripStatusResponse());
            }
        }
        private bool TripIsLocal(Trip t)
        {
            return t.OriginatingPartnerId == t.ServicingPartnerId;
        }
        protected Gateway.UpdateTripStatusRequest MakeUpdateTripStatusRequest(Trip t)
        {
            return new Gateway.UpdateTripStatusRequest(
                    clientID: tripthru.ID, tripID: t.Id, status: (Status)t.Status, driverLocation: t.DriverLocation, eta: t.ETA);
        }

        private void NewQuoteHandler(TripQuotes q)
        {
            var request = q.QuoteRequest;
            foreach (TripThruCore.Gateway partner in tripthru.partners.Values.Where(p => p.ID != request.clientID))
            {
                try
                {
                    if (PickupLocationIsServedByPartner(request, partner))
                    {
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
            q.Status = QuoteStatus.InProgress;
            StorageManager.SaveQuote(q);
        }
        private bool PickupLocationIsServedByPartner(Gateway.QuoteTripRequest r, Gateway p)
        {
            bool covered = false;
            foreach (Zone z in tripthru.GetPartnerCoverage(p.ID))
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
            var request = q.QuoteRequest;
            request.clientID = tripthru.ID;
            return request;
        }

        private void CompleteQuoteHandler(TripQuotes q)
        {
            Action<TripQuotes, Gateway.UpdateQuoteResponse> responseHandler = UpdateQuoteResponseHandler;
            if (q.autodispatch)
            {
                DispatchAutodispatchTrip(q);
                q.Status = QuoteStatus.Sent;
                StorageManager.SaveQuote(q);
            }
            else
            {
                ForwardCompleteQuote(q, tripthru.partners[q.QuoteRequest.clientID], MakeUpdateQuoteRequest(q), responseHandler);
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
                _worker.Start();
            }

            private void StartThread()
            {
                try
                {
                    while (true)
                    {
                        var trips = StorageManager.GetTripsByState(TripState.New);
                        foreach (var trip in trips)
                        {
                            new Thread( () => this._tripManager.NewTripHandler(trip) ).Start();
                        }
                        System.Threading.Thread.Sleep(_heartbeat);
                    }
                }
                catch (Exception e)
                {
                    Logger.LogDebug("Dispatch error :" + e.Message, e.StackTrace);
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
                _worker = new Thread(StartThread);
                _worker.Start();
            }

            private void StartThread()
            {
                try
                {
                    while (true)
                    {
                        var trips = StorageManager.GetDirtyTrips();
                        foreach (var trip in trips)
                        {
                            new Thread( () => this._tripManager.DirtyTripHandler(trip) ).Start();
                        }
                        System.Threading.Thread.Sleep(_heartbeat);
                    }
                }
                catch (Exception e)
                {
                    Logger.LogDebug("Update trip error :" + e.Message, e.StackTrace);
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
                _worker = new Thread(StartThread);
                _worker.Start();
            }

            private void StartThread()
            {
                try
                {
                    while (true)
                    {
                        var quotes = StorageManager.GetQuotesByStatus(QuoteStatus.New);
                        foreach (var quote in quotes)
                        {
                            new Thread( () => this._tripManager.NewQuoteHandler(quote) ).Start();
                        }
                        System.Threading.Thread.Sleep(_heartbeat);
                    }
                }
                catch (Exception e)
                {
                    Logger.LogDebug("New quote error :" + e.Message, e.StackTrace);
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
                _worker = new Thread(StartThread);
                _worker.Start();
            }

            private void StartThread()
            {
                try
                {
                    while (true)
                    {
                        var quotes = StorageManager.GetQuotesByStatus(QuoteStatus.Complete);
                        foreach (var quote in quotes)
                        {
                            new Thread( () => this._tripManager.CompleteQuoteHandler(quote) ).Start();
                        }
                        System.Threading.Thread.Sleep(_heartbeat);
                    }
                }
                catch (Exception e)
                {
                    Logger.LogDebug("Complete quote error :" + e.Message, e.StackTrace);
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

        }
        protected virtual void DispatchTrip(Trip t, Gateway partner, Gateway.DispatchTripRequest request, Action<Trip, Gateway.DispatchTripResponse> responseHandler)
        {
            partner.DispatchTripAsync(request,
                response =>
                {
                    responseHandler(t, response);
                }
            );
        }
        protected virtual void ForwardTripUpdate(Trip t, Gateway partner, Gateway.UpdateTripStatusRequest request, Action<Trip, Gateway.UpdateTripStatusResponse> responseHandler)
        {
            partner.UpdateTripStatusAsync(request,
                response =>
                {
                    responseHandler(t, response);
                }
            );
        }

        protected virtual void ForwardNewQuote(TripQuotes q, Gateway partner, Gateway.QuoteTripRequest request, Action<TripQuotes, Gateway.QuoteTripResponse> responseHandler)
        {
            partner.QuoteTripAsync(request,
                response =>
                {
                    responseHandler(q, response);
                }
            );
        }

        protected virtual void ForwardCompleteQuote(TripQuotes q, Gateway partner, Gateway.UpdateQuoteRequest request, Action<TripQuotes, Gateway.UpdateQuoteResponse> responseHandler)
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
