using ServiceStack.TripThruGateway;
using ServiceStack.TripThruGateway.TripThru;

namespace ServiceStack.TripThruGateway
{
	using System;
	using System.Collections.Generic;
	using ServiceStack.OrmLite;
	using ServiceStack.ServiceHost;
	using ServiceStack.ServiceInterface;


	public class InitPartners : IReturn<InitPartnersResponse>
	{
	}
	public class InitPartnersResponse
	{
	}

	public class InitPartnersService : Service
	{
		
		public object Any(InitPartners request)
        {
            GatewayService.TripThru = new TripThru.TripThru();

            Db.CreateTableIfNotExists<Partner>();
            Db.CreateTableIfNotExists<User>();
            Db.DeleteAll<User>();
            Db.DeleteAll<Partner>();

		    var luxor = new User
		    {
		        UserName = "Luxor Cab",
		        Password = "password",
		        Email = "partner1@tripthru.com",
		        AccessToken = "23noiasdn2123",
		        RefreshToken = "23noiasdn2123",
		        ClientId = "luxor@tripthru.com",
		        ClientSecret = "23noiasdn2123"
		    };
            Db.Insert(luxor);
            var partnerLuxor = new Partner
            {
                Name = "Luxor Cab",
                CallbackUrl = "http://localhost:17188/json/asynconeway/",
                UserId = (Int32)Db.GetLastInsertId()
            };
            Db.Insert(partnerLuxor);
            GatewayService.TripThru.AddPartner(new TripThru.TripThru.Partner(
                    partnerLuxor.Name,
                    partnerLuxor.CallbackUrl,
                    luxor.ClientId,
                    "jaosid1201231" //Made up for now
                ));


		    var yellow = new User
		    {
		        UserName = "Yellow Cab",
		        Password = "password",
		        Email = "yellowcab@tripthru.com",
		        AccessToken = "12ondazazxx21",
		        RefreshToken = "12ondazazxx21",
		        ClientId = "yellow@tripthru.com",
		        ClientSecret = "12ondazazxx21"
		    };
            Db.Insert(yellow);
            var partnerYellow = new Partner
            {
                Name = "Yellow Cab",
                CallbackUrl = "http://www.yellowcab.com/gateway/v1/",
                UserId = (Int32)Db.GetLastInsertId()
            };
            Db.Insert(partnerYellow);
            GatewayService.TripThru.AddPartner(new TripThru.TripThru.Partner(
                    partnerYellow.Name,
                    partnerYellow.CallbackUrl,
                    yellow.ClientId,
                    "jaosid1201231" //Made up for now
                ));


		    var metro = new User
		    {
		        UserName = "Metro Cab of Boston",
		        Password = "password",
		        Email = "metro@tripthru.com",
		        AccessToken = "12ondazazxx21",
		        RefreshToken = "12ondazazxx21",
		        ClientId = "metro@tripthru.com",
		        ClientSecret = "12ondazazxx21"
		    };
            Db.Insert(metro);
            var partnerMetro = new Partner
            {
                Name = "Metro Cab of Boston",
                CallbackUrl = "http://www.metrocabofboston.com/gateway/v1/",
                UserId = (Int32)Db.GetLastInsertId()
            };
            Db.Insert(partnerMetro);
            GatewayService.TripThru.AddPartner(new TripThru.TripThru.Partner(
                    partnerMetro.Name,
                    partnerMetro.CallbackUrl,
                    metro.ClientId,
                    "jaosid1201231" //Made up for now
                ));


		    var les = new User
		    {
		        UserName = "Les Taxi Blues",
		        Password = "password",
		        Email = "lestaxi@tripthru.com",
		        AccessToken = "12ondazazxx21",
		        RefreshToken = "12ondazazxx21",
		        ClientId = "les@tripthru.com",
		        ClientSecret = "12ondazazxx21"
		    };
            Db.Insert(les);
            var partnerLes = new Partner
            {
                Name = "Les Taxi Blues",
                CallbackUrl = "http://www.lestaxiblues.com/gateway/v1/",
                UserId = (Int32)Db.GetLastInsertId()
            };
            Db.Insert(partnerLes);
            GatewayService.TripThru.AddPartner(new TripThru.TripThru.Partner(
                    partnerLes.Name,
                    partnerLes.CallbackUrl,
                    les.ClientId,
                    "jaosid1201231" //Made up for now
                ));


		    var dubai = new User
		    {
		        UserName = "Dubai Taxi Corporation",
		        Password = "password",
		        Email = "dubaitaxicorp@tripthru.com",
		        AccessToken = "12ondazazxx21",
		        RefreshToken = "12ondazazxx21",
		        ClientId = "dubai@tripthru.com",
		        ClientSecret = "12ondazazxx21"
		    };
            Db.Insert(dubai);
            var partnerDubai = new Partner
            {
                Name = "Dubai Taxi Corporation",
                CallbackUrl = "http://www.dubaitaxicorporation.com/gateway/v1/",
                UserId = (Int32)Db.GetLastInsertId()
            };
            Db.Insert(partnerDubai);
            GatewayService.TripThru.AddPartner(new TripThru.TripThru.Partner(
                    partnerDubai.Name,
                    partnerDubai.CallbackUrl,
                    dubai.ClientId,
                    "jaosid1201231" //Made up for now
                ));

            Logger.OpenLog("TripThruSimulation.log", true);
            Logger.Log("Starting partners " + DateTime.UtcNow);
            Logger.Tab();
            foreach (TripThru.TripThru.Partner p in GatewayService.TripThru.partners)
                p.Log();
            Logger.Untab();

			return new InitPartnersResponse();
		}
	}
}