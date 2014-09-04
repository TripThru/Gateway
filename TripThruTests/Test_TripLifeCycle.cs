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

namespace Tests
{
    [TestFixture]
    [Category("Trip life cycle")]
    public class TripLifeCycle_Tester
    {
        [SetUp]
        public void SetUp()
        {
            Logger.OpenLog("Nunit", splunkEnabled: false);
            MapTools.ClearCache();
            StorageManager.OpenStorage(new MongoDbStorage("mongodb://localhost:27017/", "TripThru"));
            StorageManager.Reset(); // Sometimes mongo can't delete on teardown between tests
            Logger.Log("Setting up");
            Logger.Tab();
            MapTools.distance_and_time_scale = .05;
            MapTools.SetGeodataFilenames(locationNames: "Test_GeoData/Geo-Location-Names.txt",
                routes: "Test_GeoData/Geo-Routes.txt",
                locationAddresses: "Test_GeoData/Geo-Location-Addresses.txt");
            MapTools.LoadGeoData();
            Logger.Untab();
        }

        [TearDown]
        public void TearDown()
        {
            Logger.Log("Tearing down");
            StorageManager.Reset();
            MapTools.ClearCache();
        }

        [Test]
        public void EnoughDrivers_SingleTrips()
        {
            Logger.Log("Test_TripLifeCycle_EnoughDrivers_SingleTrips");
            var tripthru = new GatewayMock(new TripThru(enableTDispatch: false));
            Test_TripLifeCycle_Base lib = new Test_TripLifeCycle_Base("Test_Configurations/LocalTripsEnoughDrivers.txt",
                tripthru: tripthru,
                maxLateness: new TimeSpan(0, 5, 0));
            lib.Test_SingleTripLifecycle_ForAllPartnerFleets();
        }

        [Test]
        public void EnoughDrivers_SimultaneousTrips()
        {
            Logger.Log("Test_TripLifeCycle_EnoughDrivers_SimultaneousTrips");
            var tripthru = new GatewayMock(new TripThru(enableTDispatch: false));
            Test_TripLifeCycle_Base lib = new Test_TripLifeCycle_Base("Test_Configurations/LocalTripsEnoughDrivers.txt",
                tripthru: tripthru,
                maxLateness: new TimeSpan(0, 5, 0));
            List<SubTest> subTests = lib.MakeSimultaneousTripLifecycle_SubTests();
            List<Partner> partners = new List<Partner>() { lib.partner };
            Test_TripLifeCycle_Base.RunSubTests(partners, subTests,
                timeoutAt: DateTime.UtcNow + new TimeSpan(1, 0, 0),
                simInterval: new TimeSpan(0, 0, 1)
            );
        }

        [Test]
        [ExpectedException(typeof(AssertionException), ExpectedMessage = "But was:  Queued", MatchType = MessageMatch.Contains)]
        public void NotEnoughDrivers_SingleTrips()
        {
            Logger.Log("Test_TripLifeCycle_NotEnoughDrivers_SingleTrips");
            /* In this test since there are not enough drivers it tries to dispatch to tripthru --
             * which has an empty implementation that always rejects.
             * We expect an assertion error because we expect the trip status to change from Queued to Dispatched
             * */
            var tripthru = new GatewayMock(new TripThru(enableTDispatch: false));
            Test_TripLifeCycle_Base lib = new Test_TripLifeCycle_Base(
                filename: "Test_Configurations/LocalTripsNotEnoughDrivers.txt",
                tripthru: tripthru,
                maxLateness: new TimeSpan(0, 1, 0));
            lib.Test_SingleTripLifecycle_ForAllPartnerFleets();
        }

