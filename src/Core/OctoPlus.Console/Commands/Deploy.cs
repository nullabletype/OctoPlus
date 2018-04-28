using McMaster.Extensions.CommandLineUtils;
using OctoPlus.Console.Resources;
using System;
using System.Collections.Generic;
using System.Text;

namespace OctoPlus.Console.Commands
{
    class Deploy : BaseCommand
    {

        public static void Configure(CommandLineApplication command) 
        {
            BaseConfigure(command);
            command.Description = OptionsStrings.DeployProjects;

            command.Argument("profile", OptionsStrings.DeployFromProfile);

            command.Option("-f|--file", OptionsStrings.ProfileFile, CommandOptionType.SingleValue);
            command.Option("-a|--apikey", OptionsStrings.ProfileFile, CommandOptionType.SingleValue);
            command.Option("-u|--url", OptionsStrings.Url, CommandOptionType.SingleValue);
        }
    }

    class BaseCommand 
    {

        protected const string HelpOption = "-?|-h|--help";

        protected static void BaseConfigure(CommandLineApplication command) 
        {
            command.HelpOption(HelpOption);
        }

    }
}
