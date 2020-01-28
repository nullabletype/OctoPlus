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


using McMaster.Extensions.CommandLineUtils;
using OctoPlus.Console.ConsoleTools;
using OctoPlusCore.Interfaces;
using OctoPlusCore.JobRunners;
using OctoPlusCore.JobRunners.JobConfigs;
using OctoPlusCore.Language;
using OctoPlusCore.Models;
using OctoPlusCore.Octopus.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OctoPlus.Console.Commands
{
    internal class Promote : BaseCommand
    {
        private IProgressBar progressBar;
        private PromotionRunner runner;

        protected override bool SupportsInteractiveMode => true;
        public override string CommandName => "promote";

        public Promote(IOctopusHelper octoHelper, IProgressBar progressBar, ILanguageProvider languageProvider, PromotionRunner runner) : base(octoHelper, languageProvider)
        {
            this.progressBar = progressBar;
            this.runner = runner;
        }
        
        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);
            command.Description = languageProvider.GetString(LanguageSection.OptionsStrings, "PromoteProjects");

            AddToRegister(PromoteOptionNames.Environment, command.Option("-e|--environment", languageProvider.GetString(LanguageSection.OptionsStrings, "EnvironmentName"), CommandOptionType.SingleValue));
            AddToRegister(PromoteOptionNames.SourceEnvironment, command.Option("-s|--sourcenvironment", languageProvider.GetString(LanguageSection.OptionsStrings, "SourceEnvironment"), CommandOptionType.SingleValue));
            AddToRegister(PromoteOptionNames.GroupFilter, command.Option("-g|--groupfilter", languageProvider.GetString(LanguageSection.OptionsStrings, "GroupFilter"), CommandOptionType.SingleValue));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            progressBar.WriteStatusLine(languageProvider.GetString(LanguageSection.UiStrings, "FetchingProjectList"));
            var projectStubs = await octoHelper.GetProjectStubs();

            var environmentName = GetStringFromUser(PromoteOptionNames.SourceEnvironment, languageProvider.GetString(LanguageSection.UiStrings, "SourceEnvironment"));
            var targetEnvironmentName = GetStringFromUser(PromoteOptionNames.Environment, languageProvider.GetString(LanguageSection.UiStrings, "WhichEnvironmentPrompt"));
            var groupRestriction = GetStringFromUser(PromoteOptionNames.GroupFilter, languageProvider.GetString(LanguageSection.UiStrings, "RestrictToGroupsPrompt"));

            progressBar.WriteStatusLine(languageProvider.GetString(LanguageSection.UiStrings, "CheckingOptions"));
            var environment = await FetchEnvironmentFromUserInput(environmentName);
            var targetEnvironment = await FetchEnvironmentFromUserInput(targetEnvironmentName);

            if (environment == null || targetEnvironment == null)
            {
                return -2;
            }

            var configResult = PromotionConfig.Create(targetEnvironment, environment, groupRestriction, this.InInteractiveMode);

            if (configResult.IsFailure)
            {
                System.Console.WriteLine(configResult.Error);
                return -1;
            } 
            else 
            {
                return await runner.Run(configResult.Value, this.progressBar, InteractivePrompt, PromptForStringWithoutQuitting);
            }
        }


        private IEnumerable<int> InteractivePrompt(PromotionConfig config, (List<Project> currentProjects, List<Project> targetProjects) projects)
        {
            InteractiveRunner runner = PopulateRunner(String.Format(languageProvider.GetString(LanguageSection.UiStrings, "PromotingTo"), config.SourceEnvironment.Name, config.DestinationEnvironment.Name), projects.currentProjects, projects.targetProjects);
            return runner.GetSelectedIndexes();
        }

        private InteractiveRunner PopulateRunner(string prompt, IList<Project> projects, IList<Project> targetProjects)
        {
            var runner = new InteractiveRunner(prompt, languageProvider.GetString(LanguageSection.UiStrings, "PackageNotSelectable"), languageProvider, languageProvider.GetString(LanguageSection.UiStrings, "ProjectName"), languageProvider.GetString(LanguageSection.UiStrings, "OnSource"), languageProvider.GetString(LanguageSection.UiStrings, "OnTarget"));
            foreach (var project in projects)
            {
                var packagesAvailable = project.CurrentRelease != null;

                runner.AddRow(project.Checked, packagesAvailable, new[] {
                        project.ProjectName,
                        project.CurrentRelease.Version,
                        targetProjects.FirstOrDefault(p => p.ProjectId == project.ProjectId)?.CurrentRelease?.Version
                    });
            }
            runner.Run();
            return runner;
        }

        struct PromoteOptionNames
        {
            public const string SourceEnvironment = "sourceenvironment";
            public const string Environment = "environment";
            public const string GroupFilter = "groupfilter";
        }
    }

}
