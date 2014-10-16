using System;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using System.Collections.Generic;
using System.IO;
using TripThruTests;
using Utils;
using TripThruCore;
using System.Threading;
using TripThruCore.Storage;
using ServiceStack.TripThruGateway;
using ServiceStack.WebHost.Endpoints;
using ServiceStack.WebHost.Endpoints.Extensions;
using Funq;
using ServiceStack.Common.Web;
using ServiceStack.ServiceInterface.Cors;
using ServiceStack.Text;
using ServiceStack.ServiceHost;
using TripThruSsh;
using System.Collections.Concurrent;

namespace TripThruTests
{
    class Test_TripLifeCycle_RemoteGateway
    {
        [TestFixture]
        [Category("TripLifeCycle_Remote")]
        public class TripLifeCycle_RemoteTester
        {
            PartnersGatewayHub partnersGatewayHub;
            GatewayMock tripthru;
            SelfAppHost partnersServiceHost;
            GatewayDeploy.Environment remoteServerEnvironment = new GatewayDeploy.Environment()
            {
                host = "107.170.235.36",
                sshPort = 22,
                user = "tripservice",
                password = "Tr1PServ1CeSt@Ck",
                debug = true
            };

            [SetUp]
            public void SetUp()
            {
                Logger.OpenLog("Nunit", splunkEnabled: false);
                Logger.Log("Setting up");
                StorageManager.OpenStorage(new MongoDbStorage("mongodb://localhost:27017/", "TripThru"));
                StorageManager.Reset(); // Sometimes mongo can't delete on teardown between tests
                MapTools.distance_and_time_scale = .05;
                StartPartnersHost();
                StartTripThruHost();
            }

            private void StartPartnersHost()
            {
                ConcurrentDictionary<string, PartnerAccount> accountsByAccessToken = new ConcurrentDictionary<string, PartnerAccount>();
                var partnerAccounts = StorageManager.GetPartnerAccounts();
                foreach (var account in partnerAccounts)
                    accountsByAccessToken[account.AccessToken] = account;

                partnersGatewayHub = new PartnersGatewayHub(accountsByAccessToken);
                GatewayService.gateway = partnersGatewayHub;
                if (partnersServiceHost == null)
                {
                    partnersServiceHost = new SelfAppHost("PartnersHost");
                    partnersServiceHost.Init();
                    partnersServiceHost.Start("http://*:8081/");
                }
            }
            private void StopPartnersHost()
            {
                partnersGatewayHub = null;
            }
            private void StartTripThruHost()
            {
                GatewayDeploy.Start(remoteServerEnvironment);
                Dictionary<string, PartnerAccount> accountsByClientID = new Dictionary<string, PartnerAccount>();
                var partnerAccounts = StorageManager.GetPartnerAccounts()
                    .Where(a => a.Role == Storage.UserRole.partner);
                foreach (var account in partnerAccounts)
                    accountsByClientID[account.ClientId] = account;
                var tripThruGatewayHub = new TripThruGatewayHub(
                    tripThruUrl: "http://" + remoteServerEnvironment.host + "/TripThru.TripThruGateway/",
                    partnerConfigurationByClientID: accountsByClientID
                );
                tripthru = new GatewayMock(tripThruGatewayHub);
            }
            private void StopTripThruHost()
            {
                GatewayDeploy.Stop(remoteServerEnvironment);
                tripthru = null;
            }
            // We have a GatewayService connected to a gateway that concentrates all partners so 
            // we need to register the partners instances used by the tests. We use the mocks created by
            // Test_TripLifeCycle_Base to catch all the incoming requests used for validations.
            private void RegisterPartners(List<GatewayMock> partners)
            {
                foreach (var partner in partners)
                    partnersGatewayHub.RegisterPartner(partner, ((Partner)partner.server).PartnerFleets.First().Value.coverage);
            }


            [TearDown]
            public void TearDown()
            {
                Logger.Log("Tearing down");
                StopPartnersHost();
                StopTripThruHost();
                StorageManager.Reset();
            }

