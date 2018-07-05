using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using OctoPlus.Console.Interfaces;
using OctoPlus.Console.Resources;

namespace OctoPlus.Console.Commands
{
    class DeployWithProfile : BaseCommand
    {
        private readonly IConsoleDoJob _consoleDoJob;

        protected override bool SupportsInteractiveMode => false;
        public override string CommandName => "profile";

        public DeployWithProfile(IConsoleDoJob consoleDoJob)
        {
            this._consoleDoJob = consoleDoJob;
        }


        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);

            AddToRegister(DeployWithProfileOptionNames.File, command.Option("-f|--file", OptionsStrings.ProfileFile, CommandOptionType.SingleValue).IsRequired().Accepts(v => v.LegalFilePath()));
            AddToRegister(DeployWithProfileOptionNames.ApiKey, command.Option("-a|--apikey", OptionsStrings.ProfileFile, CommandOptionType.SingleValue));
            AddToRegister(DeployWithProfileOptionNames.Url, command.Option("-u|--url", OptionsStrings.Url, CommandOptionType.SingleValue));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var profilePath = GetOption(DeployWithProfileOptionNames.File).Value();
            System.Console.WriteLine(UiStrings.UsingProfileAtPath + profilePath);
            await this._consoleDoJob.StartJob(profilePath, null, null, true);
            return 0;
        }

        struct DeployWithProfileOptionNames
        {
            public const string File = "file";
            public const string ApiKey = "apikey";
            public const string Url = "url";
        }
    }
}
