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
using System.Text;
using System.Threading.Tasks;

namespace OctoPlusCore.Models
{
    public class ProjectDeployment
    {
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
        public IList<PackageDeployment> Packages { get; set; }
        public IList<RequiredVariableDeployment> RequiredVariables { get; set; }
        public string ChannelId { get; set; }
        public string ChannelVersionRange { get; set; }
        public string ChannelVersionTag { get; set; }
        public string ReleaseMessage { get; set; }
        public string ReleaseVersion { get; set; }
        public string LifeCycleId { get; set; }
        public string ReleaseId { get; set; }
    }
}