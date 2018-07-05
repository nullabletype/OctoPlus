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
using OctoPlusCore.Models;
using OctoPlusCore.Models.Interfaces;
using OctoPlusCore.Logging.Interfaces;
using OctoPlusCore.Octopus.Interfaces;
using OctoPlusCore.Utilities;
using OctoPlusCore.Deployment.Resources;

namespace OctoPlusCore.Deployment {
    public class Deployer : IDeployer
    {
        private IOctopusHelper helper;
        private IConfiguration configuration;

        public Deployer(IOctopusHelper helper, IConfiguration configuration)
        {
            this.helper = helper;
            this.configuration = configuration;
        }

        public async Task<DeploymentCheckResult> CheckDeployment(EnvironmentDeployment deployment)
        {
            foreach (var project in deployment.ProjectDeployments)
            {
                var lifeCyle = await this.helper.GetLifeCycle(project.LifeCycleId);
                if (lifeCyle.Phases.Any())
                {
                    var safe = false;
                    if (lifeCyle.Phases[0].OptionalDeploymentTargetEnvironmentIds.Any())
                    {
                        if (lifeCyle.Phases[0].OptionalDeploymentTargetEnvironmentIds.Contains(deployment.EnvironmentId))
                        {
                            safe = true;
                        }
                    }
                    if (!safe && lifeCyle.Phases[0].AutomaticDeploymentTargetEnvironmentIds.Any())
                    {
                        if (lifeCyle.Phases[0].AutomaticDeploymentTargetEnvironmentIds.Contains(deployment.EnvironmentId))
                        {
                            safe = true;
                        }
                    }
                    if (!safe)
                    {
                        var phaseCheck = true;
                        var deployments = await this.helper.GetDeployments(project.ReleaseId);
                        var previousEnvs = deployments.Select(d => d.EnvironmentId);
                        foreach(var phase in lifeCyle.Phases)
                        {
                            if (!phase.Optional)
                            {
                                if(phase.MinimumEnvironmentsBeforePromotion == 0)
                                {
                                    if(!previousEnvs.All(e => phase.OptionalDeploymentTargetEnvironmentIds.Contains(e)))
                                    {
                                        phaseCheck = false;
                                        break;
                                    }
                                }
                                else
                                {
                                    if(phase.MinimumEnvironmentsBeforePromotion > previousEnvs.Intersect(phase.OptionalDeploymentTargetEnvironmentIds).Count()){
                                        phaseCheck = false;
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                continue;
                            }
                        }
                        safe = phaseCheck;
                    }
                    if (!safe)
                    {

                        return new DeploymentCheckResult {
                            Success = false,
                            ErrorMessage = DeploymentStrings.FailedValidation.Replace("{{projectname}}", project.ProjectName).Replace("{{environmentname}}", deployment.EnvironmentName)
                        };
                    }
                }
            }
            return new DeploymentCheckResult { Success = true };
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
            var failedProjects = new Dictionary<ProjectDeployment, TaskDetails>();

            var taskRegister = new Dictionary<string, TaskDetails>();

            foreach (var project in deployment.ProjectDeployments) {
                Release result;
                if (string.IsNullOrEmpty(project.ReleaseId))
                {
                    uiLogger.WriteLine("Creating a release for project " + project.ProjectName + "... ");
                    result = await helper.CreateRelease(project);
                }
                else
                {
                    uiLogger.WriteLine("Fetching existing release for project " + project.ProjectName + "... ");
                    result = await helper.GetRelease(project.ReleaseId);
                }
                uiLogger.WriteLine("Complete: " + StandardSerialiser.SerializeToJsonNet(result, true));

                uiLogger.WriteLine("Deploying " + result.Version + " to " + deployment.EnvironmentName);
                var deployResult = await helper.CreateDeploymentTask(project, deployment.EnvironmentId, result.Id);
                uiLogger.WriteLine("Complete: " + StandardSerialiser.SerializeToJsonNet(deployResult, true));

                var taskDeets = await helper.GetTaskDetails(deployResult.TaskId);
                taskDeets = await StartDeployment(uiLogger, taskDeets, !deployment.DeployAsync);
                if (deployment.DeployAsync) {
                    taskRegister.Add(taskDeets.TaskId, taskDeets);
                } else {
                    if (taskDeets.State == TaskDetails.TaskState.Failed) {
                        uiLogger.WriteLine("Failed deploying " + project.ProjectName);
                        failedProjects.Add(project, taskDeets);
                    }
                    uiLogger.WriteLine("Deployed!");
                    uiLogger.WriteLine("Full Log: " + System.Environment.NewLine +
                                     await this.helper.GetTaskRawLog(taskDeets.TaskId));
                    taskDeets = await helper.GetTaskDetails(deployResult.TaskId);
                }
            }

            // This needs serious improvement.
            if (deployment.DeployAsync) {
                foreach(var tasks in taskRegister) {
                    await StartDeployment(uiLogger, tasks.Value, true);
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

        private async Task<TaskDetails> StartDeployment(IUiLogger uiLogger, TaskDetails taskDeets, bool doWait) {
            do {
                WriteStatus(uiLogger, taskDeets);
                if (doWait) {
                    await Task.Delay(1000);
                }
                taskDeets = await this.helper.GetTaskDetails(taskDeets.TaskId);
            }
            while (doWait && (taskDeets.State == TaskDetails.TaskState.InProgress || taskDeets.State == TaskDetails.TaskState.Queued));
            return taskDeets;
        }

        private static void WriteStatus(IUiLogger uiLogger, TaskDetails taskDeets) {
            if (taskDeets.State != TaskDetails.TaskState.Queued) {
                if (taskDeets.PercentageComplete < 100) {
                    uiLogger.WriteLine("Current status: " + taskDeets.State + " Percentage: " +
                                     taskDeets.PercentageComplete+ " estimated time remaining: " +
                                     taskDeets.TimeLeft);
                }
            } else {
                uiLogger.WriteLine("Currently queued... waiting");
            }
        }
    }
}