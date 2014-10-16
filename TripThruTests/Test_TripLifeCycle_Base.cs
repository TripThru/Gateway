using System;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using TripThruTests;
using Utils;
using TripThruCore;
using System.Threading;
using TripThruCore.Storage;

namespace TripThruTests
{
    public class Test_TripLifeCycle_Base
    {
        public string filename;
        public GatewayMock tripthru;
        public Partner partner;
        public GatewayMock partnerServiceMock;
        PartnerTrip.Origination? origination = null;
        PartnerTrip.Origination? service = null;
        public static TimeSpan simInterval = new TimeSpan(0, 0, 1);
        public TimeSpan maxLateness = new TimeSpan(0, 5, 0);
        public double locationVerificationTolerance = .6;
        public int _activeTrips;
        List<String> tripsList = new List<String>();
        private static bool testsRunning = false;

        public class UnitTest_SingleTripLifecycleAndReturningDriver : SubTest
        {
            PartnerFleet fleet;
            Pair<Location, Location> tripSpec;
            Test_TripLifeCycle_Base parent;
            public UnitTest_SingleTripLifecycleAndReturningDriver(Test_TripLifeCycle_Base parent, PartnerFleet fleet, Pair<Location, Location> tripSpec)
            {
                if (parent == null)
                    throw new Exception("parent must be defined");
                this.parent = parent;
                this.fleet = fleet;
                this.tripSpec = tripSpec;
            }

            public override void Run()
            {
                try
                {
                    Console.WriteLine("Trip: <" + tripSpec.First.Lat + ", " + tripSpec.First.Lng + ">, " + 
                                            "<" + tripSpec.Second.Lat + ", " + tripSpec.Second.Lng + ">");
                    parent.TestTripLifecycleAndReturningDriver(fleet, tripSpec);

                    Console.WriteLine("Success, Trip: <" + tripSpec.First.Lat + ", " + tripSpec.First.Lng + ">, " +
                                            "<" + tripSpec.Second.Lat + ", " + tripSpec.Second.Lng + ">");
                    Passed = true;
                }
                catch (Exception ex)
                {
                    Exception = ex;
                    Passed = null;
                }
            }
        }

        public Test_TripLifeCycle_Base()
        {

        }

        public Test_TripLifeCycle_Base(
            string filename, 
            GatewayMock tripthru, 
            TimeSpan? maxLateness = null, 
            PartnerTrip.Origination? origination = null, 
            PartnerTrip.Origination? service = null, 
            double? locationVerificationTolerance = null)
        {
            this.filename = filename;
            this.tripthru = tripthru;
            if (maxLateness != null)
                this.maxLateness = (TimeSpan)maxLateness;
            if (origination != null)
                this.origination = origination;
            if (service != null)
                this.service = service;
            if (locationVerificationTolerance != null)
                this.locationVerificationTolerance = (double)locationVerificationTolerance;
            PartnerConfiguration configuration = Partner.LoadPartnerConfigurationFromJsonFile(filename);
            partner = new Partner(configuration.Partner.ClientId, configuration.Partner.Name, new GatewayMock(this.tripthru), configuration.partnerFleets);
            var coverage = new List<Zone>();
            foreach (var fleet in partner.PartnerFleets.Values)
                coverage.AddRange(fleet.coverage);
            this.partnerServiceMock = new GatewayMock(partner);
            partner.tripthru.RegisterPartner(partnerServiceMock, coverage);
        }

        public List<SubTest> MakeSimultaneousTripLifecycle_SubTests()
        {
            List<SubTest> singleTrips_Subtests = new List<SubTest>();
            foreach (PartnerFleet fleet in partner.PartnerFleets.Values)
            {
                foreach (Pair<Location, Location> tripSpec in fleet.possibleTrips)
                    singleTrips_Subtests.Add(new UnitTest_SingleTripLifecycleAndReturningDriver(this, fleet, tripSpec));
            }
            return singleTrips_Subtests;
        }

