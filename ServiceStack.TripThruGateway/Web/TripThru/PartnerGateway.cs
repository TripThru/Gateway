using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using RestSharp;

namespace ServiceStack.TripThruGateway.TripThru
{
    public class PartnerGateway : Gateway
    {
        private string CallBackUrl { get; set; }
        private string AccessToken { get; set; } //this is how we authenticate with the partner for now
        private string ClientId { get; set; }
        private string Name { get; set; }
        private GatewayRestClient GatewayClient { get; set; }

        public PartnerGateway(string callbackUrl, string name, string clientId, string accessToken)
        {
            CallBackUrl = callbackUrl;
            Name = name;
            AccessToken = accessToken;
            ClientId = clientId;
            GatewayClient = new GatewayRestClient(AccessToken, CallBackUrl);
            getPartnerInfo = new GetPartnerInfo(this);
            dispatchTrip = new DispatchTrip(this);
            quoteTrip = new QuoteTrip(this);
            getTripStatus = new GetTripStatus(this);
            updateTripStatus = new UpdateTripStatus(this);
        }

        public new class GetPartnerInfo : Gateway.GetPartnerInfo
        {
            public PartnerGateway PartnerGateway;

            public GetPartnerInfo(PartnerGateway partnerGateway)
            {
                PartnerGateway = partnerGateway;
            }

            public override Response Get(Request r, RequestLog log = null)
            {
                var vehicleTypes = new List<VehicleType>();
                var fleets = new List<Fleet>();

                var response = PartnerGateway.GatewayClient.GetPartnerInfo();
                if(log != null)
                    log.Log("GetPartnerInfo called on " + PartnerGateway.Name + ": Response = " + response);
                if (response.result == Result.OK)
                {
                    fleets.AddRange(response.fleets);
                    foreach (var fleet in fleets)
                    {
                        fleet.PartnerId = PartnerGateway.ClientId;
                        fleet.PartnerName = PartnerGateway.Name;
                    }
                    vehicleTypes.AddRange(response.vehicleTypes);
                    return new Response(fleets, vehicleTypes);
                }
                return new Response(result: response.result);
            }
        }

        public new class DispatchTrip : Gateway.DispatchTrip
        {
            public PartnerGateway PartnerGateway;

            public DispatchTrip(PartnerGateway partnerGateway)
            {
                PartnerGateway = partnerGateway;
            }
            public override Response Post(Request r, RequestLog log = null)
            {
                var response = PartnerGateway.GatewayClient.DispatchTrip(r);
                if (log != null)
                    log.Log("DispatchTrip called on " + PartnerGateway.Name + ": Response = " + response);
                return response;
            }
        }

        public new class QuoteTrip : Gateway.QuoteTrip
        {
            public PartnerGateway PartnerGateway;
            public QuoteTrip(PartnerGateway partnerGateway)
            {
                PartnerGateway = partnerGateway;
            }
            public override Response Get(Request r, RequestLog log = null)
            {
                List<Quote> quotes = new List<Quote>();
                Response response = PartnerGateway.GatewayClient.QuoteTrip(r);
                if (log != null)
                    log.Log("QuoteTrip called on " + PartnerGateway.Name + ": Response = " + response);
                if (response.result == Result.OK)
                {
                    if (response.quotes != null)
                        quotes.AddRange(response.quotes);

                    Response response1 = new Response(quotes);
                    return response1;
                }
                return new Response(result: response.result);
            }
        }

        public new class GetTripStatus : Gateway.GetTripStatus
        {
            public PartnerGateway PartnerGateway;
            public GetTripStatus(PartnerGateway partnerGateway)
            {
                PartnerGateway = partnerGateway;
            }
            public override Response Get(Request r, RequestLog log = null)
            {
                Response response = PartnerGateway.GatewayClient.GetTripStatus(r);
                if (log != null)
                    log.Log("GetTripStatus called on " + PartnerGateway.Name + ": Response = " + response);
                if (response.result == Result.OK)
                {
                    response.partnerID = PartnerGateway.ClientId;
                    response.partnerName = PartnerGateway.Name;
                    return response;
                }
                return new Response(result: response.result);
            }
        }

        public new class UpdateTripStatus : Gateway.UpdateTripStatus
        {
            public PartnerGateway PartnerGateway;
            public UpdateTripStatus(PartnerGateway partnerGateway)
            {
                PartnerGateway = partnerGateway;
            }
            public override Response Post(Request r, RequestLog log = null)
            {
                var response = PartnerGateway.GatewayClient.UpdateTripStatus(r);
                if (log != null)
                    log.Log("UpdateTripStatus called on " + PartnerGateway.Name + ": Response = " + response);
                return response;
            }
        }
    }


    
}