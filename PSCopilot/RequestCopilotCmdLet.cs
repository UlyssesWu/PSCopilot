using System.Diagnostics;
using CopilotDev.NET.Api.Entity;
using CopilotDev.NET.Api.Impl;
using System.Management.Automation;

namespace PSCopilot
{
    //https://learn.microsoft.com/en-us/powershell/scripting/developer/module/how-to-write-a-powershell-binary-module?view=powershell-7.3
    //https://learn.microsoft.com/en-us/powershell/scripting/developer/module/how-to-write-a-powershell-module-manifest?view=powershell-7.3

    [Cmdlet(VerbsLifecycle.Request, "Copilot")]
    public class RequestCopilotCmdLet : Cmdlet
    {
        private readonly HttpClient _client = new();

        protected override void ProcessRecord()
        {
            var apiKeyStore = Common.ApiKeyStorePath;
            var copilotConfiguration = new CopilotConfiguration();
            var dataStore = new FileDataStore(apiKeyStore);
            var copilotAuthentication = new CopilotAuthentication(copilotConfiguration, dataStore, _client);
            copilotAuthentication.OnEnterDeviceCode += data =>
            {
                WriteObject($"Open URL {data.Url} to enter the device code: {data.UserCode}");
                if (!string.IsNullOrEmpty(data.Url))
                {
                    Process.Start(data.Url);
                }
            };
            var task = copilotAuthentication.GetAccessTokenAsync();
            task.Wait(30*1000);
            
            if (task.IsCompletedSuccessfully)
            {
                var token = task.Result;
                if (!string.IsNullOrEmpty(token))
                {
                    WriteObject("You're now ready to use copilot.");
                }
                else
                {
                    WriteObject("Cannot get copilot token.");
                }
            }
            else
            {
                WriteObject("Error when getting copilot token.");
            }

            base.ProcessRecord();
        }
    }
}
