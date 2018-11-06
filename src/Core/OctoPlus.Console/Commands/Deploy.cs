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
using OctoPlusCore.Octopus.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OctoPlus.Console.Commands.SubCommands;
using OctoPlusCore.Utilities;
using System.IO;

namespace OctoPlus.Console.Commands
{
    class Deploy : BaseCommand {

        private IConsoleDoJob consoleDoJob;
        private IConfiguration configuration;
        private IDeployer deployer;
        private IUiLogger uilogger;
        private IProgressBar progressBar;
        private readonly DeployWithProfile profile;
        private readonly DeployWithProfileDirectory profileDir;

        protected override bool SupportsInteractiveMode => true;
        public override string CommandName => "deploy";

        public Deploy(IConsoleDoJob consoleDoJob, IConfiguration configuration, IOctopusHelper octoHelper, IDeployer deployer, IUiLogger uilogger, DeployWithProfile profile, DeployWithProfileDirectory profileDir, IProgressBar progressBar) : base(octoHelper)
        {
            this.consoleDoJob = consoleDoJob;
            this.configuration = configuration;
            this.deployer = deployer;
            this.uilogger = uilogger;
            this.profile = profile;
            this.profileDir = profileDir;
            this.progressBar = progressBar;
        }

        public override void Configure(CommandLineApplication command) 
        {
            base.Configure(command);
            command.Description = OptionsStrings.DeployProjects;

            ConfigureSubCommand(profile, command);
            ConfigureSubCommand(profileDir, command);
            
            AddToRegister(DeployOptionNames.ChannelName, command.Option("-c|--channel", OptionsStrings.InteractiveDeploy, CommandOptionType.SingleValue));
            AddToRegister(DeployOptionNames.Environment, command.Option("-e|--environment", OptionsStrings.InteractiveDeploy, CommandOptionType.SingleValue));
            AddToRegister(DeployOptionNames.GroupFilter, command.Option("-g|--groupfilter", OptionsStrings.InteractiveDeploy, CommandOptionType.SingleValue));
            AddToRegister(DeployOptionNames.SaveProfile, command.Option("-s|--saveprofile", OptionsStrings.InteractiveDeploy, CommandOptionType.SingleValue));
            AddToRegister(OptionNames.ReleaseName, command.Option("-r|--releasename", OptionsStrings.ReleaseVersion, CommandOptionType.SingleValue));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var profilePath = GetStringValueFromOption(DeployOptionNames.SaveProfile);
            if (!string.IsNullOrEmpty(profilePath))
            {
                System.Console.WriteLine(string.Format(UiStrings.GoingToSaveProfile, profilePath));
            }
            progressBar.WriteStatusLine(UiStrings.FetchingProjectList);
            var projectStubs = await octoHelper.GetProjectStubs();
            var found = projectStubs.FirstOrDefault(proj => proj.ProjectName.Equals(configuration.ChannelSeedProjectName, StringComparison.CurrentCultureIgnoreCase));

            if (found == null)
            {
                System.Console.WriteLine(UiStrings.ProjectNotFound);
                return -1;
            }

            var channelName = GetStringFromUser(DeployOptionNames.ChannelName, UiStrings.WhichChannelPrompt);
            var environmentName = GetStringFromUser(DeployOptionNames.Environment, UiStrings.WhichEnvironmentPrompt);
            var groupRestriction = GetStringFromUser(DeployOptionNames.GroupFilter, UiStrings.RestrictToGroupsPrompt, allowEmpty: true);

            progressBar.WriteStatusLine(UiStrings.CheckingOptions);

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

            var channel = await octoHelper.GetChannelByProjectNameAndChannelName(found.ProjectName, channelName);

            if (channel == null)
            {
                System.Console.WriteLine(UiStrings.NoMatchingChannel);
                return -1;
            }

            progressBar.CleanCurrentLine();
            var projects = await ConvertProjectStubsToProjects(projectStubs, groupRestriction, groupIds, environment, channel);
            progressBar.CleanCurrentLine();

            var deployment = await GenerateDeployment(channel, environment, projects);

            if (deployment == null)
            {
                return -2;
            }

            if (!string.IsNullOrEmpty(profilePath))
            {
                SaveProfile(deployment, profilePath);
            }
            else
            {
                await this.deployer.StartJob(deployment, this.uilogger);
            }
            return 0;
        }

