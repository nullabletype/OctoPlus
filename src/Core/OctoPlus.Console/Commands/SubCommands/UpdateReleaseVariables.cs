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
    internal class UpdateReleaseVariables : BaseCommand
    {

        protected override bool SupportsInteractiveMode => false;
        public override string CommandName => "updatevariables";
        private IProgressBar progressBar;

        public UpdateReleaseVariables(IOctopusHelper octopusHelper, IProgressBar progressBar) : base(octopusHelper) 
        {
            this.progressBar = progressBar;
        }


        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);

            AddToRegister(UpdateReleaseVariablesOptionNames.Environment, command.Option("-e|--environment", OptionsStrings.EnvironmentName, CommandOptionType.SingleValue).IsRequired());
            AddToRegister(UpdateReleaseVariablesOptionNames.GroupFilter, command.Option("-g|--groupfilter", OptionsStrings.GroupFilter, CommandOptionType.SingleValue));
            AddToRegister(UpdateReleaseVariablesOptionNames.SkipConfirmation, command.Option("-s|--skipconfirmation", OptionsStrings.SkipConfirmation, CommandOptionType.NoValue));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var environmentName = GetStringFromUser(UpdateReleaseVariablesOptionNames.Environment, UiStrings.WhichEnvironmentPrompt);
            var groupRestriction = GetStringFromUser(UpdateReleaseVariablesOptionNames.GroupFilter, UiStrings.RestrictToGroupsPrompt, allowEmpty: true);

            var environment = await FetchEnvironmentFromUserInput(environmentName);

            if (environment == null)
            {
                return -2;
            }

            var groupIds = new List<string>();
            if (!string.IsNullOrEmpty(groupRestriction))
            {
                progressBar.WriteStatusLine(UiStrings.GettingGroupInfo);
                groupIds =
                    (await octoHelper.GetFilteredProjectGroups(groupRestriction))
                    .Select(g => g.Id).ToList();
            }

            var projectStubs = await octoHelper.GetProjectStubs();

            var toUpdate = new List<ProjectRelease>();

            progressBar.CleanCurrentLine();

            foreach (var projectStub in projectStubs)
            {
                progressBar.WriteProgress(projectStubs.IndexOf(projectStub) + 1, projectStubs.Count(),
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
                    toUpdate.Add(new ProjectRelease { Release = release, ProjectStub = projectStub});
                }
            }

            progressBar.CleanCurrentLine();

            System.Console.WriteLine();

            var table = new ConsoleTable(UiStrings.ProjectName, UiStrings.CurrentRelease);
            foreach (var release in toUpdate)
            {
                table.AddRow(release.ProjectStub.ProjectName, release.Release.Version);
            }

            table.Write(Format.Minimal);

            if (Prompt.GetYesNo(UiStrings.AreYouSureUpdateVariables, true))
            {
                foreach (var release in toUpdate)
                {
                    System.Console.WriteLine(UiStrings.Processing, release.ProjectStub.ProjectName);
                    var result = await this.octoHelper.UpdateReleaseVariables(release.Release.Id);
                    if (result)
                    {
                        System.Console.WriteLine(UiStrings.Done, release.ProjectStub.ProjectName);
                    }
                    else
                    {
                        System.Console.WriteLine(UiStrings.Failed, release.ProjectStub.ProjectName);
                    }
                }
            }
            
            return 0;
        }

        struct UpdateReleaseVariablesOptionNames
        {
            public const string Environment = "environment";
            public const string GroupFilter = "groupfilter";
            public const string SkipConfirmation = "skipconfirmation";
        }

        private class ProjectRelease
        {
            public ProjectStub ProjectStub { get; set; }
            public OctoPlusCore.Models.Release Release { get; set; }
        }
    }
}
