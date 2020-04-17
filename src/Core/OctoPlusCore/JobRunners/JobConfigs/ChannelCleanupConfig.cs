using CSharpFunctionalExtensions;

namespace OctoPlusCore.JobRunners.JobConfigs
{
    public class ChannelCleanupConfig
    {
        public string GroupFilter { get; set; }
        public bool TestMode { get; set; }

        private ChannelCleanupConfig() { }

        public static Result<ChannelCleanupConfig> Create(string filter, bool testMode)
        {

            return Result.Ok(new ChannelCleanupConfig
            {
                GroupFilter = filter,
                TestMode = testMode
            });
        }
    }
}
