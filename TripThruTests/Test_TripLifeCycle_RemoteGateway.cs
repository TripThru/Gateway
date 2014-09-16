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
using TripThruSsh;

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
                partnersGatewayHub = new PartnersGatewayHub();
                GatewayService.gateway = partnersGatewayHub;
                partnersServiceHost = new SelfAppHost("PartnersHost");
                partnersServiceHost.Start("http://127.0.0.1:9001/");
            }
            private void StopPartnersHost()
            {
                partnersGatewayHub = null;
                partnersServiceHost.Stop();
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


            [TearDown]
            public void TearDown()
            {
                Logger.Log("Tearing down");
                StopPartnersHost();
                StopTripThruHost();
                StorageManager.Reset();
            }

            [Test]
            public void EnoughDrivers_TwoPartnersShareJobs_Gateway()
            {
                Logger.Log("EnoughDrivers_TwoPartnersShareJobs_Gateway");
                var libA = new Test_TripLifeCycle_Base(
                    filename: "Test_Configurations/ForeignTripsEnoughDriversA.txt",
                    tripthru: tripthru,
                    maxLateness: new TimeSpan(0, 5, 0),
                    origination: PartnerTrip.Origination.Local,
                    service: PartnerTrip.Origination.Foreign,
                    locationVerificationTolerance: 4);
                var libB = new Test_TripLifeCycle_Base(
                    filename: "Test_Configurations/ForeignTripsEnoughDriversB.txt",
                    tripthru: tripthru,
                    maxLateness: new TimeSpan(0, 5, 0),
                    origination: PartnerTrip.Origination.Local,
                    service: PartnerTrip.Origination.Foreign,
                    locationVerificationTolerance: 4);
                List<SubTest> subTests = libA.MakeSimultaneousTripLifecycle_SubTests();
                subTests.AddRange(libB.MakeSimultaneousTripLifecycle_SubTests());
                List<Partner> partners = new List<Partner>() { libA.partner, libB.partner };
                Test_TripLifeCycle_Base.RunSubTests(partners, subTests,
                    timeoutAt: DateTime.UtcNow + new TimeSpan(1, 0, 0),
                    simInterval: new TimeSpan(0, 0, 1)
                );
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
                foreach (string filename in filePaths)
                {
                    Logger.Log("filename = " + filename);
                    var lib = new Test_TripLifeCycle_Base(
                        filename: filename,
                        tripthru: tripthru,
                        maxLateness: maxLateness,
                        locationVerificationTolerance: locationVerificationTolerance);
                    partners.Add(lib.partner);
                    subtests.AddRange(lib.MakeSimultaneousTripLifecycle_SubTests());
                }
                Test_TripLifeCycle_Base.RunSubTests(partners, subtests,
                    timeoutAt: DateTime.UtcNow + new TimeSpan(1, 0, 0),
                    simInterval: new TimeSpan(0, 0, 1)
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

        public override void Configure(Container container)
        {
            AppHostCommon.Init(container);
            this.SetConfig(AppHostCommon.GetConfig());
            this.Plugins.AddRange(AppHostCommon.GetPlugins());

            JsConfig.DateHandler = JsonDateHandler.ISO8601;
            JsConfig.EmitCamelCaseNames = true;
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
     * sending the target partner's clientID instead of TripThru's own ID by modifying the database accounts
     * setting TripThruAccessToken the be the same as each partner's AccessToken.
     * 
     * We need to find a way to start multiple AppHosts each with it's own individual partner instance
     * passed to GatewayService's constructor instead of having a static gateway.
     */
    public class PartnersGatewayHub : Gateway
    {
        private Dictionary<string, Gateway> partnersByClientID;

        public PartnersGatewayHub() : 
            base("PartnersGatewayHub", "PartnersGatewayHub")
        {
            this.partnersByClientID = new Dictionary<string, Gateway>();
        }

        private void ValidatePartnerExists(string id)
        {
            if (!partnersByClientID.ContainsKey(id))
                throw new Exception("Partner " + id + " not found");
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
            request.clientID = "TripThru";
            return partnersByClientID[request.clientID].GetPartnerInfo(request);
        }

        public override Gateway.DispatchTripResponse DispatchTrip(Gateway.DispatchTripRequest request)
        {
            ValidatePartnerExists(request.clientID);
            request.clientID = "TripThru";
            return partnersByClientID[request.clientID].DispatchTrip(request);
        }

        public override Gateway.QuoteTripResponse QuoteTrip(Gateway.QuoteTripRequest request)
        {
            ValidatePartnerExists(request.clientID);
            request.clientID = "TripThru";
            return partnersByClientID[request.clientID].QuoteTrip(request);
        }

        public override Gateway.GetTripStatusResponse GetTripStatus(Gateway.GetTripStatusRequest request)
        {
            ValidatePartnerExists(request.clientID);
            request.clientID = "TripThru";
            return partnersByClientID[request.clientID].GetTripStatus(request);
        }

        public override Gateway.UpdateTripStatusResponse UpdateTripStatus(Gateway.UpdateTripStatusRequest request)
        {
            ValidatePartnerExists(request.clientID);
            request.clientID = "TripThru";
            return partnersByClientID[request.clientID].UpdateTripStatus(request);
        }

        public override Gateway.UpdateQuoteResponse UpdateQuote(Gateway.UpdateQuoteRequest request)
        {
            ValidatePartnerExists(request.clientID);
            request.clientID = "TripThru";
            return partnersByClientID[request.clientID].UpdateQuote(request);
        }

        public override GetQuoteResponse GetQuote(GetQuoteRequest request)
        {
            ValidatePartnerExists(request.clientID);
            request.clientID = "TripThru";
            return partnersByClientID[request.clientID].GetQuote(request);
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
        private Dictionary<string, GatewayClient> tripThruClientByClientID;
        private string tripThruUrl;
        // Add configuration before partner registration to use access token and callback url when registering
        private Dictionary<string, PartnerAccount> partnerConfigurationByClientID;

        public TripThruGatewayHub(string tripThruUrl,
            Dictionary<string, PartnerAccount> partnerConfigurationByClientID) :
            base("TripThruGatewayHub", "TripThruGatewayHub")
        {
            this.tripThruClientByClientID = new Dictionary<string, GatewayClient>();
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
            tripThruClientByClientID[partner.ID] = new GatewayClient("TripThru", "TripThru", tripThruUrl, accessToken);
            return tripThruClientByClientID[partner.ID];
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
            return tripThruClientByClientID[request.clientID].GetPartnerInfo(request);
        }

        public override Gateway.DispatchTripResponse DispatchTrip(Gateway.DispatchTripRequest request)
        {
            ValidatePartnerExists(request.clientID);
            return tripThruClientByClientID[request.clientID].DispatchTrip(request);
        }

        public override Gateway.QuoteTripResponse QuoteTrip(Gateway.QuoteTripRequest request)
        {
            ValidatePartnerExists(request.clientID);
            return tripThruClientByClientID[request.clientID].QuoteTrip(request);
        }

        public override Gateway.GetTripStatusResponse GetTripStatus(Gateway.GetTripStatusRequest request)
        {
            ValidatePartnerExists(request.clientID);
            return tripThruClientByClientID[request.clientID].GetTripStatus(request);
        }

        public override Gateway.UpdateTripStatusResponse UpdateTripStatus(Gateway.UpdateTripStatusRequest request)
        {
            ValidatePartnerExists(request.clientID);
            return tripThruClientByClientID[request.clientID].UpdateTripStatus(request);
        }

        public override Gateway.UpdateQuoteResponse UpdateQuote(Gateway.UpdateQuoteRequest request)
        {
            ValidatePartnerExists(request.clientID);
            return tripThruClientByClientID[request.clientID].UpdateQuote(request);
        }

        public override GetQuoteResponse GetQuote(GetQuoteRequest request)
        {
            ValidatePartnerExists(request.clientID);
            return tripThruClientByClientID[request.clientID].GetQuote(request);
        }
    }
}
