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
using System.Linq.Expressions;
using System.Threading.Tasks;
using Octopus.Client;
using Octopus.Client.Model;
using OctoPlusCore.Models;
using OctoPlusCore.Octopus.Interfaces;
using Environment = OctoPlusCore.Models.Environment;

namespace OctoPlusCore.Octopus
{
    public class OctopusHelper : IOctopusHelper
    {
        private IOctopusAsyncClient client;

        public OctopusHelper(IOctopusAsyncClient client)
        {
            this.client = client;
        }

        public async Task<List<PackageStub>> GetPackages(string projectIdOrHref, string versionRange)
        {
            return await GetPackages(await client.Repository.Projects.Get(projectIdOrHref), versionRange);
        }

        private async Task<PackageIdResult> GetPackageId(ProjectResource project)
        {
            var process = await client.Repository.DeploymentProcesses.Get(project.DeploymentProcessId);
            if (process != null)
            {
                foreach (var step in process.Steps)
                {
                    foreach (var action in step.Actions)
                    {
                        if (action.Properties["Octopus.Action.Package.FeedId"] != null &&
                            action.Properties["Octopus.Action.Package.FeedId"].Value == "feeds-builtin")
                        {
                            if (action.Properties["Octopus.Action.Package.PackageId"] != null &&
                                !string.IsNullOrEmpty(action.Properties["Octopus.Action.Package.PackageId"].Value))
                            {
                                var packageId = action.Properties["Octopus.Action.Package.PackageId"].Value;
                                if (!string.IsNullOrEmpty(packageId))
                                {
                                    return new PackageIdResult
                                    {
                                        PackageId = packageId,
                                        StepName = step.Name
                                    };
                                }
                            }
                        }
                       
                    }
                }
            }
            return null;
        }

        private async Task<List<PackageStub>> GetPackages(ProjectResource project, string versionRange, int take = 5)
        {

            var packageIdResult = await this.GetPackageId(project);
            if (packageIdResult != null && !string.IsNullOrEmpty(packageIdResult.PackageId))
            {
                var template =
                    (await client.Repository.Feeds.Get("feeds-builtin")).Links["SearchTemplate"];

                var packages =
                    await
                        client.Get<List<PackageFromBuiltInFeedResource>>(template,
                            new
                            {
                                packageId = packageIdResult.PackageId,
                                partialMatch = false,
                                includeMultipleVersions = true,
                                take,
                                includePreRelease = true,
                                versionRange,
                            });

                var finalPackages = new List<PackageStub>();
                foreach (var package in packages)
                {
                    finalPackages.Add(ConvertPackage(package, packageIdResult.StepName));
                }
                return finalPackages;
            }

            return new List<PackageStub>();
        }

        public async Task<Release> GetReleasedVersion(string projectId, string envId)
        {
            var deployment =
                (await client.Repository.Deployments.FindOne(resource => Search(resource, projectId, envId)));
            if (deployment != null)
            {
                var release = await client.Repository.Releases.Get(deployment.ReleaseId);
                if (release != null)
                {
                    var project = await client.Repository.Projects.Get(projectId);
                    var package = await GetPackageId(project);
                    if (package != null)
                    {
                        return await this.ConvertRelease(release);
                    }
                }
            }
            return new Release {Id = "", Version = "None"};
        }

        public async Task<List<Environment>> GetEnvironments()
        {
            var envs = await client.Repository.Environments.GetAll();
            return envs.Select(ConvertEnvironment).ToList();
        }

        public async Task<Environment> GetEnvironment(string idOrName)
        {
            return ConvertEnvironment(await client.Repository.Environments.Get(idOrName));
        }

        public async Task<List<ProjectGroup>> GetFilteredProjectGroups(string filter)
        {
            var groups = await client.Repository.ProjectGroups.GetAll();
            return groups.Where(g => g.Name.ToLower().Contains(filter)).Select(ConvertProjectGroup).ToList();
        }

