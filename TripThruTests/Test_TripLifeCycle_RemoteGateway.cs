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
using ServiceStack.WebHost.Endpoints;
using ServiceStack.WebHost.Endpoints.Extensions;
using Funq;
using ServiceStack.Common.Web;
using ServiceStack.ServiceInterface.Cors;
using ServiceStack.Text;

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

    public class SelfAppHost : AppHostHttpListenerBase
    {
        public SelfAppHost(string serviceName)
            : base(serviceName, typeof(GatewayService).Assembly)
        {
            
        }

        public override void Configure(Container container)
        {
            AppHostCommon.Init(container);
            this.SetConfig(AppHostCommon.GetConfig());
            this.Plugins.AddRange(AppHostCommon.GetPlugins());

            JsConfig.DateHandler = JsonDateHandler.ISO8601;
            JsConfig.EmitCamelCaseNames = true;
        }
    }

    public static class AppHostCommon
    {
        public static void Init(Container container)
        {
        }

        public static EndpointHostConfig GetConfig()
        {
            return new EndpointHostConfig
            {
                GlobalResponseHeaders = {
					{ "Access-Control-Allow-Origin", "*" },
					{ "Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS" },
					{ "Access-Control-Allow-Headers", "Content-Type" },
				},
                DebugMode = true,
                DefaultContentType = ContentType.Json,
                AllowJsonpRequests = true,
            };
        }

        public static IEnumerable<IPlugin> GetPlugins()
        {
            yield return new CorsFeature();
        }
    }
}
