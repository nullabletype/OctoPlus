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
using OctoPlus.Console.Interfaces;
using OctoPlus.Console.Resources;
using OctoPlusCore.Configuration.Interfaces;
using OctoPlusCore.Deployment.Interfaces;
using OctoPlusCore.Logging.Interfaces;
using OctoPlusCore.Models;
using OctoPlusCore.Octopus;
using OctoPlusCore.Octopus.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctoPlus.Console.Commands {
    class Deploy : BaseCommand {

        private IConsoleDoJob consoleDoJob;
        private IOctopusHelper octoHelper;
        private IConfiguration configuration;
        private IDeployer deployer;
        private IUiLogger uilogger;

        public Deploy(IConsoleDoJob consoleDoJob, IConfiguration configuration, IOctopusHelper octoHelper, IDeployer deployer, IUiLogger uilogger)
        {
            this.consoleDoJob = consoleDoJob;
            this.configuration = configuration;
            this.octoHelper = octoHelper;
            this.deployer = deployer;
            this.uilogger = uilogger;
        }

        public async void Configure(CommandLineApplication command) 
        {
            base.Configure(command);
            command.Description = OptionsStrings.DeployProjects;

            command.Command("profile", profile => ConfigureProfileBasedDeployment(profile));
            command.Command("int", profile => ConfigureInteractiveMode(profile));

            command.OnExecute(async () =>
            {
                command.ShowHelp();
            });
        }

        private void ConfigureInteractiveMode(CommandLineApplication command)
        {
            base.Configure(command);

            command.OnExecute(async () =>
            {
                await RunInteractively(command);
            });
        }

        private async Task RunInteractively(CommandLineApplication command)
        {
            WriteStatusLine(UiStrings.FetchingProjectList);
            var projectStubs = await octoHelper.GetProjectStubs();
            var found = projectStubs.FirstOrDefault(proj => proj.ProjectName.Equals(configuration.ChannelSeedProjectName, StringComparison.CurrentCultureIgnoreCase));

            if (found == null)
            {
                System.Console.WriteLine(UiStrings.ProjectNotFound);
                return;
            }

            var channelName = PromptForStringWithoutQuitting(UiStrings.WhichChannelPrompt);
            var environmentName = PromptForStringWithoutQuitting(UiStrings.WhichEnvironmentPrompt);
            var groupRestriction = Prompt.GetString(UiStrings.RestrictToGroupsPrompt);
            WriteStatusLine(UiStrings.CheckingOptions);
            var matchingEnvironments = await octoHelper.GetMatchingEnvironments(environmentName);

            if (matchingEnvironments.Count() > 1)
            {
                System.Console.WriteLine(UiStrings.TooManyMatchingEnvironments + string.Join(", ", matchingEnvironments.Select(e => e.Name)));
                return;
            }
            else if (matchingEnvironments.Count() == 0)
            {
                System.Console.WriteLine(UiStrings.NoMatchingEnvironments);
                return;
            }

            var groupIds = new List<string>();
            if (!string.IsNullOrEmpty(groupRestriction))
            {
                WriteStatusLine(UiStrings.GettingGroupInfo);
                groupIds =
                    (await octoHelper.GetFilteredProjectGroups(groupRestriction))
                    .Select(g => g.Id).ToList();
            }

            var channel = await octoHelper.GetChannelByProjectNameAndChannelName(found.ProjectName, channelName);
            var environment = await octoHelper.GetEnvironment(matchingEnvironments.First().Id);
            var projects = new List<Project>();
            CleanCurrentLine();

            foreach (var projectStub in projectStubs)
            {
                WriteProgress(projectStubs.IndexOf(projectStub) + 1, projectStubs.Count(), String.Format(UiStrings.LoadingInfoFor, projectStub.ProjectName));
                if (!string.IsNullOrEmpty(groupRestriction))
                {
                    if (!groupIds.Contains(projectStub.ProjectGroupId))
                    {
                        continue;
                    }
                }
                var project = await octoHelper.ConvertProject(projectStub, environment.Id, channel.VersionRange);
                var currentPackage = project.CurrentRelease.SelectedPackages.FirstOrDefault();
                if (project.SelectedPackageStub == null ||
                        (currentPackage == null ? "" : currentPackage.Version) == project.SelectedPackageStub.Version ||
                        !project.AvailablePackages.Any())
                {
                    project.Checked = false;
                }
                projects.Add(project);
            }

            CleanCurrentLine();

            var deploymentOk = false;
            EnvironmentDeployment deployment;

            do
            {
                deployment = await InteractivePrompt(channel, environment, projects);
                if(deployment == null)
                {
                    return;
                }
                var result = await this.deployer.CheckDeployment(deployment);
                if (result.Success)
                {
                    deploymentOk = true;
                }
                else
                {
                    System.Console.WriteLine(UiStrings.Error + result.ErrorMessage);
                }
            } while (!deploymentOk);

            await this.deployer.StartJob(deployment, this.uilogger);
        }

        private async Task<EnvironmentDeployment> InteractivePrompt(Channel channel, OctoPlusCore.Models.Environment environment, IList<Project> projects)
        {
            var runner = new InteractiveRunner(String.Empty, UiStrings.ProjectName, UiStrings.CurrentRelease, UiStrings.CurrentPackage, UiStrings.NewPackage);
            foreach (var project in projects)
            {
                runner.AddRow(new[] {
                        project.ProjectName,
                        project.CurrentRelease.Version,
                        project.CurrentRelease.DisplayPackageVersion,
                        project.AvailablePackages.Count() > 0 ? project.AvailablePackages.First().Version : String.Empty
                    });
            }
            runner.Run();
            var indexes = runner.GetSelectedIndexes();

            if(!indexes.Any())
            {
                System.Console.WriteLine(UiStrings.NothingSelected);
                return null;
            }

            var deployment = new EnvironmentDeployment
            {
                ChannelName = channel.Name,
                DeployAsync = true,
                EnvironmentId = environment.Id,
                EnvironmentName = environment.Name
            };

            var depProjects = new List<ProjectDeployment>();

            foreach (var index in indexes)
            {
                var current = projects[index];

                if (current.AvailablePackages.Any())
                {
                    var projectChannel = await this.octoHelper.GetChannelByName(current.ProjectId, channel.Name);
                    deployment.ProjectDeployments.Add(new ProjectDeployment
                    {
                        ProjectId = current.ProjectId,
                        ProjectName = current.ProjectName,
                        PackageId = current.AvailablePackages.First().Id,
                        PackageName = current.AvailablePackages.First().Version,
                        StepName = current.AvailablePackages.First().StepName,
                        ChannelId = projectChannel.Id,
                        ChannelVersionRange = channel.VersionRange,
                        LifeCycleId = current.LifeCycleId
                    });
                }
            }

            return deployment;
        }

        private string PromptForStringWithoutQuitting(string prompt)
        {
            var channel = Prompt.GetString(prompt);
            if (string.IsNullOrEmpty(channel))
            {
                return PromptForStringWithoutQuitting(prompt);
            }
            return channel;
        }

        private void ConfigureProfileBasedDeployment(CommandLineApplication command) 
        {
            base.Configure(command);

            AddToRegister("file", command.Option("-f|--file", OptionsStrings.ProfileFile, CommandOptionType.SingleValue).IsRequired().Accepts(v => v.LegalFilePath()));
            AddToRegister("apikey", command.Option("-a|--apikey", OptionsStrings.ProfileFile, CommandOptionType.SingleValue));
            AddToRegister("url", command.Option("-u|--url", OptionsStrings.Url, CommandOptionType.SingleValue));

            command.OnExecute(async () =>
            {
                await DeployWithProfile(command);
            });
        }

        private async Task<int> DeployWithProfile(CommandLineApplication command)
        {
            var profilePath = GetOption("file").Value();
            System.Console.WriteLine(UiStrings.UsingProfileAtPath + profilePath);
            await this.consoleDoJob.StartJob(profilePath, null, null, true);
            return 0;
        }
    }
}
