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
using OctoPlusCore.Configuration.Interfaces;
using OctoPlusCore.Interfaces;
using OctoPlusCore.JobRunners;
using OctoPlusCore.JobRunners.JobConfigs;
using OctoPlusCore.Language;
using OctoPlusCore.Octopus.Interfaces;

namespace OctoPlus.Console.Commands.SubCommands
{
    class CleanupChannels : BaseCommand
    {
        private IProgressBar progressBar;
        private IConfiguration configuration;
        private ChannelsRunner runner;
        protected override bool SupportsInteractiveMode => false;
        public override string CommandName => "cleanup";

        public CleanupChannels(IOctopusHelper octopusHelper, ILanguageProvider languageProvider, IProgressBar progressBar, IConfiguration configuration, ChannelsRunner runner) : base(octopusHelper, languageProvider) 
        {
            this.progressBar = progressBar;
            this.configuration = configuration;
            this.runner = runner;
        }


        public override void Configure(CommandLineApplication command)
        {
            base.Configure(command);

            AddToRegister(EnsureEnvironmentOptionNames.GroupFilter, command.Option("-g|--groupfilter", languageProvider.GetString(LanguageSection.OptionsStrings, "GroupFilter"), CommandOptionType.SingleValue));
            AddToRegister(EnsureEnvironmentOptionNames.TestMode, command.Option("-t|--testmode", languageProvider.GetString(LanguageSection.OptionsStrings, "TestMode"), CommandOptionType.NoValue));
        }

        protected override async Task<int> Run(CommandLineApplication command)
        {
            var groupFilter = GetStringFromUser(EnsureEnvironmentOptionNames.GroupFilter, string.Empty, false);
            var testMode = GetBoolValueFromOption(EnsureEnvironmentOptionNames.TestMode);

            var config = ChannelCleanupConfig.Create(groupFilter, testMode);

            if (config.IsSuccess)
            {
                await runner.Cleanup(config.Value);
            }

            return 0;
        }

        struct EnsureEnvironmentOptionNames 
        {
            public const string GroupFilter = "groupfilter";
            public const string TestMode = "testmode";
        }
    }
}
