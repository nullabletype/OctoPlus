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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace OctoPlus.VersionChecking
{
    internal class VersionChecker : IVersionChecker
    {
        private IVersionCheckingProvider _provider;

        public VersionChecker(IVersionCheckingProvider provider)
        {
            this._provider = provider;
        }

        public async Task<VersionCheckResult> GetLatestVersion()
        {
            var latestVersion = await this._provider.GetLatestRelease();
            if (latestVersion == null)
            {
                return new VersionCheckResult();
            }
            var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
            var latestTagVersion = new Version(latestVersion.TagName);
            if (currentVersion.CompareTo(latestTagVersion) < 0)
            {
                return new VersionCheckResult
                {
                    NewVersion = true,
                    Release = latestVersion
                };
            }
            return new VersionCheckResult();
        }

    }

    internal class VersionCheckResult
    {
        public IRelease Release { get; set; }
        public bool NewVersion { get; set; }
    }
}