            [Test]
            public void EnoughDrivers_AllPartners_Gateway()
            {
                Logger.Log("EnoughDrivers_AllPartners_Gateway");
                TimeSpan maxLateness = new TimeSpan(0, 20, 0);
                double locationVerificationTolerance = 4;
                string[] filePaths = Directory.GetFiles("../../Test_Configurations/Partners/");
                Logger.Log("filePaths = " + filePaths);
                List<SubTest> subtests = new List<SubTest>();
                List<Partner> partners = new List<Partner>();
                List < GatewayMock > partnerMocks = new List<GatewayMock>();
                foreach (string filename in filePaths)
                {
                    Logger.Log("filename = " + filename);
                    var lib = new Test_TripLifeCycle_Base(
                        filename: filename,
                        tripthru: tripthru,
                        maxLateness: maxLateness,
                        locationVerificationTolerance: locationVerificationTolerance);
                    partners.Add(lib.partner);
                    partnerMocks.Add(lib.partnerServiceMock);
                    subtests.AddRange(lib.MakeSimultaneousTripLifecycle_SubTests());
                }
                RegisterPartners(partnerMocks);
                Test_TripLifeCycle_Base.RunSubTests(partners, subtests,
                    timeoutAt: DateTime.UtcNow + new TimeSpan(1, 0, 0),
                    simInterval: new TimeSpan(0, 0, 50)
                );
            }
        }
    }

    public class SelfAppHost : AppHostHttpListenerBase
    {
        public SelfAppHost(string serviceName)
            : base(serviceName, typeof(GatewayService).Assembly)
        {
            
        }

        public void AddPrefixes(List<string> prefixes)
        {
            foreach(var prefix in prefixes)
                base.Listener.Prefixes.Add(prefix);
        }

        public override void Configure(Container container)
        {
            AppHostCommon.Init(container);
            this.SetConfig(AppHostCommon.GetConfig());
            this.Plugins.AddRange(AppHostCommon.GetPlugins());

            JsConfig.DateHandler = JsonDateHandler.ISO8601;
            JsConfig.EmitCamelCaseNames = true;

            //Unhandled exceptions
            //Handle Exceptions occurring in Services:
            this.ServiceExceptionHandler = (request, exception) =>
            {
                //log your exceptions here
                Logger.LogDebug("ServiceExceptionHandler : " + exception.Message, exception.StackTrace);

                //call default exception handler or prepare your own custom response
                return DtoUtils.HandleException(this, request, exception);
            };
            //Handle Unhandled Exceptions occurring outside of Services, 
            //E.g. in Request binding or filters:
            this.ExceptionHandler = (req, res, operationName, ex) =>
            {
                Logger.LogDebug("ExceptionHandler : " + ex.Message, ex.StackTrace);
                res.Write("Error: {0}: {1}".Fmt(ex.GetType().Name, ex.Message));
                res.End();
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
        }
    }

    public static class AppHostCommon
    {
        public static void Init(Container container)
        {
        }

        public static EndpointHostConfig GetConfig()
        {
            return new EndpointHostConfig
            {
                GlobalResponseHeaders = {
					{ "Access-Control-Allow-Origin", "*" },
					{ "Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS" },
					{ "Access-Control-Allow-Headers", "Content-Type" },
				},
                ServiceStackHandlerFactoryPath = "services",
                DebugMode = true,
                DefaultContentType = ContentType.Json,
                AllowJsonpRequests = true,
            };
        }

        public static IEnumerable<IPlugin> GetPlugins()
        {
            yield return new CorsFeature();
        }
    }

    /*
     * This class is a hack to have one AppHost receiving requests for all partners, it relies on TripThru
     * sending the target partner's access token instead of TripThru's own ID by modifying the database accounts
     * setting TripThruAccessToken the be the same as each partner's AccessToken.
     * 
     * We need to find a way to start multiple AppHosts each with it's own individual partner instance
     * passed to GatewayService's constructor instead of having a static gateway.
     */
    public class PartnersGatewayHub : Gateway
    {
        private ConcurrentDictionary<string, Gateway> partnersByClientID;
        private ConcurrentDictionary<string, PartnerAccount> partnerConfigurationByAccessToken;