        public static void RunSubTests(List<Partner> partners, List<SubTest> singleTrips_Subtests, DateTime timeoutAt, TimeSpan simInterval)
        {
            Test_TripLifeCycle_Base.testsRunning = true;
            Test_TripLifeCycle_Base.simInterval = simInterval;
            var fleets = new List<PartnerFleet>();
            foreach (var partner in partners)
                fleets.AddRange(partner.PartnerFleets.Values);
            var runningThreads = new List<Thread>();
            runningThreads.AddRange(MakeFleetForeignQueueThreads(fleets, simInterval));
            runningThreads.AddRange(MakeSubTestsThreads(singleTrips_Subtests));
            foreach (var thread in runningThreads)
                thread.Start();
            
            bool? passed = null;
            while (passed == null && DateTime.UtcNow < timeoutAt)
            {
                passed = false;
                foreach (SubTest test in singleTrips_Subtests)
                {
                    if (test.Exception != null)
                    {
                        Console.WriteLine(test.Exception.Message + " : " + test.Exception);
                        Test_TripLifeCycle_Base.testsRunning = false;
                        throw test.Exception;
                    }
                    if (test.Passed == null)
                        passed = null;
                }
                Thread.Sleep(simInterval);
            }
            Test_TripLifeCycle_Base.testsRunning = false;
        }
        private static List<Thread> MakeSubTestsThreads(List<SubTest> subtests)
        {
            var threads = new List<Thread>();
            foreach (SubTest s in subtests)
            {
                var thread = new Thread(s.Run);
                thread.IsBackground = true;
                threads.Add(thread);
            }
            return threads;
        }
        //Todo: We need to process foreign trips separately since we are calling ProcessTrip not ProcessQueue for local trips
        private static List<Thread> MakeFleetForeignQueueThreads(List<PartnerFleet> fleets, TimeSpan simInterval)
        {
            var fleetForeignQueueThreads = new List<Thread>();
            foreach (var fleet in fleets)
            {
                var thread = new Thread(() => {
                    while (Test_TripLifeCycle_Base.testsRunning)
                    {
                        fleet.ProcessForeignTrips();
                        Thread.Sleep(simInterval);
                    }
                });
                thread.IsBackground = true;
                fleetForeignQueueThreads.Add(thread);
            }
            return fleetForeignQueueThreads;
        }

        public void Test_SingleTripLifecycle_ForAllPartnerFleets()
        {
            Test_TripLifeCycle_Base.testsRunning = true;
            foreach (PartnerFleet fleet in partner.PartnerFleets.Values)
            {
                var i = 1;
                foreach (Pair<Location, Location> tripStartEnd in fleet.possibleTrips)
                {
                    Console.WriteLine("Trip " + i++ + "/" + fleet.possibleTrips.Length);
                    TestTripLifecycleAndReturningDriver(fleet, tripStartEnd);
                }
                
            }
            Test_TripLifeCycle_Base.testsRunning = false;
        }

        public void TestTripLifecycleAndReturningDriver(PartnerFleet fleet, Pair<Location, Location> tripSpec)
        {
            PartnerTrip trip = fleet.GenerateTrip(fleet.passengers[0], DateTime.UtcNow, tripSpec);
            TestTripLifecycle_FromNewToComplete(fleet, trip);
            Thread.Sleep(5000); // Give tripthru a margin to finish processing the completed trip
            ValidateTripRequests(partnerServiceMock, (GatewayMock) partner.tripthru, trip);
            if (DriverHasToReturn(trip))
                ValidateReturningDriverRouteIfServiceLocal(fleet, trip);
        }

