using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
//
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Net;

namespace Company.Function
{
    public static class HttpTriggerCSharp3
    {
        [FunctionName("HttpTriggerCSharp3")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string name = req.Query["name"];
            string myServer = req.Query["server"];
            string myShare = req.Query["share"];
            string domain = req.Query["domain"];
            string user = req.Query["user"];
            string pass = req.Query["pass"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            string fqUser = string.Format( @"{0}\{1}", domain, user);

            string networkPath = string.Format(@"\\{0}\{1}", myServer, myShare);
            
            NetworkCredential credentials = new NetworkCredential(fqUser, pass);

            var netResource = new ConnectToSharedFolder.NetResource
            {
                Scope = ConnectToSharedFolder.ResourceScope.GlobalNetwork,
                ResourceType = ConnectToSharedFolder.ResourceType.Disk,
                DisplayType = ConnectToSharedFolder.ResourceDisplaytype.Share,
                RemoteName = networkPath
            };

            var userName = string.IsNullOrEmpty(credentials.Domain)
                ? credentials.UserName
                : string.Format(@"{0}\{1}", credentials.Domain, credentials.UserName);

            string responseMessage = string.IsNullOrEmpty(name)
                ? "This HTTP triggered function executed successfully. Pass a name,domain,user,pass,server,share in the query string to list the files in the share."
                : $"{name},{domain},{user},{myServer},{myShare}. This HTTP triggered function executed successfully.";

            try  
            {  
                using (new ConnectToSharedFolder(networkPath, credentials))  
                {  
                    var fileList = Directory.GetFiles(networkPath);  
        
                    responseMessage += JsonConvert.SerializeObject(fileList);
                }  
            }  
            catch (Exception ex)  
            {  
                    responseMessage += ex.ToString();
            }  
            

            return new OkObjectResult(responseMessage);
        }
    }

    public class ConnectToSharedFolder: IDisposable {

        readonly string _networkName;  

        public ConnectToSharedFolder(string networkName, NetworkCredential credentials)  
        {  
            _networkName = networkName;  
    
            var netResource = new NetResource  
            {  
                Scope = ResourceScope.GlobalNetwork,  
                ResourceType = ResourceType.Disk,  
                DisplayType = ResourceDisplaytype.Share,  
                RemoteName = networkName  
            };  
    
            var userName = string.IsNullOrEmpty(credentials.Domain)  
                ? credentials.UserName  
                : string.Format(@"{0}\{1}", credentials.Domain, credentials.UserName);  
    
            var result = WNetAddConnection2(  
                netResource,  
                credentials.Password,  
                userName,  
                0);  
    
            if (result != 0)  
            {  
                throw new Win32Exception(result, $"Error connecting to remote share: {networkName} {credentials.Domain} {credentials.UserName}");  
            }  
        } 
        ~ConnectToSharedFolder()  
        {  
            Dispose(false);  
        }  
    
        public void Dispose()  
        {  
            Dispose(true);  
            GC.SuppressFinalize(this);  
        }  
    
        protected virtual void Dispose(bool disposing)  
        {  
            WNetCancelConnection2(_networkName, 0, true);  
        }  

        [DllImport("mpr.dll")]  
        private static extern int WNetAddConnection2(NetResource netResource,  
            string password, string username, int flags);  
    
        [DllImport("mpr.dll")]  
        private static extern int WNetCancelConnection2(string name, int flags,  
            bool force);
            
        [StructLayout(LayoutKind.Sequential)]  
        public class NetResource
        {
            public ResourceScope Scope;
            public ResourceType ResourceType;
            public ResourceDisplaytype DisplayType;
            public int Usage;
            public string LocalName;
            public string RemoteName;
            public string Comment;
            public string Provider;
        }

        public enum ResourceScope : int
        {
            Connected = 1,
            GlobalNetwork,
            Remembered,
            Recent,
            Context
        };

        public enum ResourceType : int
        {
            Any = 0,
            Disk = 1,
            Print = 2,
            Reserved = 8,
        }

        public enum ResourceDisplaytype : int
        {
            Generic = 0x0,
            Domain = 0x01,
            Server = 0x02,
            Share = 0x03,
            File = 0x04,
            Group = 0x05,
            Network = 0x06,
            Root = 0x07,
            Shareadmin = 0x08,
            Directory = 0x09,
            Tree = 0x0a,
            Ndscontainer = 0x0b
        }

    }
}
