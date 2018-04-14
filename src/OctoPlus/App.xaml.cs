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
using System.Windows;
using System.Windows.Threading;
using OctoPlusCore.Configuration.Interfaces;
using OctoPlusCore.VersionChecking;
using OctoPlus.Windows;
using OctoPlus.Windows.Interfaces;
using StructureMap;
using OctoPlus.Startup;
using OctoPlus.Resources;

namespace OctoPlus {
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static IContainer _container;

        [STAThread]
        public static void Main() 
        { 
            var application = new App();
            application.InitializeComponent();
            application.Run();
            
        }

        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            this.Dispatcher.UnhandledException += DispatcherOnUnhandledException;
            var loadingWindow = new LoadingWindow();
            loadingWindow.Show(null, MiscStrings.CheckingConfiguration, MiscStrings.Initialising);
            var initResult = await BootStrap.CheckConfigurationAndInit();
            _container = initResult.container;
            if (!initResult.ConfigResult.Success) {
                MessageBox.Show(string.Join(Environment.NewLine, initResult.ConfigResult.Errors),
                    MiscStrings.LoadErrorCaption);
                Shutdown();
                return;
            }
            loadingWindow.SetStatus(MiscStrings.CheckingForNewVersion);
            var release = await _container.GetInstance<IVersionChecker>().GetLatestVersion();
            if (release.NewVersion)
            {
                MessageBox.Show(release.Release.ChangeLog, "New version " + release.Release.TagName + " is available!");
            }
            loadingWindow.Hide();
            _container.Configure(c => c.For<ILoadingWindow>().Use(loadingWindow).Singleton());
            var windowProvider = _container.GetInstance<IWindowProvider>();
            windowProvider.CreateWindow<IDeployment>().Show();
        }

        private void DispatcherOnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs dispatcherUnhandledExceptionEventArgs)
        {
            var logger = _container.GetInstance<ILogger<App>>();
            logger.Error("Octoplus stopped unexpectedly", dispatcherUnhandledExceptionEventArgs.Exception);
            string errorMessage = MiscStrings.UnhandledException + ": " + dispatcherUnhandledExceptionEventArgs.Exception.Message;
            MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            dispatcherUnhandledExceptionEventArgs.Handled = true;
        }
    }
}