        public PartnersGatewayHub(ConcurrentDictionary<string, PartnerAccount> partnerConfigurationByAccessToken) : 
            base("PartnersGatewayHub", "PartnersGatewayHub")
        {
            this.partnersByClientID = new ConcurrentDictionary<string, Gateway>();
            this.partnerConfigurationByAccessToken = partnerConfigurationByAccessToken;
        }

        private void ValidatePartnerExists(string id)
        {
            if (!partnersByClientID.ContainsKey(id))
                throw new Exception("Partner " + id + " not found");
        }

        private Gateway GetPartner(string clientID)
        {
            Gateway partner = null;
            partnersByClientID.TryGetValue(clientID, out partner);
            return partner;
        }

        // Since we need to receive a request from GatewayService with a targeted partner we return the partner's account
        public override PartnerAccount GetPartnerAccountByAccessToken(string accessToken)
        {
            if (!partnerConfigurationByAccessToken.ContainsKey(accessToken))
                return null;
            return partnerConfigurationByAccessToken[accessToken];
        }

        public override Gateway.RegisterPartnerResponse RegisterPartner(Gateway partner, List<Zone> coverage)
        {
            if (partnersByClientID.ContainsKey(partner.ID))
                throw new Exception("Partner " + partner.ID + " already exists.");
            partnersByClientID[partner.ID] = partner;
            return new RegisterPartnerResponse();
        }

        public override Gateway.GetPartnerInfoResponse GetPartnerInfo(Gateway.GetPartnerInfoRequest request)
        {
            ValidatePartnerExists(request.clientID);
            var partnerId = request.clientID;
            request.clientID = "TripThru";
            return GetPartner(partnerId).GetPartnerInfo(request);
        }

        public override Gateway.DispatchTripResponse DispatchTrip(Gateway.DispatchTripRequest request)
        {
            ValidatePartnerExists(request.clientID);
            var partnerId = request.clientID;
            request.clientID = "TripThru";
            return GetPartner(partnerId).DispatchTrip(request);
        }

        public override Gateway.QuoteTripResponse QuoteTrip(Gateway.QuoteTripRequest request)
        {
            ValidatePartnerExists(request.clientID);
            var partnerId = request.clientID;
            request.clientID = "TripThru";
            return GetPartner(partnerId).QuoteTrip(request);
        }

        public override Gateway.GetTripStatusResponse GetTripStatus(Gateway.GetTripStatusRequest request)
        {
            ValidatePartnerExists(request.clientID);
            var partnerId = request.clientID;
            request.clientID = "TripThru";
            return GetPartner(partnerId).GetTripStatus(request);
        }

        public override Gateway.UpdateTripStatusResponse UpdateTripStatus(Gateway.UpdateTripStatusRequest request)
        {
            Console.WriteLine("PH UpdateTripStatus(" + request.status + ", " + request.tripID + ") received from " + request.clientID);
            ValidatePartnerExists(request.clientID);
            var partnerId = request.clientID;
            request.clientID = "TripThru";
            return GetPartner(partnerId).UpdateTripStatus(request);
        }

        public override Gateway.UpdateQuoteResponse UpdateQuote(Gateway.UpdateQuoteRequest request)
        {
            ValidatePartnerExists(request.clientID);
            var partnerId = request.clientID;
            request.clientID = "TripThru";
            return GetPartner(partnerId).UpdateQuote(request);
        }

        public override GetQuoteResponse GetQuote(GetQuoteRequest request)
        {
            ValidatePartnerExists(request.clientID);
            var partnerId = request.clientID;
            request.clientID = "TripThru";
            return GetPartner(partnerId).GetQuote(request);
        }
    }

    /*
     * This class serves as a central point where all partner instances will send their requests
     * this way we can wrap it in a GatewayMock and track all outgoing requests.
     * 
     * Since this a class for remote gateway tests we use GatewayClient instances, one for each 
     * partner with it's corresponding access token and callback url. 
     * 
     * For now since we only have one SelfHostApp for all partners using PartnersGatewayHub, 
     * the callback url is the same for all partners.
     */
    public class TripThruGatewayHub : Gateway
    {
        private ConcurrentDictionary<string, GatewayClient> tripThruClientByClientID;
        private string tripThruUrl;
        // Add configuration before partner registration to use access token and callback url when registering
        private Dictionary<string, PartnerAccount> partnerConfigurationByClientID;

