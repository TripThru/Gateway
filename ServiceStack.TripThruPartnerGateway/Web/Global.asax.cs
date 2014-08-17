using System;
using ServiceStack.TripThruPartnerGateway.App_Start;
using Utils;

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

        protected void Application_Error(object sender, EventArgs e)
        {
            System.Exception ex = Server.GetLastError();
            System.Console.WriteLine("Application_Error : " + ex.Message, ex.StackTrace);
            Logger.LogDebug("Application_Error : " + ex.Message, ex.StackTrace);
        }
	}
}