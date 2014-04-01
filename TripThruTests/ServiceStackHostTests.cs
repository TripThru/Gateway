using System;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Utils;
using ServiceStack.TripThruGateway;
using System.Linq.Expressions;
using System.Threading;

namespace TripThruTests
{
    class ServiceStackHostTests
    {
        TripThruGatewayTestHost AppHost;

        [TestFixtureSetUp] // this method will run once before all other unit tests
        public void OnTestFixtureSetUp()
        {
            AppHost = new TripThruGatewayTestHost();
            AppHost.Init();
            AppHost.Start(TripThruGatewayTestHost.BaseUrl);
        }

        [TestFixtureTearDown] // runs once after all other unit tests
        public void OnTestFixtureTearDown()
        {
            AppHost.Dispose();
        }
    }
}
