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
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using OctoPlusCore.Language;
using OctoPlusCore.Octopus.Interfaces;

namespace OctoPlus.Console.Commands.SubCommands 
{
    class EnvironmentToTeam : BaseCommand
    {
        protected override bool SupportsInteractiveMode => false;
        public override string CommandName => "addtoteam";

        public EnvironmentToTeam(IOctopusHelper octopusHelper, ILanguageProvider languageProvider) : base(octopusHelper, languageProvider) { }


        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);

            AddToRegister(EnvironmentToTeamOptionNames.EnvId, command.Option("-e|--envid", languageProvider.GetString(LanguageSection.OptionsStrings, "EnvironmentId"), CommandOptionType.SingleValue).IsRequired());
            AddToRegister(EnvironmentToTeamOptionNames.TeamId, command.Option("-t|--teamid", languageProvider.GetString(LanguageSection.OptionsStrings, "TeamId"), CommandOptionType.SingleValue).IsRequired());
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var environmentId = GetStringFromUser(EnvironmentToTeamOptionNames.EnvId, string.Empty, false);
            var teamId = GetStringFromUser(EnvironmentToTeamOptionNames.TeamId, string.Empty, true);

            if (string.IsNullOrEmpty(environmentId)) 
            {
                System.Console.WriteLine(languageProvider.GetString(LanguageSection.UiStrings, "NoMatchingEnvironments"));
                return -1;
            }

            if (string.IsNullOrEmpty(teamId)) 
            {
                System.Console.WriteLine(languageProvider.GetString(LanguageSection.UiStrings, "TeamDoesntExist"));
                return -1;
            }

            try 
            {
                await octoHelper.AddEnvironmentToTeam(environmentId, teamId);
            }
            catch (Exception e) 
            {
                System.Console.WriteLine(String.Format(languageProvider.GetString(LanguageSection.UiStrings, "CouldntAddEnvToTeam"), e.Message));
                return -1;
            }
            System.Console.WriteLine(String.Format(languageProvider.GetString(LanguageSection.UiStrings, "Done"), String.Empty));
            return 0;
        }

        struct EnvironmentToTeamOptionNames 
        {
            public const string EnvId = "envid";
            public const string TeamId = "teamid";
        }
    }
}
