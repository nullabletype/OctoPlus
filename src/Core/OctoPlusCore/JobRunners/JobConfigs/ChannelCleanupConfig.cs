using CSharpFunctionalExtensions;

namespace OctoPlusCore.JobRunners.JobConfigs
{
    public class ChannelCleanupConfig
    {
        public string GroupFilter { get; private set; }
        public bool TestMode { get; private set; }

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