        public async Task<List<ProjectGroup>> GetProjectGroups()
        {
            return (await client.Repository.ProjectGroups.GetAll()).Select(ConvertProjectGroup).ToList();
        }

        public async Task<List<Project>> GetProjects(string environment, string channelRange)
        {
            var projects = await client.Repository.Projects.GetAll();
            var converted = new List<Project>();
            foreach (var project in projects)
            {
                converted.Add(await ConvertProject(project, environment, channelRange));
            }
            return converted;
        }

        public async Task<List<ProjectStub>> GetProjectStubs() {
            var projects = await client.Repository.Projects.GetAll();
            var converted = new List<ProjectStub>();
            foreach (var project in projects) {
                converted.Add(ConvertProject(project));
            }
            return converted;
        }

        public async Task<Project> GetProject(string idOrHref, string environment, string channelRange)
        {
            return await ConvertProject(await client.Repository.Projects.Get(idOrHref), environment, channelRange);
        }

        public async Task<bool> ValidateProjectName(string name)
        {
            var project = await client.Repository.Projects.FindOne(resource => resource.Name == name);
            return project != null;
        }

        public async Task<Project> GetProjectByName(string name, string environment, string channelRange)
        {
            return await ConvertProject(await client.Repository.Projects.FindOne(resource => resource.Name == name),
                environment,
                channelRange);
        }

        public async Task<Channel> GetChannelByProjectNameAndChannelName(string name, string channelName)
        {
            var project = await client.Repository.Projects.FindOne(resource => resource.Name == name);
            return ConvertChannel(await client.Repository.Channels.FindByName(project, channelName));
        }

        public async Task<List<Channel>> GetChannelsByProjectName(string name)
        {
            var project = await client.Repository.Projects.FindOne(resource => resource.Name == name);
            var channels = await client.Repository.Projects.GetChannels(project);
            return channels.Items.Select(ConvertChannel).ToList();
        }

        public async Task<Channel> GetChannelByName(string projectIdOrName, string channelName)
        {
            var project = await client.Repository.Projects.Get(projectIdOrName);
            return ConvertChannel(await client.Repository.Channels.FindByName(project, channelName));
        }

        public async Task<Channel> GetChannel(string channelIdOrName)
        {
            return ConvertChannel(await client.Repository.Channels.Get(channelIdOrName));
        }

        public async Task<List<Channel>> GetChannelsForProject(string projectIdOrHref)
        {
            var project = await client.Repository.Projects.Get(projectIdOrHref);
            var channels = await client.Repository.Projects.GetChannels(project);
            return channels.Items.Select(ConvertChannel).ToList();
        }

        public async Task<Release> GetRelease(string releaseIdOrHref)
        {
            return await ConvertRelease(await client.Repository.Releases.Get(releaseIdOrHref));
        }

        public async Task<LifeCycle> GetLifeCycle(string idOrHref)
        {
            return ConvertLifeCycle(await client.Repository.Lifecycles.Get(idOrHref));
        }

        public async Task<PackageFull> GetFullPackage(PackageStub stub)
        {
            var package = new PackageFull {
                Id = stub.Id,
                Version = stub.Version,
                StepName = stub.StepName
            };
            var template = (await client.Repository.Feeds.Get("feeds-builtin")).Links["NotesTemplate"];
            package.Message =
                await
                    client.Get<string>(template,
                        new
                        {
                            packageId = stub.Id,
                            version = stub.Version
                        });
            return package;
        }

        public bool Search(DeploymentResource deploymentResource, string projectId, string envId)
        {
            return deploymentResource.ProjectId == projectId && deploymentResource.EnvironmentId == envId;
        }

        private Environment ConvertEnvironment(EnvironmentResource env)
        {
            return new Environment {Id = env.Id, Name = env.Name};
        }

