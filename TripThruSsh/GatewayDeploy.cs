using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using ServiceStack.Text;
using Tamir.SharpSsh;
using System.Threading;
using TripThruCore;

namespace TripThruSsh
{
    public class GatewayDeploy
    {
        private static SshTransferProtocolBase sftpBase;
        private static SshExec ssh;
        private static string localPath;
        private static string remoteFilePath;
        private static string webServer;
        private static string monoServer;
        private static List<PartnerConfiguration> partnerConfigurations;

        public static void Deploy(
            Environment environment, 
            string projectsLocalPath, 
            string projectsRemotePath, 
            string partnerConfigurationsPath, 
            bool updateConfigurationsOnly = false, // true won't update project, just configurations files and restart
            bool simulatedPartners = false // true will deploy both gateway and simulated partners
        )
        {
            if (environment == null || environment.host == null || environment.password == null || environment.user == null || environment.sshPort == null)
                throw new Exception("Environment must be fully specified");
            if (projectsLocalPath == null || projectsRemotePath == null || (simulatedPartners && partnerConfigurationsPath == null))
                throw new Exception("Directories can't be null");

            localPath = projectsLocalPath;
            remoteFilePath = projectsRemotePath;
            monoServer = "http://" + environment.host + "/";
            webServer = "http://" + environment.host + ":8080/";
            partnerConfigurations = MakePartnersConfigurations(configurationsPath: partnerConfigurationsPath, debugMode: environment.debug);

            OpenSshConnection(environment);
            if (!updateConfigurationsOnly) 
                UploadProjects(includePartners: simulatedPartners);
            UpdateTripThruGatewayConfiguration(debugMode: environment.debug);
            if (simulatedPartners) 
                UpdatePartnersConfigurations();
            if (!updateConfigurationsOnly) 
                UpdateMonoConfiguration(includePartners: simulatedPartners);
            RestartMono(includePartners: simulatedPartners);
            CloseSshConnection();
        }
        public static void Start(Environment environment)
        {
            if (environment == null || environment.host == null || environment.password == null || environment.user == null || environment.sshPort == null)
                throw new Exception("Environment must be fully specified");
            OpenSshConnection(environment);
            StartMono();
            CloseSshConnection();
        }
        public static void Stop(Environment environment)
        {
            if (environment == null || environment.host == null || environment.password == null || environment.user == null || environment.sshPort == null)
                throw new Exception("Environment must be fully specified");
            OpenSshConnection(environment);
            StopMono();
            CloseSshConnection();
        }

