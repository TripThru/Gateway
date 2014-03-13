using System;
using System.Net.Mime;
using ServiceStack.Razor;
using ServiceStack.ServiceHost;
using ServiceStack.TripThruGateway;
using ServiceStack.WebHost.Endpoints.Extensions;
using Utils;
using ContentType = ServiceStack.Common.Web.ContentType;
using ServiceStack.Api.Swagger;

namespace ServiceStack.TripThruGateway
{
	using Funq;
	using ServiceStack.Common.Utils;
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

            //Unhandled exceptions
            //Handle Exceptions occurring in Services:
            this.ServiceExceptionHandler = (request, exception) => {

                //log your exceptions here
                Logger.LogDebug("ServiceExceptionHandler : " + exception.Message, exception.StackTrace);

                //call default exception handler or prepare your own custom response
                return DtoUtils.HandleException(this, request, exception);
            };

            //Handle Unhandled Exceptions occurring outside of Services, 
            //E.g. in Request binding or filters:
            this.ExceptionHandler = (req, res, operationName, ex) =>
            {
                 Logger.LogDebug("ExceptionHandler : "+ex.Message, ex.StackTrace);
                 res.Write("Error: {0}: {1}".Fmt(ex.GetType().Name, ex.Message));
                 res.EndServiceStackRequest(skipHeaders: true);
            };

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

            Plugins.Add(new SwaggerFeature());
		}
	}
}