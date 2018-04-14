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
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using OctoPlusCore.ChangeLogs;
using OctoPlusCore.Dtos;
using OctoPlus.Resources;
using OctoPlus.Windows.Interfaces;

namespace OctoPlus.Windows {
    /// <summary>
    /// Interaction logic for ChangeLog.xaml
    /// </summary>
    public partial class ChangeLog : IChangeLog
    {
        public ChangeLog() {
            InitializeComponent();
        }

        public void ShowChangesForAll(IEnumerable<ChangeLogCollection> logCollections)
        {
            var issues = logCollections.ToArray().SelectMany(c => c.Changes.SelectMany(i => i.Issues)).Distinct().ToArray();

            if (issues.Any())
            {
                foreach (var issue in issues)
                {
                    RenderIssue(issue);
                }
            }

            foreach (var log in logCollections)
            {
                ShowChangesForProject(log);
            }
        }

        public void ShowChangesForProject(ChangeLogCollection logCollection)
        {
            this.Title = logCollection.Project.ProjectName;
            if (!logCollection.Changes.Any())
            {
                this.ChangeLogBox.Inlines.Add(ChangeLogStrings.NoChangesFound);
                return;
            }
            this.ChangeLogBox.Inlines.Add(new Run {
                Text = logCollection.Project.ProjectName,
                FontSize = 16,
                FontWeight = FontWeights.Bold
            });
            this.ChangeLogBox.Inlines.Add(System.Environment.NewLine);
            this.ChangeLogBox.Inlines.Add(System.Environment.NewLine);
            foreach (var change in logCollection.Changes)
            {
                RenderChange(change);
            }
        }

        private void RenderChange(ChangeLogChange change)
        {
            this.ChangeLogBox.Inlines.Add(change.Date + " : " + change.Username + " : ");
            var issueHyperLink = new Hyperlink(new Run(change.Version))
            {
                NavigateUri = new Uri(change.WebUrl)
            };
            issueHyperLink.RequestNavigate += Hyperlink_RequestNavigate;
            this.ChangeLogBox.Inlines.Add(issueHyperLink);

            if (change.Issues.Any())
            {
                this.ChangeLogBox.Inlines.Add(System.Environment.NewLine);
                this.ChangeLogBox.Inlines.Add(ChangeLogStrings.Issues + ": ");
                foreach (var issue in change.Issues)
                {
                    RenderIssue(issue);
                }
            }

            this.ChangeLogBox.Inlines.Add(System.Environment.NewLine);
            this.ChangeLogBox.Inlines.Add(change.Comment);
            this.ChangeLogBox.Inlines.Add(System.Environment.NewLine);
            this.ChangeLogBox.Inlines.Add("_________");
            this.ChangeLogBox.Inlines.Add(System.Environment.NewLine + System.Environment.NewLine);
        }

        private void RenderIssue(Issue issue)
        {
            var hyperLink = new Hyperlink(new Run(issue.Name))
            {
                NavigateUri = new Uri(issue.WebUrl)
            };
            hyperLink.RequestNavigate += Hyperlink_RequestNavigate;
            this.ChangeLogBox.Inlines.Add(hyperLink);
            this.ChangeLogBox.Inlines.Add(" ");
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e) {
            System.Diagnostics.Process.Start(e.Uri.ToString());
        }
    }
}
