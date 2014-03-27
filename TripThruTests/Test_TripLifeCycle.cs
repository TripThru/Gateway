using System;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Utils;
using TripThruCore;
using System.Linq.Expressions;
using System.Threading;

namespace Tests
{
    [TestFixture]
    [Category("Trip life cycle")]
    public class TripLifeCycle_Tester
    {
        [SetUp]
        public void SetUp()
        {
            MapTools.distance_and_time_scale = .05;
            MapTools.SetGeodataFilenames("Test_GeoData/Geo-Location-Names.csv",
                                         "Test_GeoData/Geo-Routes.csv",
                                         "Test_GeoData/Geo-Location-Addresses.csv");
            MapTools.LoadGeoData();
        }

        [TearDown]
        public void TearDown()
        {
            MapTools.ClearCache();
        }

        [Test]
        public void EnoughDrivers_SingleTrips()
        {
            Console.WriteLine("Test_TripLifeCycle_EnoughDrivers_SingleTrips");
            Test_TripLifeCycle_Base lib = new Test_TripLifeCycle_Base("Test_Configurations/LocalTripsEnoughDrivers.txt",
                tripthru: new GatewayServer("EmptyGateway", "EmptyGateway"),
                timeoutTolerance: new TimeSpan(0, 5, 0));
            lib.Test_SingleTripLifecycle_ForAllPartnerFleets();
        }

        [Test]
        public void EnoughDrivers_SimultaneousTrips()
        {
            Console.WriteLine("Test_TripLifeCycle_EnoughDrivers_SimultaneousTrips");
            Test_TripLifeCycle_Base lib = new Test_TripLifeCycle_Base("Test_Configurations/LocalTripsEnoughDrivers.txt",
                tripthru: new GatewayServer("EmptyGateway", "EmptyGateway"),
                timeoutTolerance: new TimeSpan(0, 5, 0));
            lib.Test_SimultaneousTripLifecycle_ForAllPartnerFleets();
        }

        [Test]
        [ExpectedException(typeof(AssertionException), ExpectedMessage = "But was:  Queued", MatchType = MessageMatch.Contains)]
        public void NotEnoughDrivers_SingleTrips()
        {
            Console.WriteLine("Test_TripLifeCycle_NotEnoughDrivers_SingleTrips");
            /* In this test since there are not enough drivers it tries to dispatch to tripthru --
             * which has an empty implementation that always rejects.
             * We expect an assertion error because we expect the trip status to change from Queued to Dispatched
             * */
            Test_TripLifeCycle_Base lib = new Test_TripLifeCycle_Base(
                filename: "Test_Configurations/LocalTripsNotEnoughDrivers.txt",
                tripthru: new GatewayServer("EmptyGateway", "EmptyGateway"),
                timeoutTolerance: new TimeSpan(0, 1, 0));

            lib.Test_SingleTripLifecycle_ForAllPartnerFleets();
        }

        [Test]
        [ExpectedException(typeof(AssertionException), ExpectedMessage = "But was:  Queued", MatchType = MessageMatch.Contains)]
        public void NotEnoughDrivers_SimultaneousTrips()
        {
            Console.WriteLine("Test_TripLifeCycle_NotEnoughDrivers_SimultaneousTrips");
            /* In this test since there are not enough drivers it tries to dispatch to tripthru --
             * which has an empty implementation that always rejects.
             * We expect an assertion error because we expect the trip status to change from Queued to Dispatched
             * */
            Test_TripLifeCycle_Base lib = new Test_TripLifeCycle_Base(
                filename: "Test_Configurations/LocalTripsNotEnoughDriversSimultaneous.txt",
                tripthru: new GatewayServer("EmptyGateway", "EmptyGateway"),
                timeoutTolerance: new TimeSpan(0, 1, 0));

            lib.Test_SimultaneousTripLifecycle_ForAllPartnerFleets();
        }

