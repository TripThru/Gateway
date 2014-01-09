using System;
using System.Net.Mime;
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

	public class AppHost
		: AppHostBase
	{
		/// <summary>
		///     Initializes a new instance of your ServiceStack application, with the specified name and assembly containing the services.
		/// </summary>
		public AppHost() : base("TripThru gateway v1", typeof (GatewayService).Assembly)
		{
		}

		public override void Configure(Container container)
		{
			//JsConfig.DateHandler = JsonDateHandler.ISO8601;

			JsConfig.EmitCamelCaseNames = true;

			container.Register<IDbConnectionFactory>(
				c => new OrmLiteConnectionFactory("Server=127.0.0.1; Database=GatewaySandbox; Uid=root; Pwd=;", MySqlDialect.Provider));
				
			//container.Register<IDbConnectionFactory>(
			//	c => new OrmLiteConnectionFactory("~/db.sqlite".MapHostAbsolutePath(), SqliteDialect.Provider));

			Plugins.Add(new CorsFeature()); //Enable CORS

            this.RequestFilters.Add((req, res, requestDto) =>
            {
                var par = req.GetRequestParams();
                foreach (var p in par)
                {
                    Console.WriteLine(p);
                }
            });
            
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

            //Init
            using (var initPartners = container.Resolve<InitPartnersService>())
            {
                initPartners.Any(null);
            }

		}
	}
}