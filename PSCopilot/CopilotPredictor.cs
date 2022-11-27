using System.Diagnostics;
using System.Management.Automation;
using System.Management.Automation.Subsystem;
using System.Management.Automation.Subsystem.Prediction;
using CopilotDev.NET.Api;
using CopilotDev.NET.Api.Entity;
using CopilotDev.NET.Api.Impl;
using Debugger = System.Diagnostics.Debugger;

namespace PSCopilot
{
    public class CopilotPredictor : ICommandPredictor
    {
        private readonly Guid _guid;
        private ICopilotApi _copilot;
        private readonly HttpClient _client = new();

        internal CopilotPredictor(string guid)
        {
            _guid = new Guid(guid);
            var copilotConfiguration = new CopilotConfiguration();
            var dataStore = new FileDataStore(Common.ApiKeyStorePath);
            var copilotAuthentication = new CopilotAuthentication(copilotConfiguration, dataStore, _client);
            _copilot = new CopilotApi(copilotConfiguration, copilotAuthentication, _client);
        }

        /// <summary>
        /// Gets the unique identifier for a subsystem implementation.
        /// </summary>
        public Guid Id => _guid;

        /// <summary>
        /// Gets the name of a subsystem implementation.
        /// </summary>
        public string Name => "Copilot";

        /// <summary>
        /// Gets the description of a subsystem implementation.
        /// </summary>
        public string Description => "Github Copilot for PowerShell";

        /// <summary>
        /// Get the predictive suggestions. It indicates the start of a suggestion rendering session.
        /// </summary>
        /// <param name="client">Represents the client that initiates the call.</param>
        /// <param name="context">The <see cref="PredictionContext"/> object to be used for prediction.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the prediction.</param>
        /// <returns>An instance of <see cref="SuggestionPackage"/>.</returns>
        public SuggestionPackage GetSuggestion(PredictionClient client, PredictionContext context, CancellationToken cancellationToken)
        {
            //Must be fast.
            if (!Debugger.IsAttached)
            {
                Debugger.Launch();
            }
            string input = context.InputAst.Extent.Text;
            if (string.IsNullOrWhiteSpace(input))
            {
                return default;
            }

            var cancelToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cancelToken.CancelAfter(10000);
            
            var task = _copilot.GetCompletionsAsync(new CopilotParameters()
            {
                Prompt = @$"# PowerShell.ps1
{input}", MaxTokens = 140
            }, false);
            task.Wait();
            if (task.IsCompletedSuccessfully)
            {
                var result = task.Result;
                var suggestion = string.Join("", result.Select(e => e.Choices[0].Text)).TrimEnd();
                if (suggestion.Length > 0)
                {
                    suggestion = input + suggestion;
                }
                return new SuggestionPackage(new List<PredictiveSuggestion>{
                    new PredictiveSuggestion(suggestion)
                });
            }
            else
            {
                return new SuggestionPackage(new List<PredictiveSuggestion>{
                    new PredictiveSuggestion("task not completed", "copilot")
                });
            }

            return default;
        }

        #region "interface methods for processing feedback"

        /// <summary>
        /// Gets a value indicating whether the predictor accepts a specific kind of feedback.
        /// </summary>
        /// <param name="client">Represents the client that initiates the call.</param>
        /// <param name="feedback">A specific type of feedback.</param>
        /// <returns>True or false, to indicate whether the specific feedback is accepted.</returns>
        public bool CanAcceptFeedback(PredictionClient client, PredictorFeedbackKind feedback) => false;

        /// <summary>
        /// One or more suggestions provided by the predictor were displayed to the user.
        /// </summary>
        /// <param name="client">Represents the client that initiates the call.</param>
        /// <param name="session">The mini-session where the displayed suggestions came from.</param>
        /// <param name="countOrIndex">
        /// When the value is greater than 0, it's the number of displayed suggestions from the list
        /// returned in <paramref name="session"/>, starting from the index 0. When the value is
        /// less than or equal to 0, it means a single suggestion from the list got displayed, and
        /// the index is the absolute value.
        /// </param>
        public void OnSuggestionDisplayed(PredictionClient client, uint session, int countOrIndex) { }

        /// <summary>
        /// The suggestion provided by the predictor was accepted.
        /// </summary>
        /// <param name="client">Represents the client that initiates the call.</param>
        /// <param name="session">Represents the mini-session where the accepted suggestion came from.</param>
        /// <param name="acceptedSuggestion">The accepted suggestion text.</param>
        public void OnSuggestionAccepted(PredictionClient client, uint session, string acceptedSuggestion) { }

        /// <summary>
        /// A command line was accepted to execute.
        /// The predictor can start processing early as needed with the latest history.
        /// </summary>
        /// <param name="client">Represents the client that initiates the call.</param>
        /// <param name="history">History command lines provided as references for prediction.</param>
        public void OnCommandLineAccepted(PredictionClient client, IReadOnlyList<string> history) { }

        /// <summary>
        /// A command line was done execution.
        /// </summary>
        /// <param name="client">Represents the client that initiates the call.</param>
        /// <param name="commandLine">The last accepted command line.</param>
        /// <param name="success">Shows whether the execution was successful.</param>
        public void OnCommandLineExecuted(PredictionClient client, string commandLine, bool success) { }

        #endregion;
    }

    /// <summary>
    /// Register the predictor on module loading and unregister it on module un-loading.
    /// </summary>
    public class Init : IModuleAssemblyInitializer, IModuleAssemblyCleanup
    {
        private const string Identifier = "83915580-7775-4f52-8420-2a0e4461aa5b";

        /// <summary>
        /// Gets called when assembly is loaded.
        /// </summary>
        public void OnImport()
        {
            var predictor = new CopilotPredictor(Identifier);
            SubsystemManager.RegisterSubsystem(SubsystemKind.CommandPredictor, predictor);
        }

        /// <summary>
        /// Gets called when the binary module is unloaded.
        /// </summary>
        public void OnRemove(PSModuleInfo psModuleInfo)
        {
            SubsystemManager.UnregisterSubsystem(SubsystemKind.CommandPredictor, new Guid(Identifier));
        }
    }
}