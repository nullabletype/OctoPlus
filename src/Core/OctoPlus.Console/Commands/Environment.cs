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
using OctoPlusCore.Octopus;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using OctoPlusCore.Octopus.Interfaces;
using OctoPlus.Console.Commands.SubCommands;
using OctoPlusCore.Language;

namespace OctoPlus.Console.Commands {
    internal class Environment : BaseCommand
    {
        private EnsureEnvironment ensureEnv;
        private DeleteEnvironment delEnv;
        private EnvironmentToTeam envToTeam;
        private EnvironmentToLifecycle envToLifecycle;

        public Environment(IOctopusHelper octoHelper, EnsureEnvironment ensureEnv, DeleteEnvironment delEnv, EnvironmentToTeam envToTeam, EnvironmentToLifecycle envToLifecycle, ILanguageProvider languageProvider) : base(octoHelper, languageProvider)
        {
            this.ensureEnv = ensureEnv;
            this.delEnv = delEnv;
            this.envToTeam = envToTeam;
            this.envToLifecycle = envToLifecycle;
        }

        protected override bool SupportsInteractiveMode => false;
        public override string CommandName => "env";

        public override void Configure(CommandLineApplication command) 
        {
            base.Configure(command);
            ConfigureSubCommand(ensureEnv, command);
            ConfigureSubCommand(delEnv, command);
            ConfigureSubCommand(envToTeam, command);
            ConfigureSubCommand(envToLifecycle, command);

            command.Description = languageProvider.GetString(LanguageSection.OptionsStrings, "EnvironmentCommands");
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var envs = await  OctopusHelper.Default.GetEnvironments();
            var table = new ConsoleTable(languageProvider.GetString(LanguageSection.UiStrings, "Name"), languageProvider.GetString(LanguageSection.UiStrings, "Id"));
            foreach (var env in envs)
            {
                table.AddRow(env.Name, env.Id);
            }

            table.Write(Format.Minimal);
            return 0;
        }

    }
}
