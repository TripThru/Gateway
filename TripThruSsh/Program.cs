using System;
using System.Collections.Generic;

namespace TripThruSsh
{
    class Program
    {
        private static Dictionary<string, GatewayDeploy.Environment> environments = new Dictionary<string, GatewayDeploy.Environment>{
            {"sandbox", new GatewayDeploy.Environment{
                     host = "54.201.134.194",
                     user = "tripservice",
                     password = "Tr1PServ1CeSt@Ck",
                     sshPort = 22,
                     debug = false
            }},
            {"digital-ocean", new GatewayDeploy.Environment{
                     host = "107.170.240.134",
                     user = "tripservice",
                     password = "Tr1PServ1CeSt@Ck",
                     sshPort = 22,
                     debug = false
            }},
            {"digital-ocean-dev", new GatewayDeploy.Environment{
                     host = "107.170.235.36",
                     user = "tripservice",
                     password = "Tr1PServ1CeSt@Ck",
                     sshPort = 22,
                     debug = true
            }},
            {"vagrant", new GatewayDeploy.Environment{
                     host = "192.168.0.125",
                     user = "tripservice",
                     password = "Tr1PServ1CeSt@Ck",
                     sshPort = 22,
                     debug = true
            }}
        };

        public static void Main(string[] args)
        {
            GatewayDeploy.Deploy(
                environment: environments["digital-ocean-dev"],
                projectsLocalPath: @"Z:\WindowsDev\Gateway\",
                projectsRemotePath: "/home/tripservice/servicestack/",
                partnerConfigurationsPath: "PartnerConfigurations/",
                updateConfigurationsOnly: true,
                simulatedPartners: false
            );
        }
    }
}
