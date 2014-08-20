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
            return new Gateway.RegisterPartnerResponse
            {
                result = resp.result
            };
        }

        public override Gateway.GetPartnerInfoResponse GetPartnerInfo(Gateway.GetPartnerInfoRequest request)
        {
            requests++;
            Gateway.GetPartnerInfoResponse resp = server.GetPartnerInfo(request);

            Gateway.GetPartnerInfoResponse response = new Gateway.GetPartnerInfoResponse
            {
                fleets = resp.fleets,
                vehicleTypes = resp.vehicleTypes,
                result = resp.result
            };
            return response;
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
            Gateway.DispatchTripResponse response = new Gateway.DispatchTripResponse
            {
                result = resp.result,
            };
            return response;
        }

        public override Gateway.QuoteTripResponse QuoteTrip(Gateway.QuoteTripRequest request)
        {
            requests++;
            Gateway.QuoteTripResponse resp = server.QuoteTrip(request);
            if (resp.result == Gateway.Result.Rejected)
                rejects++;
            Gateway.QuoteTripResponse response = new Gateway.QuoteTripResponse
            {
                result = resp.result
            };
            return response;
        }

        public override Gateway.GetTripStatusResponse GetTripStatus(Gateway.GetTripStatusRequest request)
        {
            requests++;
            Gateway.GetTripStatusResponse resp = server.GetTripStatus(request);
            Gateway.GetTripStatusResponse response;
            if (resp.result == Gateway.Result.OK)
            {
                response = new Gateway.GetTripStatusResponse
                {
                    result = Gateway.Result.OK,
                    ETA = resp.ETA,
                    passengerName = resp.passengerName,
                    driverID = resp.driverID,
                    driverLocation = resp.driverLocation,
                    driverName = resp.driverName,
                    dropoffTime = resp.dropoffTime,
                    dropoffLocation = resp.dropoffLocation,
                    fleetName = resp.fleetName,
                    fleetID = resp.fleetID,
                    vehicleType = resp.vehicleType,
                    status = resp.status,
                    partnerName = resp.partnerName,
                    partnerID = resp.partnerID,
                    pickupTime = resp.pickupTime,
                    pickupLocation = resp.pickupLocation,
                    distance = resp.distance,
                    driverRouteDuration = resp.driverRouteDuration,
                    price = resp.price
                };
            }
            else
            {
                response = new Gateway.GetTripStatusResponse
                {
                        result = resp.result
                };
            }
            return response;
        }

        public override Gateway.UpdateTripStatusResponse UpdateTripStatus(Gateway.UpdateTripStatusRequest request)
        {
            requests++;
            UpdateTripStatusResponse resp = server.UpdateTripStatus(request);
            switch (request.status)
            {
                case Status.Cancelled:
                    cancels++;
                    break;
                case Status.Complete:
                    completes++;
                    break;
            }
            Gateway.UpdateTripStatusResponse response;
            response = new Gateway.UpdateTripStatusResponse
            {
                result = resp.result
            };
            return response;
        }

        public override Gateway.UpdateQuoteResponse UpdateQuote(Gateway.UpdateQuoteRequest request)
        {
            requests++;
            var resp = server.UpdateQuote(request);
            return new Gateway.UpdateQuoteResponse(resp.result);
        }

        public override GetQuoteResponse GetQuote(GetQuoteRequest request)
        {
            requests++;
            var resp = server.GetQuote(request);
            if (resp.result == Result.OK)
                return new Gateway.GetQuoteResponse(resp.status, resp.quotes);
            else
                return new Gateway.GetQuoteResponse(result: resp.result);
        }
    }
}
