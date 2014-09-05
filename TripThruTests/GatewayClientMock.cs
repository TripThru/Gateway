using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ServiceStack.TripThruGateway;
using TripThruCore;
using Utils;

namespace TripThruTests
{
    class GatewayClientMock : GatewayMock
    {
        public GatewayClientMock(Gateway server)
            : base(server)
        {

        }
    }
}
