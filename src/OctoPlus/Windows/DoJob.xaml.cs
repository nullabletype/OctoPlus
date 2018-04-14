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


using System.Threading.Tasks;
using System.Windows;
using Octopus.Client;
using OctoPlusCore.Deployment;
using OctoPlusCore.Deployment.Interfaces;
using OctoPlusCore.Models.Interfaces;
using OctoPlusCore.Logging.Interfaces;
using OctoPlus.Windows.Interfaces;

namespace OctoPlus.Windows
{
    /// <summary>
    /// Interaction logic for DoJob.xaml
    /// </summary>
    public partial class DoJob : Window, IUiLogger, IDoJob
    {
        private IDeployer _deployer;

        public DoJob(IDeployer deployer)
        {
            this.InitializeComponent();
            this._deployer = deployer;
        }

        public async Task StartDeployment(IOctoJob job)
        {
            await this._deployer.StartJob(job, this);
        }

        void IUiLogger.WriteLine(string toWrite)
        {
            this.OutputBox.Text += toWrite + System.Environment.NewLine;
            this.OutputBox.ScrollToEnd();
        }
    }
}