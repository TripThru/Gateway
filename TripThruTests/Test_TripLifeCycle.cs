﻿using System;
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
            Logger.OpenLog(id: "Nunit", splunkEnabled: false);
            MapTools.ClearCache();
            Logger.Log("Setting up");
            Logger.Tab();
            MapTools.distance_and_time_scale = .05;
            MapTools.SetGeodataFilenames(locationNames: "Test_GeoData/Geo-Location-Names.csv",
                routes: "Test_GeoData/Geo-Routes.csv",
                locationAddresses: "Test_GeoData/Geo-Location-Addresses.csv");
            MapTools.LoadGeoData();
            Logger.Untab();
        }

        [TearDown]
        public void TearDown()
        {
            Logger.Log("Tearing down");
            MapTools.ClearCache();
        }

        [Test]
        public void EnoughDrivers_SingleTrips()
        {
            Logger.Log("Test_TripLifeCycle_EnoughDrivers_SingleTrips");
            Test_TripLifeCycle_Base lib = new Test_TripLifeCycle_Base("Test_Configurations/LocalTripsEnoughDrivers.txt",
                tripthru: new GatewayServer("EmptyGateway", "EmptyGateway"),
                maxLateness: new TimeSpan(0, 5, 0));
            lib.Test_SingleTripLifecycle_ForAllPartnerFleets();
        }

        [Test]
        public void EnoughDrivers_SimultaneousTrips()
        {
            Logger.Log("Test_TripLifeCycle_EnoughDrivers_SimultaneousTrips");
            Test_TripLifeCycle_Base lib = new Test_TripLifeCycle_Base("Test_Configurations/LocalTripsEnoughDrivers.txt",
                tripthru: new GatewayServer("EmptyGateway", "EmptyGateway"),
                maxLateness: new TimeSpan(0, 5, 0));
            lib.Test_SimultaneousTripLifecycle_ForAllPartnerFleets();
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
            Test_TripLifeCycle_Base lib = new Test_TripLifeCycle_Base(
                filename: "Test_Configurations/LocalTripsNotEnoughDrivers.txt",
                tripthru: new GatewayServer("EmptyGateway", "EmptyGateway"),
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
            Test_TripLifeCycle_Base lib = new Test_TripLifeCycle_Base(
                filename: "Test_Configurations/LocalTripsNotEnoughDriversSimultaneous.txt",
                tripthru: new GatewayServer("EmptyGateway", "EmptyGateway"),
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
            Test_TripLifeCycle_Base lib = new Test_TripLifeCycle_Base(
                filename: "Test_Configurations/LocalTripsNotEnoughDriversSimultaneous.txt",
                tripthru: new GatewayServer("EmptyGateway", "EmptyGateway"),
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
            Test_TripLifeCycle_Base.ValidateSubTests(subTests, timeoutAt: DateTime.UtcNow + new TimeSpan(0, 10, 0), simInterval: new TimeSpan(0, 0, 1));
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
        Partner partner;
        PartnerTrip.Origination? origination = null;
        PartnerTrip.Origination? service = null;
        public TimeSpan simInterval = new TimeSpan(0, 0, 1);
        public TimeSpan maxLateness = new TimeSpan(0, 0, 2);
        public double locationVerificationTolerance = .6;

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
            partner = new Partner(configuration.Partner.ClientId, configuration.Partner.Name, tripthru, configuration.partnerFleets);
            tripthru.RegisterPartner(new GatewayLocalClient(partner));
        }
        public void Test_SimultaneousTripLifecycle_ForAllPartnerFleets()
        {
            List<SubTest> subtests = MakeSimultaneousTripLifecycle_SubTests();
            Test_TripLifeCycle_Base.ValidateSubTests(subtests, timeoutAt: DateTime.UtcNow + new TimeSpan(0, 10, 0), simInterval: new TimeSpan(0, 0, 1));
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
            ValidateReturningDriverRouteIfServiceLocal(fleet, trip);
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
                    Assert.IsTrue(response.driverLocation.Equals(trip.pickupLocation, tolerance: locationVerificationTolerance));
                if (trip.status == Status.Complete)
                    Assert.IsTrue(response.driverLocation.Equals(trip.dropoffLocation, tolerance: locationVerificationTolerance));
            }
        }
        public void ValidateNextTripStatus(PartnerFleet fleet, PartnerTrip trip, Status nextStatus)
        {
            ValidateOriginationAndService(trip);
            if (trip.status == nextStatus)
                ValidateTripThruStatus(trip);
            WaitUntilStatusReachedOrTimeout(fleet, trip, GetTimeWhenStatusShouldBeReached(trip));

            Assert.AreEqual(nextStatus, trip.status);
            if (trip.status == Status.Enroute)
            {
                Assert.IsNotNull(trip.driverLocation);
            }
            else if (trip.status == Status.PickedUp)
            {
                Assert.IsNotNull(trip.driverLocation);
                //Logger.Log("Driver farther from pickup location -- by more than " + trip.driverLocation.GetDistance(trip.pickupLocation) + " miles ");
                Assert.IsTrue(trip.driverLocation.Equals(trip.pickupLocation, tolerance: locationVerificationTolerance));
            }
            else if (trip.status == Status.Complete)
            {
                Assert.IsNotNull(trip.driverLocation);
                Assert.IsTrue(trip.driverLocation.Equals(trip.dropoffLocation, tolerance: locationVerificationTolerance));
            }
        }

        private void WaitUntilStatusReachedOrTimeout(PartnerFleet fleet, PartnerTrip trip, DateTime timeoutAt)
        {
            Status startingStatus = trip.status;
            do
            {
                // There's a reason we're calling ProcessTrip instead of ProcessQueue, as when there are multiple trips in a queue, a call to ProcessQueue
                // may end up processing more than one queue.  Then it may seem like trips jump a state (status).
                fleet.ProcessTrip(trip);
                System.Threading.Thread.Sleep(simInterval);
            } while (trip.status == startingStatus && DateTime.UtcNow < timeoutAt);
        }

        private void ValidateOriginationAndService(PartnerTrip trip)
        {
            if (trip.status == Status.Queued) //if still completely local
            {
                Assert.AreEqual(PartnerTrip.Origination.Local, trip.origination);
                Assert.AreEqual(PartnerTrip.Origination.Local, trip.service);
            }
            else
            {
                if (origination != null)
                    Assert.AreEqual(origination, trip.origination);
                if (service != null)
                    Assert.AreEqual(service, trip.service);
            }
        }

        private System.DateTime GetTimeWhenStatusShouldBeReached(PartnerTrip trip)
        {
            DateTime timeoutAt;
            switch (trip.status)
            {
                case Status.Enroute:
                case Status.PickedUp:
                case Status.Complete:
                    {
                        Assert.IsNotNull(trip.ETA);
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
            Assert.AreNotEqual(trip.ETA, null);
            DateTime timeoutAt = (DateTime) trip.ETA + maxLateness;
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