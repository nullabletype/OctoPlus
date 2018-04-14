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


using CommandLine;

namespace OctoPlusCore.Configuration
{
    public class OctoPlusOptions
    {
        [Option('p', "profile", Required = true, HelpText = "The path to a json profile file exported from OctoPlus")]
        public string ProfileFile { get; set; }

        [Option('k', "key", Required = false, HelpText = "The API key to use for the deployment")]
        public string ApiKey { get; set; }

        [Option('m', "message", Required = false, HelpText = "Release message to set")]
        public string ReleaseMessage { get; set; }

        [Option('v', "version", Required = false, HelpText = "Release version to set")]
        public string ReleaseVersion { get; set; }

        [Option('s', "forcedeployifsamepackage", Required = false,
            HelpText = "Force the project deployment if its the same package")]
        public bool ForceDeploymentIfSamePackage { get; set; }
    }
}