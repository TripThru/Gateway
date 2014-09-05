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
    class Test_TripLifeCycle_LocalGateway
    {
        [TestFixture]
        [Category("TripLifeCycle_Local")]
        public class TripLifeCycle_LocalTester
        {
            GatewayMock tripthru;

            [SetUp]
            public void SetUp()
            {
                Logger.OpenLog("Nunit", splunkEnabled: false);
                StorageManager.OpenStorage(new MongoDbStorage("mongodb://localhost:27017/", "TripThru"));
                StorageManager.Reset(); // Sometimes mongo can't delete on teardown between tests
                Logger.Log("Setting up");
                Logger.Tab();
                MapTools.distance_and_time_scale = .05;
                Logger.Untab();
                tripthru = new GatewayMock(new TripThru(enableTDispatch: false));
            }

            [TearDown]
            public void TearDown()
            {
                Logger.Log("Tearing down");
                ((TripThru)tripthru.server).Dispose();
                StorageManager.Reset();
            }

            [Test]
            public void EnoughDrivers_SingleTrips()
            {
                Logger.Log("EnoughDrivers_SingleTrips");
                Test_TripLifeCycle_Base lib = new Test_TripLifeCycle_Base("Test_Configurations/LocalTripsEnoughDrivers.txt",
                    tripthru: tripthru,
                    maxLateness: new TimeSpan(0, 5, 0));
                lib.Test_SingleTripLifecycle_ForAllPartnerFleets();
            }

            [Test]
            public void EnoughDrivers_SimultaneousTrips()
            {
                Logger.Log("EnoughDrivers_SimultaneousTrips");
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
            public void NotEnoughDrivers_SingleTrips()
            {
                Logger.Log("NotEnoughDrivers_SingleTrips");
                /* In this test since there are not enough drivers it tries to dispatch to tripthru --
                 * which has an empty implementation that always rejects.
                 * We expect an assertion error because we expect the trip status to change from Queued to Dispatched
                 * */
                Test_TripLifeCycle_Base lib = new Test_TripLifeCycle_Base(
                    filename: "Test_Configurations/LocalTripsNotEnoughDrivers.txt",
                    tripthru: tripthru,
                    maxLateness: new TimeSpan(0, 1, 0));
                lib.Test_SingleTripLifecycle_ForAllPartnerFleets();
            }

            [Test]
            public void NotEnoughDrivers_SimultaneousTrips()
            {
                Logger.Log("NotEnoughDrivers_SimultaneousTrips");
                /* In this test since there are not enough drivers it tries to dispatch to tripthru --
                 * which has an empty implementation that always rejects.
                 * We expect an assertion error because we expect the trip status to change from Queued to Dispatched
                 * */
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
                Test_TripLifeCycle_Base lib = new Test_TripLifeCycle_Base(
                    filename: "Test_Configurations/LocalTripsNotEnoughDriversSimultaneous.txt",
                    tripthru: tripthru,
                    maxLateness: new TimeSpan(0, 20, 0));
                List<SubTest> subTests = lib.MakeSimultaneousTripLifecycle_SubTests();
                List<Partner> partners = new List<Partner>() { lib.partner };
                Test_TripLifeCycle_Base.RunSubTests(partners, subTests,
                    timeoutAt: DateTime.UtcNow + new TimeSpan(1, 0, 0),
                    simInterval: new TimeSpan(0, 0, 1)
                );
            }

            [Test]
            public void EnoughDrivers_TwoPartnersShareJobs_Gateway()
            {
                Logger.Log("EnoughDrivers_TwoPartnersShareJobs_Gateway");
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
                    simInterval: new TimeSpan(0, 0, 1)
                );
            }

            [Test]
            public void EnoughDrivers_AllPartners_Gateway()
            {
                Logger.Log("EnoughDrivers_AllPartners_Gateway");
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
    }
}
