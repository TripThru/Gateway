using System;
using System.Web;

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