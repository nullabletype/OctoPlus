using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using OctoPlus.Console.Interfaces;
using OctoPlus.Console.Resources;
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

            AddToRegister(DeployWithProfileDirectoryOptionNames.ActionInstall, command.Option("--actioninstall", OptionsStrings.ForceDeployOfSamePackage, CommandOptionType.NoValue));
            AddToRegister(DeployWithProfileDirectoryOptionNames.ActionRun, command.Option("--actionrun", OptionsStrings.ForceDeployOfSamePackage, CommandOptionType.NoValue));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var profilePath = GetOption(DeployWithProfileDirectoryOptionNames.Directory).Value();
            System.Console.WriteLine(UiStrings.UsingProfileDirAtPath + profilePath);

            var option = GetOption(DeployWithProfileDirectoryOptionNames.Monitor);
            bool run = option.HasValue();
            int.TryParse(option.Value(), out int waitTime);

            try
            {
                if (!Directory.Exists(profilePath))
                {
                    System.Console.WriteLine(UiStrings.PathDoesntExist);
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
                                RunProfiles(profilePath, run, waitTime);
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
                System.Console.WriteLine(String.Format(UiStrings.UnexpectedError, e.Message));
            }

            
            return 0;
        }

        private async Task RunProfiles(string profilePath, bool run, int waitTIme)
        {
            do
            {
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
