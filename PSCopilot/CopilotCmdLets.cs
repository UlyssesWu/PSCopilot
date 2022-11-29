using System.Diagnostics;
using CopilotDev.NET.Api.Entity;
using CopilotDev.NET.Api.Impl;
using System.Management.Automation;
using Debugger = System.Diagnostics.Debugger;

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
                Console.WriteLine($"Open URL {data.Url} to enter the device code: {data.UserCode}");
                if (!string.IsNullOrEmpty(data.Url))
                {
                    Process.Start(new ProcessStartInfo(data.Url) { UseShellExecute = true });
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

    [Cmdlet(VerbsCommon.Clear, "Copilot")]
    public class ClearCopilotCmdLet : Cmdlet
    {
        protected override void ProcessRecord()
        {
            if (CopilotPredictor.Instance == null)
            {
                WriteObject("Copilot is not working.");
                return;
            }

            CopilotPredictor.Instance.ClearPredicts();
        }
    }

    [Cmdlet(VerbsCommon.Set, "Copilot")]
    public class SetCopilotCmdLet : Cmdlet
    {
        [Alias("h")]
        [Parameter]
        public bool UseHistory { get; set; } = true;
        [Alias("hi")]
        [Parameter]
        public bool UseHistoryInline { get; set; } = true;
        [Alias("t")]
        [Parameter] public int MaxTokens { get; set; } = -1;

#if DEBUG
        [Alias("dbg")]
        [Parameter] public SwitchParameter DebuggerAttach { get; set; }
#endif

        protected override void ProcessRecord()
        {
#if DEBUG
            if (DebuggerAttach)
            {
                if (!Debugger.IsAttached && DebuggerAttach)
                {
                    DebuggerAttach = false;
                    Debugger.Launch();
                }
            }
#endif

            if (CopilotPredictor.Instance == null)
            {
                WriteObject("Copilot is not working.");
                return;
            }

            if (MaxTokens is < 0 or > 10240)
            {
                MaxTokens = 140;
            }

            CopilotPredictor.Instance.MaxTokens = MaxTokens;
            CopilotPredictor.Instance.InlineSuggestionUseHistory = UseHistoryInline;
            CopilotPredictor.Instance.LineSuggestionWithHistory = UseHistory;


        }
    }
}
