using System;
using System.Web;
using Utils;

namespace ServiceStack.TripThruGateway
{
    public class Global : HttpApplication
	{
        protected void Application_Start(object sender, EventArgs e)
        {
            //Initialize your application
            (new TripThruGatewayHost()).Init();
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            System.Exception ex = Server.GetLastError();
            Logger.LogDebug("Application_Error : " + ex.Message, ex.StackTrace);
        }
	}
}