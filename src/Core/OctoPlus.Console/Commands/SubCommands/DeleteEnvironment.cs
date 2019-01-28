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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using OctoPlus.Console.Interfaces;
using OctoPlus.Console.Resources;
using OctoPlusCore.Octopus.Interfaces;

namespace OctoPlus.Console.Commands.SubCommands
{
    class DeleteEnvironment : BaseCommand
    {
        protected override bool SupportsInteractiveMode => false;
        public override string CommandName => "delete";

        public DeleteEnvironment(IOctopusHelper octopusHelper) : base(octopusHelper) { }


        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);

            AddToRegister(EnsureEnvironmentOptionNames.Id, command.Option("-e|--e", OptionsStrings.EnvironmentName, CommandOptionType.SingleValue).IsRequired());
            AddToRegister(EnsureEnvironmentOptionNames.SkipConfirmation, command.Option("-s|--skipconfirmation", OptionsStrings.SkipConfirmation, CommandOptionType.NoValue));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var id = GetStringFromUser(EnsureEnvironmentOptionNames.Id, string.Empty, false);
            var skipConfirm = GetOption(EnsureEnvironmentOptionNames.SkipConfirmation);
            var found = await this.octoHelper.GetEnvironment(id);
            if (found != null) 
            {
                System.Console.WriteLine(String.Format(UiStrings.EnvironmentFound, id));
                if (skipConfirm == null || !skipConfirm.HasValue()) 
                {
                    if (!Prompt.GetYesNo(string.Format(UiStrings.ConfirmationCheck, found.Name), false))
                    {
                        return 0;
                    }
                }
                try 
                {
                    await octoHelper.RemoveEnvironmentsFromTeams(found.Id);
                    await octoHelper.DeleteEnvironment(found.Id);
                } 
                catch (Exception e) 
                {
                    System.Console.WriteLine(UiStrings.Error + e.Message);
                    return -1;
                }
                System.Console.WriteLine(String.Format(UiStrings.Done, string.Empty));
                return 0;
            }
            System.Console.WriteLine(String.Format(UiStrings.EnvironmentNotFound, id));
            return -1;
        }

        struct EnsureEnvironmentOptionNames 
        {
            public const string Id = "id";
            public const string SkipConfirmation = "skipconfirmation";
        }
    }
}
