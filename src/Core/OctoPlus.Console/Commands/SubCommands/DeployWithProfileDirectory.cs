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
using System.Text;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using OctoPlus.Console.Interfaces;
using OctoPlusCore.Language;
using OctoPlusCore.Octopus.Interfaces;
using PeterKottas.DotNetCore.WindowsService;
using PeterKottas.DotNetCore.WindowsService.Interfaces;

namespace OctoPlus.Console.Commands.SubCommands
{
    class DeployWithProfileDirectory : BaseCommand, IMicroService
    {
        private readonly IConsoleDoJob _consoleDoJob;

        protected override bool SupportsInteractiveMode => false;
        public override string CommandName => "profiledirectory";

        public DeployWithProfileDirectory(IConsoleDoJob consoleDoJob, IOctopusHelper octopusHelper, ILanguageProvider languageProvider) : base(octopusHelper, languageProvider)
        {
            this._consoleDoJob = consoleDoJob;
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

            var option = GetOption(DeployWithProfileDirectoryOptionNames.Monitor);
            bool run = option.HasValue();
            int.TryParse(option.Value(), out int waitTime);

            try
            {
                if (!Directory.Exists(profilePath))
                {
                    System.Console.WriteLine(languageProvider.GetString(LanguageSection.UiStrings, "PathDoesntExist"));
                    return -1;
                }

                if (run)
                {
                    ServiceRunner<DeployWithProfileDirectory>.Run(config =>
                    {
                        var name = config.GetDefaultName();
                        config.SetName("OctoPlus.Service");
                        config.SetDisplayName("OctoPlus.Service");
                        config.SetDescription("");
                        config.Service(serviceConfig =>
                        {
                            serviceConfig.ServiceFactory((extraArguements, controller) =>
                            {
                                return this;
                            });

                            serviceConfig.OnStart((service, extraParams) =>
                            {
                                System.Console.WriteLine("Service {0} started", name);
                                service.Start();
                                RunProfiles(profilePath, run, waitTime).Start();
                            });

                        });
                    });
                }
                else
                {
                    await RunProfiles(profilePath, run, waitTime);
                }
            }
            catch (Exception e)
            {
                System.Console.WriteLine(String.Format(languageProvider.GetString(LanguageSection.UiStrings, "UnexpectedError"), e.Message));
            }

            
            return 0;
        }

        private async Task RunProfiles(string profilePath, bool run, int waitTIme)
        {
            do
            {
                foreach (var file in Directory.GetFiles(profilePath, "*auto.profile"))
                {
                    System.Console.WriteLine(String.Format(languageProvider.GetString(LanguageSection.UiStrings, "DeployingUsingConfig"), file));
                    await this._consoleDoJob.StartJob(file, null, null, GetOption(DeployWithProfileDirectoryOptionNames.ForceRedeploy).HasValue());
                }

                if (run)
                {
                    System.Console.WriteLine(String.Format(languageProvider.GetString(LanguageSection.UiStrings, "SleepingForSeconds"), waitTIme));
                    await Task.Delay(waitTIme * 1000);
                }

            } while (run);
        }

        public void Start()
        {
            
        }

        public void Stop()
        {
            throw new NotImplementedException();
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
