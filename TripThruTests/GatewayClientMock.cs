using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TripThruCore;
using Utils;

namespace TripThruTests
{
    public class GatewayMock : GatewayWithStats
    {
        Gateway server;
        public Dictionary<string, TripRequests> RequestsByTripId;
        public class TripRequests
        {
            public int Quote = 0;
            public int UpdateQuote = 0;
            public int GetQuote = 0;
            public int Reject = 0;
            public int Cancel = 0;
            public int Dispatch = 0;
            public int UpdateQueued = 0;
            public int UpdateDispatched = 0;
            public int UpdateEnroute = 0;
            public int UpdatePickedUp = 0;
            public int UpdateComplete = 0;
            public int GetStatus = 0;
        };

        public GatewayMock(Gateway server)
            : base(server.ID, server.name)
        {
            this.server = server;
            this.RequestsByTripId = new Dictionary<string, TripRequests>();
        }

        private void InitializeTripRequestsList(string tripId)
        {
            if (!RequestsByTripId.ContainsKey(tripId))
                RequestsByTripId[tripId] = new TripRequests();
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
            InitializeTripRequestsList(request.tripID);
            RequestsByTripId[request.tripID].Dispatch++;
            Gateway.DispatchTripResponse resp = server.DispatchTrip(request);
            if (resp.result == Gateway.Result.Rejected)
                rejects++;
            return resp;
        }

        public override Gateway.QuoteTripResponse QuoteTrip(Gateway.QuoteTripRequest request)
        {
            requests++;
            InitializeTripRequestsList(request.tripId);
            RequestsByTripId[request.tripId].Quote++;
            Gateway.QuoteTripResponse resp = server.QuoteTrip(request);
            if (resp.result == Gateway.Result.Rejected)
                rejects++;
            return resp;
        }

        public override Gateway.GetTripStatusResponse GetTripStatus(Gateway.GetTripStatusRequest request)
        {
            requests++;
            InitializeTripRequestsList(request.tripID);
            RequestsByTripId[request.tripID].GetStatus++;
            return server.GetTripStatus(request);
        }

        public override Gateway.UpdateTripStatusResponse UpdateTripStatus(Gateway.UpdateTripStatusRequest request)
        {
            requests++;
            InitializeTripRequestsList(request.tripID);
            var tripRequests = RequestsByTripId[request.tripID];
            switch (request.status)
            {
                case Status.Queued:
                    tripRequests.UpdateQueued++;
                    break;
                case Status.Dispatched:
                    tripRequests.UpdateDispatched++;
                    break;
                case Status.Enroute:
                    tripRequests.UpdateEnroute++;
                    break;
                case Status.PickedUp:
                    tripRequests.UpdatePickedUp++;
                    break;
                case Status.Complete:
                    tripRequests.UpdateComplete++;
                    completes++;
                    break;
                case Status.Rejected:
                    tripRequests.Reject++;
                    break;
                case Status.Cancelled:
                    tripRequests.Cancel++;
                    cancels++;
                    break;
            }
            return server.UpdateTripStatus(request);
        }

        public override Gateway.UpdateQuoteResponse UpdateQuote(Gateway.UpdateQuoteRequest request)
        {
            requests++;
            InitializeTripRequestsList(request.tripId);
            RequestsByTripId[request.tripId].UpdateQuote++;
            return server.UpdateQuote(request);
        }

        public override GetQuoteResponse GetQuote(GetQuoteRequest request)
        {
            requests++;
            InitializeTripRequestsList(request.tripId);
            RequestsByTripId[request.tripId].GetQuote++;
            return server.GetQuote(request);
        }
    }
}
