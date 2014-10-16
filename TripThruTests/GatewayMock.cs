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
        public Gateway server;
        public Dictionary<string, TripRequests> RequestsByTripId;
        public class TripRequests
        {
            public int Quote = 0;
            public int UpdateQuote = 0;
            public int GetQuote = 0;
            public int Dispatch = 0;
            public int RejectedUpdates = 0;
            public int CancelledUpdates = 0;
            public int QueuedUpdates = 0;
            public int DispatchedUpdates = 0;
            public int EnrouteUpdates = 0;
            public int PickedUpUpdates = 0;
            public int CompleteUpdates = 0;
            public int GetStatus = 0;
            public Gateway.UpdateTripStatusRequest CancelledRequest;
            public Gateway.UpdateTripStatusRequest RejectedRequest;
            public Gateway.UpdateTripStatusRequest QueuedRequest;
            public Gateway.UpdateTripStatusRequest DispatchedRequest;
            public Gateway.UpdateTripStatusRequest EnrouteRequest;
            public Gateway.UpdateTripStatusRequest PickedUpRequest;
            public Gateway.UpdateTripStatusRequest CompleteRequest;
        };

        public GatewayMock(Gateway server)
            : base(server.ID, server.name)
        {
            this.server = server;
            this.RequestsByTripId = new Dictionary<string, TripRequests>();
        }

        protected TripRequests GetTripRequests(string tripId)
        {
            if (!RequestsByTripId.ContainsKey(tripId))
                RequestsByTripId[tripId] = new TripRequests();
            return RequestsByTripId[tripId];
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
            LogDispatchRequest(request);
            Gateway.DispatchTripResponse resp = server.DispatchTrip(request);
            if (resp.result == Gateway.Result.Rejected)
                rejects++;
            return resp;
        }
        public override void DispatchTripAsync(Gateway.DispatchTripRequest request, Action<Gateway.DispatchTripResponse> callback)
        {
            LogDispatchRequest(request);
            server.DispatchTripAsync(request, callback);
        }
        private void LogDispatchRequest(Gateway.DispatchTripRequest request)
        {
            requests++;
            GetTripRequests(request.tripID).Dispatch++;
        }

        public override Gateway.QuoteTripResponse QuoteTrip(Gateway.QuoteTripRequest request)
        {
            LogQuoteRequest(request);
            Gateway.QuoteTripResponse resp = server.QuoteTrip(request);
            if (resp.result == Gateway.Result.Rejected)
                rejects++;
            return resp;
        }
        public override void QuoteTripAsync(Gateway.QuoteTripRequest request, Action<Gateway.QuoteTripResponse> callback)
        {
            LogQuoteRequest(request);
            server.QuoteTripAsync(request, callback);
        }
        private void LogQuoteRequest(Gateway.QuoteTripRequest request)
        {
            requests++;
            GetTripRequests(request.tripId).Quote++;
        }

        public override Gateway.GetTripStatusResponse GetTripStatus(Gateway.GetTripStatusRequest request)
        {
            requests++;
            GetTripRequests(request.tripID).GetStatus++;
            return server.GetTripStatus(request);
        }

        public override Gateway.UpdateTripStatusResponse UpdateTripStatus(Gateway.UpdateTripStatusRequest request)
        {
            Console.WriteLine("GM " + this.server.ID + " " + this.GetHashCode() +  "  UpdateTripStatus(" + request.status + ", " + request.tripID + ") received from " + request.clientID);
            LogUpdateTripStatusRequest(request);
            return server.UpdateTripStatus(request);
        }
        public override void UpdateTripStatusAsync(Gateway.UpdateTripStatusRequest request, Action<Gateway.UpdateTripStatusResponse> callback)
        {
            LogUpdateTripStatusRequest(request);
            server.UpdateTripStatusAsync(request, callback);
        }
        private void LogUpdateTripStatusRequest(Gateway.UpdateTripStatusRequest request)
        {

            requests++;
            var tripRequests = GetTripRequests(request.tripID);
            Console.WriteLine("Adding " + request.status + " request to " + tripRequests + " " + request.tripID);
            switch (request.status)
            {
                case Status.Queued:
                    tripRequests.QueuedUpdates++;
                    tripRequests.QueuedRequest = request;
                    break;
                case Status.Dispatched:
                    tripRequests.DispatchedUpdates++;
                    tripRequests.DispatchedRequest = request;
                    break;
                case Status.Enroute:
                    tripRequests.EnrouteUpdates++;
                    tripRequests.EnrouteRequest = request;
                    break;
                case Status.PickedUp:
                    tripRequests.PickedUpUpdates++;
                    tripRequests.PickedUpRequest = request;
                    break;
                case Status.Complete:
                    tripRequests.CompleteUpdates++;
                    tripRequests.CompleteRequest = request;
                    completes++;
                    break;
                case Status.Rejected:
                    tripRequests.RejectedUpdates++;
                    tripRequests.RejectedRequest = request;
                    break;
                case Status.Cancelled:
                    tripRequests.CancelledUpdates++;
                    tripRequests.CancelledRequest = request;
                    cancels++;
                    break;
            }
        }

        public override Gateway.UpdateQuoteResponse UpdateQuote(Gateway.UpdateQuoteRequest request)
        {
            LogUpdateQuoteRequest(request);
            return server.UpdateQuote(request);
        }
        public override void UpdateQuoteAsync(Gateway.UpdateQuoteRequest request, Action<Gateway.UpdateQuoteResponse> callback)
        {
            LogUpdateQuoteRequest(request);
            server.UpdateQuoteAsync(request, callback);
        }
        private void LogUpdateQuoteRequest(Gateway.UpdateQuoteRequest request)
        {
            requests++;
            GetTripRequests(request.tripId).UpdateQuote++;
        }

        public override GetQuoteResponse GetQuote(GetQuoteRequest request)
        {
            requests++;
            GetTripRequests(request.tripId).GetQuote++;
            return server.GetQuote(request);
        }
    }
}
