using Funq;
using ServiceStack.Common.Utils;
using ServiceStack.OrmLite;
using ServiceStack.Razor;
using ServiceStack.Text;
using ServiceStack.TripThruGateway;
using ServiceStack.WebHost.Endpoints;
using ContentType = ServiceStack.Common.Web.ContentType;

namespace ServiceStack.TripThruPartnerGateway.App_Start
{
    public class TripThruPartnerGatewayHost
		: AppHostBase
	{
		/// <summary>
		///     Initializes a new instance of your ServiceStack application, with the specified name and assembly containing the services.
		/// </summary>
        public TripThruPartnerGatewayHost()
            : base("TripThru partner gateway v1", typeof(ServiceStack.TripThruGateway.GatewayService).Assembly)
		{
		}

		public override void Configure(Container container)
		{
			JsConfig.DateHandler = JsonDateHandler.ISO8601;
			JsConfig.EmitCamelCaseNames = true;

			container.Register<IDbConnectionFactory>(
				c => new OrmLiteConnectionFactory("Server=127.0.0.1; Database=GatewaySandboxPartner; Uid=tripservice; Pwd=Tr1PServ1Ce@MySqL;", MySqlDialect.Provider));

            Plugins.Add(new RazorFormat());
            
			SetConfig(new EndpointHostConfig
				          {
                            GlobalResponseHeaders = {
							    { "Access-Control-Allow-Origin", "*" },
							    { "Access-Control-Allow-Methods", "GET, POST, PUT, DELETE" },
							    { "Access-Control-Allow-Headers", "Content-Type" },
						    },
                            DebugMode = true,
                            DefaultContentType = ContentType.Json,
                            AllowJsonpRequests = true
                          });

            container.RegisterAutoWiredAs<TripThruPartnerGateway.InitPartnerService, InitPartnerService>();

            /**
             * Note: since Mono by default doesn't have any trusted certificates is better to validate them in the app domain
             * than to add them manually to the deployment server
            */
            System.Net.ServicePointManager.ServerCertificateValidationCallback =
                (sender, certificate, chain, sslPolicyErrors) =>
                {
                    return true; //Todo: fix this to actually validate the certificates
                };

            //Init
		    using (var initPartners = container.Resolve<InitPartnerService>())
		    {
		        initPartners.Any(null);
		    }
		}
	}
}