using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

            public void Log()
            {
                Logger.Log("Partner = " + name + " with client id = "+ID);
            }
        }
        public List<Partner> partners;
        public Dictionary<string, Partner> partnersByID;
        public string ID;
        public TimeSpan missedBookingPeriod = new TimeSpan(0, 30, 0);
        public TripThru()
        {
            ID = "TripThru";
            partnersByID = new Dictionary<string, Partner>();
            registerPartner = new RegisterPartner(this);
            getPartnerInfo = new GetPartnerInfo(this);
            dispatchTrip = new DispatchTrip(this);
            quoteTrip = new QuoteTrip(this);
            getTripStatus = new GetTripStatus(this);
            updateTripStatus = new UpdateTripStatus(this);

            partners = new List<Partner>();
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
            public override Response Post(Request r)
            {
                Partner partner = new Partner(r.name, r.callback_url, r.clientID, r.accessToken);
                tripthru.AddPartner(partner);
                Response response = new Response(partner.ID);
                Logger.Log("Registering partner: " + partner.name + " with TripThru, Response: " + response);
                return response;
            }
        }
        public new class GetPartnerInfo : Gateway.GetPartnerInfo
        {
            public TripThru tripthru;
            public GetPartnerInfo(TripThru tripthru)
            {
                this.tripthru = tripthru;
            }
            public override Response Get(Request r)
            {
                if (r.fleets != null || r.vehicleTypes != null || r.coverage != null)
                    throw new Exception("Filters currently not supported");
                List<VehicleType> vehicleTypes = new List<VehicleType>();
                List<Fleet> fleets = new List<Fleet>();
                r.clientID = tripthru.ID;
                foreach (Partner p in tripthru.partners)
                {
                    Response response = p.PartnerGateway.getPartnerInfo.Get(r);
                    if (response.result == Result.OK)
                    {
                        fleets.AddRange(response.fleets);
                        vehicleTypes.AddRange(response.vehicleTypes);
                    }
                }
                Response resp = new Response(fleets, vehicleTypes);
                Logger.Log("GetPartnerInfo called on TripThru, Response: " + resp);
                return resp;

            }
        }
        public new class DispatchTrip : Gateway.DispatchTrip
        {
            public TripThru tripthru;
            public DispatchTrip(TripThru tripthru)
            {
                this.tripthru = tripthru;
            }
            public override Response Post(Request r)
            {
                // Note: GetTrip populates the foreignTripID
                Logger.Log("DispatchTrip called on TripThru -- Request: " + r);
                Logger.Tab();
                Partner partner = null;
                if (r.partnerID == null)
                {
                    Logger.Log("DispatchTrip called on TripThru: (auto) mode, so quote trip through all partners");
                    Logger.Tab();
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
                        Logger.Log("No partners are available");
                        return new Response(result: Result.Rejected);
                    }
                    else if (response.result != Result.OK)
                    {
                        Logger.Log("QuoteTrip call failed, Response = "+response);
                        return new Response(result : response.result);
                    }
                    else
                    {
                        Quote bestQuote = null;
                        DateTime bestETA = r.pickupTime + tripthru.missedBookingPeriod; // not more than 30 minues late
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
                            Logger.Log("Best quote " + bestQuote + " from " + partner.name);
                        }
                        else
                            Logger.Log("There are no partners to handle this trip");

                    }
                    Logger.Untab();
                }
                else
                    partner = tripthru.partnersByID[r.partnerID];
                Response response1;
                if (partner != null)
                {
                    r.foreignID += ":" + r.clientID;
                    r.clientID = tripthru.ID;
                    response1 = partner.PartnerGateway.dispatchTrip.Post(r);
                    if (response1.result == Result.OK)
                    {
                        response1.tripID += ":" + partner.ID;
                    }
                    else
                    {
                        Logger.Log("DispatchTrip call to "+partner.name+" failed, Response = "+response1);
                    }
                }
                else
                    response1 = new Response(result: Result.Rejected);
                Logger.Untab();
                Logger.Log("Response: " + response1);
                return response1;
            }
        }
        public new class QuoteTrip : Gateway.QuoteTrip
        {
            public TripThru tripthru;
            public QuoteTrip(TripThru tripthru)
            {
                this.tripthru = tripthru;
            }
            public override Response Get(Request r)
            {
                Logger.Log("QuoteTrip called on TripThru -- Request: " + r);
                Logger.Tab();
                var quotes = new List<Quote>();
                string clientID = r.clientID;
                r.clientID = tripthru.ID;

                foreach (Partner p in tripthru.partners)
                {
                    if (p.ID == clientID)
                        continue;
                    Response response = p.PartnerGateway.quoteTrip.Get(r);
                    if (response.result == Result.OK)
                    {
                        if (response.quotes != null)
                            quotes.AddRange(response.quotes);
                    }
                }
                Response response1 = new Response(quotes);
                Logger.Untab();
                Logger.Log("Response: " + response1);
                Logger.Tab();
                foreach (Quote q in quotes)
                    Logger.Log(q.ToString());
                Logger.Untab();
                return response1;
            }
        }
        public new class GetTripStatus : Gateway.GetTripStatus
        {
            public TripThru tripthru;
            public GetTripStatus(TripThru tripthru)
            {
                this.tripthru = tripthru;
            }
            public override Response Get(Request r)
            {
                Logger.Log("GetTripStatus called on TripThru -- Request: " + r);
                Logger.Tab();
                string partnerID = r.tripID.Substring(r.tripID.LastIndexOf(':') + 1);
                r.tripID = r.tripID.Substring(0, r.tripID.LastIndexOf(':'));
                Partner partner = tripthru.partnersByID[partnerID];
                r.clientID = tripthru.ID;
                Response response = partner.PartnerGateway.getTripStatus.Get(r);
                if (response.result == Result.OK)
                {
                    response.partnerID = partner.ID;
                    response.partnerName = partner.name;
                }
                Logger.Untab();
                return response;
            }
        }
        public new class UpdateTripStatus : Gateway.UpdateTripStatus
        {
            public TripThru tripthru;
            public UpdateTripStatus(TripThru tripthru)
            {
                this.tripthru = tripthru;
            }
            public override Response Post(Request r)
            {
                Logger.Log("UpdateTripStatus called on TripThru -- Request: " + r);
                Logger.Tab();
                // Note: GetTrip populates the foreignTripID
                string partnerID = r.tripID.Substring(r.tripID.LastIndexOf(':') + 1);
                r.tripID = r.tripID.Substring(0, r.tripID.LastIndexOf(':'));
                Partner partner = tripthru.partnersByID[partnerID];
                r.clientID = tripthru.ID;
                Response response = partner.PartnerGateway.updateTripStatus.Post(r);
                Logger.Untab();
                return response;
            }
        }
    }
}