        public void TestTripLifecycle_FromNewToComplete(PartnerFleet fleet, PartnerTrip trip)
        {
            int activeTrips, response;
            Assert.AreEqual(Status.New, trip.status,"The trip: " + trip.ID + " isn't new.");
            
            lock (fleet)
            {
                fleet.QueueTrip(trip);
                response = partner.tripsByID.Count(t => t.Value.origination == PartnerTrip.Origination.Local && t.Value.status != Status.Complete);
                activeTrips = ++_activeTrips;
            }

            Assert.AreEqual(Status.Queued, trip.status, "The trip: " + trip.ID + " was not queued.");
            Assert.AreEqual(activeTrips, response, "Active trips count doesn't match. Trip: " + trip.ID);
            ValidateNextTripStatus(fleet, trip, Status.Dispatched);
            ValidateNextTripStatus(fleet, trip, Status.Enroute);
            ValidateNextTripStatus(fleet, trip, Status.PickedUp);
            ValidateNextTripStatus(fleet, trip, Status.Complete);
            
            lock (fleet)
            {
                var trips = partner.tripsByID.Where(
                    t => t.Value.origination == PartnerTrip.Origination.Local && t.Value.status == Status.Complete);

                foreach (var tripp in trips)
                {
                    try
                    {
                        if (tripsList.Contains(tripp.Key)) continue;
                        tripsList.Add(tripp.Key);
                        --_activeTrips;
                    }catch
                    {}
                }
            }
        }

        public void ValidateNextTripStatus(PartnerFleet fleet, PartnerTrip trip, Status nextStatus)
        {
            var timeoutAt = GetTimeWhenStatusShouldBeReached(trip);
            var statusReached = false;
            if (nextStatus == Status.Dispatched)
                statusReached = WaitUntilTripIsSuccessfullyDispatchedToTripThruOrTimesout(fleet, trip, timeoutAt);
            else
                statusReached = WaitUntilStatusReachedOrTimeout(fleet, trip, nextStatus, timeoutAt);

            Assert.IsTrue(statusReached,
                "Reached timeout but trip didn't advance to expected status " + nextStatus + ". Trip ID: " + trip.ID +
                ". ETA: " + trip.ETA.ToString() + ". Timeout at: " + timeoutAt.ToString() + ". Time now: " + DateTime.UtcNow.ToString());
 

            Thread.Sleep(new TimeSpan(0, 0, 5)); // Give enough time for updates to reach all parties

            /* 
             * - If trip is still in Queued status we need to verify that it actually got a Rejected update from tripthru.
             * - When trip is foreign it's also possible that the servicing partner sends more that one update before tripthru 
             *   notifies the first one received, giving the impression that we skipped a status, so we verify 
             *   this to make sure it was actually sent.
             */
            if (trip.status != nextStatus )
            {
                if (trip.status == Status.Queued)
                    ValidateTripWasRejected(trip);
                else if (TripIsForeign(trip))
                    ValidateTripThruReceivedStatusUpdateButSkippedIt(trip, nextStatus);
                return;
            }

            Assert.AreEqual(nextStatus, trip.status, "The trip did not advance to the expected status. Trip ID: " + trip.ID);
            ValidateTripThruStatus(trip);
            ValidateTripStatusLocation(trip, trip.status, trip.driverLocation);
        }

        private bool TripIsForeign(PartnerTrip trip)
        {
            /* 
             * Check against fleet coverage zone instead of trip.service since trip could still be queued.
             */
            return !trip.PartnerFleet.FleetServesLocation(trip.pickupLocation);
        }

