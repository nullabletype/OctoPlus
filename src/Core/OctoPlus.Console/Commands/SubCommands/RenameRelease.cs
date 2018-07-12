using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NuGet.Versioning;
using OctoPlus.Console.ConsoleTools;
using OctoPlus.Console.Interfaces;
using OctoPlus.Console.Resources;
using OctoPlusCore.Models;
using OctoPlusCore.Octopus.Interfaces;

namespace OctoPlus.Console.Commands.SubCommands
{
    internal class RenameRelease : BaseCommand
    {

        protected override bool SupportsInteractiveMode => false;
        public override string CommandName => "rename";

        public RenameRelease(IOctopusHelper octopusHelper) : base(octopusHelper) { }


        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);

            AddToRegister(RenameReleaseOptionNames.Environment, command.Option("-e|--environment", OptionsStrings.EnvironmentName, CommandOptionType.SingleValue).IsRequired());
            AddToRegister(RenameReleaseOptionNames.ReleaseName, command.Option("-r|--releasename", OptionsStrings.ReleaseVersion, CommandOptionType.SingleValue).IsRequired());
            AddToRegister(RenameReleaseOptionNames.GroupFilter, command.Option("-g|--groupfilter", OptionsStrings.GroupFilter, CommandOptionType.SingleValue));
            AddToRegister(RenameReleaseOptionNames.SkipConfirmation, command.Option("-s|--skipconfirmation", OptionsStrings.SkipConfirmation, CommandOptionType.NoValue));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var environmentName = GetStringFromUser(RenameReleaseOptionNames.Environment, UiStrings.WhichEnvironmentPrompt);
            var releaseName = GetStringFromUser(RenameReleaseOptionNames.ReleaseName, UiStrings.ReleaseNamePrompt);
            var groupRestriction = GetStringFromUser(RenameReleaseOptionNames.GroupFilter, UiStrings.RestrictToGroupsPrompt, allowEmpty: true);

            var environment = await FetchEnvironmentFromUserInput(environmentName);

            if (environment == null)
            {
                return -2;
            }

            if (!SemanticVersion.TryParse(releaseName, out _))
            {
                System.Console.WriteLine(UiStrings.InvalidReleaseVersion);
                return -2;
            }

            var groupIds = new List<string>();
            if (!string.IsNullOrEmpty(groupRestriction))
            {
                WriteStatusLine(UiStrings.GettingGroupInfo);
                groupIds =
                    (await octoHelper.GetFilteredProjectGroups(groupRestriction))
                    .Select(g => g.Id).ToList();
            }

            var projectStubs = await octoHelper.GetProjectStubs();

            var toRename = new List<ProjectRelease>();

            CleanCurrentLine();

            foreach (var projectStub in projectStubs)
            {
                WriteProgress(projectStubs.IndexOf(projectStub) + 1, projectStubs.Count(),
                    String.Format(UiStrings.LoadingInfoFor, projectStub.ProjectName));
                if (!string.IsNullOrEmpty(groupRestriction))
                {
                    if (!groupIds.Contains(projectStub.ProjectGroupId))
                    {
                        continue;
                    }
                }

                var release = await this.octoHelper.GetReleasedVersion(projectStub.ProjectId, environment.Id);
                if (release != null && !release.Version.Equals("none", StringComparison.InvariantCultureIgnoreCase))
                {
                    toRename.Add(new ProjectRelease { Release = release, ProjectStub = projectStub});
                }
            }

            CleanCurrentLine();

            System.Console.WriteLine();

            var table = new ConsoleTable(UiStrings.ProjectName, UiStrings.CurrentRelease);
            foreach (var release in toRename)
            {
                table.AddRow(release.ProjectStub.ProjectName, release.Release.Version);
            }

            table.Write(Format.Minimal);

            if (Prompt.GetYesNo(String.Format(UiStrings.GoingToRename, releaseName), true))
            {
                foreach (var release in toRename)
                {
                    System.Console.WriteLine(UiStrings.Processing, release.ProjectStub.ProjectName);
                    var result = await this.octoHelper.RenameRelease(release.Release.Id, releaseName);
                    if (result.success)
                    {
                        System.Console.WriteLine(UiStrings.Done, release.ProjectStub.ProjectName);
                    }
                    else
                    {
                        System.Console.WriteLine(UiStrings.Failed, release.ProjectStub.ProjectName, result.error);
                    }
                }
            }
            
            return 0;
        }

        struct RenameReleaseOptionNames
        {
            public const string Environment = "environment";
            public const string GroupFilter = "groupfilter";
            public const string ReleaseName = "releasename";
            public const string SkipConfirmation = "skipconfirmation";
        }

        private class ProjectRelease
        {
            public ProjectStub ProjectStub { get; set; }
            public OctoPlusCore.Models.Release Release { get; set; }
        }
    }
}
