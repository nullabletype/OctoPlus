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

namespace OctoPlus.Console.Commands {
    abstract class BaseCommand 
    {
        private Dictionary<string, CommandOption> OptionRegister;

        public BaseCommand()
        {
            OptionRegister = new Dictionary<string, CommandOption>();
        }

        protected const string HelpOption = "-?|-h|--help";

        protected void Configure(CommandLineApplication command) 
        {
            command.HelpOption(HelpOption);
            command.ThrowOnUnexpectedArgument = true;
        }

        protected void AddToRegister(string key, CommandOption option)
        {
            OptionRegister.Add(key, option);
        }

        protected CommandOption GetOption(string key)
        {
            return OptionRegister[key];
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

    }
}
