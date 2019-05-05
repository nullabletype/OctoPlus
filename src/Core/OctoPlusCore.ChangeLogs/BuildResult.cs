namespace OctoPlusCore.ChangeLogs
{
    public class BuildResult
    {
        public string Id { get; set; }
        public string ConfigurationName { get; set; }
        public string Message { get; set; }
        public string BuildNumber { get; set; }
        public string WebUrl { get; set; }
        public BuildStatus Status { get; set; }
    }
}
