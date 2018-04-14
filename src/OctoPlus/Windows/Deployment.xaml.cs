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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Octopus.Client;
using Octopus.Client.Model;
using OctoPlus.ChangeLogs;
using OctoPlus.ChangeLogs.TeamCity;
using OctoPlus.Configuration;
using OctoPlus.Configuration.Interfaces;
using OctoPlus.Dtos;
using OctoPlus.Octopus.Interfaces;
using OctoPlus.Resources;
using OctoPlus.Utilities;
using OctoPlus.Windows.Interfaces;
using Environment = OctoPlus.Dtos.Environment;

namespace OctoPlus.Windows
{
    /// <summary>
    /// Interaction logic for Deployment.xaml
    /// </summary>
    public partial class Deployment : Window, IDeployment
    {
        public ObservableCollection<Project> Projects { get; set; }

        public ObservableCollection<Environment> Environments { get; set; }

        public ObservableCollection<Channel> Channels { get; set; }

        public Channel Channel { get; set; }

        public Environment Environment { get; set; }

        private static bool SettingProjects { get; set; }

        private bool _allowProjectLoad;

        private IConfiguration _configuration;
        private IOctopusHelper _helper;
        private IWindowProvider _windowProvider;
        private IChangeLogProvider _changeLogProvider;
        private ILoadingWindow _loadingWindow;

        public Deployment(IConfiguration configuration, IOctopusHelper helper, IWindowProvider windowProvider,
            IChangeLogProvider provider, ILoadingWindow loadingWindow)
        {
            this._configuration = configuration;
            this._helper = helper;
            this._windowProvider = windowProvider;
            this._changeLogProvider = provider;
            this._loadingWindow = loadingWindow;
            this.Projects = new ObservableCollection<Project>();
            this.Environments = new ObservableCollection<Environment>();
            this.Channels = new ObservableCollection<Channel>();
            this.InitializeComponent();
            this.Closing += OnClosing;
            this.ContentRendered += OnContentRendered;
        }

        private async void OnContentRendered(object sender, EventArgs eventArgs)
        {
            _allowProjectLoad = !this._configuration.DisableAutoLoad;
            await SetEnvironments();
        }

        private void OnClosing(object sender, CancelEventArgs cancelEventArgs)
        {
            this._loadingWindow.Close();
        }

