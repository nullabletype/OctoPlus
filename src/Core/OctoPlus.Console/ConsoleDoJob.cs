﻿#region copyright
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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OctoPlus.Console.Interfaces;
using OctoPlusCore.Deployment.Interfaces;
using OctoPlusCore.Models;
using OctoPlusCore.Logging.Interfaces;
using OctoPlusCore.Octopus.Interfaces;
using OctoPlusCore.Utilities;
using OctoPlus.Console.ConsoleTools;
using System.Text;

namespace OctoPlus.Console {
    public class ConsoleDoJob : IUiLogger, IConsoleDoJob
    {
        private IDeployer deployer;
        private readonly IOctopusHelper helper;
        private IProgressBar progressBar;

        public ConsoleDoJob(IOctopusHelper helper, IDeployer deployer, IProgressBar progressBar)
        {
            this.helper = helper;
            this.deployer = deployer;
            this.progressBar = progressBar;
        }

        public async Task StartJob(string pathToProfile, string message, string releaseVersion,
            bool forceDeploymentIfSamePackage)
        {
            if (!File.Exists(pathToProfile))
            {
                this.WriteLine("Couldn't find file at " + pathToProfile);
                return;
            }
            try
            {
                var job =
                    StandardSerialiser.DeserializeFromJsonNet<EnvironmentDeployment>(File.ReadAllText(pathToProfile));

                var projects = new List<ProjectDeployment>();

                foreach (var project in job.ProjectDeployments)
                {
                    var octoProject =
                        await
                            this.helper.GetProject(project.ProjectId, job.EnvironmentId,
                                project.ChannelVersionRange, project.ChannelVersionTag);
                        var packages =
                        await this.helper.GetPackages(octoProject.ProjectId, project.ChannelVersionRange, project.ChannelVersionTag);
                    foreach (var package in project.Packages)
                    {
                        if (package.PackageId == "latest")
                        {
                            // Filter to packages specifically for this package step, then update the package versions
                            var availablePackages = packages.Where(pack => pack.StepId == package.StepId);

                            package.PackageId = availablePackages.First().SelectedPackage.Id;
                            package.PackageName = availablePackages.First().SelectedPackage.Version;
                            package.StepName = availablePackages.First().SelectedPackage.StepName;
                        }
                    }
                    if (!forceDeploymentIfSamePackage)
                    {
                        if (!await IsDeploymentRequired(job, project)) 
                        {
                            continue;
                        }
                    }
                    if (!string.IsNullOrEmpty(message))
                    {
                        project.ReleaseMessage = message;
                    }
                    if (!string.IsNullOrEmpty(releaseVersion))
                    {
                        project.ReleaseVersion = releaseVersion;
                    }
                    projects.Add(project);
                }

                job.ProjectDeployments = projects;

                await this.deployer.StartJob(job, this, true);
            }
            catch (Exception e)
            {
                this.WriteLine("Couldn't deploy! " + e.Message + e.StackTrace);
            }
        }

        private async Task<bool> IsDeploymentRequired(EnvironmentDeployment job, ProjectDeployment project)
        {
            var needsDeploy = false;
            var currentRelease = await this.helper.GetReleasedVersion(project.ProjectId, job.EnvironmentId);
            if (currentRelease != null && !string.IsNullOrEmpty(currentRelease.Id))
            {
                // Check if we have any packages that are different versions. If they're the same, we don't need to deploy.
                foreach (var package in project.Packages)
                {
                    if (!currentRelease.SelectedPackages.Any(pack => pack.StepName == package.StepName && package.PackageName == pack.Version))
                    {
                        needsDeploy = true;
                    }
                }
            }
            return needsDeploy;
        }

        public void WriteLine(string toWrite)
        {
            System.Console.WriteLine(toWrite);
        }

        public void WriteStatusLine(string status) 
        {
            this.progressBar.WriteStatusLine(status);
        }

        public void CleanCurrentLine() 
        {
            this.progressBar.CleanCurrentLine();
        }

        public void WriteProgress(int current, int total, string message) 
        {
            this.progressBar.WriteProgress(current, total, message);
        }
    }
}