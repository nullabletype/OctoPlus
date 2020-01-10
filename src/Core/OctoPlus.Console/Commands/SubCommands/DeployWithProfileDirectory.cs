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
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using OctoPlusCore.Language;
using OctoPlusCore.Octopus.Interfaces;
using OctoPlusCore.JobRunners.Interfaces;
using OctoPlusCore.JobRunners.JobConfigs;

namespace OctoPlus.Console.Commands.SubCommands
{
    class DeployWithProfileDirectory : BaseCommand
    {
        private readonly IJobRunner _consoleDoJob;
        private readonly DeployWithProfileDirectoryRunner _runner;

        protected override bool SupportsInteractiveMode => false;
        public override string CommandName => "profiledirectory";

        public DeployWithProfileDirectory(IJobRunner consoleDoJob, IOctopusHelper octopusHelper, ILanguageProvider languageProvider, DeployWithProfileDirectoryRunner runner) : base(octopusHelper, languageProvider)
        {
            this._consoleDoJob = consoleDoJob;
            this._runner = runner;
        }


        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);

            AddToRegister(DeployWithProfileDirectoryOptionNames.Directory, command.Option("-d|--directory", languageProvider.GetString(LanguageSection.OptionsStrings, "ProfileFileDirectory"), CommandOptionType.SingleValue).IsRequired().Accepts(v => v.LegalFilePath()));
            AddToRegister(DeployWithProfileDirectoryOptionNames.ForceRedeploy, command.Option("-r|--forceredeploy", languageProvider.GetString(LanguageSection.OptionsStrings, "ForceDeployOfSamePackage"), CommandOptionType.NoValue));
            AddToRegister(DeployWithProfileDirectoryOptionNames.Monitor, command.Option("-m|--monitor", languageProvider.GetString(LanguageSection.OptionsStrings, "MonitorForPackages"), CommandOptionType.SingleValue).Accepts(v => v.RegularExpression("[0-9]*", languageProvider.GetString(LanguageSection.UiStrings, "ParameterNotANumber"))));
            
            AddToRegister(DeployWithProfileDirectoryOptionNames.ActionInstall, command.Option("--actioninstall", languageProvider.GetString(LanguageSection.OptionsStrings, "ForceDeployOfSamePackage"), CommandOptionType.NoValue));
            AddToRegister(DeployWithProfileDirectoryOptionNames.ActionRun, command.Option("--actionrun", languageProvider.GetString(LanguageSection.OptionsStrings, "ForceDeployOfSamePackage"), CommandOptionType.NoValue));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var profilePath = GetOption(DeployWithProfileDirectoryOptionNames.Directory).Value();
            System.Console.WriteLine(languageProvider.GetString(LanguageSection.UiStrings, "UsingProfileDirAtPath") + profilePath);

            TryGetIntValueFromOption(DeployWithProfileDirectoryOptionNames.Monitor, out int waitTime);
            var foreRedeploy = GetOption(DeployWithProfileDirectoryOptionNames.ForceRedeploy).HasValue();

            var config = DeployWithProfileDirectoryConfig.Create(languageProvider, profilePath, waitTime, foreRedeploy);

            if (config.IsFailure)
            {
                System.Console.WriteLine(config.Error);
                return -1;
            } 
            else
            {
                try
                {
                    await _runner.Run(config.Value);
                }
                catch (Exception e)
                {
                    System.Console.WriteLine(String.Format(languageProvider.GetString(LanguageSection.UiStrings, "UnexpectedError"), e.Message));
                }
            }

            return 0;
        }

        struct DeployWithProfileDirectoryOptionNames
        {
            public const string Directory = "directory";
            public const string ForceRedeploy = "forceredeploy";
            public const string Monitor = "monitor";
            public const string ActionInstall = "action:install";
            public const string ActionRun = "action:run";
        }
    }
}