        private static List<PartnerConfiguration> MakePartnersConfigurations(string configurationsPath, bool debugMode)
        {
            List<PartnerConfiguration> partnerConfigurations = new List<PartnerConfiguration>();
            string[] partnerConfigurationsFiles = Directory.GetFiles(configurationsPath, "*.txt");
            foreach (var partnerConfigurationFile in partnerConfigurationsFiles)
            {
                var configuration =
                    JsonSerializer.DeserializeFromString<PartnerConfiguration>(File.ReadAllText(partnerConfigurationFile));
                if (configuration.Enabled)
                {
                    configuration.TripThruUrlMono = monoServer + configuration.TripThruUrlMono;
                    configuration.Partner.CallbackUrlMono = monoServer + configuration.Partner.CallbackUrlMono;
                    configuration.Partner.WebUrl = webServer + configuration.Partner.WebUrl;
                    configuration.host.debug = debugMode;

                    string configStr = JsonSerializer.SerializeToString<PartnerConfiguration>(configuration);
                    File.WriteAllText(partnerConfigurationFile, configStr);

                    partnerConfigurations.Add(configuration);
                }
            }
            return partnerConfigurations;
        }
        private static void OpenSshConnection(Environment environment)
        {
            ssh = new SshExec(environment.host, environment.user, environment.password);
            ssh.Connect(environment.sshPort);
            Console.WriteLine("Connected");

            sftpBase = new Tamir.SharpSsh.Sftp(environment.host, environment.user, environment.password);
            sftpBase.OnTransferStart += new FileTransferEvent(sftpBase_OnTransferStart);
            sftpBase.OnTransferEnd += new FileTransferEvent(sftpBase_OnTransferEnd);
            Console.WriteLine("Trying to Open Connection...");
            sftpBase.Connect(environment.sshPort);
            Console.WriteLine("Connected Successfully !");
        }
        private static void UploadProjects(bool includePartners)
        {
            //Remove any old files and upload projects
            Console.WriteLine("Uploading projects");
            ssh.RunCommand("cd " + remoteFilePath);
            ssh.RunCommand("rm -rf " + remoteFilePath + "ServiceStack.* Booking*");
            ssh.RunCommand("mkdir -p " + remoteFilePath + "ServiceStack.TripThruGateway/Web");
            UploadDirectory(localPath + "ServiceStack.TripThruGateway/Web", remoteFilePath + "ServiceStack.TripThruGateway/Web");
            ssh.RunCommand("mv " + remoteFilePath + "ServiceStack.TripThruGateway/Web/mono/*  " + remoteFilePath +
                           "ServiceStack.TripThruGateway/Web/bin");
            if (includePartners)
            {
                ssh.RunCommand("mkdir -p " + remoteFilePath + "ServiceStack.TripThruPartnerGateway/Web");
                ssh.RunCommand("mkdir -p " + remoteFilePath + "BookingWebsite");
                UploadDirectory(localPath + "BookingWebsite", remoteFilePath + "BookingWebsite");
                UploadDirectory(localPath + "ServiceStack.TripThruPartnerGateway/Web", remoteFilePath + "ServiceStack.TripThruPartnerGateway/Web");
                ssh.RunCommand("mv " + remoteFilePath + "ServiceStack.TripThruPartnerGateway/Web/mono/*  " +
                               remoteFilePath + "ServiceStack.TripThruPartnerGateway/Web/bin");
            }
        }
        private static void UpdateTripThruGatewayConfiguration(bool debugMode)
        {
            ssh.RunCommand("rm " + remoteFilePath + "ServiceStack.TripThruGateway/Web/HostConfig.txt");
            var configuration = JsonSerializer.DeserializeFromString<HostConfiguration>(
                File.ReadAllText("TripThruGatewayConfigurations/HostConfig.txt"));

            configuration.debug = debugMode;

            string configStr = JsonSerializer.SerializeToString<HostConfiguration>(configuration);
            File.WriteAllText("TripThruGatewayConfigurations/HostConfig.txt", configStr);

            sftpBase.Put("TripThruGatewayConfigurations/HostConfig.txt",
                        remoteFilePath + "ServiceStack.TripThruGateway/Web/HostConfig.txt");
        }
        private static void UpdatePartnersConfigurations()
        {
            string[] partnerConfigurations = Directory.GetFiles("PartnerConfigurations/", "*.txt");
            foreach (var partnerConfiguration in partnerConfigurations)
            {
                var configuration =
                    JsonSerializer.DeserializeFromString<PartnerConfiguration>(File.ReadAllText(partnerConfiguration));
                var name = configuration.Partner.Name.Replace(" ", "");
                Console.WriteLine("Configuring " + name);
                ssh.RunCommand("cp -a " + remoteFilePath + "ServiceStack.TripThruPartnerGateway/  " + remoteFilePath +
                                "ServiceStack." + name + "/");
                ssh.RunCommand("rm " + remoteFilePath + "ServiceStack." + name + "/Web/PartnerConfiguration.txt");
                sftpBase.Put(partnerConfiguration,
                    remoteFilePath + "ServiceStack." + name + "/Web/PartnerConfiguration.txt");
                sftpBase.Put(partnerConfiguration,
                    remoteFilePath + "ServiceStack." + name + "/Web/PartnerConfiguration.txt");

                var bookingwebConfig = new System.IO.StreamWriter("config.txt");
                bookingwebConfig.WriteLine("HomeUrl=" + configuration.Partner.WebUrl);
                bookingwebConfig.WriteLine("RelativeHomeUrl=" + configuration.Partner.WebUrlRelative);
                bookingwebConfig.WriteLine("TripThruUrl=" + configuration.TripThruUrlMono);
                bookingwebConfig.WriteLine("PartnerUrl=" + configuration.Partner.CallbackUrlMono);
                bookingwebConfig.WriteLine("PartnerName=" + name);
                bookingwebConfig.WriteLine("PartnerId=" + configuration.Partner.ClientId);
                bookingwebConfig.Flush();
                bookingwebConfig.Close();
                ssh.RunCommand("rm " + remoteFilePath + "BookingWebsite/inc/tripthru/config.txt");
                sftpBase.Put("config.txt", remoteFilePath + "BookingWebsite/inc/tripthru/");
                ssh.RunCommand("rm " + remoteFilePath + "BookingWebsite/images/taxi-cars_logo.png");
                var x = name + ".png";
                var y = remoteFilePath + "BookingWebsite/images/taxi-cars_logo.png";
                sftpBase.Put("PartnerConfigurations/" + name + ".png",
                    remoteFilePath + "BookingWebsite/images/taxi-cars_logo.png");
                ssh.RunCommand("rm -rf /var/www/sanfran/Bookings" + name);
                ssh.RunCommand("cp -a " + remoteFilePath + "BookingWebsite/ /var/www/sanfran/Bookings" + name);
            }
        }
        private static void UpdateMonoConfiguration(bool includePartners)
        {
            //create fast-cgi mono webapp config
            var webappConfig = new System.IO.StreamWriter("tripthru.webapp");
            webappConfig.WriteLine("<apps>");
            webappConfig.Flush();

            webappConfig.WriteLine(@"<web-application>
                                    <name>TripThru.TripThruGateway</name>
                                    <vhost>*</vhost>
                                    <vport>80</vport>
                                    <vpath>/TripThru.TripThruGateway</vpath>
                                    <path>/var/www/ServiceStack.TripThruGateway/Web</path>
                                 </web-application>"
                );
            webappConfig.Flush();

            if (includePartners)
            {

                foreach (var config in partnerConfigurations)
                {
                    webappConfig.WriteLine(@"<web-application>
                                    <name>TripThru.{0}</name>
                                    <vhost>*</vhost>
                                    <vport>80</vport>
                                    <vpath>/TripThru.{0}</vpath>
                                    <path>/var/www/ServiceStack.{0}/Web</path>
                                 </web-application>", config.Partner.Name.Replace(" ", "")
                    );
                    webappConfig.Flush();
                }
            }

            webappConfig.WriteLine("</apps>");
            webappConfig.Flush();
            webappConfig.Close();

            Console.WriteLine("Updating mono webapp config");
            ssh.RunCommand("rm /etc/rc.d/init.d/mono-fastcgi/tripthru.webapp");
            sftpBase.Put("tripthru.webapp", "/etc/rc.d/init.d/mono-fastcgi/tripthru.webapp");
        }
        private static void RestartMono(bool includePartners)
        {
            StopMono();
            Console.WriteLine("Updating web folder");
            ssh.RunCommand("rm -rf /var/www/ServiceStack.*");
            ssh.RunCommand("cp -a " + remoteFilePath + "/ServiceStack.* /var/www/");
            StartMono();
            if (includePartners)
            {
                Console.WriteLine("Sleep 8 seconds, waiting for mono to initialize.");
                Thread.Sleep(8000);
                var client = new System.Net.WebClient();
                foreach (PartnerConfiguration config in partnerConfigurations)
                {
                    Console.WriteLine("Sending request to: \n" + @config.Partner.CallbackUrlMono.ToString());
                    while (true)
                    {
                        try
                        {
                            var response = client.DownloadString(@config.Partner.CallbackUrlMono.ToString());
                            var analyzeResponse = JsonSerializer.DeserializeFromString<ResponseRequest>(response);
                            if (analyzeResponse.ResultCode.Equals("OK"))
                            {
                                Console.WriteLine("Correct.");
                                break;
                            }
                            else
                            {
                                Thread.Sleep(1000);
                            }
                        }
                        catch (Exception ex)
                        {

                        }
                    }
                }
            }

            Console.WriteLine("Done!");
        }
        private static void StopMono()
        {
            Console.WriteLine("Stopping mono");
            ssh.RunCommand("kill -9 $(netstat -tpan |grep \"LISTEN\"|grep :9000|awk -F' ' '{print $7}'|awk -F'/' '{print $1}')");
        }
        private static void StartMono()
        {
            Console.WriteLine("Starting mono");
            ssh.RunCommand("nohup fastcgi-mono-server4 --appconfigdir /etc/rc.d/init.d/mono-fastcgi /socket=tcp:127.0.0.1:9000 /logfile=/var/log/mono/fastcgi.log > tripthru.out 2> tripthru.err < /dev/null &");
        }
        private static void CloseSshConnection()
        {
            Console.WriteLine("Closing connection");
            sftpBase.Close();
            ssh.Close();
        }

