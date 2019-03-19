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
using System.Text;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using OctoPlus.Console.Interfaces;
using OctoPlusCore.Language;
using OctoPlusCore.Octopus.Interfaces;

namespace OctoPlus.Console.Commands.SubCommands
{
    class DeployWithProfile : BaseCommand
    {
        private readonly IConsoleDoJob _consoleDoJob;

        protected override bool SupportsInteractiveMode => false;
        public override string CommandName => "profile";

        public DeployWithProfile(IConsoleDoJob consoleDoJob, IOctopusHelper octopusHelper, ILanguageProvider languageProvider) : base(octopusHelper, languageProvider)
        {
            this._consoleDoJob = consoleDoJob;
        }


        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);

            AddToRegister(DeployWithProfileOptionNames.File, command.Option("-f|--file", languageProvider.GetString(LanguageSection.OptionsStrings, "ProfileFile"), CommandOptionType.SingleValue).IsRequired().Accepts(v => v.LegalFilePath()));
            AddToRegister(DeployWithProfileOptionNames.ForceRedeploy, command.Option("-r|--forceredeploy", languageProvider.GetString(LanguageSection.OptionsStrings, "ForceDeployOfSamePackage"), CommandOptionType.NoValue));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var profilePath = GetOption(DeployWithProfileOptionNames.File).Value();
            System.Console.WriteLine(languageProvider.GetString(LanguageSection.UiStrings, "UsingProfileAtPath") + profilePath);
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
