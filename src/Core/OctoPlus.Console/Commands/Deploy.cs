using Microsoft.Extensions.CommandLineUtils;
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
