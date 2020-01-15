using CSharpFunctionalExtensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OctoPlusCore.JobRunners.JobConfigs
{
    public class DeployConfig
    {
        public Models.Channel Channel { get; private set; }
        public Models.Channel DefaultFallbackChannel { get; private set; }
        public Models.Environment Environment { get; private set; }
        public string GroupFilter { get; private set; }
        public bool RunningInteractively { get; private set; }
        public string SaveProfile { get; private set; }
        public bool FallbackToDefaultChannel => DefaultFallbackChannel != null;

        private DeployConfig() { }

        public static Result<DeployConfig> Create (Models.Environment env, Models.Channel channel, Models.Channel defaultFallbackChannel, string filter, string saveProfile, bool runningInteractively)
        {
            if (env == null || string.IsNullOrEmpty(env.Id))
            {
                return Result.Failure<DeployConfig>("destiniation environment is not set correctly");
            }

            if (channel == null || string.IsNullOrEmpty(channel.Id))
            {
                return Result.Failure<DeployConfig>("channel is not set correctly");
            }

            if (defaultFallbackChannel != null && string.IsNullOrEmpty(defaultFallbackChannel.Id))
            {
                return Result.Failure<DeployConfig>("default channel is not set correctly");
            }

            if (saveProfile != null)
            {
                if (!Uri.IsWellFormedUriString(saveProfile, UriKind.RelativeOrAbsolute))
                {
                    return Result.Failure<DeployConfig>("path to save profile is not set correctly");
                }
            }

            return Result.Ok(new DeployConfig
            {
                Environment = env,
                Channel = channel,
                DefaultFallbackChannel = defaultFallbackChannel,
                SaveProfile = saveProfile,
                GroupFilter = filter,
                RunningInteractively = runningInteractively
            });
        }
    }
}
