using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace ServiceStack.TripThruGateway.TripThru
{
    public class TripThru : Gateway
    {
        
        public class Partner : IDName
        {
            public PartnerGateway PartnerGateway;
            public Partner(string name, string callbackUrl, string clientId, string accessToken)
                : base(name)
            {
                this.name = name;
                this.ID = clientId;
                this.PartnerGateway = new PartnerGateway(callbackUrl, name, clientId, accessToken);
            }
        }

        void CleanUpTrip(string tripID)
        {
            originatingPartnerByTrip.Remove(tripID);
            servicingPartnerByTrip.Remove(tripID);
        }

        
        public List<Partner> partners;
        public Dictionary<string, Partner> partnersByID;
        public Dictionary<string, Partner> originatingPartnerByTrip;
        public Dictionary<string, Partner> servicingPartnerByTrip;
        public Dictionary<string, List<Zone>> partnerCoverage;
        public string ID;
        public TimeSpan missedBookingPeriod = new TimeSpan(0, 30, 0);
        public TripThru()
        {
            ID = "TripThru";
            name = "TripThru";
            partnersByID = new Dictionary<string, Partner>();
            originatingPartnerByTrip = new Dictionary<string, Partner>();
            servicingPartnerByTrip = new Dictionary<string, Partner>();
            partnerCoverage = new Dictionary<string, List<Zone>>();
            registerPartner = new RegisterPartner(this);
            getPartnerInfo = new GetPartnerInfo(this);
            dispatchTrip = new DispatchTrip(this);
            quoteTrip = new QuoteTrip(this);
            getTripStatus = new GetTripStatus(this);
            updateTripStatus = new UpdateTripStatus(this);
            getTrips = new GetTrips(this);
            partners = new List<Partner>();
            garbageCleanup = new GarbageCleanup<string>(new TimeSpan(0, 1, 0), CleanUpTrip);
        }
        public Partner GetDestinationPartner(string clientID, string tripID)
        {
            if (originatingPartnerByTrip.ContainsKey(tripID))
            {
                Partner partner = originatingPartnerByTrip[tripID];
                if (partner.ID == clientID && servicingPartnerByTrip.ContainsKey(tripID))
                {
                    partner = servicingPartnerByTrip[tripID];
                }
                else
                {
                    return null;
                }
                return partner;
            }
            return null;
        }
        public void AddPartner(Partner partner)
        {
            partners.Add(partner);
            partnersByID.Add(partner.ID, partner);
        }

        public new class RegisterPartner : Gateway.RegisterPartner
        {
            public TripThru tripthru;
            public RegisterPartner(TripThru tripthru)
            {
                this.tripthru = tripthru;
            }
            public override Response Post(Request r, RequestLog log = null)
            {
                log = Logger.CreateNewRequestLog();
                try
                {
                    tripthru.requests++;
                    Partner partner = new Partner(r.name, r.callback_url, r.clientID, r.accessToken);
                    tripthru.AddPartner(partner);
                    Response response = new Response(partner.ID);
                    log.Log("Registering partner: " + partner.name + " with TripThru, Response: " + response);
                    new GetPartnerCoverageThread(partner, tripthru.ID, tripthru.partnerCoverage);
                    Logger.Log(log);
                    return response;
                }
                catch (Exception e)
                {
                    tripthru.exceptions++;
                    log.Log("Exception :" + e.Message);
                    Logger.Log(log);
                    return new Response(result: Result.UnknownError);
                }
            }
        }
        public new class GetPartnerInfo : Gateway.GetPartnerInfo
        {
            public TripThru tripthru;
            public GetPartnerInfo(TripThru tripthru)
            {
                this.tripthru = tripthru;
            }
            public override Response Get(Request r, RequestLog log = null)
            {
                log = Logger.CreateNewRequestLog();
                try
                {
                    if (r.fleets != null || r.vehicleTypes != null || r.coverage != null)
                        throw new Exception("Filters currently not supported");
                    tripthru.requests++;
                    List<VehicleType> vehicleTypes = new List<VehicleType>();
                    List<Fleet> fleets = new List<Fleet>();
                    r.clientID = tripthru.ID;
                    foreach (Partner p in tripthru.partners)
                    {
                        log.Tab();
                        Response response = p.PartnerGateway.getPartnerInfo.Get(r, log);
                        if (response.result == Result.OK)
                        {
                            fleets.AddRange(response.fleets);
                            vehicleTypes.AddRange(response.vehicleTypes);
                        }
                        log.Untab();
                    }
                    Response resp = new Response(fleets, vehicleTypes);
                    log.Log("GetPartnerInfo called on TripThru, Response: " + resp);
                    Logger.Log(log);
                    return resp;
                }
                catch (Exception e)
                {
                    tripthru.exceptions++;
                    log.Log("Exception :" + e.Message);
                    Logger.Log(log);
                    return new Response(result: Result.UnknownError);
                }

            }
        }
        public new class DispatchTrip : Gateway.DispatchTrip
        {
            public TripThru tripthru;
            public DispatchTrip(TripThru tripthru)
            {
                this.tripthru = tripthru;
            }
            public override Response Post(Request r, RequestLog log = null)
            {
                log = Logger.CreateNewRequestLog();
                try
                {
                    tripthru.requests++;
                    // Note: GetTrip populates the foreignTripID
                    log.Log("DispatchTrip called on TripThru -- Request: " + r);
                    log.Tab();
                    Partner partner = null;
                    if (r.partnerID == null)
                    {
                        log.Log("DispatchTrip called on TripThru: (auto) mode, so quote trip through all partners");
                        log.Tab();
                        // Dispatch to partner with shortest ETA
                        QuoteTrip.Response response = tripthru.quoteTrip.Get(new QuoteTrip.Request(
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
                            log.Log("No partners are available");
                            Logger.Log(log);
                            tripthru.rejects++;
                            return new Response(result: Result.Rejected);
                        }
                        else if (response.result != Result.OK)
                        {
                            log.Log("QuoteTrip call failed, Response = " + response);
                            Logger.Log(log);
                            tripthru.rejects++;
                            return new Response(result: response.result);
                        }
                        else
                        {
                            Quote bestQuote = null;
                            DateTime bestETA = r.pickupTime + tripthru.missedBookingPeriod;
                                // not more than 30 minues late
                            foreach (Quote q in response.quotes)
                            {
                                if (q.ETA < bestETA)
                                {
                                    bestETA = (DateTime) q.ETA;
                                    bestQuote = q;
                                }
                            }
                            if (bestQuote != null)
                            {
                                partner = tripthru.partnersByID[bestQuote.PartnerId];
                                r.fleetID = bestQuote.FleetId;
                                log.Log("Best quote " + bestQuote + " from " + partner.name);
                            }
                            else
                                log.Log("There are no partners to handle this trip");

                        }
                        log.Untab();
                    }
                    else
                        partner = tripthru.partnersByID[r.partnerID];
                    Response response1;
                    if (partner != null)
                    {
                        Partner client = tripthru.partnersByID[r.clientID];
                        tripthru.originatingPartnerByTrip.Add(r.tripID, client);
                        tripthru.servicingPartnerByTrip.Add(r.tripID, partner);
                        r.clientID = tripthru.ID;
                        response1 = partner.PartnerGateway.dispatchTrip.Post(r, log); 
                        if (response1.result != Result.OK)
                        {
                            tripthru.activeTrips.Add(r.tripID);
                            log.Log("DispatchTrip call to " + partner.name + " failed, Response = " + response1);
                        }
                    }
                    else
                    {
                        tripthru.rejects++;
                        response1 = new Response(result: Result.Rejected);
                    }
                    log.Untab();
                    log.Log("Response: " + response1);
                    Logger.Log(log);
                    return response1;
                }
                catch (Exception e)
                {
                    tripthru.exceptions++;
                    log.Untab();
                    log.Log("Exception :" + e.Message);
                    Logger.Log(log);
                    return new Response(result: Result.UnknownError);
                }
            }
        }
        public new class QuoteTrip : Gateway.QuoteTrip
        {
            public TripThru tripthru;
            public QuoteTrip(TripThru tripthru)
            {
                this.tripthru = tripthru;
            }
            public override Response Get(Request r, RequestLog log = null)
            {
                log = Logger.CreateNewRequestLog();
                try
                {
                    tripthru.requests++;
                    log.Log("QuoteTrip called on TripThru -- Request: " + r);
                    log.Tab();
                    var quotes = new List<Quote>();
                    string clientID = r.clientID;
                    r.clientID = tripthru.ID;

                    foreach (Partner p in tripthru.partners)
                    {
                        if (p.ID == clientID)
                            continue;

                        bool covered = false;
                        foreach (Zone z in tripthru.partnerCoverage[p.ID])
                        {
                            if (z.IsInside(r.pickupLocation))
                            {
                                covered = true;
                                break;
                            }
                        }

                        if (covered)
                        {
                            Response response = p.PartnerGateway.quoteTrip.Get(r, log);
                            if (response.result == Result.OK)
                            {
                                if (response.quotes != null)
                                    quotes.AddRange(response.quotes);
                            }
                        }
                    }
                    Response response1 = new Response(quotes);
                    log.Log("Response: " + response1);
                    foreach (Quote q in quotes)
                        log.Log(q.ToString());
                    log.Untab();
                    Logger.Log(log);
                    return response1;
                }
                catch (Exception e)
                {
                    tripthru.exceptions++;
                    log.Untab();
                    log.Log("Exception :" + e.Message);
                    Logger.Log(log);
                    return new Response(result: Result.UnknownError);
                }
            }
        }
        public new class GetTrips : Gateway.GetTrips
        {
            public TripThru parent;
            public GetTrips(TripThru parent)
            {
                this.parent = parent;
            }
            public override Response Get(Request r, RequestLog log = null)
            {
                parent.requests++;
                return new Response(new List<string>(parent.originatingPartnerByTrip.Keys));
            }
        }
        public new class GetTripStatus : Gateway.GetTripStatus
        {
            public TripThru tripthru;
            public GetTripStatus(TripThru tripthru)
            {
                this.tripthru = tripthru;
            }
            public override Response Get(Request r, RequestLog log = null)
            {
                log = Logger.CreateNewRequestLog();
                try
                {
                    tripthru.requests++;
                    log.Log("GetTripStatus called on TripThru -- Request: " + r);
                    log.Tab();
                    Partner partner = tripthru.GetDestinationPartner(r.clientID, r.tripID);
                    if (partner != null)
                    {
                        r.clientID = tripthru.ID;
                        Response response = partner.PartnerGateway.getTripStatus.Get(r, log);
                        if (response.result == Result.OK)
                        {
                            if (response.status == Status.Complete || response.status == Status.Cancelled || response.status == Status.Rejected)
                                tripthru.DeactivateTrip(r.tripID, (Status)response.status, log, response.price, response.distance);
                    
                            response.partnerID = partner.ID;
                            response.partnerName = partner.name;
                        }
                        log.Untab();
                        Logger.Log(log);
                        return response;
                    }
                    log.Untab();
                    Logger.Log(log);
                    return new Response(result: Result.NotFound);
                }
                catch (Exception e)
                {
                    tripthru.exceptions++;
                    log.Untab();
                    log.Log("Exception :" + e.Message);
                    Logger.Log(log);
                    return new Response(result: Result.UnknownError);
                }
            }
        }
        public new class UpdateTripStatus : Gateway.UpdateTripStatus
        {
            public TripThru tripthru;
            public UpdateTripStatus(TripThru tripthru)
            {
                this.tripthru = tripthru;
            }
            public override Response Post(Request r, RequestLog log = null)
            {
                log = Logger.CreateNewRequestLog();
                try
                {
                    tripthru.requests++;
                    log.Log("UpdateTripStatus called on TripThru -- Request: " + r);
                    log.Tab();
                    Partner destPartner = tripthru.GetDestinationPartner(r.clientID, r.tripID);
                    if (destPartner != null)
                    {
                        r.clientID = tripthru.ID;
                        Response response = destPartner.PartnerGateway.updateTripStatus.Post(r, log);
                        if (response.result == Result.OK)
                        {
                            if (r.status == Status.Complete)
                            {
                                Partner origPartner = tripthru.partnersByID[r.clientID];
                                GetTripStatus.Response resp =
                                    origPartner.PartnerGateway.getTripStatus.Get(new GetTripStatus.Request(r.clientID,
                                        r.tripID), log);
                                tripthru.DeactivateTrip(r.tripID, Status.Complete, log, resp.price, resp.distance);
                            }
                            else if (r.status == Status.Cancelled || r.status == Status.Rejected)
                                tripthru.DeactivateTrip(r.tripID, r.status, log);
                        }
                        log.Untab();
                        Logger.Log(log);
                        return response;
                    }
                    log.Untab();
                    Logger.Log(log);
                    return new Response(result: Result.NotFound);
                }
                catch (Exception e)
                {
                    tripthru.exceptions++;
                    log.Untab();
                    log.Log("Exception :" + e.Message);
                    Logger.Log(log);
                    return new Response(result: Result.UnknownError);
                }
            }
        }
    }

    public class GetPartnerCoverageThread : IDisposable
    {
        private TripThru.Partner _partner;
        private string _id;
        private Dictionary<string, List<Zone>> _partnerCoverageList; 
        private Thread _worker;
        private volatile bool _workerTerminateSignal = false;

        public GetPartnerCoverageThread(TripThru.Partner partner, string tripThruId, Dictionary<string, List<Zone>> partnerCoverageList)
        {
            this._partner = partner;
            this._id = tripThruId;
            this._partnerCoverageList = partnerCoverageList;
            _worker = new Thread(StartThread);
            _worker.Start();
        }

        private void StartThread()
        {
            PartnerGateway.GetPartnerInfo.Response resp = _partner.PartnerGateway.getPartnerInfo.Get(new PartnerGateway.GetPartnerInfo.Request(_id));
            List<Zone> coverage = new List<Zone>();
            foreach (Fleet f in resp.fleets)
                coverage.AddRange(f.Zone);
            lock (_partnerCoverageList)
            {
                _partnerCoverageList[_partner.ID] = coverage;
            }
        }

        public void Dispose()
        {
            
        }
    }
}