        private bool WaitUntilStatusReachedOrTimeout(PartnerFleet fleet, PartnerTrip trip, Status nextStatus, DateTime timeoutAt)
        {
            while (trip.status != nextStatus && DateTime.UtcNow < timeoutAt && Test_TripLifeCycle_Base.testsRunning)
            {
                /*
                 * There's a reason we're calling ProcessTrip instead of ProcessQueue, as when there are multiple trips 
                 * in a queue, a call to ProcessQueue may end up processing more than one queue.  
                 * Then it may seem like trips jump a state (status).
                */
                fleet.ProcessTrip(trip);
                Thread.Sleep(simInterval);
            }
            return trip.status == nextStatus;
        }
        private bool WaitUntilTripIsSuccessfullyDispatchedToTripThruOrTimesout(PartnerFleet fleet, PartnerTrip trip, DateTime timeoutAt)
        {
            /* 
             * The trip will advance to dispatched status on the partner's side but tripthru could reject it so we wait 
             * to confirm if tripthru successfully dispatched the trip.
             */
            Status? status = null;
            while (status != Status.Dispatched && DateTime.UtcNow < timeoutAt && Test_TripLifeCycle_Base.testsRunning)
            {
                /*
                 * There's a reason we're calling ProcessTrip instead of ProcessQueue, as when there are multiple trips 
                 * in a queue, a call to ProcessQueue may end up processing more than one queue.  
                 * Then it may seem like trips jump a state (status).
                 */
                fleet.ProcessTrip(trip);
                Thread.Sleep(simInterval);

                var response = tripthru.GetTripStatus(new Gateway.GetTripStatusRequest(partner.ID, trip.publicID));
                if (response.result == Gateway.Result.OK)
                    status = response.status;
            }
            return status == Status.Dispatched;
        }

        private void ValidateTripWasRejected(PartnerTrip trip)
        {
            var message = "Trip didn't advance from Queued status but wasn't rejected either. Trip: " + trip.ID;
            Assert.IsTrue(partnerServiceMock.RequestsByTripId.ContainsKey(trip.publicID), message + ". No requests received.");
            var requests = partnerServiceMock.RequestsByTripId[trip.publicID];
            Assert.GreaterOrEqual(requests.RejectedUpdates, 1, message + ". No rejected update received.");
        }

        private void ValidateTripThruStatus(PartnerTrip trip)
        {
            var tripId = trip.publicID;
            Gateway.GetTripStatusResponse response = tripthru.GetTripStatus(new Gateway.GetTripStatusRequest(partner.ID, tripId));
            if (trip.status != response.status && TripIsForeign(trip))
                ValidateTripThruReceivedStatusUpdateButSkippedIt(trip, trip.status);
            else
                Assert.AreEqual(trip.status, response.status, "The trip local status doesn't match the gateway status. Trip ID: " + tripId);
            ValidateTripStatusLocation(trip, trip.status, response.driverLocation);
        }

        private void ValidateTripThruReceivedStatusUpdateButSkippedIt(PartnerTrip trip, Status status)
        {
            var tripId = trip.publicID;
            var requests = tripthru.RequestsByTripId[tripId];
            Location location = null;
            switch (status)
            {
                case Status.Dispatched:
                    Assert.AreEqual(1, requests.DispatchedUpdates, 
                        "TripThru didn't receive Dispatched trip status update and trip is "
                        + trip.status +" . TripID: " + trip.ID);
                    location = requests.DispatchedRequest.driverLocation;
                    break;
                case Status.Enroute:
                    Assert.AreEqual(1, requests.EnrouteUpdates,
                        "TripThru didn't receive Enroute trip status update and trip is "
                        + trip.status + " . TripID: " + trip.ID);
                    location = requests.EnrouteRequest.driverLocation;
                    break;
                case Status.PickedUp:
                    Assert.AreEqual(1, requests.PickedUpUpdates,
                        "TripThru didn't receive PickedUp trip status update and trip is "
                        + trip.status + " . TripID: " + trip.ID);
                    location = requests.PickedUpRequest.driverLocation;
                    break;
                case Status.Complete:
                    Assert.AreEqual(1, requests.CompleteUpdates,
                        "TripThru didn't receive Complete trip status update and trip is "
                        + trip.status + " . TripID: " + trip.ID);
                    location = requests.CompleteRequest.driverLocation;
                    break;
            }
            ValidateTripStatusLocation(trip, status, location);
        }

