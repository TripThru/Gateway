using System;
using System.Web;
using TripThru.Gateway.App_Start;

namespace ServiceStack.TripThruGateway
{
    public class Global : HttpApplication
	{
        protected void Application_Start(object sender, EventArgs e)
        {
            //Initialize your application
            (new TripThruGatewayHost()).Init();
        }
	}
}