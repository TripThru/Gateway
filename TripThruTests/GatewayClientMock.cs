using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ServiceStack.TripThruGateway;
using TripThruCore;
using Utils;

namespace TripThruTests
{
    class GatewayClientMock : GatewayWithStats
    {
        Gateway server;

        public GatewayClientMock(Gateway server)
            : base(server.ID, server.name)
        {
            this.server = server;
        }

        public override Gateway.RegisterPartnerResponse RegisterPartner(Gateway gateway, List<Zone> coverage)
        {
            requests++;
            Gateway.RegisterPartnerResponse resp = server.RegisterPartner(gateway, coverage);
            if (resp.result == Gateway.Result.Rejected)
                rejects++;
            return resp;
        }

        public override Gateway.GetPartnerInfoResponse GetPartnerInfo(Gateway.GetPartnerInfoRequest request)
        {
            requests++;
            return server.GetPartnerInfo(request);
        }

        public override Gateway.DispatchTripResponse DispatchTrip(Gateway.DispatchTripRequest request)
        {
            requests++;
            requests++; //Assuming Tripthru will quote itself in AutoDispatch
            Gateway.DispatchTripResponse resp = server.DispatchTrip(request);
            if (resp.result == Gateway.Result.Rejected)
                rejects++;
            /*if (resp.result == Result.OK)
            {
                var resp1 = GetTripStatus(new GetTripStatusRequest(request.clientID, request.tripID));
                if (resp1.distance != null) distance = distance + resp1.distance.Value;
                if (resp1.price != null) fare = fare + resp1.price.Value;
            }*/
            return resp;
        }

        public override Gateway.QuoteTripResponse QuoteTrip(Gateway.QuoteTripRequest request)
        {
            requests++;
            Gateway.QuoteTripResponse resp = server.QuoteTrip(request);
            if (resp.result == Gateway.Result.Rejected)
                rejects++;
            return resp;
        }

        public override Gateway.GetTripStatusResponse GetTripStatus(Gateway.GetTripStatusRequest request)
        {
            requests++;
            return server.GetTripStatus(request);
        }

        public override Gateway.UpdateTripStatusResponse UpdateTripStatus(Gateway.UpdateTripStatusRequest request)
        {
            requests++;
            switch (request.status)
            {
                case Status.Cancelled:
                    cancels++;
                    break;
                case Status.Complete:
                    completes++;
                    break;
            }
            return server.UpdateTripStatus(request);
        }

        public override Gateway.UpdateQuoteResponse UpdateQuote(Gateway.UpdateQuoteRequest request)
        {
            requests++;
            return server.UpdateQuote(request);
        }

        public override GetQuoteResponse GetQuote(GetQuoteRequest request)
        {
            requests++;
            return server.GetQuote(request);
        }
    }
}
