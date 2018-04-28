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
using OctoPlus.Console.Interfaces;
using OctoPlus.Console.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OctoPlus.Console.Commands {
    class Deploy : BaseCommand {

        private IConsoleDoJob consoleDoJob;
        public Deploy(IConsoleDoJob consoleDoJob)
        {
            this.consoleDoJob = consoleDoJob;
        }

        public async void Configure(CommandLineApplication command) 
        {
            base.Configure(command);
            command.Description = OptionsStrings.DeployProjects;

            command.Command("profile", profile => ConfigureProfileBasedDeployment(profile));

            command.OnExecute(async () =>
            {
                command.ShowHelp();
            });
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
