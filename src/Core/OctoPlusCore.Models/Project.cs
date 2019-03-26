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
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OctoPlusCore.Models
{
    public class Project : INotifyPropertyChanged {

        private bool _checked { get; set; }

        public bool Checked
        {
            get { return this._checked; }
            set
            {
                this._checked = value;
                this.OnPropertyChanged();
            }
        }

        public IList<PackageStub> SelectedPackageStubs
        {
            get
            {
                return AvailablePackages.Select(p => p.SelectedPackage).ToList();
            }
        }

        public string ProjectName { get; set; }
        public string ProjectId { get; set; }
        public Release CurrentRelease { get; set; }
        public IList<PackageStep> AvailablePackages { get; set; }
        public string ProjectGroupId { get; set; }
        public string LifeCycleId { get; set; }
        public IList<RequiredVariable> RequiredVariables { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public Project()
        {
            AvailablePackages = new List<PackageStep>();
            RequiredVariables = new List<RequiredVariable>();
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual bool IsDeployable { get { return AvailablePackages != null && AvailablePackages.Any(x => x.SelectedPackage != null); } }

    }
}