        public async Task SetEnvironments() {
            this.ShowLoadingDialog(MiscStrings.TitleLoadingEnvironments, MiscStrings.TitleLoadingEnvironments);
        
            int index = 0;
            if (this.Environments == null)
            {
                this.Environments = new ObservableCollection<Environment>();
            }
            else
            {
                this.Environments.Clear();
            }
            this.Environment = null;
            var envs = await this._helper.GetEnvironments();
            int counter = 0;
            foreach (var env in envs)
            {
                if (this.Environment == null)
                {
                    this.Environment = env;
                }
                this.Environments.Add(env);

                if (!string.IsNullOrEmpty(this._configuration.DefaultEnvironmentName))
                {
                    if (env.Name.Equals(this._configuration.DefaultEnvironmentName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        index = counter;
                    }
                    else if (counter == 0)
                    {
                        index = counter;
                    }
                }

                counter++;
            }

            this.EnvironmentsBox.SelectedIndex = index;

            await this.SetChannels();
        }

        private void ShowLoadingDialog(string status, string title) {
            this._loadingWindow.ShowDialog(this, status, title);
        }

        public async Task SetProjects()
        {
            if (!SettingProjects && this._allowProjectLoad)
            {
                this.ShowLoadingDialog(MiscStrings.TitleLoadingProjects, MiscStrings.TitleLoadingProjects);
                this.MainGrid.IsEnabled = false;
                SettingProjects = true;
                if (this.Projects == null)
                {
                    this.Projects = new ObservableCollection<Project>();
                }
                else
                {
                    this.Projects.Clear();
                }
                var envId = this.Environment.Id;
                this.ProjectList.ItemContainerGenerator.StatusChanged += this.ItemContainerGeneratorOnStatusChanged;

                var groupIds = new List<string>();
                if (!string.IsNullOrEmpty(this._configuration.ProjectGroupFilterString))
                {
                    groupIds =
                        (await this._helper.GetFilteredProjectGroups(this._configuration.ProjectGroupFilterString))
                        .Select(g => g.Id).ToList();
                }
                else
                {
                    groupIds =
                        (await this._helper.GetProjectGroups()).Select(g => g.Id).ToList();
                }
                //var projects = await this._helper.GetProjects(envId, this.Channel.VersionRange);
                var projectStubs = await this._helper.GetProjectStubs();
                foreach (var projectStub in projectStubs)
                {
                    if (!groupIds.Contains(projectStub.ProjectGroupId)) {
                        continue;
                    }
                    this._loadingWindow.Show(this,
                        MiscStrings.LoadingProject.Replace("{project}", projectStub.ProjectName));
                    var project = await this._helper.ConvertProject(projectStub, envId, this.Channel.VersionRange);
                    var currentPackage = project.CurrentRelease.SelectedPackages.FirstOrDefault();
                    if (project.SelectedPackageStub == null ||
                        (currentPackage == null ? "" : currentPackage.Version) == project.SelectedPackageStub.Version ||
                        !project.AvailablePackages.Any())
                    {
                        project.Checked = false;
                    }
                    this.Projects.Add(project);
                }

                this.MainGrid.IsEnabled = true;
                SettingProjects = false;
                _loadingWindow.Hide();
            }
            else
            {
                _loadingWindow.Hide();
            }
        }

        private void ItemContainerGeneratorOnStatusChanged(object sender, EventArgs eventArgs)
        {
            foreach (var item in ((ItemContainerGenerator)sender).Items)
            {
                ListViewItem lvi = (ListViewItem) this.ProjectList.ItemContainerGenerator.ContainerFromItem(item);
                if (lvi != null)
                {
                    var comboBox = this.FindByName("packagecombo", lvi) as ComboBox;
                    if (comboBox.SelectedIndex < 0)
                    {
                        comboBox.SelectedIndex = 0;
                    }
                    if (comboBox.Items.Count == 0)
                    {
                        lvi.IsEnabled = false;
                        lvi.Background = new SolidColorBrush(Colors.LightGray);
                    }
                }
            }
        }

        private async Task SetChannels()
        {
            this.ShowLoadingDialog(MiscStrings.TitleLoadingChannels, MiscStrings.TitleLoadingChannels);
            int index = 0;
            if (this.Channels == null)
            {
                this.Channels = new ObservableCollection<Channel>();
            }
            else
            {
                this.Channels.Clear();
            }
            this.Channel = null;

            var channelsForProject =
                await this._helper.GetChannelsByProjectName(this._configuration.ChannelSeedProjectName);
            int counter = 0;
            foreach (var channel in channelsForProject)
            {
                if (channel.Name.Equals("default", StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }
                if (this.Channel == null)
                {
                    this.Channel = channel;
                }
                this.Channels.Add(new Channel {Id = channel.Id, Name = channel.Name});
                if (!string.IsNullOrEmpty(this._configuration.DefaultChannelName))
                {
                    if (channel.Name.ToLower() == "master")
                    {
                        index = counter;
                    }
                }
                else if (counter == 0)
                {
                    index = counter;
                }
                counter++;
            }

            this.ChannelsBox.SelectedIndex = index;
            this.SetProjects();
        }

        private async void ChannelsBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this._configuration.DisableAutoLoad)
            {
                this.SetComboBoxColour(this.LoadButton);
                return;
            }

            if (this.ChannelsBox.SelectedValue == null)
            {
                return;
            }

            this.Channel = await this._helper.GetChannel((string) this.ChannelsBox.SelectedValue);
            this.SetProjects();
        }

        private async void GenerateProfileButton_Click(object sender, RoutedEventArgs e)
        {
            await this.PrepareDeployment(true);
        }

        private async void DeployButton_Click(object sender, RoutedEventArgs e)
        {
            await this.PrepareDeployment();
        }