        private void ValidateTripStatusLocation(PartnerTrip trip, Status status, Location location)
        {
            switch (status)
            {
                case Status.Enroute:
                    Assert.IsNotNull(location, "The trip is Enroute but the location is null. Trip ID: " + trip.ID);
                    break;
                case Status.PickedUp:
                    Assert.IsNotNull(location, "The trip is PickedUp but the location is null. Trip ID: " + trip.ID);
                    Assert.IsTrue(location.Equals(trip.pickupLocation, tolerance: locationVerificationTolerance),
                        "The trip is PickedUp but the location is out to the tolerance area. Trip ID: " + trip.ID + "."
                        + "Expected " + trip.pickupLocation.getID() + " but got " + location.getID());
                    break;
                case Status.Complete:
                    Assert.IsNotNull(location, "The trip is Complete but the location is null. Trip ID: " + trip.ID);
                    Assert.IsTrue(location.Equals(trip.dropoffLocation, tolerance: locationVerificationTolerance),
                        "The trip is Complete but the lLocation is out to the tolerance area. Trip ID: " + trip.ID + "."
                        + "Expected " + trip.dropoffLocation.getID() + " but got " + location.getID());
                    break;
            }
        }

        private DateTime GetTimeWhenStatusShouldBeReached(PartnerTrip trip)
        {
            DateTime timeoutAt;
            switch (trip.status)
            {
                case Status.Enroute:
                case Status.PickedUp:
                case Status.Complete:
                    {
                        Assert.IsNotNull(trip.ETA, "The trip ETA is null. Trip ID:" + trip.ID);
                        timeoutAt = (DateTime)trip.ETA + maxLateness;
                        break;
                    }
                default:
                    {
                        timeoutAt = DateTime.UtcNow + maxLateness;
                        break;
                    }
            }
            return timeoutAt;
        }

        private bool DriverHasToReturn(PartnerTrip trip)
        {
            return trip.status == Status.Complete || trip.status == Status.PickedUp || trip.status == Status.Enroute;
        }

        private void ValidateReturningDriverRouteIfServiceLocal(PartnerFleet fleet, PartnerTrip trip)
        {
            if (trip.service == PartnerTrip.Origination.Foreign)
                return;
            Status currentStatus = trip.status;
            Assert.AreNotEqual(trip.ETA, null, "The trip ETA is null. Trip: " + trip.ID);
            DateTime timeoutAt = (DateTime) trip.ETA + maxLateness;
            while (!trip.driver.location.Equals(fleet.location, tolerance: locationVerificationTolerance) && Test_TripLifeCycle_Base.testsRunning)
            {
                fleet.UpdateReturningDriverLocations();
                //Assert.IsFalse(DateTime.UtcNow > timeoutAt, "The timeoutAt is less than UtcNow. Trip ID: " + trip.ID);
                System.Threading.Thread.Sleep(simInterval);
            }
        }

