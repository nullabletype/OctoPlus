using CSharpFunctionalExtensions;
using NuGet.Versioning;

namespace OctoPlusCore.JobRunners.JobConfigs
{
    public class RenameReleaseConfig
    {
        public string ReleaseName { get; private set; }
        public bool RunningInteractively { get; private set; }
        public Models.Environment Environment { get; private set; }
        public string GroupFilter { get; private set; }

        private RenameReleaseConfig() { }

        public static Result<RenameReleaseConfig> Create(string filter, Models.Environment environment, bool interactive, string releaseName)
        {

            if (environment == null || string.IsNullOrEmpty(environment.Id))
            {
                return Result.Failure<RenameReleaseConfig>("Environment is not set correctly");
            }

            if (!SemanticVersion.TryParse(releaseName, out _))
            {
                return Result.Failure<RenameReleaseConfig>("Release name is not a valid semantic version!");
            }

            return Result.Ok(new RenameReleaseConfig
            {
                GroupFilter = filter,
                ReleaseName = releaseName,
                Environment = environment,
                RunningInteractively = interactive
            });
        }
    }
}
