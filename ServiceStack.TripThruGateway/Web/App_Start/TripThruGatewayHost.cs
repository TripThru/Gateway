using System;
using System.Net.Mime;
using ServiceStack.Razor;
using ServiceStack.ServiceHost;
using ServiceStack.TripThruGateway;
using ServiceStack.WebHost.Endpoints.Extensions;
using TripThru.Gateway.App_Start;
using ContentType = ServiceStack.Common.Web.ContentType;

namespace TripThru.Gateway.App_Start
{
	using Funq;
	using ServiceStack.Common.Utils;
	using ServiceStack.OrmLite;
	using ServiceStack.OrmLite.Sqlite;
	using ServiceStack.ServiceInterface.Cors;
	using ServiceStack.Text;
	using ServiceStack.WebHost.Endpoints;

	public class TripThruGatewayHost
		: AppHostBase
	{
		/// <summary>
		///     Initializes a new instance of your ServiceStack application, with the specified name and assembly containing the services.
		/// </summary>
		public TripThruGatewayHost() : base("TripThru gateway v1", typeof (GatewayService).Assembly)
		{
		}

		public override void Configure(Container container)
		{
			JsConfig.DateHandler = JsonDateHandler.ISO8601;
			JsConfig.EmitCamelCaseNames = true;

			container.Register<IDbConnectionFactory>(
                c => new OrmLiteConnectionFactory("Server=127.0.0.1; Database=GatewaySandbox; Uid=tripservice; Pwd=Tr1PServ1Ce@MySqL;", MySqlDialect.Provider));
				
			//container.Register<IDbConnectionFactory>(
			//	c => new OrmLiteConnectionFactory("~/db.sqlite".MapHostAbsolutePath(), SqliteDialect.Provider));

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
            using (var initPartners = container.Resolve<InitGatewayService>())
            {
                initPartners.Any(null);
            }

		}
	}
}