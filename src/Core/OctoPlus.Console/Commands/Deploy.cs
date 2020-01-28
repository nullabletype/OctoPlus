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
using OctoPlusCore.Configuration.Interfaces;
using OctoPlusCore.Models;
using OctoPlusCore.Octopus.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OctoPlus.Console.Commands.SubCommands;
using OctoPlusCore.Language;
using OctoPlusCore.Interfaces;
using OctoPlusCore.JobRunners;
using OctoPlusCore.JobRunners.JobConfigs;

namespace OctoPlus.Console.Commands
{
    class Deploy : BaseCommand {

        private IConfiguration configuration;
        private IProgressBar progressBar;
        private readonly DeployWithProfile profile;
        private readonly DeployWithProfileDirectory profileDir;
        private readonly DeployRunner runner;

        protected override bool SupportsInteractiveMode => true;
        public override string CommandName => "deploy";

        public Deploy(DeployRunner deployRunner, IConfiguration configuration, IOctopusHelper octoHelper, DeployWithProfile profile, DeployWithProfileDirectory profileDir, IProgressBar progressBar, ILanguageProvider languageProvider) : base(octoHelper, languageProvider)
        {
            this.configuration = configuration;
            this.profile = profile;
            this.profileDir = profileDir;
            this.progressBar = progressBar;
            this.runner = deployRunner;
        }

        public override void Configure(CommandLineApplication command) 
        {
            base.Configure(command);
            command.Description = languageProvider.GetString(LanguageSection.OptionsStrings, "DeployProjects");

            ConfigureSubCommand(profile, command);
            ConfigureSubCommand(profileDir, command);
            
            AddToRegister(DeployOptionNames.ChannelName, command.Option("-c|--channel", languageProvider.GetString(LanguageSection.OptionsStrings, "DeployChannel"), CommandOptionType.SingleValue));
            AddToRegister(DeployOptionNames.Environment, command.Option("-e|--environment", languageProvider.GetString(LanguageSection.OptionsStrings, "EnvironmentName"), CommandOptionType.SingleValue));
            AddToRegister(DeployOptionNames.GroupFilter, command.Option("-g|--groupfilter", languageProvider.GetString(LanguageSection.OptionsStrings, "GroupFilter"), CommandOptionType.SingleValue));
            AddToRegister(DeployOptionNames.SaveProfile, command.Option("-s|--saveprofile", languageProvider.GetString(LanguageSection.OptionsStrings, "SaveProfile"), CommandOptionType.SingleValue));
            AddToRegister(DeployOptionNames.DefaultFallback, command.Option("-d|--fallbacktodefault", languageProvider.GetString(LanguageSection.OptionsStrings, "FallbackToDefault"), CommandOptionType.NoValue));
            AddToRegister(OptionNames.ReleaseName, command.Option("-r|--releasename", languageProvider.GetString(LanguageSection.OptionsStrings, "ReleaseVersion"), CommandOptionType.SingleValue));
        }


