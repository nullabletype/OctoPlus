using OctoPlusCore.Deployment.Interfaces;
using OctoPlusCore.Interfaces;
using OctoPlusCore.JobRunners.JobConfigs;
using OctoPlusCore.Language;
using OctoPlusCore.Logging.Interfaces;
using OctoPlusCore.Models;
using OctoPlusCore.Octopus.Interfaces;
using OctoPlusCore.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OctoPlusCore.JobRunners
{
    public class DeployRunner
    {
        private ILanguageProvider _languageProvider;
        private IOctopusHelper helper;
        private IDeployer deployer;
        private IUiLogger uiLogger;

        private DeployConfig _currentConfig;
        private IProgressBar progressBar;

        public DeployRunner(ILanguageProvider languageProvider, IOctopusHelper helper, IDeployer deployer, IUiLogger uiLogger)
        {
            this._languageProvider = languageProvider;
            this.helper = helper;
            this.deployer = deployer;
            this.uiLogger = uiLogger;
        }

        public async Task<int> Run(DeployConfig config, IProgressBar progressBar, List<ProjectStub> projectStubs, Func<DeployConfig, List<Project>, IEnumerable<int>> setDeploymentProjects, Func<string, string> userPrompt, Func<string, string> promptForReleaseName)
        {
            _currentConfig = config;
            this.progressBar = progressBar;
            var groupIds = new List<string>();

            if (!string.IsNullOrEmpty(_currentConfig.GroupFilter))
            {
                progressBar.WriteStatusLine(_languageProvider.GetString(LanguageSection.UiStrings, "GettingGroupInfo"));
                groupIds =
                    (await helper.GetFilteredProjectGroups(_currentConfig.GroupFilter))
                    .Select(g => g.Id).ToList();
            }

            progressBar.CleanCurrentLine();
            var projects = await ConvertProjectStubsToProjects(projectStubs, groupIds);
            progressBar.CleanCurrentLine();

            var deployment = await GenerateDeployment(projects, setDeploymentProjects);
            var result = await this.deployer.CheckDeployment(deployment);

            if (!result.Success)
            {
                Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "Error") + result.ErrorMessage);
                return -1;
            }

            SetReleaseName(promptForReleaseName, deployment);

            deployer.FillRequiredVariables(deployment.ProjectDeployments, userPrompt, _currentConfig.RunningInteractively);

            deployment.FallbackToDefaultChannel = _currentConfig.FallbackToDefaultChannel;

            if (!string.IsNullOrEmpty(_currentConfig.SaveProfile))
            {
                SaveProfile(deployment);
            }
            else
            {
                await this.deployer.StartJob(deployment, this.uiLogger);
            }

            return 0;
        }

        private void SetReleaseName(Func<string, string> promptForReleaseName, EnvironmentDeployment deployment)
        {
            var releaseName = _currentConfig.ReleaseName;

            if (_currentConfig.RunningInteractively && string.IsNullOrEmpty(_currentConfig.ReleaseName))
            {
                releaseName = promptForReleaseName(_languageProvider.GetString(LanguageSection.UiStrings, "ReleaseNamePrompt"));
            }

            if (!string.IsNullOrEmpty(releaseName))
            {
                foreach (var project in deployment.ProjectDeployments)
                {
                    project.ReleaseVersion = releaseName;
                }
            }
        }

        private async Task<EnvironmentDeployment> GenerateDeployment(List<Project> projects, Func<DeployConfig, List<Project>, IEnumerable<int>> setDeploymentProjects)
        {
            EnvironmentDeployment deployment;

            var indexes = new List<int>();

            if (_currentConfig.RunningInteractively)
            {                
                indexes.AddRange(setDeploymentProjects(_currentConfig, projects));
                if (!indexes.Any())
                {
                    System.Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "NothingSelected"));
                    return null;
                }
            }
            else
            {
                for (int i = 0; i < projects.Count(); i++)
                {
                    if (projects[i].Checked)
                    {
                        indexes.Add(i);
                    }
                }
            }

            deployment = await PrepareEnvironmentDeployment(projects, indexes);

            return deployment;
        }

        private async Task<List<Project>> ConvertProjectStubsToProjects(List<ProjectStub> projectStubs, List<string> groupIds)
        {
            var projects = new List<Project>();

            foreach (var projectStub in projectStubs)
            {
                progressBar.WriteProgress(projectStubs.IndexOf(projectStub) + 1, projectStubs.Count(),
                    String.Format(_languageProvider.GetString(LanguageSection.UiStrings, "LoadingInfoFor"), projectStub.ProjectName));
                if (!string.IsNullOrEmpty(_currentConfig.GroupFilter))
                {
                    if (!groupIds.Contains(projectStub.ProjectGroupId))
                    {
                        continue;
                    }
                }

                var project = await helper.ConvertProject(projectStub, _currentConfig.Environment.Id, _currentConfig.Channel.VersionRange, _currentConfig.Channel.VersionTag);
                var currentPackages = project.CurrentRelease.SelectedPackages;
                project.Checked = false;
                if (project.SelectedPackageStubs != null)
                {
                    foreach (var package in project.AvailablePackages)
                    {
                        var stub = package.SelectedPackage;
                        if (stub == null)
                        {
                            if (_currentConfig.DefaultFallbackChannel != null)
                            {
                                project = await helper.ConvertProject(projectStub, _currentConfig.Environment.Id, _currentConfig.DefaultFallbackChannel.VersionRange, _currentConfig.DefaultFallbackChannel.VersionTag);
                                stub = project.AvailablePackages.FirstOrDefault(p => p.StepId == package.StepId).SelectedPackage;
                            }
                        }
                        var matchingCurrent = currentPackages.FirstOrDefault(p => p.StepId == package.StepId);
                        if (matchingCurrent != null && stub != null)
                        {
                            project.Checked = matchingCurrent.Version != stub.Version;
                            break;
                        }
                        else
                        {
                            if (stub == null)
                            {
                                project.Checked = false;
                            }
                            project.Checked = true;
                            break;
                        }
                    }
                }

                projects.Add(project);
            }

            return projects;
        }

        private void SaveProfile(EnvironmentDeployment deployment)
        {
            foreach (var project in deployment.ProjectDeployments)
            {
                foreach (var package in project.Packages)
                {
                    package.PackageId = "latest";
                    package.PackageName = "latest";
                }
            }
            var content = StandardSerialiser.SerializeToJsonNet(deployment, true);
            File.WriteAllText(_currentConfig.SaveProfile, content);
            Console.WriteLine(string.Format(_languageProvider.GetString(LanguageSection.UiStrings, "ProfileSaved"), _currentConfig.SaveProfile));
        }

        private async Task<EnvironmentDeployment> PrepareEnvironmentDeployment(IList<Project> projects, IEnumerable<int> indexes = null)
        {
            var deployment = new EnvironmentDeployment
            {
                ChannelName = _currentConfig.Channel.Name,
                DeployAsync = true,
                EnvironmentId = _currentConfig.Environment.Id,
                EnvironmentName = _currentConfig.Environment.Name
            };

            var count = 0;

            if (_currentConfig.ForceRedeploy)
            {
                var projectsWithAvailablePackages = projects.Where(p => p.AvailablePackages.Any());
                foreach (var project in projectsWithAvailablePackages)
                {
                    progressBar.WriteProgress(count++, projectsWithAvailablePackages.Count(), string.Format(_languageProvider.GetString(LanguageSection.UiStrings, "BuildingDeploymentJob"), project.ProjectName));
                    deployment.ProjectDeployments.Add(await GenerateProjectDeployment(project));
                }
            }
            else
            {
                foreach (var index in indexes)
                {
                    var current = projects[index];

                    if (current.AvailablePackages.Any())
                    {
                        progressBar.WriteProgress(count++, indexes.Count(), string.Format(_languageProvider.GetString(LanguageSection.UiStrings, "BuildingDeploymentJob"), projects[index].ProjectName));
                        deployment.ProjectDeployments.Add(await GenerateProjectDeployment(current));
                    }
                }
            }

            progressBar.CleanCurrentLine();

            return deployment;
        }

        private async Task<ProjectDeployment> GenerateProjectDeployment(Project current)
        {

            if (current.AvailablePackages == null)
            {
                System.Console.WriteLine($"No packages found for {current.ProjectName}");
            }

            var projectChannel = await this.helper.GetChannelByName(current.ProjectId, _currentConfig.Channel.Name);
            if (_currentConfig.DefaultFallbackChannel != null && projectChannel == null)
            {
                projectChannel = await this.helper.GetChannelByName(current.ProjectId, _currentConfig.DefaultFallbackChannel.Name);
            } 
            return new ProjectDeployment
            {
                ProjectId = current.ProjectId,
                ProjectName = current.ProjectName,
                Packages = current.AvailablePackages.Where(x => x.SelectedPackage != null).Select(x => new PackageDeployment
                {
                    PackageId = x.SelectedPackage.Id,
                    PackageName = x.SelectedPackage.Version,
                    StepId = x.StepId,
                    StepName = x.StepName
                }).ToList(),
                ChannelId = projectChannel?.Id,
                ChannelVersionRange = projectChannel?.VersionRange,
                ChannelVersionTag = projectChannel?.VersionTag,
                ChannelName = projectChannel?.Name,
                LifeCycleId = current.LifeCycleId,
                RequiredVariables = current?.RequiredVariables?.Select(r => new RequiredVariableDeployment { Id = r.Id, ExtraOptions = r.ExtraOptions, Name = r.Name, Type = r.Type, Value = r.Value }).ToList()
            };
        }
    }
}
