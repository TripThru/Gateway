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
    using TripThruCore.Storage;


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
                Logger.OpenLog("TripThruGateway");

                StorageManager.OpenStorage(new SqliteStorage("~/../../Db/db.sqlite".MapHostAbsolutePath()));
                PartnerAccount partnerAccount = new PartnerAccount
                {
                    UserName = "GoGoCabi",
                    Password = "coolapp",
                    Email = "",
                    AccessToken = "iUaySN4P1v3a1m5kQ3K1XvCIa8NkV1Psr",
                    RefreshToken = "",
                    ClientId = "gogocabi@tripthru.com",
                    ClientSecret = "",
                    TripThruAccessToken = ""
                };
                StorageManager.CreatePartnerAccount(partnerAccount);

                GatewayService.gateway = new TripThru();
                //Logger.OpenLog("TripThruGateway", "c:\\Users\\Edward\\");

                foreach (PartnerAccount account in StorageManager.GetPartnerAccounts())
                {
                    if (account.CallbackUrl != null && account.PartnerName != null)
                        GatewayService.gateway.RegisterPartner(
                            new GatewayClient(
                                account.ClientId,
                                account.PartnerName,
                                account.TripThruAccessToken,
                                account.CallbackUrl
                            )
                        );
                }

                MapTools.SetGeodataFilenames("~/App_Data/Geo-Location-Names.txt".MapHostAbsolutePath(), "~/App_Data/Geo-Routes.txt".MapHostAbsolutePath(), "~/App_Data/Geo-Location-Addresses.csv".MapHostAbsolutePath());
                MapTools.LoadGeoData();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
            }
            return new InitGatewayResponse();
		}
	}
}