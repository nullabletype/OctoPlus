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

namespace OctoPlus.Console.Commands
{
    internal class Promote : BaseCommand
    {
        private IConfiguration configuration;
        private IDeployer deployer;
        private IUiLogger uilogger;
        private IProgressBar progressBar;

        protected override bool SupportsInteractiveMode => true;
        public override string CommandName => "promote";

        public Promote(IConfiguration configuration, IOctopusHelper octoHelper, IDeployer deployer, IUiLogger uilogger, IProgressBar progressBar) : base(octoHelper)
        {
            this.configuration = configuration;
            this.deployer = deployer;
            this.uilogger = uilogger;
            this.progressBar = progressBar;
        }
        
        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);
            command.Description = OptionsStrings.PromoteProjects;

            AddToRegister(PromoteOptionNames.Environment, command.Option("-e|--environment", OptionsStrings.EnvironmentName, CommandOptionType.SingleValue));
            AddToRegister(PromoteOptionNames.SourceEnvironment, command.Option("-s|--sourcenvironment", OptionsStrings.SourceEnvironment, CommandOptionType.SingleValue));
            AddToRegister(PromoteOptionNames.GroupFilter, command.Option("-g|--groupfilter", OptionsStrings.GroupFilter, CommandOptionType.SingleValue));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            progressBar.WriteStatusLine(UiStrings.FetchingProjectList);
            var projectStubs = await octoHelper.GetProjectStubs();
            var found = projectStubs.FirstOrDefault(proj => proj.ProjectName.Equals(configuration.ChannelSeedProjectName, StringComparison.CurrentCultureIgnoreCase));

            if (found == null)
            {
                System.Console.WriteLine(UiStrings.ProjectNotFound);
                return -1;
            }

            var environmentName = GetStringFromUser(PromoteOptionNames.SourceEnvironment, UiStrings.SourceEnvironment);
            var targetEnvironmentName = GetStringFromUser(PromoteOptionNames.Environment, UiStrings.WhichEnvironmentPrompt);
            var groupRestriction = GetStringFromUser(PromoteOptionNames.GroupFilter, UiStrings.RestrictToGroupsPrompt);

            progressBar.WriteStatusLine(UiStrings.CheckingOptions);
            var environment = await FetchEnvironmentFromUserInput(environmentName);
            var targetEnvironment = await FetchEnvironmentFromUserInput(targetEnvironmentName);

            if (environment == null || targetEnvironment == null)
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

            progressBar.CleanCurrentLine();

            var (projects, targetProjects) = await GenerateProjectList(projectStubs, groupRestriction, groupIds, environment, targetEnvironment);

            progressBar.CleanCurrentLine();

            var deployment = await GenerateDeployment(environment, targetEnvironment, projects, targetProjects);

            if (deployment == null)
            {
                return -2;
            }

            await this.deployer.StartJob(deployment, this.uilogger);

            return 0;
        }

        private async Task<EnvironmentDeployment> GenerateDeployment(OctoPlusCore.Models.Environment environment, OctoPlusCore.Models.Environment targetEnvironment, List<Project> projects,
            List<Project> targetProjects)
        {
            var deploymentOk = false;
            EnvironmentDeployment deployment;

            do
            {
                deployment = InteractivePrompt(environment, targetEnvironment, projects, targetProjects);
                if (deployment == null)
                {
                    return deployment;
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

            FillRequiredVariables(deployment.ProjectDeployments);

            return deployment;
        }

        private async Task<(List<Project> projects, List<Project> targetProjects)> GenerateProjectList(List<ProjectStub> projectStubs, string groupRestriction, List<string> groupIds, OctoPlusCore.Models.Environment environment,
            OctoPlusCore.Models.Environment targetEnvironment)
        {
            var projects = new List<Project>();
            var targetProjects = new List<Project>();

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

                var project = await octoHelper.ConvertProject(projectStub, environment.Id, null, null);
                var targetProject = await octoHelper.ConvertProject(projectStub, targetEnvironment.Id, null, null);

                var currentRelease = project.CurrentRelease;
                var currentTargetRelease = targetProject.CurrentRelease;
                if (currentRelease == null)
                {
                    continue;
                }

                if (currentTargetRelease != null && currentTargetRelease.Id == currentRelease.Id)
                {
                    project.Checked = false;
                }
                else
                {
                    project.Checked = true;
                }

                projects.Add(project);
                targetProjects.Add(targetProject);
            }

            return (projects, targetProjects);
        }

        private EnvironmentDeployment InteractivePrompt(OctoPlusCore.Models.Environment environment, OctoPlusCore.Models.Environment targetEnvironment, IList<Project> projects, IList<Project> targetProjects)
        {
            InteractiveRunner runner = PopulateRunner(String.Format(UiStrings.PromotingTo, environment.Name, targetEnvironment.Name), projects, targetProjects);
            var indexes = runner.GetSelectedIndexes();

            if (!indexes.Any())
            {
                System.Console.WriteLine(UiStrings.NothingSelected);
                return null;
            }

            var deployment = new EnvironmentDeployment
            {
                ChannelName = string.Empty,
                DeployAsync = true,
                EnvironmentId = targetEnvironment.Id,
                EnvironmentName = targetEnvironment.Name
            };

            foreach (var index in indexes)
            {
                var current = projects[index];
                var currentTarget = targetProjects[index];

                if (current.CurrentRelease != null)
                {
                    deployment.ProjectDeployments.Add(new ProjectDeployment
                    {
                        ProjectId = currentTarget.ProjectId,
                        ProjectName = currentTarget.ProjectName,
                        LifeCycleId = currentTarget.LifeCycleId,
                        ReleaseId = current.CurrentRelease.Id
                    });
                }
            }

            return deployment;
        }

        private static InteractiveRunner PopulateRunner(string prompt, IList<Project> projects, IList<Project> targetProjects)
        {
            var runner = new InteractiveRunner(prompt, UiStrings.PackageNotSelectable, UiStrings.ProjectName, UiStrings.OnSource, UiStrings.OnTarget);
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
