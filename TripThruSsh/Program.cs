using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using ServiceStack.Text;
using Tamir.SharpSsh;
using System.Threading;

namespace TripThruSsh
{
    class Program
    {

        private static Boolean fullDeploy = true; //if true will upload and replace everything, else just update partner configuations
        private static string localPath = "C:\\Users\\DanielErnesto\\Documents\\Projects\\";
        private static string remoteFilePath = "/home/tripservice/servicestack/";
        private static string host = "54.201.134.194";
        private static string user = "tripservice";
        private static string password = "Tr1PServ1CeSt@Ck";

        private static SshTransferProtocolBase sftpBase;
        private static SshExec ssh;
        

        private static void Main(string[] args)
        {
            ssh = new SshExec(host, user, password);
            ssh.Connect(22);
            Console.WriteLine("Connected");

            sftpBase = new Tamir.SharpSsh.Sftp(host, user, password);
            sftpBase.OnTransferStart += new FileTransferEvent(sftpBase_OnTransferStart);
            sftpBase.OnTransferEnd += new FileTransferEvent(sftpBase_OnTransferEnd);
            Console.WriteLine("Trying to Open Connection...");
            sftpBase.Connect();
            Console.WriteLine("Connected Successfully !");
            
            if (fullDeploy)
            {
                //Remove any old files and upload projects
                Console.WriteLine("Uploading projects");
                ssh.RunCommand("cd " + remoteFilePath);
                ssh.RunCommand("rm -rf " + remoteFilePath + "ServiceStack.* Booking*");
                ssh.RunCommand("mkdir -p " + remoteFilePath + "ServiceStack.TripThruGateway/Web");
                ssh.RunCommand("mkdir -p " + remoteFilePath + "ServiceStack.TripThruPartnerGateway/Web");
                ssh.RunCommand("mkdir -p " + remoteFilePath + "BookingWebsite");
                var omittedDirectories = new List<string> { "packages" };
                UploadDirectory(localPath + "BookingWebsite", remoteFilePath + "BookingWebsite");
                UploadDirectory(localPath + "ServiceStack.TripThruGateway/Web", remoteFilePath + "ServiceStack.TripThruGateway/Web");
                UploadDirectory(localPath + "ServiceStack.TripThruPartnerGateway/Web", remoteFilePath + "ServiceStack.TripThruPartnerGateway/Web");
                ssh.RunCommand("mv " + remoteFilePath + "ServiceStack.TripThruGateway/Web/bin/mono/*  " + remoteFilePath +
                               "ServiceStack.TripThruGateway/Web/bin");
                ssh.RunCommand("mv " + remoteFilePath + "ServiceStack.TripThruPartnerGateway/Web/bin/mono/*  " +
                               remoteFilePath + "ServiceStack.TripThruPartnerGateway/Web/bin");
            }

            var webappNames = new List<string>();
            string[] partnerConfigurations = Directory.GetFiles("PartnerConfigurations/", "*.txt");
            List<string> partnerscallbackUrlMono = new List<string>();
            foreach (var partnerConfiguration in partnerConfigurations)
            {
                var configuration = JsonSerializer.DeserializeFromString<PartnerConfiguration>(File.ReadAllText(partnerConfiguration));
                if (configuration.Enabled)
                {
                    partnerscallbackUrlMono.Add(configuration.Partner.CallbackUrlMono);
                    var name = configuration.Partner.Name.Replace(" ", "");
                    Console.WriteLine("Configuring " + name);
                    webappNames.Add(name);
                    ssh.RunCommand("cp -a " + remoteFilePath + "ServiceStack.TripThruPartnerGateway/  " + remoteFilePath +
                                   "ServiceStack." + name + "/");
                    ssh.RunCommand("rm " + remoteFilePath + "ServiceStack." + name + "/Web/PartnerConfiguration.txt");
                    sftpBase.Put(partnerConfiguration,
                        remoteFilePath + "ServiceStack." + name + "/Web/PartnerConfiguration.txt");

                    var bookingwebConfig = new System.IO.StreamWriter("config.txt");
                    bookingwebConfig.WriteLine("HomeUrl=" + configuration.Partner.WebUrl);
                    bookingwebConfig.WriteLine("RelativeHomeUrl=" + configuration.Partner.WebUrlRelative);
                    bookingwebConfig.WriteLine("TripThruUrl=" + configuration.TripThruUrlMono);
                    bookingwebConfig.WriteLine("TripThruAccessToken=" + "jaosid1201231"); //fixed tripthru access token
                    bookingwebConfig.WriteLine("PartnerUrl=" + configuration.Partner.CallbackUrlMono);
                    bookingwebConfig.WriteLine("PartnerAccessToken=" + configuration.Partner.AccessToken);
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

            if (fullDeploy)
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

                foreach (var webapp in webappNames)
                {
                    webappConfig.WriteLine(@"<web-application>
                                    <name>TripThru.{0}</name>
                                    <vhost>*</vhost>
                                    <vport>80</vport>
                                    <vpath>/TripThru.{0}</vpath>
                                    <path>/var/www/ServiceStack.{0}/Web</path>
                                 </web-application>", webapp
                    );
                    webappConfig.Flush();
                }

                webappConfig.WriteLine("</apps>");
                webappConfig.Flush();
                webappConfig.Close();

                Console.WriteLine("Updating mono webapp config");
                ssh.RunCommand("rm /etc/rc.d/init.d/mono-fastcgi/tripthru.webapp");
                sftpBase.Put("tripthru.webapp", "/etc/rc.d/init.d/mono-fastcgi/tripthru.webapp");
            }
            
            Console.WriteLine("Stopping mono");
            ssh.RunCommand("kill -9 $(netstat -tpan |grep \"LISTEN\"|grep :9000|awk -F' ' '{print $7}'|awk -F'/' '{print $1}')");

            Console.WriteLine("Updating web folder");
            ssh.RunCommand("rm -rf /var/www/ServiceStack.*");
            ssh.RunCommand("cp -a " + remoteFilePath + "/ServiceStack.* /var/www/");

            Thread startMono = new Thread(
                    delegate()
                    {
                        Console.WriteLine("Starting mono");
                        ssh.RunCommand("fastcgi-mono-server4 --appconfigdir /etc/rc.d/init.d/mono-fastcgi /socket=tcp:127.0.0.1:9000 /logfile=/var/log/mono/fastcgi.log &");
                    });
            startMono.Start();

            Console.WriteLine("Sleep 8 seconds, waiting for mono to initialize.");
            Thread.Sleep(8000);

            var client = new System.Net.WebClient();
            foreach (string callbackUrlMono in partnerscallbackUrlMono)
            {
                Console.WriteLine("Sending request to: \n" + @callbackUrlMono.ToString() + "log");
                while (true)
                {
                    try
                    {
                        var response = client.DownloadString(@callbackUrlMono.ToString() + "log");
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


            Console.WriteLine("Done!");
            startMono.Abort();
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
    }

    public class PartnerConfiguration
    {
        public string TripThruUrlMono { get; set; }
        public ConfigPartner Partner { get; set; }
        public Boolean Enabled { get; set; }

        public class ConfigPartner
        {
            public string Name { get; set; }
            public string WebUrl { get; set; }
            public string WebUrlRelative { get; set; }
            public string CallbackUrlMono { get; set; }
            public string AccessToken { get; set; }
            public string ClientId { get; set; }
        }
    }

    public class ResponseRequest
    {
        public string Result { get; set; }
        public string ResultCode { get; set; }
    }
}
