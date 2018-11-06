using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using OctoPlus.Console.Interfaces;
using OctoPlus.Console.Resources;
using OctoPlusCore.Octopus.Interfaces;

namespace OctoPlus.Console.Commands.SubCommands
{
    class DeployWithProfile : BaseCommand
    {
        private readonly IConsoleDoJob _consoleDoJob;

        protected override bool SupportsInteractiveMode => false;
        public override string CommandName => "profile";

        public DeployWithProfile(IConsoleDoJob consoleDoJob, IOctopusHelper octopusHelper) : base(octopusHelper)
        {
            this._consoleDoJob = consoleDoJob;
        }


        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);

            AddToRegister(DeployWithProfileOptionNames.File, command.Option("-f|--file", OptionsStrings.ProfileFile, CommandOptionType.SingleValue).IsRequired().Accepts(v => v.LegalFilePath()));
            AddToRegister(DeployWithProfileOptionNames.ForceRedeploy, command.Option("-r|--forceredeploy", OptionsStrings.ForceDeployOfSamePackage, CommandOptionType.NoValue));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var profilePath = GetOption(DeployWithProfileOptionNames.File).Value();
            System.Console.WriteLine(UiStrings.UsingProfileAtPath + profilePath);
            await this._consoleDoJob.StartJob(profilePath, null, null, GetOption(DeployWithProfileOptionNames.ForceRedeploy).HasValue());
            return 0;
        }

        struct DeployWithProfileOptionNames
        {
            public const string File = "file";
            public const string ForceRedeploy = "forceredeploy";
        }
    }
}