        private static void UploadDirectory(string dirPath, string uploadPath)
        {
            string[] files = Directory.GetFiles(dirPath, "*.*");
            string[] subDirs = Directory.GetDirectories(dirPath);

            foreach (string file in files)
            {
                sftpBase.Put(file, uploadPath + "/" + Path.GetFileName(file));
            }

            foreach (string subDir in subDirs)
            {
                ssh.RunCommand("mkdir " + uploadPath + "/\"" + Path.GetFileName(subDir) + "\"");
                UploadDirectory(subDir, uploadPath + "/" + Path.GetFileName(subDir));
            }
        }
        private static void sftpBase_OnTransferStart(string src, string dst, int transferredBytes, int totalBytes, string message)
        {
            Console.WriteLine("File Transfer Started...");
            Console.WriteLine("Transferred Bytes :" + transferredBytes);
        }
        private static void sftpBase_OnTransferEnd(string src, string dst, int transferredBytes, int totalBytes, string message)
        {
            Console.WriteLine("File Transfer is in Progress stage...");
            Console.WriteLine("Transferred Bytes :" + transferredBytes);
            Console.WriteLine("Total Bytes :" + totalBytes);
        }

        private class ResponseRequest
        {
            public string Result { get; set; }
            public string ResultCode { get; set; }
        }

        public class Environment
        {
            public string host { get; set; }
            public string user { get; set; }
            public string password { get; set; }
            public int sshPort { get; set; }
            public bool debug { get; set; }
        }
    }
}
