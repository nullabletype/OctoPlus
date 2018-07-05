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
        private readonly DeployWithProfile profile;

        protected override bool SupportsInteractiveMode => true;
        public override string CommandName => "deploy";

        public Deploy(IConsoleDoJob consoleDoJob, IConfiguration configuration, IOctopusHelper octoHelper, IDeployer deployer, IUiLogger uilogger, DeployWithProfile profile)
        {
            this.consoleDoJob = consoleDoJob;
            this.configuration = configuration;
            this.octoHelper = octoHelper;
            this.deployer = deployer;
            this.uilogger = uilogger;
            this.profile = profile;
        }

        public override void Configure(CommandLineApplication command) 
        {
            base.Configure(command);
            command.Description = OptionsStrings.DeployProjects;

            ConfigureSubCommand(profile, command);

            AddToRegister(DeployOptionNames.ChannelName, command.Option("-c|--channel", OptionsStrings.InteractiveDeploy, CommandOptionType.SingleValue));
            AddToRegister(DeployOptionNames.Environment, command.Option("-e|--environment", OptionsStrings.InteractiveDeploy, CommandOptionType.SingleValue));
            AddToRegister(DeployOptionNames.GroupFilter, command.Option("-g|--groupfilter", OptionsStrings.InteractiveDeploy, CommandOptionType.SingleValue));
            AddToRegister(DeployOptionNames.ReleaseName, command.Option("-r|--releasename", OptionsStrings.ReleaseVersion, CommandOptionType.SingleValue));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            WriteStatusLine(UiStrings.FetchingProjectList);
            var projectStubs = await octoHelper.GetProjectStubs();
            var found = projectStubs.FirstOrDefault(proj => proj.ProjectName.Equals(configuration.ChannelSeedProjectName, StringComparison.CurrentCultureIgnoreCase));

            if (found == null)
            {
                System.Console.WriteLine(UiStrings.ProjectNotFound);
                return -1;
            }

            var channelName = GetStringFromUser(DeployOptionNames.ChannelName, UiStrings.WhichChannelPrompt);
            var environmentName = GetStringFromUser(DeployOptionNames.Environment, UiStrings.WhichEnvironmentPrompt);
            var groupRestriction = GetStringFromUser(DeployOptionNames.GroupFilter, UiStrings.RestrictToGroupsPrompt);
            var releaseName = GetStringFromUser(DeployOptionNames.ReleaseName, UiStrings.ReleaseNamePrompt);

            WriteStatusLine(UiStrings.CheckingOptions);
            var matchingEnvironments = await octoHelper.GetMatchingEnvironments(environmentName);

            if (matchingEnvironments.Count() > 1)
            {
                System.Console.WriteLine(UiStrings.TooManyMatchingEnvironments + string.Join(", ", matchingEnvironments.Select(e => e.Name)));
                return -1;
            }
            else if (matchingEnvironments.Count() == 0)
            {
                System.Console.WriteLine(UiStrings.NoMatchingEnvironments);
                return -1;
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
                else
                {
                    project.Checked = true;
                }
                projects.Add(project);
            }

            CleanCurrentLine();

            var deploymentOk = false;
            EnvironmentDeployment deployment;

            if (this.InInteractiveMode)
            {
                do
                {
                    deployment = await InteractivePrompt(channel, environment, projects);
                    if (await ValidateDeployment(deployment, deployer)) return -1;
                } while (!deploymentOk);
            }
            else
            {
                deployment = await PrepareEnvironmentDeployment(channel, environment, projects, all: true);
                if (await ValidateDeployment(deployment, deployer)) return -2;
            }

            await this.deployer.StartJob(deployment, this.uilogger);
            return 0;
        }

        private async Task<EnvironmentDeployment> InteractivePrompt(Channel channel, OctoPlusCore.Models.Environment environment, IList<Project> projects)
        {
            InteractiveRunner runner = PopulateRunner(String.Format(UiStrings.DeployingTo, channel.Name, environment.Name), projects);
            var indexes = runner.GetSelectedIndexes();

            if (!indexes.Any())
            {
                System.Console.WriteLine(UiStrings.NothingSelected);
                return null;
            }

            var deployment = await PrepareEnvironmentDeployment(channel, environment, projects, indexes);

            return deployment;
        }

        private async Task<EnvironmentDeployment> PrepareEnvironmentDeployment(Channel channel, OctoPlusCore.Models.Environment environment, IList<Project> projects, IEnumerable<int> indexes = null, bool all = false)
        {
            var deployment = new EnvironmentDeployment
            {
                ChannelName = channel.Name,
                DeployAsync = true,
                EnvironmentId = environment.Id,
                EnvironmentName = environment.Name
            };

            if (all)
            {
                foreach (var project in projects)
                {
                    if (project.AvailablePackages.Any())
                    {
                        deployment.ProjectDeployments.Add(await GenerateProjectDeployment(channel, project));
                    }
                }
            }
            else
            {
                foreach (var index in indexes)
                {
                    var current = projects[index];

                    if (current.AvailablePackages.Any())
                    {
                        deployment.ProjectDeployments.Add(await GenerateProjectDeployment(channel, current));
                    }
                }
            }

            return deployment;
        }

        private async Task<ProjectDeployment> GenerateProjectDeployment(Channel channel, Project current)
        {
            var projectChannel = await this.octoHelper.GetChannelByName(current.ProjectId, channel.Name);
            return new ProjectDeployment
            {
                ProjectId = current.ProjectId,
                ProjectName = current.ProjectName,
                PackageId = current.AvailablePackages.First().Id,
                PackageName = current.AvailablePackages.First().Version,
                StepName = current.AvailablePackages.First().StepName,
                ChannelId = projectChannel.Id,
                ChannelVersionRange = channel.VersionRange,
                LifeCycleId = current.LifeCycleId
            };
        }

        private InteractiveRunner PopulateRunner(string prompt, IList<Project> projects)
        {
            var runner = new InteractiveRunner(prompt, UiStrings.ProjectName, UiStrings.CurrentRelease, UiStrings.CurrentPackage, UiStrings.NewPackage);
            foreach (var project in projects)
            {
                runner.AddRow(project.Checked, new[] {
                        project.ProjectName,
                        project.CurrentRelease.Version,
                        project.CurrentRelease.DisplayPackageVersion,
                        project.AvailablePackages.Count() > 0 ? project.AvailablePackages.First().Version : String.Empty
                    });
            }
            runner.Run();
            return runner;
        }

        private void ConfigureProfileBasedDeployment(CommandLineApplication command) 
        {
            base.Configure(command);

            AddToRegister(DeployOptionNames.File, command.Option("-f|--file", OptionsStrings.ProfileFile, CommandOptionType.SingleValue).IsRequired().Accepts(v => v.LegalFilePath()));
            AddToRegister(DeployOptionNames.ApiKey, command.Option("-a|--apikey", OptionsStrings.ProfileFile, CommandOptionType.SingleValue));
            AddToRegister(DeployOptionNames.Url, command.Option("-u|--url", OptionsStrings.Url, CommandOptionType.SingleValue));

            command.OnExecute(async () =>
            {
                await DeployWithProfile();
            });
        }

        private async Task<int> DeployWithProfile()
        {
            var profilePath = GetOption(DeployOptionNames.File).Value();
            System.Console.WriteLine(UiStrings.UsingProfileAtPath + profilePath);
            await this.consoleDoJob.StartJob(profilePath, null, null, true);
            return 0;
        }

        struct DeployOptionNames
        {
            public const string File = "file";
            public const string ApiKey = "apikey";
            public const string Url = "url";
            public const string ChannelName = "channel";
            public const string Environment = "environment";
            public const string GroupFilter = "groupfilter";
            public const string ReleaseName = "ReleaseName";
        }
    }
}