        private async Task PrepareDeployment(bool saveProfile = false)
        {
            this._loadingWindow.ShowDialog(this, DeploymentWindowStrings.LoadingBuildingDeploymentJob, DeploymentWindowStrings.LoadingGettingReadyToDeploy);
            var deployment = await ExtractSelectedDeployment(saveProfile);

            if (!saveProfile)
            {
                this._loadingWindow.SetStatus(DeploymentWindowStrings.LoadingValidatingDeployment);

                foreach (var project in deployment.ProjectDeployments)
                {
                    var lifeCyle = await this._helper.GetLifeCycle(project.LifeCycleId);
                    if (lifeCyle.Phases.Any())
                    {
                        var safe = false;
                        if (lifeCyle.Phases[0].OptionalDeploymentTargetEnvironmentIds.Any())
                        {
                            if (lifeCyle.Phases[0].OptionalDeploymentTargetEnvironmentIds.Contains(this.Environment.Id))
                            {
                                safe = true;
                            }
                        }
                        if (!safe && lifeCyle.Phases[0].AutomaticDeploymentTargetEnvironmentIds.Any())
                        {
                            if (lifeCyle.Phases[0].AutomaticDeploymentTargetEnvironmentIds.Contains(this.Environment.Id))
                            {
                                safe = true;
                            }
                        }
                        if (!safe)
                        {
                            MessageBox.Show(DeploymentWindowStrings.FailedValidation.Replace("{{projectname}}", project.ProjectName).Replace("{{environmentname}}", this.Environment.Name));
                            this._loadingWindow.Hide();
                            return;
                        }
                    }
                }

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("Going to deploy the following to " + deployment.EnvironmentName);
                sb.AppendLine("");

                foreach (var projDeploy in deployment.ProjectDeployments)
                {
                    sb.AppendLine(projDeploy.ProjectName + " : " + projDeploy.PackageName);
                }

                var result = MessageBox.Show(sb.ToString(),
                    "Are you sure you want to destroy " + deployment.EnvironmentName + "?", MessageBoxButton.YesNo);
                this._loadingWindow.Hide();
                if (result == MessageBoxResult.Yes)
                {
                    var jobWindow = this._windowProvider.CreateWindow<IDoJob>();
                    var task = jobWindow.StartDeployment(deployment);
                    jobWindow.ShowDialog();
                    await task;
                }
            }
            else
            {
                this._loadingWindow.Hide();
                var content = StandardSerialiser.SerializeToJsonNet(deployment, true);
                SaveFileDialog saveFileDialog = new SaveFileDialog();
                if (saveFileDialog.ShowDialog() == true)
                {
                    File.WriteAllText(saveFileDialog.FileName, content);
                }
            }
        }

        private async Task<EnvironmentDeployment> ExtractSelectedDeployment(bool saveProfile)
        {
            var deployment = new EnvironmentDeployment {
                EnvironmentId = this.Environment.Id,
                EnvironmentName = this.Environment.Name,
                ProjectDeployments = new List<ProjectDeployment>(),
                ChannelName = this.Channel.Name,
                DeployAsync = true
            };


            foreach (var item in this.ProjectList.Items)
            {
                var project = (Project) item;
                ListViewItem lvi = (ListViewItem) this.ProjectList.ItemContainerGenerator.ContainerFromItem(item);
                if (lvi != null)
                {
                    var comboBox = this.FindByName("packagecombo", lvi) as ComboBox;
                    var checkBox = this.FindByName("projectselectioncheckbox", lvi) as CheckBox;
                    if (checkBox.IsChecked.Value)
                    {
                        var channel = await this._helper.GetChannelByName(project.ProjectId, this.Channel.Name);
                        deployment.ProjectDeployments.Add(new ProjectDeployment
                        {
                            ProjectId = project.ProjectId,
                            ProjectName = project.ProjectName,
                            PackageId = saveProfile ? "latest" : comboBox.SelectedValue.ToString(),
                            PackageName = saveProfile ? "latest" : ((PackageStub) comboBox.SelectionBoxItem).Version,
                            StepName = saveProfile ? "latest" : ((PackageStub) comboBox.SelectionBoxItem).StepName,
                            ChannelId = channel.Id,
                            ChannelVersionRange = this.Channel.VersionRange,
                            LifeCycleId = project.LifeCycleId
                        });
                    }
                }
            }
            return deployment;
        }

