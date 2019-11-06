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
using OctoPlus.Console.ConsoleTools;
using OctoPlus.Console.Interfaces;
using OctoPlusCore.Configuration.Interfaces;
using OctoPlusCore.Language;
using OctoPlusCore.Models;
using OctoPlusCore.Octopus.Interfaces;

namespace OctoPlus.Console.Commands.SubCommands
{
    class ShowEnvironment : BaseCommand
    {
        protected override bool SupportsInteractiveMode => false;
        public override string CommandName => "delete";
        private IProgressBar progressBar;
        IConfiguration configuration;

        public ShowEnvironment(IOctopusHelper octopusHelper, ILanguageProvider languageProvider, IProgressBar progressBar, IConfiguration configuration) : base(octopusHelper, languageProvider) 
        {
            this.progressBar = progressBar;
            this.configuration = configuration;
        }


        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);

            AddToRegister(ShowEnvironmentOptionNames.Id, command.Option("-e|--e", languageProvider.GetString(LanguageSection.OptionsStrings, "EnvironmentName"), CommandOptionType.SingleValue).IsRequired());
            AddToRegister(ShowEnvironmentOptionNames.GroupFilter, command.Option("-g|--groupfilter", languageProvider.GetString(LanguageSection.OptionsStrings, "GroupFilter"), CommandOptionType.SingleValue));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var id = GetStringFromUser(ShowEnvironmentOptionNames.Id, string.Empty, false);
            var groupFilter = GetStringFromUser(ShowEnvironmentOptionNames.GroupFilter, string.Empty, true);

            var found = await this.octoHelper.GetEnvironment(id);
            if (found != null) 
            {
                progressBar.WriteStatusLine(languageProvider.GetString(LanguageSection.UiStrings, "FetchingProjectList"));
                var projectStubs = await octoHelper.GetProjectStubs();

                var groupIds = new List<string>();
                if (!string.IsNullOrEmpty(groupFilter))
                {
                    progressBar.WriteStatusLine(languageProvider.GetString(LanguageSection.UiStrings, "GettingGroupInfo"));
                    groupIds =
                        (await octoHelper.GetFilteredProjectGroups(groupFilter))
                        .Select(g => g.Id).ToList();
                }

                var releases = new List<(OctoPlusCore.Models.Release Release, Deployment Deployment)>();

                var table = new ConsoleTable("Project", "Release Name", "Packages", "Deployed On", "Deployed By");

                foreach (var projectStub in projectStubs)
                {
                    if (!string.IsNullOrEmpty(groupFilter))
                    {
                        if (!groupIds.Contains(projectStub.ProjectGroupId))
                        {
                            continue;
                        }
                    }

                    var release = await octoHelper.GetReleasedVersion(projectStub.ProjectId, found.Id);

                    table.AddRow(projectStub.ProjectName, release.Release.Version);
                }



            }
            System.Console.WriteLine(String.Format(languageProvider.GetString(LanguageSection.UiStrings, "EnvironmentNotFound"), id));
            return -1;
        }

        struct ShowEnvironmentOptionNames 
        {
            public const string Id = "id";
            public const string GroupFilter = "groupfilter";
        }
    }
}
