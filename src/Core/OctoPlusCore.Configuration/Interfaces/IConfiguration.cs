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


using System.Collections.Generic;

namespace OctoPlusCore.Configuration.Interfaces
{
    public interface IConfiguration
    {
        string ApiKey { get; set; }
        string OctopusUrl { get; set; }
        List<string> ChannelSeedProjectNames { get;set; }
        ChangeLogProviderConfiguration ChangeProviderConfiguration { get; set; }
        bool EnableTrace { get; set; }
        int CacheTimeoutInSeconds { get; set; }
        string DefaultChannel { get; set; }
    }
}