        protected override async Task<int> Run(CommandLineApplication command)
        {
            var profilePath = GetStringValueFromOption(DeployOptionNames.SaveProfile);
            if (!string.IsNullOrEmpty(profilePath))
            {
                System.Console.WriteLine(string.Format(languageProvider.GetString(LanguageSection.UiStrings, "GoingToSaveProfile"), profilePath));
            }
            progressBar.WriteStatusLine(languageProvider.GetString(LanguageSection.UiStrings, "FetchingProjectList"));
            var projectStubs = await octoHelper.GetProjectStubs();
            var found = projectStubs.Where(proj => configuration.ChannelSeedProjectNames.Select(c => c.ToLower()).Contains(proj.ProjectName.ToLower()));

            if (!found.Any())
            {
                System.Console.WriteLine(languageProvider.GetString(LanguageSection.UiStrings, "ProjectNotFound"));
                return -1;
            }

            var channelName = GetStringFromUser(DeployOptionNames.ChannelName, languageProvider.GetString(LanguageSection.UiStrings, "WhichChannelPrompt"));
            var environmentName = GetStringFromUser(DeployOptionNames.Environment, languageProvider.GetString(LanguageSection.UiStrings, "WhichEnvironmentPrompt"));
            var groupRestriction = GetStringFromUser(DeployOptionNames.GroupFilter, languageProvider.GetString(LanguageSection.UiStrings, "RestrictToGroupsPrompt"), allowEmpty: true);
            var forceDefault = GetOption(DeployOptionNames.DefaultFallback).HasValue();

            progressBar.WriteStatusLine(languageProvider.GetString(LanguageSection.UiStrings, "CheckingOptions"));

            var environment = await FetchEnvironmentFromUserInput(environmentName);

            if (environment == null)
            {
                return -2;
            }

            Channel channel = null;
            foreach (var project in found)
            {
                channel = await octoHelper.GetChannelByProjectNameAndChannelName(project.ProjectName, channelName);
                if (channel != null)
                {
                    break;
                }
            }

            if (channel == null)
            {
                System.Console.WriteLine(languageProvider.GetString(LanguageSection.UiStrings, "NoMatchingChannel"));
                return -1;
            }

            Channel defaultChannel = null;

            if (forceDefault && !string.IsNullOrEmpty(configuration.DefaultChannel)) {
                defaultChannel = await octoHelper.GetChannelByProjectNameAndChannelName(found.First().ProjectName, configuration.DefaultChannel);
            }

            var configResult = DeployConfig.Create(environment, channel, defaultChannel, groupRestriction, GetStringValueFromOption(DeployOptionNames.SaveProfile), this.InInteractiveMode);


            if (configResult.IsFailure)
            {
                System.Console.WriteLine(configResult.Error);
                return -1;
            }
            else
            {
                return await runner.Run(configResult.Value, progressBar, projectStubs, InteractivePrompt, PromptForStringWithoutQuitting, text => { return Prompt.GetString(text); });
            }
        }

        private IEnumerable<int> InteractivePrompt(DeployConfig config, IList<Project> projects)
        {
            InteractiveRunner runner = PopulateRunner(String.Format(languageProvider.GetString(LanguageSection.UiStrings, "DeployingTo"), config.Channel.Name, config.Environment.Name), languageProvider.GetString(LanguageSection.UiStrings, "PackageNotSelectable"), projects);
            return runner.GetSelectedIndexes();
        }
        

        private InteractiveRunner PopulateRunner(string prompt, string unselectableText, IEnumerable<Project> projects)
        {
            var runner = new InteractiveRunner(prompt, unselectableText, languageProvider, languageProvider.GetString(LanguageSection.UiStrings, "ProjectName"), languageProvider.GetString(LanguageSection.UiStrings, "CurrentRelease"), languageProvider.GetString(LanguageSection.UiStrings, "CurrentPackage"), languageProvider.GetString(LanguageSection.UiStrings, "NewPackage"));
            foreach (var project in projects)
            {
                var packagesAvailable = project.AvailablePackages.Count > 0 && project.AvailablePackages.All(p => p.SelectedPackage != null);
                
                runner.AddRow(project.Checked, packagesAvailable, new[] {
                    project.ProjectName,
                    project.CurrentRelease.Version,
                    project.AvailablePackages.Count > 1 ? languageProvider.GetString(LanguageSection.UiStrings, "Multi") : project.CurrentRelease.DisplayPackageVersion,
                    project.AvailablePackages.Count > 1 ? languageProvider.GetString(LanguageSection.UiStrings, "Multi") : (packagesAvailable ? project.AvailablePackages.First().SelectedPackage.Version : string.Empty)
                });
                
            }
            runner.Run();
            return runner;
        }

        struct DeployOptionNames
        {
            public const string ChannelName = "channel";
            public const string Environment = "environment";
            public const string GroupFilter = "groupfilter";
            public const string SaveProfile = "saveprofile";
            public const string DefaultFallback = "fallbacktodefault";
        }
    }
}
