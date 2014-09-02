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
            var tripthru = new TripThru(enableTDispatch: false);
            Test_TripLifeCycle_Base lib = new Test_TripLifeCycle_Base("Test_Configurations/LocalTripsEnoughDrivers.txt",
                tripthru: tripthru,
                maxLateness: new TimeSpan(0, 5, 0));
            lib.Test_SingleTripLifecycle_ForAllPartnerFleets();
        }

        [Test]
        public void EnoughDrivers_SimultaneousTrips()
        {
            Logger.Log("Test_TripLifeCycle_EnoughDrivers_SimultaneousTrips");
            var tripthru = new TripThru(enableTDispatch: false);
            Test_TripLifeCycle_Base lib = new Test_TripLifeCycle_Base("Test_Configurations/LocalTripsEnoughDrivers.txt",
                tripthru: tripthru,
                maxLateness: new TimeSpan(0, 5, 0));
            lib.Test_SimultaneousTripLifecycle_ForAllPartnerFleets();
        }

        [Test]
        [ExpectedException(typeof(AssertionException), ExpectedMessage = "But was:  Rejected", MatchType = MessageMatch.Contains)]
        public void NotEnoughDrivers_SingleTrips()
        {
            Logger.Log("Test_TripLifeCycle_NotEnoughDrivers_SingleTrips");
            /* In this test since there are not enough drivers it tries to dispatch to tripthru --
             * which has an empty implementation that always rejects.
             * We expect an assertion error because we expect the trip status to change from Queued to Dispatched
             * */
            var tripthru = new TripThru(enableTDispatch: false);
            Test_TripLifeCycle_Base lib = new Test_TripLifeCycle_Base(
                filename: "Test_Configurations/LocalTripsNotEnoughDrivers.txt",
                tripthru: tripthru,
                maxLateness: new TimeSpan(0, 1, 0));
            lib.Test_SingleTripLifecycle_ForAllPartnerFleets();
        }

        [Test]
        [ExpectedException(typeof(AssertionException), ExpectedMessage = "But was:  Rejected", MatchType = MessageMatch.Contains)]
        public void NotEnoughDrivers_SimultaneousTrips_VerifyRejected()
        {
            Logger.Log("NotEnoughDrivers_SimultaneousTrips_VerifyRejected");
            /* In this test since there are not enough drivers it tries to dispatch to tripthru --
             * which has an empty implementation that always rejects.
             * We expect an assertion error because we expect the trip status to change from Queued to Dispatched
             * */
            var tripthru = new TripThru(enableTDispatch: false);
            Test_TripLifeCycle_Base lib = new Test_TripLifeCycle_Base(
                filename: "Test_Configurations/LocalTripsNotEnoughDriversSimultaneous.txt",
                tripthru: tripthru,
                maxLateness: new TimeSpan(0, 1, 0));
            lib.Test_SimultaneousTripLifecycle_ForAllPartnerFleets();
        }

        [Test]
        public void NotEnoughDrivers_SimultaneousTrips_AllowTimeForDriversToBecomeAvailable()
        {
            Logger.Log("NotEnoughDrivers_SimultaneousTrips_AllowTimeForDriversToBecomeAvailable");
            /* In this test since there are not enough drivers it tries to dispatch to tripthru --
             * which has an empty implementation that always rejects.
             * We expect an assertion error because we expect the trip status to change from Queued to Dispatched
             * */
            var tripthru = new TripThru(enableTDispatch: false);
            Test_TripLifeCycle_Base lib = new Test_TripLifeCycle_Base(
                filename: "Test_Configurations/LocalTripsNotEnoughDriversSimultaneous.txt",
                tripthru: tripthru,
                maxLateness: new TimeSpan(0, 10, 0));
            lib.Test_SimultaneousTripLifecycle_ForAllPartnerFleets();
        }

        [Test]
        public void EnoughDrivers_TwoPartnersShareJobs_Gateway()
        {
            Logger.Log("Test_TripLifeCycle_EnoughDrivers_TwoPartnersShareJobs_Gateway");
            var tripthru = new TripThru(enableTDispatch: false);
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
            Test_TripLifeCycle_Base.ValidateSubTests(subTests, 
                timeoutAt: DateTime.UtcNow + new TimeSpan(0, 10, 0), 
                simInterval : new TimeSpan(0, 0, 1)
                );
            Thread.Sleep(new TimeSpan(0,0,1));
        }

        [Test]
        public void AllPartners_Gateway()
        {
            Logger.Log("AllPartners_Gateway");
            var tripthru = new TripThru(enableTDispatch: false);
            TimeSpan maxLateness = new TimeSpan(0, 25, 0);
            double locationVerificationTolerance = 4;
            string[] filePaths = Directory.GetFiles("../../Test_Configurations/Partners/");
            Logger.Log("filePaths = " + filePaths);
            List<SubTest> subtests = new List<SubTest>();
            foreach (string filename in filePaths)
            {
                Logger.Log("filename = " + filename);
                var lib = new Test_TripLifeCycle_Base(
                    filename: filename,
                    tripthru: tripthru,
                    maxLateness: maxLateness,
                    locationVerificationTolerance: locationVerificationTolerance);
                subtests.AddRange(lib.MakeSimultaneousTripLifecycle_SubTests());
            } 
            Test_TripLifeCycle_Base.ValidateSubTests(subtests, timeoutAt: DateTime.UtcNow + new TimeSpan(0, 30, 0), simInterval: new TimeSpan(0, 0, 1));
        }

    }

    public class Test_TripLifeCycle_Base
    {
        public string filename;
        public Gateway tripthru;
        public Partner partner;
        private GatewayMock partnerServiceMock;
        PartnerTrip.Origination? origination = null;
        PartnerTrip.Origination? service = null;
        public TimeSpan simInterval = new TimeSpan(0, 0, 1);
        public TimeSpan maxLateness = new TimeSpan(0, 0, 2);
        public double locationVerificationTolerance = .6;
        public int _activeTrips;
        List<String> tripsList = new List<String>();

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
            Gateway tripthru, 
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
            partner = new Partner(configuration.Partner.ClientId, configuration.Partner.Name, new GatewayMock(tripthru), configuration.partnerFleets);
            var coverage = new List<Zone>();
            foreach (var fleet in partner.PartnerFleets.Values)
                coverage.AddRange(fleet.coverage);
            this.partnerServiceMock = new GatewayMock(partner);
            partner.tripthru.RegisterPartner(partnerServiceMock, coverage);
        }

        public void Test_SimultaneousTripLifecycle_ForAllPartnerFleets()
        {
            List<SubTest> subtests = MakeSimultaneousTripLifecycle_SubTests();
            ValidateSubTests(subtests, timeoutAt: DateTime.UtcNow + new TimeSpan(0, 10, 0), simInterval: new TimeSpan(0, 0, 1));
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

        public static void ValidateSubTests(List<SubTest> singleTrips_Subtests, DateTime timeoutAt, TimeSpan simInterval)
        {
            foreach (SubTest u in singleTrips_Subtests)
                new Thread(u.Run).Start();

            bool? passed = null;
            while (passed == null && DateTime.UtcNow < timeoutAt)
            {
                passed = false;
                foreach (SubTest test in singleTrips_Subtests)
                {
                    if (test.Exception != null)
                    {
                        Console.WriteLine(test.Exception.Message + " : " + test.Exception);
                        throw test.Exception;
                    }
                    if (test.Passed == null)
                        passed = null;
                }
                Thread.Sleep(simInterval);
            }
        }

        public void Test_SingleTripLifecycle_ForAllPartnerFleets()
        {
            foreach (PartnerFleet fleet in partner.PartnerFleets.Values)
            {
                var i = 1;
                foreach (Pair<Location, Location> tripStartEnd in fleet.possibleTrips)
                {
                    Console.WriteLine("Trip " + i++ + "/" + fleet.possibleTrips.Length);
                    TestTripLifecycleAndReturningDriver(fleet, tripStartEnd);
                }
                
            }
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

        public void ValidateTripThruStatus(PartnerTrip trip)
        {
            var tripId = trip.publicID;
            Gateway.GetTripStatusResponse response = tripthru.GetTripStatus(new Gateway.GetTripStatusRequest(partner.ID, tripId));
            Assert.AreEqual(trip.status, response.status, "The trip local status doesn't match the gateway status. Trip ID: " + tripId);
            if (trip.status == Status.Enroute)
                Assert.IsNotNull(response.driverLocation, "The trip is Enroute but the driverLocation is null. Trip ID: " + tripId);
            if (trip.status == Status.PickedUp)
                Assert.IsTrue(response.driverLocation.Equals(trip.pickupLocation, tolerance: locationVerificationTolerance), "The trip is PickedUp but the driverLocation is out to the tolerance area. Trip ID: " + tripId);
            if (trip.status == Status.Complete)
                Assert.IsTrue(response.driverLocation.Equals(trip.dropoffLocation, tolerance: locationVerificationTolerance), "The trip is Complete but the driverLocation is out to the tolerance area. Trip ID: " + tripId);
        }

        public void ValidateNextTripStatus(PartnerFleet fleet, PartnerTrip trip, Status nextStatus)
        {
            if (nextStatus == Status.Dispatched)
                WaitUntilTripIsSuccessfullyDispatchedToTripThruOrTimesout(fleet, trip, GetTimeWhenStatusShouldBeReached(trip));
            else
                WaitUntilStatusReachedOrTimeout(fleet, trip, nextStatus, GetTimeWhenStatusShouldBeReached(trip));
            if (trip.status == nextStatus)
                ValidateTripThruStatus(trip);

            Assert.AreEqual(nextStatus, trip.status, "The trip did not advance to the next status. Trip ID: " + trip.ID);
            switch (trip.status)
            {
                case Status.Enroute:
                    Assert.IsNotNull(trip.driverLocation, "The trip is Enroute but the driverLocation is null. Trip ID: " + trip.ID);
                    break;
                case Status.PickedUp:
                    Assert.IsNotNull(trip.driverLocation, "The trip is PickedUp but the driverLocation is null. Trip ID: " + trip.ID);
                    Assert.IsTrue(trip.driverLocation.Equals(trip.pickupLocation, tolerance: locationVerificationTolerance), "The trip is PickedUp but the driverLocation is out to the tolerance area. Trip ID: " + trip.ID);
                    break;
                case Status.Complete:
                    Assert.IsNotNull(trip.driverLocation, "The trip is Complete but the driverLocation is null. Trip ID: " + trip.ID);
                    Assert.IsTrue(trip.driverLocation.Equals(trip.dropoffLocation, tolerance: locationVerificationTolerance), "The trip is Complete but the driverLocation is out to the tolerance area. Trip ID: " + trip.ID);
                    break;
            }
        }

        private void WaitUntilStatusReachedOrTimeout(PartnerFleet fleet, PartnerTrip trip, Status nextStatus, DateTime timeoutAt)
        {
            while (trip.status != nextStatus && DateTime.UtcNow < timeoutAt)
            {
                // There's a reason we're calling ProcessTrip instead of ProcessQueue, as when there are multiple trips in a queue, a call to ProcessQueue
                // may end up processing more than one queue.  Then it may seem like trips jump a state (status).
                fleet.ProcessTrip(trip);
                Thread.Sleep(simInterval);
            }
        }
        private void WaitUntilTripIsSuccessfullyDispatchedToTripThruOrTimesout(PartnerFleet fleet, PartnerTrip trip, DateTime timeoutAt)
        {
            // The trip will advance to dispatched status on the partner's side but tripthru could reject it so we wait to confirm if
            // tripthru successfully dispatched the trip.
            Status? status = null;
            while (status != Status.Dispatched && DateTime.UtcNow < timeoutAt)
            {
                Console.WriteLine("Waiting for " + trip.ID + " to be dispatched");
                // There's a reason we're calling ProcessTrip instead of ProcessQueue, as when there are multiple trips in a queue, a call to ProcessQueue
                // may end up processing more than one queue.  Then it may seem like trips jump a state (status).
                fleet.ProcessTrip(trip);
                Thread.Sleep(simInterval);

                var response = tripthru.GetTripStatus(new Gateway.GetTripStatusRequest(partner.ID, trip.publicID));
                if (response.result == Gateway.Result.OK)
                    status = response.status;
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
            while (!trip.driver.location.Equals(fleet.location, tolerance: locationVerificationTolerance))
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
            Assert.LessOrEqual(receivedRequests.UpdateDispatched, 1, "Should receive status update at most once");
            Assert.LessOrEqual(receivedRequests.UpdateEnroute, 1, "Should receive status update at most once");
            Assert.LessOrEqual(receivedRequests.UpdatePickedUp, 1, "Should receive status update at most once");
            Assert.LessOrEqual(receivedRequests.UpdateComplete, 1, "Should receive status update at most once");

            if (trip.status == Status.Complete)
            {
                Assert.AreEqual(1, receivedRequests.UpdateDispatched, "Should receive this update only once");
                Assert.AreEqual(1, receivedRequests.UpdateEnroute, "Should receive this update only once");
                Assert.AreEqual(1, receivedRequests.UpdatePickedUp, "Should receive this update only once");
                Assert.AreEqual(1, receivedRequests.UpdateComplete, "Should receive this update only once");
            }
        }
        public void ValidateSentRequestsForTripServiceLocal(GatewayMock.TripRequests receivedRequests, GatewayMock.TripRequests sentRequests, PartnerTrip trip)
        {
            if (trip.origination == PartnerTrip.Origination.Foreign)
                Assert.GreaterOrEqual(receivedRequests.Dispatch, 1, "Should receive dispatch request at least once");
            Assert.LessOrEqual(sentRequests.UpdateDispatched, 1, "Should send status update at most once");
            Assert.LessOrEqual(sentRequests.UpdateEnroute, 1, "Should send status update at most once");
            Assert.LessOrEqual(sentRequests.UpdatePickedUp, 1, "Should send status update at most once");
            Assert.LessOrEqual(sentRequests.UpdateComplete, 1, "Should send status update at most once");

            if (trip.status == Status.Complete)
            {
                Assert.AreEqual(1, sentRequests.UpdateDispatched, "Should send status update only once");
                Assert.AreEqual(1, sentRequests.UpdateEnroute, "Should send status update only once");
                Assert.AreEqual(1, sentRequests.UpdatePickedUp, "Should send status update only once");
                Assert.AreEqual(1, sentRequests.UpdateComplete, "Should send status update only once");
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
