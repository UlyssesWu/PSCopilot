using System.Collections.Immutable;
using System.Management.Automation;
using System.Management.Automation.Subsystem;
using System.Management.Automation.Subsystem.Prediction;
using CopilotDev.NET.Api;
using CopilotDev.NET.Api.Entity;
using CopilotDev.NET.Api.Impl;

namespace PSCopilot
{
    public class CopilotPredictor : ICommandPredictor
    {
        internal static CopilotPredictor? Instance = null;

        private readonly Guid _guid;
        private readonly ICopilotApi _copilot;
        private readonly HttpClient _client = new();

        private HashSet<string> _inlinePredicts = new HashSet<string>();
        private HashSet<string> _historyPredicts = new HashSet<string>();
        private IReadOnlyList<string>? _history = null;

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

        public bool InlineSuggestionUseHistory { get; set; } = true;
        public bool LineSuggestionWithHistory { get; set; } = true;

        public int MaxTokens { get; set; } = 140;
        public int PredictCountEachQuery { get; set; } = 2;

        private string _osPlatform = GetPlatform();
        private string _lastFailedCommand = "";
        private string[] _stopWords = new[] {"\n", ";"};

        /// <summary>
        /// Get the predictive suggestions. It indicates the start of a suggestion rendering session.
        /// </summary>
        /// <param name="client">Represents the client that initiates the call.</param>
        /// <param name="context">The <see cref="PredictionContext"/> object to be used for prediction.</param>
        /// <param name="cancellationToken">The cancellation token to cancel the prediction.</param>
        /// <returns>An instance of <see cref="SuggestionPackage"/>.</returns>
        public SuggestionPackage GetSuggestion(PredictionClient client, PredictionContext context, CancellationToken cancellationToken)
        {
            string input = context.InputAst.Extent.Text;
            if (string.IsNullOrWhiteSpace(input))
            {
                if (_historyPredicts.Count > 0)
                {
                    return new SuggestionPackage(_historyPredicts.Select(s => new PredictiveSuggestion(s)).ToList());
                }
                return default;
            }

            PerformInlinePredict(input).ConfigureAwait(false);

            var hp = _historyPredicts.ToImmutableArray();
            var ip = _inlinePredicts.Reverse().Take(8).ToImmutableArray();
            List<PredictiveSuggestion> p = new List<PredictiveSuggestion>();

            var hits = ip.Where(s => s.StartsWith(input, StringComparison.InvariantCultureIgnoreCase)).ToHashSet();
            p.AddRange(hits.Select(s => new PredictiveSuggestion(s)));
            _inlinePredicts.RemoveWhere(s => !hits.Contains(s));
            if (hp.Length > 0)
            {
                var historyPredict = hp[^1];
                p.Add(new(historyPredict));
            }
            if (p.Count > 0)
            {
                return new SuggestionPackage(p);
            }

            return default;
        }

        private async Task PerformInlinePredict(string current)
        {
            string text = current;
            if (InlineSuggestionUseHistory && _history is { Count: > 0 })
            {
                text = string.Join(Environment.NewLine, _history.Where(s => s != _lastFailedCommand)) + Environment.NewLine + current;
            }
            var completions = await _copilot.GetCompletionsAsync(new CopilotParameters()
            {
//                Prompt = @$"# PowerShell.ps1 # running on {_osPlatform}
//{text}",
Prompt = $@"PowerShell 7.3.0 ({_osPlatform})
PS > {text}",
                MaxTokens = MaxTokens, Stop = _stopWords
            }, false);

            var suggestion = string.Join("", completions.Select(e => e.Choices[0].Text)).TrimEnd();
            if (!string.IsNullOrEmpty(suggestion))
            {
                _inlinePredicts.Add(current + suggestion);
            }
        }

        public void ClearPredicts()
        {
            _inlinePredicts.Clear();
            _historyPredicts.Clear();
            _history = null;
        }

        private static string GetPlatform()
        {
            if (OperatingSystem.IsWindows())
            {
                return "Windows";
            }

            if (OperatingSystem.IsLinux())
            {
                return "Linux";
            }

            if (OperatingSystem.IsMacOS())
            {
                return "MacOS";
            }

            return "Windows";
        }

        #region "interface methods for processing feedback"

        /// <summary>
        /// Gets a value indicating whether the predictor accepts a specific kind of feedback.
        /// </summary>
        /// <param name="client">Represents the client that initiates the call.</param>
        /// <param name="feedback">A specific type of feedback.</param>
        /// <returns>True or false, to indicate whether the specific feedback is accepted.</returns>
        public bool CanAcceptFeedback(PredictionClient client, PredictorFeedbackKind feedback)
        {
            if (feedback is PredictorFeedbackKind.CommandLineAccepted or PredictorFeedbackKind.CommandLineExecuted)
            {
                return true;
            }

            return false;
        }

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
        public async void OnCommandLineAccepted(PredictionClient client, IReadOnlyList<string> history)
        {
            _inlinePredicts.Clear();
            _historyPredicts.Clear();
            _history = history;

            if (!LineSuggestionWithHistory)
            {
                return;
            }

            var result = await _copilot.GetCompletionsAsync(new CopilotParameters()
            {
                Prompt = @$"# {_osPlatform}_PowerShell.ps1
{string.Join(Environment.NewLine, history.Where(s => s != _lastFailedCommand))}
",
                MaxTokens = MaxTokens, N = PredictCountEachQuery
            }, false);
            
            if (result is { Count: > 0 })
            {
                var suggestions = result.GroupBy(e => e.Choices[0].Index)
                    .Select(g => string.Join("", g.Select(e => e.Choices[0].Text)).TrimEnd()).Where(s => !string.IsNullOrEmpty(s))
                    .ToArray();
                
                if (suggestions.Length > 0)
                {
                    foreach (var suggestion in suggestions)
                        _historyPredicts.Add(suggestion);
                }
            }
        }

        /// <summary>
        /// A command line was done execution.
        /// </summary>
        /// <param name="client">Represents the client that initiates the call.</param>
        /// <param name="commandLine">The last accepted command line.</param>
        /// <param name="success">Shows whether the execution was successful.</param>
        public void OnCommandLineExecuted(PredictionClient client, string commandLine, bool success)
        {
            if (!success)
            {
                _lastFailedCommand = commandLine;
                _inlinePredicts.Clear();
            }
        }

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
            CopilotPredictor.Instance = predictor;
        }

        /// <summary>
        /// Gets called when the binary module is unloaded.
        /// </summary>
        public void OnRemove(PSModuleInfo psModuleInfo)
        {
            SubsystemManager.UnregisterSubsystem(SubsystemKind.CommandPredictor, new Guid(Identifier));
            CopilotPredictor.Instance = null;
        }
    }
}