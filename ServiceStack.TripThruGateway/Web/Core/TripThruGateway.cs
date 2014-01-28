using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Utils;

namespace ServiceStack.TripThruGateway
{
    public class TripThru : Gateway
    {
        
        public class Partner : IDName
        {
            public GatewayClient PartnerClient;
            public Partner(Gateway parent, string name, string callbackUrl, string clientId, string accessToken)
                : base(name)
            {
                this.name = name;
                this.ID = clientId;
                this.PartnerClient = parent.GetClientGateway(accessToken, callbackUrl);
//                this.PartnerGateway = new PartnerGateway(callbackUrl, name, clientId, accessToken);
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
        private Dictionary<string, List<Zone>> _partnerCoverage;

        List<Zone> GetPartnerCoverage(string partnerID)
        {
            if (!_partnerCoverage.ContainsKey(partnerID))
            {
                Partner partner = partnersByID[partnerID];
                Gateway.GetPartnerInfo.Response resp = partner.PartnerClient.GetPartnerInfo(new Gateway.GetPartnerInfo.Request(ID));
                List<Zone> coverage = new List<Zone>();
                foreach (Fleet f in resp.fleets)
                    coverage.AddRange(f.Zone);
                _partnerCoverage.Add(partner.ID, coverage);
            }
            return _partnerCoverage[partnerID];
        }
        public TimeSpan missedBookingPeriod = new TimeSpan(0, 30, 0);
        public TripThru(GetGatewayClientDelegate getClientGateway) : base(getClientGateway)
        {
            ID = "TripThru";
            name = "TripThru";
            partnersByID = new Dictionary<string, Partner>();
            originatingPartnerByTrip = new Dictionary<string, Partner>();
            servicingPartnerByTrip = new Dictionary<string, Partner>();
            _partnerCoverage = new Dictionary<string, List<Zone>>();
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
            public TripThru parent;
            public RegisterPartner(TripThru parent)
            {
                this.parent = parent;
            }
            public override Response Post(Request r)
            {
                try
                {
                    parent.requests++;
                    Partner partner = new Partner(parent, r.name, r.callback_url, r.clientID, r.accessToken);
                    parent.AddPartner(partner);
                    Response response = new Response(partner.ID);
                    return response;
                }
                catch (Exception e)
                {
                    parent.exceptions++;
                    Logger.Log("Exception: " + e.Message);
                    throw e;
                }
            }
        }
        public new class GetPartnerInfo : Gateway.GetPartnerInfo
        {
            public TripThru parent;
            public GetPartnerInfo(TripThru parent)
            {
                this.parent = parent;
            }
            public override Response Get(Request r)
            {
                try
                {
                    if (r.fleets != null || r.vehicleTypes != null || r.coverage != null)
                        throw new Exception("Filters currently not supported");
                    parent.requests++;
                    List<VehicleType> vehicleTypes = new List<VehicleType>();
                    List<Fleet> fleets = new List<Fleet>();
                    r.clientID = parent.ID;
                    foreach (Partner p in parent.partners)
                    {
                        Logger.Tab();
                        Response response = p.PartnerClient.GetPartnerInfo(r);
                        if (response.result == Result.OK)
                        {
                            fleets.AddRange(response.fleets);
                            vehicleTypes.AddRange(response.vehicleTypes);
                        }
                        Logger.Untab();
                    }
                    Response resp = new Response(fleets, vehicleTypes);
                    return resp;
                }
                catch (Exception e)
                {
                    parent.exceptions++;
                    Logger.Log("Exception: " + e.Message);
                    throw e;
                }

            }
        }
        public new class DispatchTrip : Gateway.DispatchTrip
        {
            public TripThru parent;
            public DispatchTrip(TripThru parent)
            {
                this.parent = parent;
            }
            public override Response Post(Request r)
            {
                try
                {
                    parent.requests++;
                    // Note: GetTrip populates the foreignTripID
                    Partner partner = null;
                    if (r.partnerID == null)
                    {
                        Logger.Log("Auto mode, so quote trip through all partners");
                        Logger.Tab();
                        // Dispatch to partner with shortest ETA
                        QuoteTrip.Response response = parent.quoteTrip.Get(new QuoteTrip.Request(
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
                            Logger.Untab();
                            parent.rejects++;
                            return new Response(result: Result.Rejected);
                        }
                        else if (response.result != Result.OK)
                        {
                            Logger.Log("QuoteTrip call failed"); 
                            Logger.Untab();
                            Logger.Untab();
                            parent.rejects++;
                            return new Response(result: response.result);
                        }
                        else
                        {
                            Quote bestQuote = null;
                            DateTime bestETA = r.pickupTime + parent.missedBookingPeriod;
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
                                partner = parent.partnersByID[bestQuote.PartnerId];
                                r.fleetID = bestQuote.FleetId;
                                Logger.Log("Best quote " + bestQuote + " from " + partner.name);
                            }
                            else
                                Logger.Log("There are no partners to handle this trip within an exceptable service time");

                        }
                        Logger.Untab();
                    }
                    else
                        partner = parent.partnersByID[r.partnerID];
                    Response response1;
                    if (partner != null)
                    {
                        Partner client = parent.partnersByID[r.clientID];
                        parent.originatingPartnerByTrip.Add(r.tripID, client);
                        parent.servicingPartnerByTrip.Add(r.tripID, partner);
                        r.clientID = parent.ID;
                        response1 = partner.PartnerClient.DispatchTrip(r); 
                        if (response1.result != Result.OK)
                        {
                            parent.activeTrips.Add(r.tripID);
                            Logger.Log("DispatchTrip to " + partner.name + " failed");
                        }
                    }
                    else
                    {
                        parent.rejects++;
                        response1 = new Response(result: Result.Rejected);
                    }
                    return response1;
                }
                catch (Exception e)
                {
                    parent.exceptions++;
                    Logger.Log("Exception: " + e.Message);
                    throw e;
                }
            }
        }
        public new class QuoteTrip : Gateway.QuoteTrip
        {
            public TripThru parent;
            public QuoteTrip(TripThru parent)
            {
                this.parent = parent;
            }
            public override Response Get(Request r)
            {
                try
                {
                    parent.requests++;
                    var quotes = new List<Quote>();

                    foreach (Partner p in parent.partners)
                    {
                        if (p.ID == r.clientID)
                            continue;

                        bool covered = false;
                        foreach (Zone z in parent.GetPartnerCoverage(p.ID))
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
                            r.clientID = parent.ID;
                            Response response = p.PartnerClient.QuoteTrip(r);
                            if (response.result == Result.OK)
                            {
                                if (response.quotes != null)
                                    quotes.AddRange(response.quotes);
                            }
                            r.clientID = clientID;
                        }
                    }
                    Response response1 = new Response(quotes);
                    return response1;
                }
                catch (Exception e)
                {
                    parent.exceptions++;
                    Logger.Log("Exception: " + e.Message);
                    throw e;
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
            public override Response Get(Request r)
            {
                parent.requests++;
                return new Response(new List<string>(parent.originatingPartnerByTrip.Keys));
            }
        }
        public new class GetTripStatus : Gateway.GetTripStatus
        {
            public TripThru parent;
            public GetTripStatus(TripThru tripthru)
            {
                this.parent = tripthru;
            }
            public override Response Get(Request r)
            {
                try
                {
                    parent.requests++;
                    Partner partner = parent.GetDestinationPartner(r.clientID, r.tripID);
                    if (partner != null)
                    {
                        r.clientID = parent.ID;
                        Response response = partner.PartnerClient.GetTripStatus(r);
                        if (response.result == Result.OK)
                        {
                            if (response.status == Status.Complete || response.status == Status.Cancelled || response.status == Status.Rejected)
                                parent.DeactivateTrip(r.tripID, (Status)response.status, response.price, response.distance);
                    
                            response.partnerID = partner.ID;
                            response.partnerName = partner.name;
                        }
                        Logger.Untab();
                        return response;
                    }
                    return new Response(result: Result.NotFound);
                }
                catch (Exception e)
                {
                    parent.exceptions++;
                    Logger.Log("Exception: " + e.Message);
                    throw e;
                }
            }
        }
        public new class UpdateTripStatus : Gateway.UpdateTripStatus
        {
            public TripThru parent;
            public UpdateTripStatus(TripThru parent)
            {
                this.parent = parent;
            }
            public override Response Post(Request r)
            {
                try
                {
                    parent.requests++;
                    Partner destPartner = parent.GetDestinationPartner(r.clientID, r.tripID);
                    if (destPartner != null)
                    {
                        r.clientID = parent.ID;
                        Response response = destPartner.PartnerClient.UpdateTripStatus(r);
                        if (response.result == Result.OK)
                        {
                            if (r.status == Status.Complete)
                            {
                                Partner origPartner = parent.partnersByID[r.clientID];
                                GetTripStatus.Response resp =
                                    origPartner.PartnerClient.GetTripStatus(new GetTripStatus.Request(r.clientID, r.tripID));
                                parent.DeactivateTrip(r.tripID, Status.Complete, resp.price, resp.distance);
                            }
                            else if (r.status == Status.Cancelled || r.status == Status.Rejected)
                                parent.DeactivateTrip(r.tripID, r.status);
                        }
                        Logger.Untab();
                        return response;
                    }
                    return new Response(result: Result.NotFound);
                }
                catch (Exception e)
                {
                    parent.exceptions++;
                    Logger.Log("Exception: " + e.Message);
                    throw e;
                }
            }
        }
    }

}
