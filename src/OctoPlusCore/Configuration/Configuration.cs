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


using OctoPlusCore.Configuration.Interfaces;

namespace OctoPlusCore.Configuration
{
    class Configuration : IConfiguration
    {
        public string ApiKey { get; set; }
        public string OctopusUrl { get; set; }
        public string ChannelSeedProjectName { get; set; }
        public string ProjectGroupFilterString { get; set; }
        public string DefaultEnvironmentName { get; set; }
        public string DefaultChannelName { get; set; }
        public ChangeLogProviderConfiguration ChangeProviderConfiguration { get; set; }
        public bool EnableTrace { get; set; }
        public bool DisableAutoLoad { get; set; }
    }
}