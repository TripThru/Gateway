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
    public class Test_TripLifeCycle
    {
        readonly TimeSpan simInterval = new TimeSpan(0, 0, 1);
        readonly TimeSpan timeoutTolerance = new TimeSpan(0, 0, 2);

        [SetUp]
        public void SetUp()
        {
            MapTools.distance_and_time_scale = .05;
            MapTools.SetGeodataFilenames("../../Test_GeoData/Geo-Location-Names.csv", "../../Test_GeoData/Geo-Routes.csv", "../../Test_GeoData/Geo-Location-Addresses.csv");
            MapTools.LoadGeoData();
        }

        [TearDown]
        public void TearDown()
        {
            MapTools.ClearCache();
        }

        [Test]
        public void Test_LocalTripsEnoughDrivers()
        {
            var gateway = new Test_TripLifeCycle_Base("Test_Configurations/LocalTripsEnoughDrivers.txt", 
                null);
            Assert.That(gateway.Validate(), Is.True);
        }
        [Test]
        public void Test_LocalTripsNotEnoughDrivers()
        {
            var gateway = new Test_TripLifeCycle_Base("Test_Configurations/LocalTripsNotEnoughDrivers.txt", 
                new GatewayServer("EmptyGateway", "EmptyGateway"));
            Assert.That(gateway.Validate(), Is.False);
        }
    }

    public class UnitTest
    {
        public virtual void Setup() { }
        public virtual bool Validate() { return true; }
        public virtual void Teardown() { }
    }

    public class SubTest
    {
        public bool? passed = null;
        public virtual void Run()
        {

        }
    }
    public class Test_TripLifeCycle_Base : UnitTest
    {
        public string filename;
        public Gateway tripthru;
        readonly TimeSpan simInterval = new TimeSpan(0, 0, 1);
        readonly TimeSpan timeoutTolerance = new TimeSpan(0, 0, 2);

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
                passed = parent.TestTripLifecycleAndReturningDriver(fleet, tripSpec);
            }
        }

        public Test_TripLifeCycle_Base(string filename, Gateway tripthru)
        {
            this.filename = filename;
            this.tripthru = tripthru;
        }
        public override bool Validate()
        {
            bool result = Test_TripLifeCycle_ForAllPartnerFleets();
            return result;
        }

        private bool Test_TripLifeCycle_ForAllPartnerFleets()
        {
            if (!Test_SingleTripLifecycle_ForAllPartnerFleets())
                return false;
            if (!Test_SimultaneousTripLifecycle_ForAllPartnerFleets())
                return false;
            return true;
        }
        public Partner LoadPartnerConfiguration()
        {
            PartnerConfiguration configuration = Partner.LoadPartnerConfigurationFromJsonFile(filename);
            Partner partner = new Partner(configuration.Partner.ClientId, configuration.Partner.Name, tripthru, configuration.partnerFleets);
            return partner;
        }
        private bool Test_SimultaneousTripLifecycle_ForAllPartnerFleets()
        {
            Partner partner = LoadPartnerConfiguration();
            List<SubTest> singleTrips_Subtests = new List<SubTest>();
            foreach (PartnerFleet fleet in partner.PartnerFleets.Values)
            {
                foreach (Pair<Location, Location> tripSpec in fleet.possibleTrips)
                    singleTrips_Subtests.Add(new UnitTest_SingleTripLifecycleAndReturningDriver(this, fleet, tripSpec));
            }
            foreach (SubTest u in singleTrips_Subtests)
                new Thread(u.Run).Start();
            DateTime timeout = DateTime.UtcNow + new TimeSpan(0, 10, 0);
            bool? passed = null;
            while (passed == null && DateTime.UtcNow < timeout)
            {
                passed = false;
                foreach (SubTest test in singleTrips_Subtests)
                {
                    if (test.passed == null)
                        passed = null;
                    else if (!((bool)test.passed))
                        return false;
                }
                System.Threading.Thread.Sleep(simInterval);
            }
            return true;
        }

        private bool Test_SingleTripLifecycle_ForAllPartnerFleets()
        {
            Partner partner = LoadPartnerConfiguration();
            foreach (PartnerFleet fleet in partner.PartnerFleets.Values)
            {
                foreach (Pair<Location, Location> tripStartEnd in fleet.possibleTrips)
                {
                    if (!TestTripLifecycleAndReturningDriver(fleet, tripStartEnd))
                        return false;
                }
            }

            return true;
        }
        private bool TestTripLifecycleAndReturningDriver(PartnerFleet fleet, Pair<Location, Location> tripSpec)
        {
            PartnerTrip trip = fleet.GenerateTrip(fleet.passengers[0], DateTime.UtcNow, tripSpec);
            if (!TestTripLifecycle_FromNewToComplete(fleet, trip))
                return false;
            if (!ValidateReturningDriverRoute(fleet, trip))
                return false;
            return true;
        }

        private bool TestTripLifecycle_FromNewToComplete(PartnerFleet fleet, PartnerTrip trip)
        {
            if (trip.status != Status.New)
                return false;
            fleet.QueueTrip(trip);
            if (!ValidateNextTripStatus(fleet, trip, Status.Queued))
                return false;
            if (!ValidateNextTripStatus(fleet, trip, Status.Dispatched))
                return false;
            if (!ValidateNextTripStatus(fleet, trip, Status.Enroute))
                return false;
            if (!ValidateNextTripStatus(fleet, trip, Status.PickedUp))
                return false;
            if (!ValidateNextTripStatus(fleet, trip, Status.Complete))
                return false;
            return true;
        }
        public bool ValidateNextTripStatus(PartnerFleet fleet, PartnerTrip trip, Status nextStatus)
        {
            if (trip.status == nextStatus)
                return true;
            TimeSpan timeout = new TimeSpan(0);
            switch (trip.status)
            {
                case Status.Queued: timeout = timeoutTolerance; break;
                case Status.Dispatched: timeout = timeoutTolerance; break;
                case Status.Enroute: timeout = trip.driver.route.duration + timeoutTolerance; break;
                case Status.PickedUp: timeout = trip.driver.route.duration + timeoutTolerance; break;
                case Status.Complete: timeout = trip.driver.route.duration + timeoutTolerance; break;
            }

            Status startingStatus = trip.status;
            DateTime timeoutAt = DateTime.UtcNow + timeout;
            do
            {
                // There's a reason we're calling ProcessTrip instead of ProcessQueue, as when there are multiple trips in a queue, a call to ProcessQueue
                // may end up processing more than one queue.  Then it may seem like trips jump a state (status).
                fleet.ProcessTrip(trip);

            } while (trip.status == startingStatus && DateTime.UtcNow < timeoutAt);
            if (trip.status != nextStatus)
                return false;
            if (trip.status == Status.PickedUp && !trip.driver.location.Equals(trip.pickupLocation))
                return false;
            if (trip.status == Status.Complete && !trip.driver.location.Equals(trip.dropoffLocation))
                return false;
            return true;
        }
        public bool ValidateReturningDriverRoute(PartnerFleet fleet, PartnerTrip trip)
        {
            TimeSpan timeout = trip.driver.route.duration + timeoutTolerance;
            Status currentStatus = trip.status;
            DateTime timeoutAt = DateTime.UtcNow + timeout;
            while (!trip.driver.location.Equals(fleet.location))
            {
                fleet.UpdateReturningDriverLocations();
                if (DateTime.UtcNow > timeoutAt)
                    return false;
                System.Threading.Thread.Sleep(simInterval);
            }
            return true;
        }
    }
}
