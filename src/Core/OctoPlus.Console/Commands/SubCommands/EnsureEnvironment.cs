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
using System.Linq;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using OctoPlusCore.Language;
using OctoPlusCore.Octopus.Interfaces;

namespace OctoPlus.Console.Commands.SubCommands
{
    class EnsureEnvironment : BaseCommand
    {
        protected override bool SupportsInteractiveMode => false;
        public override string CommandName => "ensure";

        public EnsureEnvironment(IOctopusHelper octopusHelper, ILanguageProvider languageProvider) : base(octopusHelper, languageProvider) { }


        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);

            AddToRegister(EnsureEnvironmentOptionNames.Name, command.Option("-n|--name", languageProvider.GetString(LanguageSection.OptionsStrings, "EnvironmentName"), CommandOptionType.SingleValue).IsRequired());
            AddToRegister(EnsureEnvironmentOptionNames.Description, command.Option("-d|--description", languageProvider.GetString(LanguageSection.OptionsStrings, "Description"), CommandOptionType.SingleValue));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var name = GetStringFromUser(EnsureEnvironmentOptionNames.Name, string.Empty, false);
            var description = GetStringFromUser(EnsureEnvironmentOptionNames.Description, string.Empty, true);
            var found = await this.octoHelper.GetMatchingEnvironments(name);
            OctoPlusCore.Models.Environment env = null;
            if (found.Any()) 
            {
                System.Console.WriteLine(String.Format(languageProvider.GetString(LanguageSection.UiStrings, "EnvironmentFound"), name));
                env = found.First();
            } 
            else 
            {
                System.Console.WriteLine(String.Format(languageProvider.GetString(LanguageSection.UiStrings, "EnvironmentNotFound"), name));
                env = await octoHelper.CreateEnvironment(name, description);
            }
            System.Console.WriteLine(String.Format(languageProvider.GetString(LanguageSection.UiStrings, "EnvionmentId"), env.Id));
            return 0;
        }

        struct EnsureEnvironmentOptionNames 
        {
            public const string Name = "name";
            public const string Description = "description";
        }
    }
}
