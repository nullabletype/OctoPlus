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


using OctoPlusCore.Language;
using CSharpFunctionalExtensions;

namespace OctoPlusCore.JobRunners.JobConfigs
{
    public class DeployWithProfileDirectoryConfig
    {
        private DeployWithProfileDirectoryConfig () { }

        public static Result<DeployWithProfileDirectoryConfig> Create(ILanguageProvider languageProvider, string directory, int? delay, bool forceRedploy)
        {

            if (!System.IO.Directory.Exists(directory))
            {
                System.Console.WriteLine();
                return Result.Failure<DeployWithProfileDirectoryConfig>(languageProvider.GetString(LanguageSection.UiStrings, "PathDoesntExist"));
            }

            var config = new DeployWithProfileDirectoryConfig
            {
                Directory = directory,
                MonitorDelay = delay.HasValue ? delay.Value : 0,
                MonitorDirectory = delay.HasValue,
                ForceRedeploy = forceRedploy
            };
            return Result.Ok(config);
        }

        public string Directory { get; private set; }
        public bool MonitorDirectory { get; private set; }
        public bool ForceRedeploy { get; private set; }
        public int MonitorDelay { get; private set; }

    }
}
