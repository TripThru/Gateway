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
using ServiceStack.TripThruGateway;

namespace TripThruTests
{
    class Test_TripLifeCycle_RemoteGateway
    {
        [TestFixture]
        [Category("TripLifeCycle_Remote")]
        public class TripLifeCycle_RemoteTester
        {
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
            }

            [TearDown]
            public void TearDown()
            {
                Logger.Log("Tearing down");
                StorageManager.Reset();
            }

            [Test]
            public void EnoughDrivers_TwoPartnersShareJobs_Gateway()
            {
                Logger.Log("EnoughDrivers_TwoPartnersShareJobs_Gateway");
            }

            [Test]
            public void EnoughDrivers_AllPartners_Gateway()
            {
                Logger.Log("EnoughDrivers_AllPartners_Gateway");
            }
        }
    }
}
