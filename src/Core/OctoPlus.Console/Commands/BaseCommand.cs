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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using OctoPlus.Console.Resources;
using OctoPlusCore.Deployment.Interfaces;
using OctoPlusCore.Models;

namespace OctoPlus.Console.Commands {
    abstract class BaseCommand 
    {
        protected abstract bool SupportsInteractiveMode { get; }
        public abstract string CommandName { get; }
        protected abstract Task<int> Run(CommandLineApplication command);

        protected const string HelpOption = "-?|-h|--help";
        private readonly Dictionary<string, CommandOption> _optionRegister;
        private readonly List<BaseCommand> _subCommands;
        protected bool InInteractiveMode { get; private set; }

        protected BaseCommand()
        {
            _optionRegister = new Dictionary<string, CommandOption>();
            _subCommands = new List<BaseCommand>();
        }

        public virtual void Configure(CommandLineApplication command) 
        {
            command.HelpOption(HelpOption);
            command.ThrowOnUnexpectedArgument = true;
            if (this.SupportsInteractiveMode)
            {
                AddToRegister(OptionNames.Interactive, command.Option("-i|--interactive", OptionsStrings.InteractiveDeploy, CommandOptionType.NoValue));
            }
            command.OnExecute(async () =>
            {
                if (GetOption(OptionNames.Interactive).HasValue())
                {
                    SetInteractiveMode((true));
                }

                var code = await this.Run(command);
                if (code != 0)
                {
                    if (code == -1)
                    {
                        command.ShowHelp();
                    }
                }
            });
        }

        protected void ConfigureSubCommand(BaseCommand child, CommandLineApplication command)
        {
            command.Command(child.CommandName, child.Configure);
        }

        protected void SetInteractiveMode(bool mode)
        {
            this.InInteractiveMode = mode;
        }

        protected void AddToRegister(string key, CommandOption option)
        {
            _optionRegister.Add(key, option);
        }

        protected CommandOption GetOption(string key)
        {
            return _optionRegister[key];
        }

        protected string GetStringValueFromOption(string key)
        {
            var option = GetOption(key);
            if (option.HasValue())
            {
                return option.Value();
            }
            return string.Empty;
        }

        protected void WriteStatusLine(string status)
        {
            var builder = new StringBuilder(status);
            while (builder.Length < System.Console.BufferWidth)
            {
                builder.Append(" ");
            }
            System.Console.SetCursorPosition(0, System.Console.CursorTop);
            System.Console.Write(status);
            System.Console.SetCursorPosition(0, System.Console.CursorTop);
        }

        protected void CleanCurrentLine()
        {
            var builder = new StringBuilder("\r");
            while (builder.Length < System.Console.BufferWidth)
            {
                builder.Append(" ");
            }
            System.Console.SetCursorPosition(0, System.Console.CursorTop);
            System.Console.Write(builder.ToString());
            System.Console.SetCursorPosition(0, System.Console.CursorTop);
        }

        protected void WriteProgress(int current, int total, string message)
        {
            var builder = new StringBuilder("\r[");
            for(int i = 1; i <= current; i++)
            {
                builder.Append("*");
            }
            for (int i = current + 1; i <= total; i++)
            {
                builder.Append("-");
            }
            builder.Append("]");
            if (!string.IsNullOrEmpty(message))
            {
                builder.Append($" | {message}");
            }
            while(builder.Length < System.Console.BufferWidth)
            {
                builder.Append(" ");
            }
            System.Console.SetCursorPosition(0, System.Console.CursorTop);
            System.Console.Write(builder.ToString());
        }

        protected string GetStringFromUser(string optionName, string prompt)
        {
            var option = GetStringValueFromOption(optionName);
            if (string.IsNullOrEmpty(option))
            {
                option = PromptForStringWithoutQuitting(prompt);
            }

            return option;
        }

        protected string PromptForStringWithoutQuitting(string prompt)
        {
            var channel = Prompt.GetString(prompt);
            if (string.IsNullOrEmpty(channel))
            {
                return PromptForStringWithoutQuitting(prompt);
            }
            return channel;
        }

        protected async Task<bool> ValidateDeployment(EnvironmentDeployment deployment, IDeployer deployer)
        {
            if (deployment == null)
            {
                return true;
            }

            var result = await deployer.CheckDeployment(deployment);
            if (result.Success)
            {
                return true;
            }
            else
            {
                System.Console.WriteLine(UiStrings.Error + result.ErrorMessage);
            }

            return false;
        }

        public struct OptionNames
        {
            public const string Interactive = "interactive";
        }

    }
}