        [Test]
        [ExpectedException(typeof(AssertionException), ExpectedMessage = "But was:  Queued", MatchType = MessageMatch.Contains)]
        public void NotEnoughDrivers_SimultaneousTrips_VerifyRejected()
        {
            Logger.Log("NotEnoughDrivers_SimultaneousTrips_VerifyRejected");
            /* In this test since there are not enough drivers it tries to dispatch to tripthru --
             * which has an empty implementation that always rejects.
             * We expect an assertion error because we expect the trip status to change from Queued to Dispatched
             * */
            var tripthru = new GatewayMock(new TripThru(enableTDispatch: false));
            Test_TripLifeCycle_Base lib = new Test_TripLifeCycle_Base(
                filename: "Test_Configurations/LocalTripsNotEnoughDriversSimultaneous.txt",
                tripthru: tripthru,
                maxLateness: new TimeSpan(0, 1, 0));
            List<SubTest> subTests = lib.MakeSimultaneousTripLifecycle_SubTests();
            List<Partner> partners = new List<Partner>() { lib.partner };
            Test_TripLifeCycle_Base.RunSubTests(partners, subTests,
                timeoutAt: DateTime.UtcNow + new TimeSpan(0, 10, 0),
                simInterval: new TimeSpan(0, 0, 1)
            );
        }

        [Test]
        public void NotEnoughDrivers_SimultaneousTrips_AllowTimeForDriversToBecomeAvailable()
        {
            Logger.Log("NotEnoughDrivers_SimultaneousTrips_AllowTimeForDriversToBecomeAvailable");
            /* In this test since there are not enough drivers it tries to dispatch to tripthru --
             * which has an empty implementation that always rejects.
             * We expect an assertion error because we expect the trip status to change from Queued to Dispatched
             * */
            var tripthru = new GatewayMock(new TripThru(enableTDispatch: false));
            Test_TripLifeCycle_Base lib = new Test_TripLifeCycle_Base(
                filename: "Test_Configurations/LocalTripsNotEnoughDriversSimultaneous.txt",
                tripthru: tripthru,
                maxLateness: new TimeSpan(0, 20, 0));
            List<SubTest> subTests = lib.MakeSimultaneousTripLifecycle_SubTests();
            List<Partner> partners = new List<Partner>() { lib.partner };
            Test_TripLifeCycle_Base.RunSubTests(partners, subTests,
                timeoutAt: DateTime.UtcNow + new TimeSpan(1, 0, 0), 
                simInterval : new TimeSpan(0, 0, 1)
            );
        }

        [Test]
        public void EnoughDrivers_TwoPartnersShareJobs_Gateway()
        {
            Logger.Log("Test_TripLifeCycle_EnoughDrivers_TwoPartnersShareJobs_Gateway");
            var tripthru = new GatewayMock(new TripThru(enableTDispatch: false));
            var libA = new Test_TripLifeCycle_Base(
                filename: "Test_Configurations/ForeignTripsEnoughDriversA.txt",
                tripthru: tripthru,
                maxLateness: new TimeSpan(0, 5, 0),
                origination: PartnerTrip.Origination.Local,
                service: PartnerTrip.Origination.Foreign, 
                locationVerificationTolerance: 4);
            var libB = new Test_TripLifeCycle_Base(
                filename: "Test_Configurations/ForeignTripsEnoughDriversB.txt",
                tripthru: tripthru,
                maxLateness: new TimeSpan(0, 5, 0),
                origination: PartnerTrip.Origination.Local,
                service: PartnerTrip.Origination.Foreign,
                locationVerificationTolerance: 4);
            List<SubTest> subTests = libA.MakeSimultaneousTripLifecycle_SubTests();
            subTests.AddRange(libB.MakeSimultaneousTripLifecycle_SubTests());
            List<Partner> partners = new List<Partner>() { libA.partner, libB.partner };
            Test_TripLifeCycle_Base.RunSubTests(partners, subTests, 
                timeoutAt: DateTime.UtcNow + new TimeSpan(1, 0, 0), 
                simInterval : new TimeSpan(0, 0, 1)
            );
        }

