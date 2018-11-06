﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using OctoPlus.Console.Interfaces;
using OctoPlus.Console.Resources;
using OctoPlusCore.Octopus.Interfaces;

namespace OctoPlus.Console.Commands.SubCommands
{
    class DeployWithProfileDirectory : BaseCommand
    {
        private readonly IConsoleDoJob _consoleDoJob;

        protected override bool SupportsInteractiveMode => false;
        public override string CommandName => "profiledirectory";

        public DeployWithProfileDirectory(IConsoleDoJob consoleDoJob, IOctopusHelper octopusHelper) : base(octopusHelper)
        {
            this._consoleDoJob = consoleDoJob;
        }


        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);

            AddToRegister(DeployWithProfileDirectoryOptionNames.Directory, command.Option("-d|--directory", OptionsStrings.ProfileFileDirectory, CommandOptionType.SingleValue).IsRequired().Accepts(v => v.LegalFilePath()));
            AddToRegister(DeployWithProfileDirectoryOptionNames.ForceRedeploy, command.Option("-r|--forceredeploy", OptionsStrings.ForceDeployOfSamePackage, CommandOptionType.NoValue));
            AddToRegister(DeployWithProfileDirectoryOptionNames.Monitor, command.Option("-m|--monitor", OptionsStrings.MonitorForPackages, CommandOptionType.SingleValue).Accepts(v => v.RegularExpression("[0-9]*", UiStrings.ParameterNotANumber)));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var profilePath = GetOption(DeployWithProfileDirectoryOptionNames.Directory).Value();
            System.Console.WriteLine(UiStrings.UsingProfileDirAtPath + profilePath);

            var option = GetOption(DeployWithProfileDirectoryOptionNames.Monitor);
            bool run = option.HasValue();
            int.TryParse(option.Value(), out int waitTIme);

            try
            {
                do
                {
                    if (!Directory.Exists(profilePath))
                    {
                        System.Console.WriteLine(UiStrings.PathDoesntExist);
                        return -1;
                    }

                    foreach (var file in Directory.GetFiles(profilePath, "*auto.profile"))
                    {
                        System.Console.WriteLine(String.Format(UiStrings.DeployingUsingConfig, file));
                        await this._consoleDoJob.StartJob(file, null, null, GetOption(DeployWithProfileDirectoryOptionNames.ForceRedeploy).HasValue());
                    }

                    if (run)
                    {
                        System.Console.WriteLine(String.Format(UiStrings.SleepingForSeconds, waitTIme));
                        await Task.Delay(waitTIme * 1000);
                    }

                } while (run);
            }
            catch (Exception e)
            {
                System.Console.WriteLine(String.Format(UiStrings.UnexpectedError, e.Message));
            }

            
            return 0;
        }

        struct DeployWithProfileDirectoryOptionNames
        {
            public const string Directory = "directory";
            public const string ForceRedeploy = "forceredeploy";
            public const string Monitor = "monitor";
        }
    }
}