        public async Task<Project> ConvertProject(ProjectStub project, string env, string channelRange)
        {
            var projectRes = await this.client.Repository.Projects.Get(project.ProjectId);
            var packages = await this.GetPackages(projectRes, channelRange);
            return new Project {
                CurrentRelease = await this.GetReleasedVersion(project.ProjectId, env),
                ProjectName = project.ProjectName,
                ProjectId = project.ProjectId,
                Checked = true,
                ProjectGroupId = project.ProjectGroupId,
                AvailablePackages = packages,
                SelectedPackageStub = packages != null && packages.Any() ? packages.First() : null,
                LifeCycleId = project.LifeCycleId
            };
        }

        private async Task<Project> ConvertProject(ProjectResource project, string env, string channelRange) {
            var packages = await this.GetPackages(project, channelRange);
            return new Project
            {
                CurrentRelease = await this.GetReleasedVersion(project.Id, env),
                ProjectName = project.Name,
                ProjectId = project.Id,
                Checked = true,
                ProjectGroupId = project.ProjectGroupId,
                AvailablePackages = packages,
                SelectedPackageStub = packages != null && packages.Any() ? packages.First() : null,
                LifeCycleId = project.LifecycleId
            };
        }

        private ProjectStub ConvertProject(ProjectResource project) {
            return new ProjectStub {
                ProjectName = project.Name,
                ProjectId = project.Id,
                Checked = true,
                ProjectGroupId = project.ProjectGroupId,
                LifeCycleId = project.LifecycleId
            };
        }

        private LifeCycle ConvertLifeCycle(LifecycleResource lifeCycle)
        {
            var lc = new LifeCycle
            {
                Name = lifeCycle.Name,
                Id = lifeCycle.Id,
                Description = lifeCycle.Description
            };
            if (lifeCycle.Phases != null)
            {
                foreach (var phase in lifeCycle.Phases)
                {
                    var newPhase = new Phase
                    {
                        Name = phase.Name,
                        Id = phase.Id

                    };
                    if (phase.OptionalDeploymentTargets != null)
                    {
                        newPhase.OptionalDeploymentTargetEnvironmentIds = phase.OptionalDeploymentTargets.ToList();
                    }
                    if (phase.AutomaticDeploymentTargets != null)
                    {
                        newPhase.AutomaticDeploymentTargetEnvironmentIds = phase.AutomaticDeploymentTargets.ToList();
                    }
                    if (newPhase.AutomaticDeploymentTargetEnvironmentIds.Any() || newPhase.OptionalDeploymentTargetEnvironmentIds.Any())
                    {
                        lc.Phases.Add(newPhase);
                    }
                }
            }
            return lc;
        }

        private Channel ConvertChannel(ChannelResource channel)
        {
            var versionRange = String.Empty;
            if (channel.Rules.Any())
            {
                versionRange = channel.Rules[0].VersionRange;
            }
            return new Channel {Id = channel.Id, Name = channel.Name, VersionRange = versionRange};
        }

        private ProjectGroup ConvertProjectGroup(ProjectGroupResource projectGroup)
        {
            return new ProjectGroup() {Id = projectGroup.Id, Name = projectGroup.Name};
        }

        private async Task<Release> ConvertRelease(ReleaseResource release)
        {
            var project = await client.Repository.Projects.Get(release.ProjectId);
            var packageId = await this.GetPackageId(project);
            var packages =
                release.SelectedPackages.Select(
                    p => ConvertPackage(p, packageId == null ? string.Empty : packageId.PackageId)).ToList();
            return new Release
            {
                Id = release.Id,
                Version = release.Version,
                SelectedPackages = packages,
                DisplayPackageVersion = packages.Any() ? packages.First().Version : string.Empty
            };
        }

        private PackageStub ConvertPackage(SelectedPackage package, string packageId)
        {
            return new PackageStub {Version = package.Version, StepName = package.StepName, Id = packageId};
        }

        private PackageStub ConvertPackage(PackageResource package, string stepName)
        {
            return new PackageStub {Id = package.PackageId, Version = package.Version, StepName = stepName};
        }

        private class PackageIdResult
        {
            public string PackageId { get; set; }
            public string StepName { get; set; }
        }
    }
}