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

using McMaster.Extensions.CommandLineUtils;
using OctoPlus.Console.ConsoleTools;
using OctoPlus.Console.Interfaces;
using OctoPlus.Console.Resources;
using OctoPlusCore.Configuration.Interfaces;
using OctoPlusCore.Models;
using OctoPlusCore.Octopus;
using OctoPlusCore.Octopus.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctoPlus.Console.Commands {
    class Deploy : BaseCommand {

        private IConsoleDoJob consoleDoJob;
        private IOctopusHelper octoHelper;
        private IConfiguration configuration;

        public Deploy(IConsoleDoJob consoleDoJob, IConfiguration configuration, IOctopusHelper octoHelper)
        {
            this.consoleDoJob = consoleDoJob;
            this.configuration = configuration;
            this.octoHelper = octoHelper;
        }

        public async void Configure(CommandLineApplication command) 
        {
            base.Configure(command);
            command.Description = OptionsStrings.DeployProjects;

            command.Command("profile", profile => ConfigureProfileBasedDeployment(profile));
            command.Command("int", profile => ConfigureInteractiveMode(profile));

            command.OnExecute(async () =>
            {
                command.ShowHelp();
            });
        }

        private void ConfigureInteractiveMode(CommandLineApplication command)
        {
            base.Configure(command);

            command.OnExecute(async () =>
            {
                await RunInteractively(command);
            });
        }

        private async Task RunInteractively(CommandLineApplication command)
        {
            WriteStatusLine("Fetching project list");
            var projectStubs = await octoHelper.GetProjectStubs();
            var found = projectStubs.FirstOrDefault(proj => proj.ProjectName.Equals(configuration.ChannelSeedProjectName, StringComparison.CurrentCultureIgnoreCase));

            if (found == null)
            {
                System.Console.WriteLine("Provided seed project couldn't be found. I can't continue!");
                return;
            }

            var channelName = PromptForStringWithoutQuitting("Which channel do you wish to deploy?");
            var environmentName = PromptForStringWithoutQuitting("Which environment do you wish to deploy to?");
            var groupRestriction = Prompt.GetString("Do you want to restrict to certain product groups?");
            WriteStatusLine("Checking your options...");
            var matchingEnvironments = await octoHelper.GetMatchingEnvironments(environmentName);

            if (matchingEnvironments.Count() > 1)
            {
                System.Console.WriteLine("Too many enviroments match your criteria: " + string.Join(", ", matchingEnvironments.Select(e => e.Name)));
                return;
            }
            else if (matchingEnvironments.Count() == 0)
            {
                System.Console.WriteLine("No environments match your criteria!");
                return;
            }

            var groupIds = new List<string>();
            if (!string.IsNullOrEmpty(groupRestriction))
            {
                WriteStatusLine("Getting group info");
                groupIds =
                    (await octoHelper.GetFilteredProjectGroups(groupRestriction))
                    .Select(g => g.Id).ToList();
            }

            var channel = await octoHelper.GetChannelByProjectNameAndChannelName(found.ProjectName, channelName);
            var environment = await octoHelper.GetEnvironment(matchingEnvironments.First().Id);
            var projects = new List<Project>();
            CleanCurrentLine();
            foreach (var projectStub in projectStubs)
            {
                WriteProgress(projectStubs.IndexOf(projectStub) + 1, projectStubs.Count(), $"Loading Info for {(projectStub.ProjectName)}");
                if (!string.IsNullOrEmpty(groupRestriction))
                {
                    if (!groupIds.Contains(projectStub.ProjectGroupId))
                    {
                        continue;
                    }
                }
                var project = await octoHelper.ConvertProject(projectStub, environment.Id, channel.VersionRange);
                var currentPackage = project.CurrentRelease.SelectedPackages.FirstOrDefault();
                if (project.SelectedPackageStub == null ||
                        (currentPackage == null ? "" : currentPackage.Version) == project.SelectedPackageStub.Version ||
                        !project.AvailablePackages.Any())
                {
                    project.Checked = false;
                }
                projects.Add(project);
            }
            CleanCurrentLine();

            await InteractivePrompt(channel, environment, projects);
        }

        private async Task<EnvironmentDeployment> InteractivePrompt(Channel channel, OctoPlusCore.Models.Environment environment, IList<Project> projects)
        {
            bool run = true;
            while (run)
            {
                var table = new ConsoleTable("#", "*", "Project Name", "Current Release", "Current Package", "New Package");

                var rowPosition = 1;

                foreach (var project in projects)
                {
                    table.AddRow(new[] {
                        rowPosition.ToString(),
                        project.Checked ? "*" : String.Empty,
                        project.ProjectName,
                        project.CurrentRelease.Version,
                        project.CurrentRelease.DisplayPackageVersion,
                        project.AvailablePackages.Count() > 0 ? project.AvailablePackages.First().Version : String.Empty
                    });
                    rowPosition++;
                }

                table.Write();

                System.Console.WriteLine(" Update: 1 | Remove: 2 | Continue: c | Exit: e");
                var prompt = Prompt.GetString("");

                switch (prompt)
                {
                    case "1":
                        SelectProjectsForDeployment(projects, true);
                        break;
                    case "2":
                        SelectProjectsForDeployment(projects, false);
                        break;
                    case "c":
                        break;
                    case "e":
                        run = false;
                        break;
                    default:
                        await InteractivePrompt(channel, environment, projects);
                        break;
                }
            }

            return null;
        }

        private void SelectProjectsForDeployment(IList<Project> projects, bool select)
        {
            var range = GetRangeFromPrompt(projects.Count());
            foreach(var index in range)
            {
                projects[index - 1].Checked = select;
            }
        }

        private string PromptForStringWithoutQuitting(string prompt)
        {
            var channel = Prompt.GetString(prompt);
            if (string.IsNullOrEmpty(channel))
            {
                return PromptForStringWithoutQuitting(prompt);
            }
            return channel;
        }

        private void ConfigureProfileBasedDeployment(CommandLineApplication command) 
        {
            base.Configure(command);

            AddToRegister("file", command.Option("-f|--file", OptionsStrings.ProfileFile, CommandOptionType.SingleValue).IsRequired().Accepts(v => v.LegalFilePath()));
            AddToRegister("apikey", command.Option("-a|--apikey", OptionsStrings.ProfileFile, CommandOptionType.SingleValue));
            AddToRegister("url", command.Option("-u|--url", OptionsStrings.Url, CommandOptionType.SingleValue));

            command.OnExecute(async () =>
            {
                await DeployWithProfile(command);
            });
        }

        private async Task<int> DeployWithProfile(CommandLineApplication command)
        {
            var profilePath = GetOption("file").Value();
            System.Console.WriteLine("Using profile at path " + profilePath);
            await this.consoleDoJob.StartJob(profilePath, null, null, true);
            return 0;
        }
    }
}
