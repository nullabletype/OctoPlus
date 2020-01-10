using OctoPlusCore.Deployment.Interfaces;
using OctoPlusCore.Interfaces;
using OctoPlusCore.JobRunners.Interfaces;
using OctoPlusCore.JobRunners.JobConfigs;
using OctoPlusCore.Language;
using OctoPlusCore.Logging.Interfaces;
using OctoPlusCore.Models;
using OctoPlusCore.Octopus.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctoPlusCore.JobRunners
{
    public class PromotionRunner
    {
        private readonly IJobRunner _jobRunner;
        private ILanguageProvider _languageProvider;
        private IOctopusHelper helper;
        private IDeployer deployer;
        private IUiLogger uiLogger;

        private PromotionConfig _currentConfig;
        private IProgressBar progressBar;

        public PromotionRunner(IJobRunner jobRunner, ILanguageProvider languageProvider, IOctopusHelper helper, IDeployer deployer, IUiLogger uiLogger)
        {
            this._jobRunner = jobRunner;
            this._languageProvider = languageProvider;
            this.helper = helper;
            this.deployer = deployer;
            this.uiLogger = uiLogger;
        }

        public async Task<int> Run(PromotionConfig config, IProgressBar progressBar, Func<PromotionConfig, List<Project>, List<Project>, IEnumerable<int>> setDeploymentProjects, Func<string, string> userPrompt)
        {
            this._currentConfig = config;
            this.progressBar = progressBar;

            var (projects, targetProjects) = await GenerateProjectList();

            var indexes = setDeploymentProjects(_currentConfig, projects, targetProjects);

            var deployment = GenerateEnvironmentDeployment(indexes, projects, targetProjects);

            var deploymentOk = false;

            do
            {
                if (deployment == null)
                {
                    return -1;
                }

                var result = await this.deployer.CheckDeployment(deployment);
                if (result.Success)
                {
                    deploymentOk = true;
                }
                else
                {
                    Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "Error") + result.ErrorMessage);
                }
            } while (!deploymentOk);

            FillRequiredVariables(deployment.ProjectDeployments, userPrompt);

            await this.deployer.StartJob(deployment, this.uiLogger);

            return 0;
        }

        private EnvironmentDeployment GenerateEnvironmentDeployment(IEnumerable<int> indexes, List<Project> projects, List<Project> targetProjects)
        {
            if (!indexes.Any())
            {
                Console.WriteLine(_languageProvider.GetString(LanguageSection.UiStrings, "NothingSelected"));
                return null;
            }

            var deployment = new EnvironmentDeployment
            {
                ChannelName = string.Empty,
                DeployAsync = true,
                EnvironmentId = _currentConfig.DestinationEnvironment.Id,
                EnvironmentName = _currentConfig.DestinationEnvironment.Name
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

        private async Task<(List<Project> projects, List<Project> targetProjects)> GenerateProjectList()
        {
            var projects = new List<Project>();
            var targetProjects = new List<Project>();

            progressBar.WriteStatusLine(_languageProvider.GetString(LanguageSection.UiStrings, "FetchingProjectList"));
            var projectStubs = await helper.GetProjectStubs();

            var groupIds = new List<string>();
            if (!string.IsNullOrEmpty(_currentConfig.GroupFilter))
            {
                progressBar.WriteStatusLine(_languageProvider.GetString(LanguageSection.UiStrings, "GettingGroupInfo"));
                groupIds =
                    (await helper.GetFilteredProjectGroups(_currentConfig.GroupFilter))
                    .Select(g => g.Id).ToList();
            }

            progressBar.CleanCurrentLine();

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

                var project = await helper.ConvertProject(projectStub, _currentConfig.SourceEnvironment.Id, null, null);
                var targetProject = await helper.ConvertProject(projectStub, _currentConfig.DestinationEnvironment.Id, null, null);

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

        protected void FillRequiredVariables(List<ProjectDeployment> projects, Func<string, string> userPrompt)
        {
            foreach (var project in projects)
            {
                if (project.RequiredVariables != null)
                {
                    foreach (var requirement in project.RequiredVariables)
                    {
                        do
                        {
                            var prompt = String.Format(_languageProvider.GetString(LanguageSection.UiStrings, "VariablePrompt"), requirement.Name, project.ProjectName);
                            if (!string.IsNullOrEmpty(requirement.ExtraOptions))
                            {
                                prompt = prompt + String.Format(_languageProvider.GetString(LanguageSection.UiStrings, "VariablePromptAllowedValues"), requirement.ExtraOptions);
                            }
                            requirement.Value = userPrompt(prompt);
                        } while (_currentConfig.RunningInteractively && string.IsNullOrEmpty(requirement.Value));
                    }

                }
            }
        }
    }
}