        private FrameworkElement FindByName(string name, FrameworkElement root)
        {
            Stack<FrameworkElement> tree = new Stack<FrameworkElement>();
            tree.Push(root);

            while (tree.Count > 0)
            {
                FrameworkElement current = tree.Pop();
                if (current.Name == name)
                {
                    return current;
                }

                int count = VisualTreeHelper.GetChildrenCount(current);
                for (int i = 0; i < count; ++i)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(current, i);
                    if (child is FrameworkElement)
                    {
                        tree.Push((FrameworkElement) child);
                    }
                }
            }

            return null;
        }

        private void SetComboBoxColour(Button button)
        {
            button.Background = Brushes.LightPink;
        }

        private void ResetComboBoxColour(Button button)
        {
            button.ClearValue(Button.BackgroundProperty);
        }

        private async void EnvironmentsBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (this._configuration.DisableAutoLoad)
            {
                this.SetComboBoxColour(this.LoadButton);
                return;
            }

            if (this.EnvironmentsBox.SelectedValue == null)
            {
                return;
            }
            this.Environment = await this._helper.GetEnvironment((string) this.EnvironmentsBox.SelectedValue);
            if (this.Projects.Any())
            {
                this.SetProjects();
            }
        }

        private void DeselectAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in this.Projects) {
                item.Checked = false;
            }
        }

        private async void Viewchangelogbutton_OnClick(object sender, RoutedEventArgs e)
        {
            this._loadingWindow.ShowDialog(this, DeploymentWindowStrings.LoadingGettingChangeLogForProduct, DeploymentWindowStrings.LoadingGettingChangeLog);
            var button = (Button) sender;
            var project = (Project) button.DataContext;
            var fullTo = await this._helper.GetFullPackage(project.SelectedPackageStub);
            ChangeLogCollection changes = null;
            if (project.CurrentRelease != null && project.CurrentRelease.SelectedPackages.Any())
            {
                var fullFrom = await this._helper.GetFullPackage(project.CurrentRelease.SelectedPackages[0]);
                changes = this._changeLogProvider.GetChanges(fullFrom, fullTo, project);
            }
            else
            {
                changes = this._changeLogProvider.GetChanges(fullTo, project);
            }
            var changelog = this._windowProvider.CreateWindow<IChangeLog>();
            this._loadingWindow.Hide();
            changelog.Show();
            changelog.ShowChangesForProject(changes);
        }

        private void Projectselectioncheckbox_Onclick(object sender, RoutedEventArgs e) {

        }

        private void Packagecombo_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            
        }

        private void Close(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            this._allowProjectLoad = true;
            if (this.ChannelsBox.SelectedValue == null)
            {
                return;
            }

            if (this.EnvironmentsBox.SelectedValue == null)
            {
                return;
            }

            this.Channel = await this._helper.GetChannel((string)this.ChannelsBox.SelectedValue);
            this.Environment = await this._helper.GetEnvironment((string)this.EnvironmentsBox.SelectedValue);
            this.SetProjects();

            this.ResetComboBoxColour(this.LoadButton);
            this.ResetComboBoxColour(this.LoadButton);
        }

        private async void Viewhistorybutton_OnClick(object sender, RoutedEventArgs e)
        {
            this._loadingWindow.ShowDialog(this, DeploymentWindowStrings.LoadingGettingChangeLogForProduct, DeploymentWindowStrings.LoadingGettingChangeLog);
            var button = (Button)sender;
            var project = (Project)button.DataContext;
            var fullFrom = await this._helper.GetFullPackage(project.CurrentRelease.SelectedPackages[0]);
            ChangeLogCollection changes = this._changeLogProvider.GetChanges(fullFrom, project);
            var changelog = this._windowProvider.CreateWindow<IChangeLog>();
            this._loadingWindow.Hide();
            changelog.Show();
            changelog.ShowChangesForProject(changes);
        }
    }
}