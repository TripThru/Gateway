using System;
using ServiceStack.TripThruPartnerGateway.App_Start;

namespace ServiceStack.TripThruPartnerGateway
{
    using System.Web;

    public class Global : HttpApplication
	{
        protected void Application_Start(object sender, EventArgs e)
        {
            //Initialize your application
            (new TripThruPartnerGatewayHost()).Init();
        }
	}
}