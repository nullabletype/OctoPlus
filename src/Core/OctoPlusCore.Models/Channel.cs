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


using System.Text.RegularExpressions;

namespace OctoPlusCore.Models
{
    public class Channel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string VersionRange { get; set; }
        public string VersionTag { get; set; }

        public bool ValidateVersion(string version)
        {
            var versionSplit = version.Split('-');
            var success = true;
            if (!string.IsNullOrEmpty(VersionRange))
            {
                var range = NuGet.Versioning.VersionRange.Parse(VersionRange);
                var semVersion = NuGet.Versioning.NuGetVersion.Parse(version);
                success = range.Satisfies(semVersion);
            }

            if (success)
            {
                if (!string.IsNullOrEmpty(VersionTag))
                {
                    success = Regex.IsMatch(versionSplit.Length > 1 ? versionSplit[1] : string.Empty, VersionTag);
                }
            }
            return success;
        }
    }
}