        private void SaveProfile(EnvironmentDeployment deployment, string profilePath)
        {
            foreach(var project in deployment.ProjectDeployments)
            {
                foreach(var package in project.Packages)
                {
                    package.PackageId = "latest";
                    package.PackageName = "latest";
                }
            }
            var content = StandardSerialiser.SerializeToJsonNet(deployment, true);
            File.WriteAllText(profilePath, content);
            System.Console.WriteLine(string.Format(UiStrings.ProfileSaved, profilePath));
        }

        private async Task<EnvironmentDeployment> GenerateDeployment(Channel channel, OctoPlusCore.Models.Environment environment, List<Project> projects)
        {
            EnvironmentDeployment deployment;

            if (this.InInteractiveMode)
            {
                bool deploymentOk;
                do
                {
                    deployment = await InteractivePrompt(channel, environment, projects);
                    deploymentOk = await ValidateDeployment(deployment, deployer);
                } while (!deploymentOk);
            }
            else
            {
                deployment = await PrepareEnvironmentDeployment(channel, environment, projects, all: true);
                if (!await ValidateDeployment(deployment, deployer)) return null;
            }

            var releaseName = PromptForReleaseName();

            if (!string.IsNullOrEmpty(releaseName))
            {
                foreach (var project in deployment.ProjectDeployments)
                {
                    project.ReleaseVersion = releaseName;
                }
            }

            FillRequiredVariables(deployment.ProjectDeployments);

            return deployment;
        }

        private async Task<List<Project>> ConvertProjectStubsToProjects(List<ProjectStub> projectStubs, string groupRestriction, List<string> groupIds,
            OctoPlusCore.Models.Environment environment, Channel channel)
        {
            var projects = new List<Project>();

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

                var project = await octoHelper.ConvertProject(projectStub, environment.Id, channel.VersionRange);
                var currentPackages = project.CurrentRelease.SelectedPackages;
                project.Checked = false;
                if (project.SelectedPackageStubs != null) 
                {
                    foreach(var stub in project.SelectedPackageStubs)
                    {
                        if(stub == null)
                        {
                            continue;
                        }
                        var matchingCurrent = currentPackages.FirstOrDefault(p => p.StepId == stub.StepId);
                        if (matchingCurrent != null) {
                            if (matchingCurrent.Version != stub.Version)
                            {
                                project.Checked = true;
                                break;
                            }
                        }
                        else
                        {
                            project.Checked = true;
                            break;
                        }
                    }
                }

                projects.Add(project);
            }

            return projects;
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
                Packages = current.AvailablePackages.Select(x => new PackageDeployment {
                    PackageId = x.SelectedPackage.Id,
                    PackageName = x.SelectedPackage.Version,
                    StepId = x.StepId,
                    StepName = x.StepName
                }).ToList(),
                ChannelId = projectChannel.Id,
                ChannelVersionRange = channel.VersionRange,
                LifeCycleId = current.LifeCycleId,
                RequiredVariables = current?.RequiredVariables?.Select(r => new RequiredVariableDeployment { Id = r.Id, ExtraOptions = r.ExtraOptions, Name = r.Name, Type = r.Type, Value = r.Value }).ToList()
            };
        }

        private InteractiveRunner PopulateRunner(string prompt, IEnumerable<Project> projects)
        {
            var runner = new InteractiveRunner(prompt, UiStrings.ProjectName, UiStrings.CurrentRelease, UiStrings.CurrentPackage, UiStrings.NewPackage);
            foreach (var project in projects)
            {
                var packagesAvailable = project.AvailablePackages.Count > 0 && project.AvailablePackages.All(p => p.SelectedPackage != null);
                
                runner.AddRow(project.Checked, new[] {
                    project.ProjectName,
                    project.CurrentRelease.Version,
                    project.AvailablePackages.Count > 1 ? UiStrings.Multi : project.CurrentRelease.DisplayPackageVersion,
                    project.AvailablePackages.Count > 1 ? UiStrings.Multi : (packagesAvailable ? project.AvailablePackages.First().SelectedPackage.Version : string.Empty)
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
        }
    }
}