        [Test]
        public void EnoughDrivers_AllPartners_Gateway()
        {
            Logger.Log("AllPartners_Gateway");
            var tripthru = new GatewayMock(new TripThru(enableTDispatch: false));
            TimeSpan maxLateness = new TimeSpan(0, 20, 0);
            double locationVerificationTolerance = 4;
            string[] filePaths = Directory.GetFiles("../../Test_Configurations/Partners/");
            Logger.Log("filePaths = " + filePaths);
            List<SubTest> subtests = new List<SubTest>();
            List<Partner> partners = new List<Partner>();
            foreach (string filename in filePaths)
            {
                Logger.Log("filename = " + filename);
                var lib = new Test_TripLifeCycle_Base(
                    filename: filename,
                    tripthru: tripthru,
                    maxLateness: maxLateness,
                    locationVerificationTolerance: locationVerificationTolerance);
                partners.Add(lib.partner);
                subtests.AddRange(lib.MakeSimultaneousTripLifecycle_SubTests());
            }
            Test_TripLifeCycle_Base.RunSubTests(partners, subtests,
                timeoutAt: DateTime.UtcNow + new TimeSpan(1, 0, 0), 
                simInterval: new TimeSpan(0, 0, 1)
            );
        }

    }

    public class Test_TripLifeCycle_Base
    {
        public string filename;
        public GatewayMock tripthru;
        public Partner partner;
        private GatewayMock partnerServiceMock;
        PartnerTrip.Origination? origination = null;
        PartnerTrip.Origination? service = null;
        public TimeSpan simInterval = new TimeSpan(0, 0, 1);
        public TimeSpan maxLateness = new TimeSpan(0, 0, 2);
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
            Assert.AreEqual(activeTrips, response, "Active trips count doesn't match.");
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
            if (nextStatus == Status.Dispatched)
                WaitUntilTripIsSuccessfullyDispatchedToTripThruOrTimesout(fleet, trip, GetTimeWhenStatusShouldBeReached(trip));
            else
                WaitUntilStatusReachedOrTimeout(fleet, trip, nextStatus, GetTimeWhenStatusShouldBeReached(trip));

            /* 
             * When trip is foreign and it doesn't advance to the expected status:
             * - If trip is still in Queued status we need to verify that it actually got a Rejected update from tripthru.
             * - It's also possible that the servicing partner sends more that one update before tripthru 
             *   notifies the first one received, giving the impression that we skipped a status, so we verify 
             *   this to make sure it was actually sent.
             */
            if (trip.status != nextStatus && TripIsForeign(trip))
            {
                if (trip.status == Status.Queued)
                    ValidateTripWasRejected(trip);
                else
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

        private void WaitUntilStatusReachedOrTimeout(PartnerFleet fleet, PartnerTrip trip, Status nextStatus, DateTime timeoutAt)
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
        }
        private void WaitUntilTripIsSuccessfullyDispatchedToTripThruOrTimesout(PartnerFleet fleet, PartnerTrip trip, DateTime timeoutAt)
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
            Thread.Sleep(new TimeSpan(0, 0, 5)); // Give tripthru enough time to notify update to originating partner
        }

        private void ValidateTripWasRejected(PartnerTrip trip)
        {
            var requests = partnerServiceMock.RequestsByTripId[trip.ID];
            Assert.GreaterOrEqual(requests.RejectedUpdates, 1,
                "Trip didn't advance from Queued status but wasn't never rejected");
        }

        private void ValidateTripThruStatus(PartnerTrip trip)
        {
            var tripId = trip.publicID;
            Gateway.GetTripStatusResponse response = tripthru.GetTripStatus(new Gateway.GetTripStatusRequest(partner.ID, tripId));
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
                        "TripThru didn't receive Dispatched trip status update. TripID: " + tripId);
                    break;
                case Status.Enroute:
                    Assert.AreEqual(1, requests.EnrouteUpdates,
                        "TripThru didn't receive Enroute trip status update. TripID: " + tripId);
                    break;
                case Status.PickedUp:
                    Assert.AreEqual(1, requests.PickedUpUpdates,
                        "TripThru didn't receive PickedUp trip status update. TripID: " + tripId);
                    location = requests.PickedUpRequest.driverLocation;
                    break;
                case Status.Complete:
                    Assert.AreEqual(1, requests.CompleteUpdates,
                        "TripThru didn't receive Complete trip status update. TripID: " + tripId);
                    location = requests.PickedUpRequest.driverLocation;
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

