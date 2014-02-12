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
        public List<Gateway> partners;
        public Dictionary<string, Gateway> partnersByID;
        public Dictionary<string, Gateway> originatingPartnerByTrip;
        public Dictionary<string, Gateway> servicingPartnerByTrip;
        private Dictionary<string, List<Zone>> _partnerCoverage;

        List<Zone> GetPartnerCoverage(string partnerID)
        {
            if (!_partnerCoverage.ContainsKey(partnerID))
            {
                Gateway partner = partnersByID[partnerID];
                Gateway.GetPartnerInfoResponse resp = partner.GetPartnerInfo(new Gateway.GetPartnerInfoRequest(ID));
                List<Zone> coverage = new List<Zone>();
                foreach (Fleet f in resp.fleets)
                    coverage.AddRange(f.Coverage);
                _partnerCoverage.Add(partner.ID, coverage);
            }
            return _partnerCoverage[partnerID];
        }
        public TimeSpan missedBookingPeriod = new TimeSpan(0, 30, 0);
        public TripThru()
            : base("TripThru", "TripThru")
        {
            partnersByID = new Dictionary<string, Gateway>();
            originatingPartnerByTrip = new Dictionary<string, Gateway>();
            servicingPartnerByTrip = new Dictionary<string, Gateway>();
            _partnerCoverage = new Dictionary<string, List<Zone>>();
            partners = new List<Gateway>();

            LoadTDispatchIntegrations();

            garbageCleanup = new GarbageCleanup<string>(new TimeSpan(0, 1, 0), CleanUpTrip);
        }
        public override string GetName(string clientID)
        {
            return partnersByID[clientID].name;
        }
        public Gateway GetDestinationPartner(string tripID)
        {
            if (servicingPartnerByTrip.ContainsKey(tripID))
                return servicingPartnerByTrip[tripID];
            return null;
        }
        public Gateway GetDestinationPartner(string clientID, string tripID)
        {
            if (originatingPartnerByTrip.ContainsKey(tripID))
            {
                Gateway partner = originatingPartnerByTrip[tripID];
                if (partner.ID == clientID) // who's asking?
                {
                    // the originating partner is asking.
                    if (servicingPartnerByTrip.ContainsKey(tripID))
                        return servicingPartnerByTrip[tripID];
                    else
                        throw new Exception("Fatal Error: destination could not be found");
                }
                // the servicing partner must be asking.
                return partner;
            }
            throw new Exception("Fatal Error: destination could not be found");

        }
        public void AddPartner(Gateway partner)
        {
            partners.Add(partner);
            partnersByID.Add(partner.ID, partner);
        }

        public override RegisterPartnerResponse RegisterPartner(Gateway partner)
        {
            try
            {
                requests++;
                AddPartner(partner);
                RegisterPartnerResponse response = new RegisterPartnerResponse(partner.ID);
                return response;
            }
            catch (Exception e)
            {
                exceptions++;
                Logger.Log("Exception=" + e.Message);
                Console.WriteLine(e.StackTrace);
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
                foreach (Gateway p in partners)
                {
                    Logger.Tab();
                    GetPartnerInfoResponse response = p.GetPartnerInfo(r);
                    if (response.result == Result.OK)
                    {
                        fleets.AddRange(response.fleets);
                        vehicleTypes.AddRange(response.vehicleTypes);
                    }
                    Logger.Untab();
                }
                GetPartnerInfoResponse resp = new GetPartnerInfoResponse(fleets, vehicleTypes);
                return resp;
            }
            catch (Exception e)
            {
                exceptions++;
                Logger.Log("Exception=" + e.Message);
                Console.WriteLine(e.StackTrace);
                return new GetPartnerInfoResponse(result: Result.UnknownError);
            }
        }
        public override DispatchTripResponse DispatchTrip(DispatchTripRequest r)
        {
            try
            {
                requests++;
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
                    if (response.result == Result.Rejected)
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
                        DateTime bestETA = TimeZoneInfo.ConvertTimeToUtc(r.pickupTime) + missedBookingPeriod;
                        // not more than 30 minues late
                        foreach (Quote q in response.quotes)
                        {
                            if (q.ETA < bestETA)
                            {
                                bestETA = (DateTime)q.ETA;
                                bestQuote = q;
                            }
                        }
                        if (bestQuote != null)
                        {
                            partner = partnersByID[bestQuote.PartnerId];
                            r.fleetID = bestQuote.FleetId;
                            Logger.Log("Best quote " + bestQuote + " from " + partner.name);
                        }
                        else
                            Logger.Log("There are no partners to handle this trip within an exceptable service time");

                    }
                    Logger.Untab();
                }
                else
                    partner = partnersByID[r.partnerID];
                DispatchTripResponse response1;
                if (partner != null)
                {
                    Gateway client = partnersByID[r.clientID];
                    originatingPartnerByTrip.Add(r.tripID, client);
                    Logger.Log("Origination="+client.name);
                    servicingPartnerByTrip.Add(r.tripID, partner);
                    Logger.Log("Dispatch="+partner.name);
                    r.clientID = ID;
                    response1 = partner.DispatchTrip(r);
                    if (response1.result != Result.OK)
                    {
                        Logger.Log("DispatchTrip to " + partner.name + " failed");
                    }
                    else
                    {
                        activeTrips.Add(r.tripID);
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
                Console.WriteLine(e.StackTrace);
                return new DispatchTripResponse(result: Result.UnknownError);
            }
        }
        public override QuoteTripResponse QuoteTrip(QuoteTripRequest r)
        {
            try
            {
                requests++;
                var quotes = new List<Quote>();

                foreach (Gateway p in partners)
                {
                    if (p.ID == r.clientID)
                        continue;

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
                Console.WriteLine(e.StackTrace);
                return new QuoteTripResponse(result: Result.UnknownError);
            }
        }
        public override GetTripsResponse GetTrips(GetTripsRequest r)
        {
            requests++;
            return new GetTripsResponse(new List<string>(originatingPartnerByTrip.Keys));
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
                        if (response.status == Status.Complete || response.status == Status.Cancelled || response.status == Status.Rejected)
                            DeactivateTrip(r.tripID, (Status)response.status, response.price, response.distance);

                        response.partnerID = partner.ID;
                        response.partnerName = partner.name;
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
                Console.WriteLine(e.StackTrace);
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
                    Logger.Log("Destination partner=" + destPartner.name);
                    string clientID = r.clientID;
                    r.clientID = ID;
                    UpdateTripStatusResponse response = destPartner.UpdateTripStatus(r);
                    r.clientID = clientID;
                    if (response.result == Result.OK)
                    {
                        if (r.status == Status.Complete)
                        {
                            Gateway origPartner = partnersByID[r.clientID];
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
                Logger.Log("Exception=" + e.Message);
                Console.WriteLine(e.StackTrace);
                return new UpdateTripStatusResponse(result: Result.UnknownError);
            }
        }

        public class Office
        {
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
                    RegisterPartner(partner);
                }
                string configStr = JsonSerializer.SerializeToString<TDispatchOfficeConfigs>(config);
                File.WriteAllText("~/Custom Integrations/TDispatchOffices.txt".MapHostAbsolutePath(), configStr);   // We don't need this while testing.  The main purpose is to save the tokens so token reresh not constantly needed
            }

            //TDispatchIntegration sanFranOffice = (TDispatchIntegration) partnersByID["52b4053948efcb7ac1137d41"];
            //DispatchTripResponse response = sanFranOffice.DispatchTrip(new DispatchTripRequest(clientID: "test",
            //    tripID: "test", pickupLocation: new Location(37.78906, -122.402127), pickupTime: DateTime.UtcNow,
            //    passengerID: "test", passengerName: "test-ed", luggage: 1, persons: 1, dropoffLocation: new Location(37.78906, -122.402127), vehicleType: VehicleType.Sedan));

            //TDispatchAPI.Booking booking = sanFranOffice.activeTrips["test"];
            //sanFranOffice.api.RejectBooking(booking.pk);
        }
        public override void Update()
        {
            foreach (Gateway p in partners)
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