        [Test]
        public void EnoughDrivers_TwoPartnersShareJobsThroughTripThru()
        {
            Console.WriteLine("Test_TripLifeCycle_EnoughDrivers_TwoPartnersShareJobsThroughTripThru");
            var tripthru = new TripThru(enableTDispatch: false);
            var libA = new Test_TripLifeCycle_Base(
                filename: "Test_Configurations/ForeignTripsEnoughDriversA.txt",
                tripthru: tripthru,
                timeoutTolerance: new TimeSpan(0, 5, 0),
                origination: PartnerTrip.Origination.Local,
                service: PartnerTrip.Origination.Foreign);
            var libB = new Test_TripLifeCycle_Base(
                filename: "Test_Configurations/ForeignTripsEnoughDriversB.txt",
                tripthru: tripthru,
                timeoutTolerance: new TimeSpan(0, 5, 0),
                origination: PartnerTrip.Origination.Local,
                service: PartnerTrip.Origination.Foreign);

            List<SubTest> subTests = libA.MakeSimultaneousTripLifecycle_SubTests();
            subTests.AddRange(libB.MakeSimultaneousTripLifecycle_SubTests());
            libA.ValidateSubTests(subTests);
        }
    }

    public class Test_TripLifeCycle_Base
    {
        public string filename;
        public Gateway tripthru;
        Partner partner;
        PartnerTrip.Origination origination = PartnerTrip.Origination.Local;
        PartnerTrip.Origination service = PartnerTrip.Origination.Local;
        public TimeSpan simInterval = new TimeSpan(0, 0, 1);
        public TimeSpan timeoutTolerance = new TimeSpan(0, 0, 2);

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
                    this.passed = true;
                }
                catch (Exception ex)
                {
                    this.exception = ex;
                    passed = null;
                }
            }
        }

        public Test_TripLifeCycle_Base()
        {

        }

        public Test_TripLifeCycle_Base(string filename, Gateway tripthru, TimeSpan? timeoutTolerance = null, PartnerTrip.Origination? origination = null, PartnerTrip.Origination? service = null)
        {
            this.filename = filename;
            this.tripthru = tripthru;
            if (timeoutTolerance != null)
                this.timeoutTolerance = (TimeSpan)timeoutTolerance;
            if (origination != null)
                this.origination = (PartnerTrip.Origination)origination;
            if (service != null)
                this.service = (PartnerTrip.Origination)service;
            PartnerConfiguration configuration = Partner.LoadPartnerConfigurationFromJsonFile(filename);
            partner = new Partner(configuration.Partner.ClientId, configuration.Partner.Name, tripthru, configuration.partnerFleets);
            tripthru.RegisterPartner(new GatewayLocalClient(partner));
        }
        public void Test_SimultaneousTripLifecycle_ForAllPartnerFleets()
        {
            List<SubTest> singleTrips_Subtests = MakeSimultaneousTripLifecycle_SubTests();
            ValidateSubTests(singleTrips_Subtests);
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

        public void ValidateSubTests(List<SubTest> singleTrips_Subtests)
        {
            foreach (SubTest u in singleTrips_Subtests)
                new Thread(u.Run).Start();

            DateTime timeout = DateTime.UtcNow + new TimeSpan(0, 10, 0);
            bool? passed = null;
            while (passed == null && DateTime.UtcNow < timeout)
            {
                passed = false;
                foreach (SubTest test in singleTrips_Subtests)
                {
                    if (test.exception != null)
                    {
                        Console.WriteLine(test.exception.Message + " : " + test.exception);
                        throw test.exception;
                    }
                    if (test.passed == null)
                        passed = null;
                }
                System.Threading.Thread.Sleep(simInterval);
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
            ValidateReturningDriverRoute(fleet, trip);
        }

        public void TestTripLifecycle_FromNewToComplete(PartnerFleet fleet, PartnerTrip trip)
        {
            Assert.AreEqual(Status.New, trip.status);
            fleet.QueueTrip(trip);
            Assert.AreEqual(Status.Queued, trip.status);
            ValidateNextTripStatus(fleet, trip, Status.Dispatched);
            ValidateNextTripStatus(fleet, trip, Status.Enroute);
            ValidateNextTripStatus(fleet, trip, Status.PickedUp);
            ValidateNextTripStatus(fleet, trip, Status.Complete);
        }
        public void ValidateTripThruStatus(PartnerTrip trip)
        {
            if (trip.origination == PartnerTrip.Origination.Foreign || trip.service == PartnerTrip.Origination.Foreign)
            {
                Gateway.GetTripStatusResponse response = tripthru.GetTripStatus(new Gateway.GetTripStatusRequest(partner.ID, trip.ID));
                Assert.AreEqual(trip.status, response.status);
                if (trip.status == Status.Enroute)
                    Assert.IsNotNull(response.driverLocation);
                if (trip.status == Status.PickedUp)
                    Assert.IsTrue(response.driverLocation.Equals(trip.pickupLocation));
                if (trip.status == Status.Complete)
                    Assert.IsTrue(response.driverLocation.Equals(trip.dropoffLocation));
            }
        }
        public void ValidateNextTripStatus(PartnerFleet fleet, PartnerTrip trip, Status nextStatus)
        {
            if (trip.status == Status.Queued) //if still completely local
            {
                Assert.AreEqual(PartnerTrip.Origination.Local, trip.origination);
                Assert.AreEqual(PartnerTrip.Origination.Local, trip.service);
            }
            else
            {
                Assert.AreEqual(origination, trip.origination);
                Assert.AreEqual(service, trip.service);
            }
            if (trip.status == nextStatus)
                ValidateTripThruStatus(trip);
            TimeSpan timeout = new TimeSpan(0);
            switch (trip.status)
            {
                case Status.Queued: timeout = timeoutTolerance; break;
                case Status.Dispatched: timeout = timeoutTolerance; break;
                case Status.Enroute: timeout = (TimeSpan)trip.driverRouteDuration + timeoutTolerance; break;
                case Status.PickedUp: timeout = (TimeSpan)trip.driverRouteDuration + timeoutTolerance; break;
                case Status.Complete: timeout = (TimeSpan)trip.driverRouteDuration + timeoutTolerance; break;
            }

            Status startingStatus = trip.status;
            DateTime timeoutAt = DateTime.UtcNow + timeout;
            do
            {
                // There's a reason we're calling ProcessTrip instead of ProcessQueue, as when there are multiple trips in a queue, a call to ProcessQueue
                // may end up processing more than one queue.  Then it may seem like trips jump a state (status).
                fleet.ProcessTrip(trip);
            } while (trip.status == startingStatus && DateTime.UtcNow < timeoutAt);

            Assert.AreEqual(nextStatus, trip.status);
            if (trip.status == Status.Enroute)
                Assert.IsNotNull(trip.driver.location);
            if (trip.status == Status.PickedUp)
                Assert.IsTrue(trip.driver.location.Equals(trip.pickupLocation));
            if (trip.status == Status.Complete)
                Assert.IsTrue(trip.driver.location.Equals(trip.dropoffLocation));
            ValidateTripThruStatus(trip);
        }
        public void ValidateReturningDriverRoute(PartnerFleet fleet, PartnerTrip trip)
        {
            TimeSpan timeout = ((TimeSpan)trip.driverRouteDuration) + timeoutTolerance;
            Status currentStatus = trip.status;
            DateTime timeoutAt = DateTime.UtcNow + timeout;
            while (!trip.driver.location.Equals(fleet.location))
            {
                fleet.UpdateReturningDriverLocations();
                Assert.IsFalse(DateTime.UtcNow > timeoutAt);
                System.Threading.Thread.Sleep(simInterval);
            }
        }
    }

    public class SubTest
    {
        public bool? passed = null;
        public Exception exception = null;
        public virtual void Run()
        {

        }
    }

}
