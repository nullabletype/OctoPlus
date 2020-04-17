using OctoPlusCore.Interfaces;
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
    public class ChannelsRunner
    {
        private IProgressBar progressBar;
        private IOctopusHelper octopusHelper;
        private ILanguageProvider languageProvider;
        private IUiLogger uiLogger;

        public ChannelsRunner(IProgressBar progressBar, IOctopusHelper octopusHelper, ILanguageProvider languageProvider, IUiLogger uiLogger)
        {
            this.octopusHelper = octopusHelper;
            this.progressBar = progressBar;
            this.languageProvider = languageProvider;
            this.uiLogger = uiLogger;
        }

        public async Task<bool> Cleanup(ChannelCleanupConfig config)
        {
            List<(string ProjectId, string ProjectName, string ChannelId, string ChannelName)> toDelete = new List<(string ProjectId, string ProjectName, string ChannelId, string ChannelName)>();

            if (!string.IsNullOrEmpty(config.GroupFilter))
            {
                var groupIds = (await octopusHelper.GetFilteredProjectGroups(config.GroupFilter)).Select(g => g.Id);
                var projectStubs = await octopusHelper.GetProjectStubs();

                foreach (var projectStub in projectStubs)
                {
                    progressBar.WriteProgress(projectStubs.IndexOf(projectStub) + 1, projectStubs.Count(),
                                        String.Format(languageProvider.GetString(LanguageSection.UiStrings, "LoadingInfoFor"), projectStub.ProjectName));
                    if (!string.IsNullOrEmpty(config.GroupFilter))
                    {
                        if (!groupIds.Contains(projectStub.ProjectGroupId))
                        {
                            continue;
                        }
                    }

                    var channels = await octopusHelper.GetChannelsForProject(projectStub.ProjectId, 9999);
                    var packageSteps = await octopusHelper.GetPackages(projectStub.ProjectId, null, null, 9999);
                    var packages = packageSteps.SelectMany(p => p.AvailablePackages);

                    foreach (var channel in channels)
                    {
                        if (!packages.Any(p => channel.ValidateVersion(p.Version)))
                        {
                            toDelete.Add((projectStub.ProjectId, projectStub.ProjectName, channel.Id, channel.Name));
                        }
                    }
                }
            }

            List<(string ProjectName, string ChannelName, IEnumerable<Release> Releases)> failed = new List<(string ProjectName, string ChannelName, IEnumerable<Release> Releases)>();

            foreach (var current in toDelete)
            {
                var message = "";
                if (config.TestMode)
                {
                    message += languageProvider.GetString(LanguageSection.UiStrings, "Test") + " ";
                }
                message += String.Format(languageProvider.GetString(LanguageSection.UiStrings, "RemovingChannel"), current.ChannelName, current.ProjectName);

                uiLogger.WriteLine(message);

                if (!config.TestMode)
                {
                    var result = await octopusHelper.RemoveChannel(current.ChannelId);
                    if (!result.Success)
                    {
                        failed.Add((current.ProjectName, current.ChannelName, result.Releases));
                    }
                }

            }

            foreach(var current in failed)
            {
                uiLogger.WriteLine(String.Format(languageProvider.GetString(LanguageSection.UiStrings, "CouldntRemoveChannelReleases"), current.ChannelName, current.ProjectName, string.Join(',', current.Releases.Select(r => r.Version))));
            }

            return !failed.Any();
        }

    }
}
