namespace PSCopilot
{
    internal static class Common
    {
        public static readonly string ApiKeyStorePath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "copilot.json");
    }
}
