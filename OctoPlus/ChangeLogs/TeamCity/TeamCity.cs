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
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using OctoPlus.Configuration.Interfaces;
using OctoPlus.Dtos;
using OctoPlus.Utilities;

namespace OctoPlus.ChangeLogs.TeamCity
{
    internal class TeamCity : IChangeLogProvider
    {
        private readonly IConfiguration _configuration;
        private readonly IWebRequestHelper _webRequestHelper;
        private readonly Regex _buildIdRegex;
        private readonly Regex _issueIdRegex;
        private ILogger<TeamCity> _log;

        public TeamCity(IConfiguration configuration, IWebRequestHelper webRequestHelper, ILogger<TeamCity> log)
        {
            this._configuration = configuration;
            this._webRequestHelper = webRequestHelper;
            this._log = log;
            this._buildIdRegex = new Regex(configuration.ChangeProviderConfiguration.BuildIdFormat,
                RegexOptions.Compiled);
            if (!string.IsNullOrEmpty(configuration.ChangeProviderConfiguration.IssueFormat))
            {
                this._log.Info("Compiling the following regex: " + configuration.ChangeProviderConfiguration.IssueFormat);
                this._issueIdRegex = new Regex(configuration.ChangeProviderConfiguration.IssueFormat);
            }
        }

        public bool CanProvideChangeTracking(PackageFull fromPackageStub, PackageFull toPackageStub)
        {
            return CanProvideChangeTracking(fromPackageStub) && CanProvideChangeTracking(toPackageStub);
        }

        public bool CanProvideChangeTracking(PackageFull fromPackage)
        {
            return !string.IsNullOrEmpty(GetBuildId(fromPackage));
        }

        private string GetBuildId(PackageFull package)
        {
            this._log.Info("Been asked to find the build id in a package: " + package.Id);
            var found = _buildIdRegex.Match(package.Message);
            if (found.Success)
            {
                var foundGroup = found.Groups["buildid"];
                if (foundGroup != null && foundGroup.Success && !string.IsNullOrEmpty(foundGroup.Value))
                {
                    this._log.Info("Found the build id: " + foundGroup.Value);
                    return foundGroup.Value;
                }
            }
            this._log.Info("Couldn't find the build id in package: " + package.Id);
            return string.Empty;
        }

        public ChangeLogCollection GetChanges(PackageFull toPackage, Project project)
        {
            this._log.Info("Getting build id for " + toPackage.Id);
            var toBuildId = GetBuildId(toPackage);
            if (!string.IsNullOrEmpty(toBuildId))
            {
                this._log.Info("Build id found for package " + toPackage.Id + ". Fetching build");
                var build = this._webRequestHelper.GetXmlWebRequestWithBasicAuth<ChangeBuilds>(
                    _configuration.ChangeProviderConfiguration.BaseUrl +
                    "/app/rest/builds/?locator=id:" + toBuildId,
                    _configuration.ChangeProviderConfiguration.Username,
                    _configuration.ChangeProviderConfiguration.Password).Builds.FirstOrDefault();

                this._log.Info("Getting latest change for build id" + toBuildId);
                var toBuildChange = GetChangeFromBuildId(toBuildId, build);

                if (toBuildChange != null && build != null)
                {
                    this._log.Info("found the latest change: " + toBuildChange.Id + ", getting change list");
                    var document =
                        this._webRequestHelper.GetXmlWebRequestWithBasicAuth<ChangeList>(
                            _configuration.ChangeProviderConfiguration.BaseUrl +
                            "/app/rest/changes?locator=sinceChange:(id:" + (toBuildChange.Id - 50) + "),buildType:(id:" +
                            build.BuildTypeId + ")",
                            _configuration.ChangeProviderConfiguration.Username,
                            _configuration.ChangeProviderConfiguration.Password);
                    this._log.Info("build found for package " + toPackage.Id);
                    var changes = GetChangeLogChanges(document, toBuildChange);
                    return new ChangeLogCollection
                    {
                        Project = project,
                        Changes = changes
                    };
                }
                else
                {
                    this._log.Info("couldn't get the latest change for build " + toBuildId);
                }
            }
            else
            {
                this._log.Info("Couldn't find build for " + toPackage.Id);
            }
            return new ChangeLogCollection
            {
                Project = project,
                Changes = new List<ChangeLogChange>()
            };
        }

        public ChangeLogCollection GetChanges(PackageFull fromPackage, PackageFull toPackage, Project project)
        {
            this._log.Info("Going to get changes between build " + fromPackage.Id + " and " + toPackage.Id + " in project " + project.ProjectId);
            var fromBuildId = GetBuildId(fromPackage);
            var toBuildId = GetBuildId(toPackage);
            if (!string.IsNullOrEmpty(fromBuildId) && !string.IsNullOrEmpty(toBuildId))
            {
                this._log.Info("Managed to get both build ids from packages. From: " + fromBuildId + " to: " + toBuildId);
                var build = GetChangeBuild(fromBuildId);
                var toBuild = GetChangeBuild(fromBuildId);

                var fromBuildChange = GetChangeFromBuildId(fromBuildId, build);

                var toBuildChange = GetChangeFromBuildId(toBuildId, toBuild);

                if (toBuildChange != null && fromBuildChange != null && build != null)
                {
                    this._log.Info("Managed to fetch both builds and their latest changes. Going to get the list of changes between builds.");
                    var document =
                        this._webRequestHelper.GetXmlWebRequestWithBasicAuth<ChangeList>(
                            _configuration.ChangeProviderConfiguration.BaseUrl +
                            "/app/rest/changes?locator=sinceChange:(id:" + fromBuildChange.Id + "),buildType:(id:" +
                            build.BuildTypeId + ")",
                            _configuration.ChangeProviderConfiguration.Username,
                            _configuration.ChangeProviderConfiguration.Password);

                    var changes = GetChangeLogChanges(document, toBuildChange);
                    return new ChangeLogCollection
                    {
                        Project = project,
                        Changes = changes
                    };
                }

            }
            else
            {
                this._log.Info("Couldn't get the build id for the packages");
            }
            return new ChangeLogCollection
            {
                Project = project,
                Changes = new List<ChangeLogChange>()
            };
        }

