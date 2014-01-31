using ServiceStack.TripThruGateway;
using Utils;
namespace ServiceStack.TripThruGateway
{
	using System;
	using System.Collections.Generic;
	using ServiceStack.OrmLite;
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

        GatewayClient GetGatewayClient(string accessToken, string callbackURL)
        {
            return new GatewayRestClient(accessToken, callbackURL);
        }
        public object Any(InitGateway request)
        {
            GatewayService.gateway = new TripThru(GetGatewayClient);

            Db.CreateTableIfNotExists<Partner>();
            Db.CreateTableIfNotExists<User>();
            Db.DeleteAll<User>();
            Db.DeleteAll<Partner>();

		    var luxor = new User
		    {
		        UserName = "Luxor Cab",
		        Password = "password",
		        Email = "partner1@tripthru.com",
		        AccessToken = "luxor23noiasdn2123",
		        RefreshToken = "23noiasdn2123",
		        ClientId = "luxor@tripthru.com",
		        ClientSecret = "23noiasdn2123"
		    };
            Db.Insert(luxor);


		    var yellow = new User
		    {
		        UserName = "Yellow Cab",
		        Password = "password",
		        Email = "yellowcab@tripthru.com",
		        AccessToken = "yellow12ondazazxx21",
		        RefreshToken = "12ondazazxx21",
		        ClientId = "yellow@tripthru.com",
		        ClientSecret = "12ondazazxx21"
		    };
            Db.Insert(yellow);


		    var metro = new User
		    {
		        UserName = "Metro Cab of Boston",
		        Password = "password",
		        Email = "metro@tripthru.com",
		        AccessToken = "metro12ondazazxx21",
		        RefreshToken = "12ondazazxx21",
		        ClientId = "metro@tripthru.com",
		        ClientSecret = "12ondazazxx21"
		    };
            Db.Insert(metro);

		    var les = new User
		    {
		        UserName = "Les Taxi Blues",
		        Password = "password",
		        Email = "lestaxi@tripthru.com",
		        AccessToken = "les12ondazazxx21",
		        RefreshToken = "12ondazazxx21",
		        ClientId = "les@tripthru.com",
		        ClientSecret = "12ondazazxx21"
		    };
            Db.Insert(les);


		    var dubai = new User
		    {
		        UserName = "Dubai Taxi Corporation",
		        Password = "password",
		        Email = "dubaitaxicorp@tripthru.com",
		        AccessToken = "dubai12ondazazxx21",
		        RefreshToken = "12ondazazxx21",
		        ClientId = "dubai@tripthru.com",
		        ClientSecret = "12ondazazxx21"
		    };
            Db.Insert(dubai);

            var tripthruweb = new User
            {
                UserName = "Tripthru Web",
                Password = "password",
                Email = "web@tripthru.com",
                AccessToken = "web12ondazazxx21",
                RefreshToken = "12ondazazxx21",
                ClientId = "web@tripthru.com",
                ClientSecret = "12ondazazxx21"
            };
            Db.Insert(tripthruweb);

            Logger.OpenLog();
            Logger.SetLogId("gateway");
            return new InitGatewayResponse();
		}
	}
}