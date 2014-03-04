using ServiceStack.Common.Utils;
using ServiceStack.Text;
using ServiceStack.TripThruGateway;
using Utils;
using TripThruCore;

namespace ServiceStack.TripThruGateway
{
	using System;
	using System.Collections.Generic;
	using ServiceStack.ServiceHost;
	using ServiceStack.ServiceInterface;


    public class InitGateway : IReturn<InitGatewayResponse>
	{
	}
    public class InitGatewayResponse
	{
	}

    public class InitGatewayService : Service
	{

        public object Any(InitGateway request)
        {
            try
            {
                GatewayService.gateway = new TripThru(); // TODO: do we need this?
                Logger.OpenLog("TripThruGateway");
                //Logger.OpenLog("TripThruGateway", "c:\\Users\\Edward\\");
                MapTools.LoadGeoData("~/App_Data/Geo-Location-Names.csv".MapHostAbsolutePath(), "~/App_Data/Geo-Routes.csv".MapHostAbsolutePath(), "~/App_Data/Geo-Location-Addresses.csv".MapHostAbsolutePath());

            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
            }
            return new InitGatewayResponse();
		}
	}
}