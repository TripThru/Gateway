using System;
using System.Collections.Generic;
using System.Net.Mime;
using System.IO;
using ServiceStack.Common;
using ServiceStack.Razor;
using ServiceStack.ServiceHost;
using ServiceStack.ServiceInterface;
using ServiceStack.TripThruGateway;
using ServiceStack.WebHost.Endpoints.Extensions;
using Utils;
using ContentType = ServiceStack.Common.Web.ContentType;
using ServiceStack.Api.Swagger;
using ServiceStack.ServiceInterface;
using ServiceStack.ServiceInterface.Auth;


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
                            AllowJsonpRequests = true,
                            DefaultRedirectPath = "/stats",
                            MetadataRedirectPath = "/stats",
                            MetadataCustomPath = "/stats"
                          });

            var configuration = 
                JsonSerializer.DeserializeFromString<HostConfiguration>(
                File.ReadAllText("~/HostConfig.txt".MapHostAbsolutePath()));
            //Authentication
		    Plugins.Add(
                new AuthFeature(() => new AuthUserSession(),
                    new IAuthProvider[] {
                        new CustomCredentialsAuthProvider(
                            new Dictionary<string, string>(){ {"tripthru", "optimize"} },
                            configuration.host.virtualPath
                        )
                    }
                )
                {
                    HtmlRedirect = "~/login.html",
                    ServiceRoutes = new Dictionary<Type, string[]> {
                        { typeof(AuthService), new[]{"/auth", "/auth/{provider}"}},
                        { typeof(AssignRolesService), new[]{"/assignroles"} },
                        { typeof(UnAssignRolesService), new[]{"/unassignroles"} },
                    }
                }
            );

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

    public class CustomCredentialsAuthProvider : CredentialsAuthProvider
    {
        private Dictionary<string, string> authenticatedUsers;
        private string referrerUrl;
        public CustomCredentialsAuthProvider(Dictionary<string, string> usersWithPassword, string referrerUrl)
        {
            this.authenticatedUsers = usersWithPassword;
            this.referrerUrl = referrerUrl;
        }

        public override bool TryAuthenticate(IServiceBase authService, string userName, string password)
        {
            return authenticatedUsers.ContainsKey(userName) && authenticatedUsers[userName] == password;
        }

        public override void OnAuthenticated(IServiceBase authService,
            IAuthSession session, IOAuthTokens tokens, Dictionary<string, string> authInfo)
        {
            session.ReferrerUrl = referrerUrl;
            session.IsAuthenticated = true;
            authService.SaveSession(session, new TimeSpan(7, 0, 0, 0));
        }
    }

    public class HostConfiguration
    {
        public Host host { get; set; }

        public class Host
        {
            public string virtualPath { get; set; }
        }
    }
}
