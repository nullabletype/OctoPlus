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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using OctoPlus.Console.Interfaces;
using OctoPlusCore.Octopus.Interfaces;
using OctoPlusCore.Utilities;
using OctoPlusCore.Models;
using OctoPlusCore.Models.Variables;
using OctoPlusCore.Language;

namespace OctoPlus.Console.Commands.SubCommands 
{
    partial class VariablesWithProfile : BaseCommand
    {
        protected override bool SupportsInteractiveMode => false;
        public override string CommandName => "profile";

        public VariablesWithProfile(IOctopusHelper octopusHelper, ILanguageProvider languageProvider) : base(octopusHelper, languageProvider) { }


        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);

            AddToRegister(VariablesWithProfileOptionNames.File, command.Option("-f|--filepath", languageProvider.GetString(LanguageSection.OptionsStrings, "ProfileFile"), CommandOptionType.SingleValue).IsRequired().Accepts(v => v.LegalFilePath()));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var file = GetStringFromUser(VariablesWithProfileOptionNames.File, string.Empty, false);

            var config = StandardSerialiser.DeserializeFromJsonNet<VariableSetCollection>(File.ReadAllText(file));

            if (config != null) 
            {
                foreach(var varSet in config.VariableSets) 
                {
                    System.Console.WriteLine(String.Format(languageProvider.GetString(LanguageSection.UiStrings, "UpdatingVariableSet"), varSet.Id, varSet.Variables.Count));
                    try 
                    {
                        await octoHelper.UpdateVariableSet(varSet);
                    } 
                    catch (Exception e) 
                    {
                        System.Console.WriteLine(String.Format(languageProvider.GetString(LanguageSection.UiStrings, "FailedUpdatingVariableSet"), e.Message));
                        return -1;
                    }
                }
            } 
            else 
            {
                System.Console.WriteLine(languageProvider.GetString(LanguageSection.UiStrings, "FailedParsingVariableFile"));
            }

            System.Console.WriteLine(String.Format(languageProvider.GetString(LanguageSection.UiStrings, "Done"), string.Empty));

            return 0;
        }

        struct VariablesWithProfileOptionNames 
        {
            public const string File = "file";
            public const string Description = "description";
        }
    }
}
