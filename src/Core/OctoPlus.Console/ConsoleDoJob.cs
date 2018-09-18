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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OctoPlus.Console.Interfaces;
using OctoPlusCore.Deployment.Interfaces;
using OctoPlusCore.Models;
using OctoPlusCore.Logging.Interfaces;
using OctoPlusCore.Octopus.Interfaces;
using OctoPlusCore.Utilities;

namespace OctoPlus.Console {
    public class ConsoleDoJob : IUiLogger, IConsoleDoJob
    {
        private IDeployer _deployer;
        private readonly IOctopusHelper _helper;

        public ConsoleDoJob(IOctopusHelper helper, IDeployer deployer)
        {
            this._helper = helper;
            this._deployer = deployer;
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
                            this._helper.GetProject(project.ProjectId, job.EnvironmentId,
                                project.ChannelVersionRange);
                    foreach (var package in project.Packages)
                    {
                        if (package.PackageId == "latest")
                        {
                            var packages =
                                await this._helper.GetPackages(octoProject.ProjectId, project.ChannelVersionRange);
                            package.PackageId = packages.First().SelectedPackage.Id;
                            package.PackageName = packages.First().SelectedPackage.Version;
                            package.StepName = packages.First().SelectedPackage.StepName;
                        }
                    }
                    if (!forceDeploymentIfSamePackage)
                    {
                        var currentRelease = await this._helper.GetReleasedVersion(project.ProjectId, job.EnvironmentId);
                        if (currentRelease != null && !string.IsNullOrEmpty(currentRelease.Id)) 
                        {
                            var release = await this._helper.GetRelease(currentRelease.Id);
                            var currentPackage = release.SelectedPackages[0];
                            if (project.Packages.All(p => release.SelectedPackages.Any(s => p.StepName == p.StepName && s.Version == p.PackageName)))
                            {
                                continue;
                            }
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

                await this._deployer.StartJob(job, this, true);
            }
            catch (Exception e)
            {
                this.WriteLine("Couldn't deploy! " + e.Message + e.StackTrace);
            }
        }

        public void WriteLine(string toWrite)
        {
            System.Console.WriteLine(toWrite);
        }
    }
}