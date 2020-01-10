using CSharpFunctionalExtensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace OctoPlusCore.JobRunners.JobConfigs
{
    public class PromotionConfig
    {
        public Models.Environment DestinationEnvironment { get; set; }
        public Models.Environment SourceEnvironment { get; set; }
        public string GroupFilter { get; set; }
        public bool RunningInteractively { get; set; }

        private PromotionConfig() { }

        public static Result<PromotionConfig> Create (Models.Environment destEnv, Models.Environment srcEnv, string filter, bool runningInteractively)
        {
            if (destEnv == null || string.IsNullOrEmpty(destEnv.Id))
            {
                return Result.Failure<PromotionConfig>("destiniation environment is not set correctly");
            }

            if (srcEnv == null || string.IsNullOrEmpty(srcEnv.Id))
            {
                return Result.Failure<PromotionConfig>("source environment is not set correctly");
            }

            return Result.Ok(new PromotionConfig
            {
                DestinationEnvironment = destEnv,
                SourceEnvironment = srcEnv,
                GroupFilter = filter,
                RunningInteractively = runningInteractively
            });
        }
    }
}
