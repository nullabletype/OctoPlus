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


using System.Collections.Generic;
using System.Threading.Tasks;
using Octopus.Client.Model;
using OctoPlusCore.Models;
using Microsoft.Extensions.Caching.Memory;

namespace OctoPlusCore.Octopus.Interfaces
{
    public interface IOctopusHelper
    {
        void SetCacheImplementation(IMemoryCache cache, int cacheTimeout);
        Task<IList<PackageStep>> GetPackages(string projectIdOrHref, string versionRange);
        Task<Release> GetReleasedVersion(string projectId, string envId);
        bool Search(DeploymentResource deploymentResource, string projectId, string envId);
        Task<List<Environment>> GetEnvironments();
        Task<List<Environment>> GetMatchingEnvironments(string keyword);
        Task<Environment> GetEnvironment(string idOrName);
        Task<Channel> GetChannelByName(string projectIdOrName, string channelName);
        Task<Channel> GetChannel(string channelIdOrName);
        Task<List<Channel>> GetChannelsForProject(string projectIdOrHref);
        Task<Project> GetProjectByName(string name, string environment, string channelRange);
        Task<Channel> GetChannelByProjectNameAndChannelName(string name, string channelName);
        Task<List<Channel>> GetChannelsByProjectName(string name);
        Task<List<ProjectGroup>> GetFilteredProjectGroups(string filter);
        Task<List<ProjectGroup>> GetProjectGroups();
        Task<List<Project>> GetProjects(string environment, string channelRange);
        Task<Project> GetProject(string idOrHref, string environment, string channelRange);
        Task<Release> GetRelease(string releaseIdOrHref);
        Task<TaskDetails> GetTaskDetails(string taskId);
        Task<IEnumerable<TaskStub>> GetDeploymentTasks(int skip, int take);
        Task<string> GetTaskRawLog(string taskId);
        Task<Release> CreateRelease(ProjectDeployment project);
        Task<Deployment> CreateDeploymentTask(ProjectDeployment project, string environmentId, string releaseId);
        Task<bool> ValidateProjectName(string name);
        Task<PackageFull> GetFullPackage(PackageStub stub);
        Task<List<ProjectStub>> GetProjectStubs();
        Task<Project> ConvertProject(ProjectStub project, string env, string channelRange);
        Task<LifeCycle> GetLifeCycle(string idOrHref);
        Task<IEnumerable<Deployment>> GetDeployments(string releaseId);
        Task<(string error, bool success)> RenameRelease(string releaseId, string newReleaseVersion);
    }
}