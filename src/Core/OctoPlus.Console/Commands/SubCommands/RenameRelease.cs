#region copyright
/*
    OctoPlus Deployment Coordinator. Provides extra tooling to help 
    deploy software through Octopus Deploy.

    Copyright (C) 2018  Steven Davies

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/
#endregion


using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using NuGet.Versioning;
using OctoPlus.Console.ConsoleTools;
using OctoPlusCore.Interfaces;
using OctoPlusCore.Language;
using OctoPlusCore.Models;
using OctoPlusCore.Octopus.Interfaces;

namespace OctoPlus.Console.Commands.SubCommands
{
    internal class RenameRelease : BaseCommand
    {

        protected override bool SupportsInteractiveMode => false;
        public override string CommandName => "rename";
        private IProgressBar progressBar;

        public RenameRelease(IOctopusHelper octopusHelper, IProgressBar progressBar, ILanguageProvider languageProvider) : base(octopusHelper, languageProvider)
        {
            this.progressBar = progressBar;
        }


        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);

            AddToRegister(RenameReleaseOptionNames.Environment, command.Option("-e|--environment", languageProvider.GetString(LanguageSection.OptionsStrings, "EnvironmentName"), CommandOptionType.SingleValue).IsRequired());
            AddToRegister(RenameReleaseOptionNames.ReleaseName, command.Option("-r|--releasename", languageProvider.GetString(LanguageSection.OptionsStrings, "ReleaseVersion"), CommandOptionType.SingleValue).IsRequired());
            AddToRegister(RenameReleaseOptionNames.GroupFilter, command.Option("-g|--groupfilter", languageProvider.GetString(LanguageSection.OptionsStrings, "GroupFilter"), CommandOptionType.SingleValue));
            AddToRegister(RenameReleaseOptionNames.SkipConfirmation, command.Option("-s|--skipconfirmation", languageProvider.GetString(LanguageSection.OptionsStrings, "SkipConfirmation"), CommandOptionType.NoValue));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var environmentName = GetStringFromUser(RenameReleaseOptionNames.Environment, languageProvider.GetString(LanguageSection.UiStrings, "WhichEnvironmentPrompt"));
            var releaseName = GetStringFromUser(RenameReleaseOptionNames.ReleaseName, languageProvider.GetString(LanguageSection.UiStrings, "ReleaseNamePrompt"));
            var groupRestriction = GetStringFromUser(RenameReleaseOptionNames.GroupFilter, languageProvider.GetString(LanguageSection.UiStrings, "RestrictToGroupsPrompt"), allowEmpty: true);

            var environment = await FetchEnvironmentFromUserInput(environmentName);

            if (environment == null)
            {
                return -2;
            }

            if (!SemanticVersion.TryParse(releaseName, out _))
            {
                System.Console.WriteLine(languageProvider.GetString(LanguageSection.UiStrings, "InvalidReleaseVersion"));
                return -2;
            }

            var groupIds = new List<string>();
            if (!string.IsNullOrEmpty(groupRestriction))
            {
                progressBar.WriteStatusLine(languageProvider.GetString(LanguageSection.UiStrings, "GettingGroupInfo"));
                groupIds =
                    (await octoHelper.GetFilteredProjectGroups(groupRestriction))
                    .Select(g => g.Id).ToList();
            }

            var projectStubs = await octoHelper.GetProjectStubs();

            var toRename = new List<ProjectRelease>();

            progressBar.CleanCurrentLine();

            foreach (var projectStub in projectStubs)
            {
                progressBar.WriteProgress(projectStubs.IndexOf(projectStub) + 1, projectStubs.Count(),
                    String.Format(languageProvider.GetString(LanguageSection.UiStrings, "LoadingInfoFor"), projectStub.ProjectName));
                if (!string.IsNullOrEmpty(groupRestriction))
                {
                    if (!groupIds.Contains(projectStub.ProjectGroupId))
                    {
                        continue;
                    }
                }

                var release = (await this.octoHelper.GetReleasedVersion(projectStub.ProjectId, environment.Id)).Release;
                if (release != null && !release.Version.Equals("none", StringComparison.InvariantCultureIgnoreCase))
                {
                    toRename.Add(new ProjectRelease { Release = release, ProjectStub = projectStub});
                }
            }

            progressBar.CleanCurrentLine();

            System.Console.WriteLine();

            var table = new ConsoleTable(languageProvider.GetString(LanguageSection.UiStrings, "ProjectName"), languageProvider.GetString(LanguageSection.UiStrings, "CurrentRelease"));
            foreach (var release in toRename)
            {
                table.AddRow(release.ProjectStub.ProjectName, release.Release.Version);
            }

            table.Write(Format.Minimal);

            if (Prompt.GetYesNo(String.Format(languageProvider.GetString(LanguageSection.UiStrings, "GoingToRename"), releaseName), true))
            {
                foreach (var release in toRename)
                {
                    System.Console.WriteLine(languageProvider.GetString(LanguageSection.UiStrings, "Processing"), release.ProjectStub.ProjectName);
                    var result = await this.octoHelper.RenameRelease(release.Release.Id, releaseName);
                    if (result.success)
                    {
                        System.Console.WriteLine(languageProvider.GetString(LanguageSection.UiStrings, "Done"), release.ProjectStub.ProjectName);
                    }
                    else
                    {
                        System.Console.WriteLine(languageProvider.GetString(LanguageSection.UiStrings, "Failed"), release.ProjectStub.ProjectName, result.error);
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