        private ChangeBuild GetChangeBuild(string fromBuildId)
        {
            this._log.Info("Been asked to get the build with this id " + fromBuildId);
            var build = this._webRequestHelper.GetXmlWebRequestWithBasicAuth<ChangeBuilds>(
                _configuration.ChangeProviderConfiguration.BaseUrl +
                "/app/rest/builds/?locator=id:" + fromBuildId,
                _configuration.ChangeProviderConfiguration.Username,
                _configuration.ChangeProviderConfiguration.Password).Builds.FirstOrDefault();
            if (build != null)
            {
                this._log.Info("Found build with this id " + fromBuildId);
            }
            else
            {
                this._log.Info("couldn't find build with this id " + fromBuildId);
            }
            return build;
        }

        private List<ChangeLogChange> GetChangeLogChanges(ChangeList document, Change toBuildChange)
        {
            this._log.Info("Building list of changes up to build " + toBuildChange.Id);
            var changes = new List<ChangeLogChange>();
            foreach (var current in document.Changes.Where(c => c.Id <= toBuildChange.Id))
            {
                this._log.Info("Processing " + current.Id);
                var change = this._webRequestHelper.GetXmlWebRequestWithBasicAuth<Change>(
                    _configuration.ChangeProviderConfiguration.BaseUrl + current.Href,
                    _configuration.ChangeProviderConfiguration.Username,
                    _configuration.ChangeProviderConfiguration.Password);
                var changeLogChange = new ChangeLogChange
                {
                    Id = change.Id.ToString(),
                    Version = change.Version,
                    Comment = change.Comment,
                    Date = DateTime.ParseExact
                    (change.Date,
                        "yyyyMMdd'T'HHmmsszzz",
                        CultureInfo.InvariantCulture),
                    Username = change.Username,
                    WebUrl = change.WebUrl
                };
                changes.Add(changeLogChange);
                if (this._issueIdRegex != null)
                {
                    this._log.Info("Scanning for issue ids in " + current.Id);
                    var matches = this._issueIdRegex.Matches(changeLogChange.Comment);
                    foreach (Match match in matches)
                    {
                        var issueId = match.Groups["issueid"];
                        if (!string.IsNullOrEmpty(issueId?.Value))
                        {
                            this._log.Info("Found an issue id in " + current.Id + " with value " + issueId.Value);
                            changeLogChange.Issues.Add(new Issue
                            {
                                Name = issueId.Value,
                                WebUrl =
                                    this._configuration.ChangeProviderConfiguration.IssueReplacementUrl.Replace(
                                        "{issueid}", issueId.Value)
                            });
                        }
                    }
                }
            }
            this._log.Info("Done, found " + changes.Count + " changes");
            return changes;
        }

        private Change GetChangeFromBuildId(string toBuildId, ChangeBuild build)
        {
            this._log.Info("Been asked to find the latest change for build " + toBuildId);
            var toBuildChange = InnerToBuildChange(toBuildId);

            if (toBuildChange == null)
            {
                this._log.Info("Couldn't find a change in the latest build, so going to fetch the build history and search until I find the latest.");
                //Couldn't find any changes in this build, but we need a build id. Going to have to find the last change
                var builds = this._webRequestHelper.GetXmlWebRequestWithBasicAuth<ChangeBuilds>(
                _configuration.ChangeProviderConfiguration.BaseUrl +
                "/app/rest/builds/?locator=buildType:" + build.BuildTypeId,
                _configuration.ChangeProviderConfiguration.Username,
                _configuration.ChangeProviderConfiguration.Password);

                foreach (var foundBuild in builds.Builds.Where(b => long.Parse(b.Id) <= long.Parse(toBuildId)))
                {
                    this._log.Info("Searching in build " + foundBuild.Id);
                    toBuildChange = InnerToBuildChange(foundBuild.Id);
                    if (toBuildChange != null)
                    {
                        this._log.Info("Found a change in " + foundBuild.Id);
                        break;
                    }
                }
            }
            return toBuildChange;
        }

        private Change InnerToBuildChange(string toBuildId)
        {
            this._log.Info("Been asked to fetch a single change with id " + toBuildId);
            var toBuildChange = this._webRequestHelper.GetXmlWebRequestWithBasicAuth<ChangeList>(
                _configuration.ChangeProviderConfiguration.BaseUrl +
                "/app/rest/changes?locator=build:(id:" + toBuildId + ")",
                _configuration.ChangeProviderConfiguration.Username,
                _configuration.ChangeProviderConfiguration.Password).Changes.FirstOrDefault();
            return toBuildChange;
        }
    }
}