        public void ValidateReturningDriverRouteIfServiceLocal(PartnerFleet fleet, PartnerTrip trip)
        {
            if (trip.service == PartnerTrip.Origination.Foreign)
                return;
            Status currentStatus = trip.status;
            Assert.AreNotEqual(trip.ETA, null, "The trip ETA is null. Trip ID");
            DateTime timeoutAt = (DateTime) trip.ETA + maxLateness;
            while (!trip.driver.location.Equals(fleet.location, tolerance: locationVerificationTolerance) && Test_TripLifeCycle_Base.testsRunning)
            {
                fleet.UpdateReturningDriverLocations();
                //Assert.IsFalse(DateTime.UtcNow > timeoutAt, "The timeoutAt is less than UtcNow. Trip ID: " + trip.ID);
                System.Threading.Thread.Sleep(simInterval);
            }
        }

        public void ValidateTripRequests(GatewayMock originatingGateway, GatewayMock servicingGateway, PartnerTrip trip)
        {
            var id = trip.publicID;
            Assert.IsTrue(originatingGateway.RequestsByTripId.ContainsKey(id), "Should have received at least one request");
            Assert.IsTrue(servicingGateway.RequestsByTripId.ContainsKey(id), "Should have sent at least one request");

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
        public void ValidateSentRequestsForTripServiceForeign(GatewayMock.TripRequests receivedRequests, GatewayMock.TripRequests sentRequests, PartnerTrip trip)
        {
            Assert.Greater(sentRequests.Dispatch, 0, "Never made a dispatch request");
        }
        public void ValidateReceivedRequestsForTripServiceForeign(GatewayMock.TripRequests receivedRequests, GatewayMock.TripRequests sentRequests, PartnerTrip trip)
        {
            Assert.GreaterOrEqual(sentRequests.Dispatch, 1, "Should send dispatch request at least once");
            Assert.LessOrEqual(receivedRequests.DispatchedUpdates, 1, "Should receive dispatch status update at most once");
            Assert.LessOrEqual(receivedRequests.EnrouteUpdates, 1, "Should receive enroute status update at most once");
            Assert.LessOrEqual(receivedRequests.PickedUpUpdates, 1, "Should receive pickuedup status update at most once");
            Assert.LessOrEqual(receivedRequests.CompleteUpdates, 1, "Should receive complete status update at most once");
            
            if (trip.status == Status.Complete)
            {
                //Don't check dispatched update received because sometimes it changes to enroute before tripthru notifies status
                Assert.AreEqual(1, receivedRequests.EnrouteUpdates, "Should receive enroute status update only once");
                Assert.AreEqual(1, receivedRequests.PickedUpUpdates, "Should receive pickedup status update only once");
                Assert.AreEqual(1, receivedRequests.CompleteUpdates, "Should receive complete status update only once");
            }
        }
        public void ValidateSentRequestsForTripServiceLocal(GatewayMock.TripRequests receivedRequests, GatewayMock.TripRequests sentRequests, PartnerTrip trip)
        {
            if (trip.origination == PartnerTrip.Origination.Foreign)
                Assert.GreaterOrEqual(receivedRequests.Dispatch, 1, "Should receive dispatch request at least once");
            Assert.LessOrEqual(sentRequests.DispatchedUpdates, 1, "Should send dispatch status update at most once");
            Assert.LessOrEqual(sentRequests.EnrouteUpdates, 1, "Should send enroute status update at most once");
            Assert.LessOrEqual(sentRequests.PickedUpUpdates, 1, "Should send pickedup status update at most once");
            Assert.LessOrEqual(sentRequests.CompleteUpdates, 1, "Should send complete status update at most once");

            if (trip.status == Status.Complete)
            {
                Assert.AreEqual(1, sentRequests.DispatchedUpdates, "Should send dispatch status update only once");
                Assert.AreEqual(1, sentRequests.EnrouteUpdates, "Should send enroute status update only once");
                Assert.AreEqual(1, sentRequests.PickedUpUpdates, "Should send pickedup status update only once");
                Assert.AreEqual(1, sentRequests.CompleteUpdates, "Should send complete status update only once");
            }
        }
        public void ValidateReceivedRequestsForTripServiceLocal(GatewayMock.TripRequests receivedRequests, GatewayMock.TripRequests sentRequests, PartnerTrip trip)
        {
            Assert.AreEqual(1, receivedRequests.GetStatus, "Should receive GetTripStatus request to update gateway stats once");
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
