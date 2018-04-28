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
using OctoPlus.Console.Resources;
using OctoPlusCore.Octopus;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace OctoPlus.Console.Commands {
    class Environment : BaseCommand {

        public Environment(CommandLineApplication command) 
        {
            base.Configure(command);
            command.Description = OptionsStrings.EnvironmentCommands;

            command.Command("list", list => ConfigureListCommand(list));

            command.OnExecute(async () =>
            {
                
            });
        }

        private void ConfigureListCommand(CommandLineApplication command) 
        {
            base.Configure(command);

            command.OnExecute(async () =>
            {
                await ExecuteListCommand(command);
            });
        }

        private async Task ExecuteListCommand(CommandLineApplication command) 
        {
            var envs = await  OctopusHelper.Default.GetEnvironments();
            var table = new ConsoleTable(UiStrings.Name, UiStrings.Id);
            foreach (var env in envs)
            {
                table.AddRow(new [] { env.Name, env.Id });
            }

            table.Write();
        }
    }
}
