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


using System.ComponentModel;
using OctoPlus.ChangeLogs;
using OctoPlus.ChangeLogs.TeamCity;
using OctoPlus.Console;
using OctoPlus.Console.Interfaces;
using OctoPlus.Deployment;
using OctoPlus.Deployment.Interfaces;
using OctoPlus.Octopus;
using OctoPlus.Octopus.Interfaces;
using OctoPlus.Utilities;
using OctoPlus.VersionChecking;
using OctoPlus.VersionChecking.GitHub;
using StructureMap;

namespace OctoPlus.StructureMap
{
    class UtilitiesRegistry : Registry
    {
        public UtilitiesRegistry()
        {
            For<IOctopusHelper>().Use<OctopusHelper>().Singleton();
            For<IDeployer>().Use<Deployer>().Singleton();
            For<IConsoleDoJob>().Use<ConsoleDoJob>();
            For<IChangeLogProvider>().Use<TeamCity>();
            For<IWebRequestHelper>().Use<WebRequestHelper>();
            For<IVersionCheckingProvider>().Use<GitHubVersionChecker>();
            For<IVersionChecker>().Use<VersionChecker>();
        }
    }
}