        private void ValidateTripRequests(GatewayMock originatingGateway, GatewayMock servicingGateway, PartnerTrip trip)
        {
            var id = trip.publicID;
            Assert.IsTrue(originatingGateway.RequestsByTripId.ContainsKey(id), "Should have received at least one request. Trip: " + trip.ID);
            Assert.IsTrue(servicingGateway.RequestsByTripId.ContainsKey(id), "Should have sent at least one request. Trip: " + trip.ID);

            var receivedRequests = originatingGateway.RequestsByTripId[id];
            var sentRequests = servicingGateway.RequestsByTripId[id];
            if (trip.service == PartnerTrip.Origination.Foreign)
            {
                ValidateSentRequestsForTripServiceForeign(receivedRequests, sentRequests, trip);
                ValidateReceivedRequestsForTripServiceForeign(receivedRequests, sentRequests, trip);
            }
            else
            {
                ValidateSentRequestsForTripServiceLocal(receivedRequests, sentRequests, trip);
                ValidateReceivedRequestsForTripServiceLocal(receivedRequests, sentRequests, trip);
            }
        }
        private void ValidateSentRequestsForTripServiceForeign(GatewayMock.TripRequests receivedRequests, GatewayMock.TripRequests sentRequests, PartnerTrip trip)
        {
            Assert.Greater(sentRequests.Dispatch, 0, "Never made a dispatch request. Trip: " + trip.ID);
        }
        private void ValidateReceivedRequestsForTripServiceForeign(GatewayMock.TripRequests receivedRequests, GatewayMock.TripRequests sentRequests, PartnerTrip trip)
        {
            Assert.GreaterOrEqual(sentRequests.Dispatch, 1, "Should send dispatch request at least once. Trip: " + trip.ID);
            Assert.LessOrEqual(receivedRequests.DispatchedUpdates, 1, "Should receive dispatch status update at most once. Trip: " + trip.ID);
            Assert.LessOrEqual(receivedRequests.EnrouteUpdates, 1, "Should receive enroute status update at most once. Trip: " + trip.ID);
            Assert.LessOrEqual(receivedRequests.PickedUpUpdates, 1, "Should receive pickuedup status update at most once. Trip: " + trip.ID);
            Assert.LessOrEqual(receivedRequests.CompleteUpdates, 1, "Should receive complete status update at most once. Trip: " + trip.ID);
            
            if (trip.status == Status.Complete)
            {
                var updateRequests = receivedRequests.DispatchedUpdates
                    + receivedRequests.EnrouteUpdates
                    + receivedRequests.PickedUpUpdates
                    + receivedRequests.CompleteUpdates;
                Assert.GreaterOrEqual(updateRequests, 1, "Should have received at least one status update request. Trip: " + trip.ID);
            }
        }
        private void ValidateSentRequestsForTripServiceLocal(GatewayMock.TripRequests receivedRequests, GatewayMock.TripRequests sentRequests, PartnerTrip trip)
        {
            if (trip.origination == PartnerTrip.Origination.Foreign)
                Assert.GreaterOrEqual(receivedRequests.Dispatch, 1, "Should receive dispatch request at least once. Trip: " + trip.ID);
            Assert.LessOrEqual(sentRequests.DispatchedUpdates, 1, "Should send dispatch status update at most once. Trip: " + trip.ID);
            Assert.LessOrEqual(sentRequests.EnrouteUpdates, 1, "Should send enroute status update at most once. Trip: " + trip.ID);
            Assert.LessOrEqual(sentRequests.PickedUpUpdates, 1, "Should send pickedup status update at most once. Trip: " + trip.ID);
            Assert.LessOrEqual(sentRequests.CompleteUpdates, 1, "Should send complete status update at most once. Trip: " + trip.ID);

            if (trip.status == Status.Complete)
            {
                Assert.AreEqual(1, sentRequests.DispatchedUpdates, "Should send dispatch status update only once. Trip: " + trip.ID);
                Assert.AreEqual(1, sentRequests.EnrouteUpdates, "Should send enroute status update only once. Trip: " + trip.ID);
                Assert.AreEqual(1, sentRequests.PickedUpUpdates, "Should send pickedup status update only once. Trip: " + trip.ID);
                Assert.AreEqual(1, sentRequests.CompleteUpdates, "Should send complete status update only once. Trip: " + trip.ID);
            }
        }
        private void ValidateReceivedRequestsForTripServiceLocal(GatewayMock.TripRequests receivedRequests, GatewayMock.TripRequests sentRequests, PartnerTrip trip)
        {
            if (trip.status == Status.Complete)
                Assert.AreEqual(1, receivedRequests.GetStatus, "Should receive GetTripStatus request to update gateway stats once. Trip: " + trip.ID);
        }
    }

    public class SubTest
    {
        public bool? Passed = null;
        public Exception Exception = null;
        public virtual void Run()
        {

        }
    }

}
