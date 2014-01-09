using System;
using TripThru.Gateway.App_Start;

namespace TripThru.Gateway
{
	using System.Web;

	public class Global : HttpApplication
	{
        protected void Application_Start(object sender, EventArgs e)
        {
            //Initialize your application
            (new AppHost()).Init();
        }
	}
}