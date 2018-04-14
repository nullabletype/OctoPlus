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
using System.Threading.Tasks;
using Octopus.Client;
using Octopus.Client.Model;
using OctoPlusCore.Configuration.Interfaces;
using OctoPlusCore.Deployment.Interfaces;
using OctoPlusCore.Dtos;
using OctoPlusCore.Dtos.Interfaces;
using OctoPlusCore.Logging.Interfaces;
using OctoPlusCore.Octopus.Interfaces;
using OctoPlusCore.Utilities;

namespace OctoPlusCore.Deployment {
    public class Deployer : IDeployer
    {
        private IOctopusHelper helper;
        private IOctopusAsyncClient client;
        private IConfiguration configuration;

        public Deployer(IOctopusAsyncClient client, IOctopusHelper helper, IConfiguration configuration)
        {
            this.helper = helper;
            this.client = client;
            this.configuration = configuration;
        }

        public async Task StartJob(IOctoJob job, IUiLogger uiLogger, bool suppressMessages = false)
        {
            if (job is EnvironmentDeployment)
            {
                await this.ProcessEnvironmentDeployment((EnvironmentDeployment) job, suppressMessages, uiLogger);
            }
        }

        private async Task ProcessEnvironmentDeployment(EnvironmentDeployment deployment, bool suppressMessages,
            IUiLogger uiLogger)
        {
            uiLogger.WriteLine("Starting deployment!");
            var failedProjects = new Dictionary<ProjectDeployment, TaskDetailsResource>();


            var user = await client.Repository.Users.GetCurrent();
            uiLogger.WriteLine("Deploying as username " + user.Username);
            var taskRegister = new Dictionary<TaskResource, TaskDetailsResource>();

            foreach (var project in deployment.ProjectDeployments) {
                uiLogger.WriteLine("Creating a release for project " + project.ProjectName + "... ");
                var result =
                    await
                        client.Repository.Releases.Create(new ReleaseResource {
                            Assembled = DateTimeOffset.UtcNow,
                            ChannelId = project.ChannelId,
                            LastModifiedBy = user.Username,
                            LastModifiedOn = DateTimeOffset.UtcNow,
                            ProjectId = project.ProjectId,
                            ReleaseNotes = project.ReleaseMessage ?? string.Empty,
                            Version = project.ReleaseVersion ?? project.PackageName.Split('.')[0] + ".i",
                            SelectedPackages =
                                new List<SelectedPackage>
                                {
                                    new SelectedPackage {Version = project.PackageName, StepName = project.StepName}
                                },
                        });
                uiLogger.WriteLine("Complete: " + StandardSerialiser.SerializeToJsonNet(result, true));
                uiLogger.WriteLine("Deploying " + result.Version + " to " + deployment.EnvironmentName);
                var deployResult = await client.Repository.Deployments.Create(new DeploymentResource {
                    ChannelId = project.ChannelId,
                    Comments = "Initiated by OctoPlus",
                    Created = DateTimeOffset.UtcNow,
                    EnvironmentId = deployment.EnvironmentId,
                    LastModifiedBy = user.Username,
                    LastModifiedOn = DateTimeOffset.UtcNow,
                    Name = project.ProjectName + ":" + project.PackageName,
                    ProjectId = project.ProjectId,
                    ReleaseId = result.Id
                });
                uiLogger.WriteLine("Complete: " + StandardSerialiser.SerializeToJsonNet(deployResult, true));
                var task = await client.Repository.Tasks.Get(deployResult.TaskId);
                var taskDeets = await client.Repository.Tasks.GetDetails(task);
                taskDeets = await StartDeployment(uiLogger, task, taskDeets, !deployment.DeployAsync);
                if (deployment.DeployAsync) {
                    taskRegister.Add(task, taskDeets);
                } else {
                    if (taskDeets.Task.State == TaskState.Failed) {
                        uiLogger.WriteLine("Failed deploying " + project.ProjectName);
                        failedProjects.Add(project, taskDeets);
                        var webLink = taskDeets.Links["Web"];
                    }
                    uiLogger.WriteLine("Deployed!");
                    uiLogger.WriteLine("Full Log: " + System.Environment.NewLine +
                                     await client.Repository.Tasks.GetRawOutputLog(task));
                    taskDeets = await client.Repository.Tasks.GetDetails(task);
                }
            }

            // This needs serious improvement.
            if (deployment.DeployAsync) {
                foreach(var tasks in taskRegister) {
                    await StartDeployment(uiLogger, tasks.Key, tasks.Value, true);
                }
            }

            uiLogger.WriteLine("Done deploying!");
            if (failedProjects.Any())
            {
                uiLogger.WriteLine("Some projects didn't deploy successfully: ");
                foreach (var failure in failedProjects)
                {
                    var link = string.Empty;
                    if (failure.Value.Links != null)
                    {
                        if (failure.Value.Links.ContainsKey("Web"))
                        {
                            link = configuration.OctopusUrl + failure.Value.Links["Web"];
                        }
                    }
                    uiLogger.WriteLine(failure.Key.ProjectName + ": " + link);
                }
            }
            if (!suppressMessages)
            {
                uiLogger.WriteLine("Done deploying!" +
                                (failedProjects.Any() ? " There were failures though. Check the log." : string.Empty));
            }
        }

        private async Task<TaskDetailsResource> StartDeployment(IUiLogger uiLogger, TaskResource task, TaskDetailsResource taskDeets, bool doWait) {
            do {
                WriteStatus(uiLogger, taskDeets);
                if (doWait) {
                    await Task.Delay(1000);
                }
                taskDeets = await client.Repository.Tasks.GetDetails(task);
            }
            while (doWait && (taskDeets.Task.State == TaskState.Executing || taskDeets.Task.State == TaskState.Queued));
            return taskDeets;
        }

        private static void WriteStatus(IUiLogger uiLogger, TaskDetailsResource taskDeets) {
            if (taskDeets.Task.State != TaskState.Queued) {
                if (taskDeets.Progress.ProgressPercentage < 100) {
                    uiLogger.WriteLine("Current status: " + taskDeets.Task.State + " Percentage: " +
                                     taskDeets.Progress.ProgressPercentage + " estimated time remaining: " +
                                     taskDeets.Progress.EstimatedTimeRemaining);
                    if (taskDeets.ActivityLogs != null) {
                        foreach (var activity in taskDeets.ActivityLogs) {
                            uiLogger.WriteLine("Activity " + activity.Name + " Is in state " +
                                             activity.Status + " : " + activity.ProgressPercentage + "%");
                            uiLogger.WriteLine("Message: " + activity.ProgressMessage);
                        }
                    }
                }
            } else {
                uiLogger.WriteLine("Currently queued... waiting");
            }
        }
    }
}