        public TripThruGatewayHub(string tripThruUrl,
            Dictionary<string, PartnerAccount> partnerConfigurationByClientID) :
            base("TripThruGatewayHub", "TripThruGatewayHub")
        {
            this.tripThruClientByClientID = new ConcurrentDictionary<string, GatewayClient>();
            this.tripThruUrl = tripThruUrl;
            this.partnerConfigurationByClientID = partnerConfigurationByClientID;
        }

        private void ValidatePartnerExists(string id)
        {
            if (!tripThruClientByClientID.ContainsKey(id))
                throw new Exception("Partner " + id + " not found");
        }
        private GatewayClient CreateTripThruGatewayClient(Gateway partner)
        {
            if (!partnerConfigurationByClientID.ContainsKey(partner.ID))
                throw new Exception("Access token not added for " + partner.ID);
            var accessToken = partnerConfigurationByClientID[partner.ID].AccessToken;
            var client = new GatewayClient("TripThru", "TripThru", tripThruUrl, accessToken);
            tripThruClientByClientID.TryAdd(partner.ID, client);
            return client;
        }
        private GatewayClient GetTripThruClient(string partnerId)
        {
            GatewayClient client = null;
            tripThruClientByClientID.TryGetValue(partnerId, out client);
            return client;
        }
        private Gateway.RegisterPartnerRequest MakeRegisterPartnerRequest(Gateway partner, List<Zone> coverage)
        {
            var config = partnerConfigurationByClientID[partner.ID];
            return new RegisterPartnerRequest(
                name: partner.name,
                callback_url: config.CallbackUrl,
                accessToken: config.AccessToken,
                coverage: coverage
            );
        }

        public override Gateway.RegisterPartnerResponse RegisterPartner(Gateway partner, List<Zone> coverage)
        {
            if (tripThruClientByClientID.ContainsKey(partner.ID))
                throw new Exception("Partner " + partner.ID + " already exists.");
            return CreateTripThruGatewayClient(partner).RegisterPartner(MakeRegisterPartnerRequest(partner, coverage));
        }

        public override Gateway.GetPartnerInfoResponse GetPartnerInfo(Gateway.GetPartnerInfoRequest request)
        {
            ValidatePartnerExists(request.clientID);
            return GetTripThruClient(request.clientID).GetPartnerInfo(request);
        }

        public override Gateway.DispatchTripResponse DispatchTrip(Gateway.DispatchTripRequest request)
        {
            ValidatePartnerExists(request.clientID);
            return GetTripThruClient(request.clientID).DispatchTrip(request);
        }

        public override Gateway.QuoteTripResponse QuoteTrip(Gateway.QuoteTripRequest request)
        {
            ValidatePartnerExists(request.clientID);
            return GetTripThruClient(request.clientID).QuoteTrip(request);
        }

        public override Gateway.GetTripStatusResponse GetTripStatus(Gateway.GetTripStatusRequest request)
        {
            ValidatePartnerExists(request.clientID);
            return GetTripThruClient(request.clientID).GetTripStatus(request);
        }

        public override Gateway.UpdateTripStatusResponse UpdateTripStatus(Gateway.UpdateTripStatusRequest request)
        {
            ValidatePartnerExists(request.clientID);
            return GetTripThruClient(request.clientID).UpdateTripStatus(request);
        }

        public override Gateway.UpdateQuoteResponse UpdateQuote(Gateway.UpdateQuoteRequest request)
        {
            ValidatePartnerExists(request.clientID);
            return GetTripThruClient(request.clientID).UpdateQuote(request);
        }

        public override GetQuoteResponse GetQuote(GetQuoteRequest request)
        {
            ValidatePartnerExists(request.clientID);
            return GetTripThruClient(request.clientID).GetQuote(request);
        }
    }
}
