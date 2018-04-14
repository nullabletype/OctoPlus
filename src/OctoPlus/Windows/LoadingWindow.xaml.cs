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
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using OctoPlus.Resources;
using OctoPlus.Windows.Interfaces;

namespace OctoPlus.Windows {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class LoadingWindow : ILoadingWindow
    {
        private const int GWL_STYLE = -16;
        private const int WS_SYSMENU = 0x80000;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public LoadingWindow()
        {
            InitializeComponent();
            this.Loaded += OnLoaded;
        }

        private void AttemptToCentre(Window owner) 
        {
            if (owner != null) 
            {
                this.Left = ((owner.Width / 2) - (this.ActualWidth / 2)) + owner.Left;
                this.Top = ((owner.Height / 2) - (this.ActualHeight / 2)) + owner.Top;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs routedEventArgs)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) & ~WS_SYSMENU);
        }

        public void Show(Window owner, string initialStatus, string windowLabel = null)
        {
            this.Title = windowLabel ?? MiscStrings.Loading;
            StatusLabel.Content = initialStatus;
            this.AttemptToCentre(owner);
            if (this.Visibility != Visibility.Visible) 
            {
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Send,
                                                       new Action(this.Show));
            }
        }

        public void ShowDialog(Window owner, string initialStatus, string windowLabel = null) {
            this.Title = windowLabel ?? MiscStrings.Loading;
            StatusLabel.Content = initialStatus;
            this.AttemptToCentre(owner);
            if (this.Visibility != Visibility.Visible) 
            {
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Send,
                                                       new Action(() => { this.ShowDialog(); }));
            }
        }

        public void SetStatus(string status, string windowLabel = null)
        {

            Application.Current.Dispatcher.BeginInvoke(
                DispatcherPriority.Send,
                new Action(() =>
                {
                    if (windowLabel != null) 
                    {
                        this.Title = windowLabel;
                    }
                    StatusLabel.Content = status;
                }));
        }

    }
}