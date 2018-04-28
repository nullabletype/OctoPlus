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

using CommandLine;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OctoPlus.Console.ConsoleOptions;
using OctoPlus.Console.Interfaces;
using OctoPlusCore.ChangeLogs.Interfaces;
using OctoPlusCore.ChangeLogs.TeamCity;
using OctoPlusCore.Configuration;
using OctoPlusCore.Configuration.Interfaces;
using OctoPlusCore.Deployment;
using OctoPlusCore.Deployment.Interfaces;
using OctoPlusCore.Logging;
using OctoPlusCore.Octopus;
using OctoPlusCore.Octopus.Interfaces;
using OctoPlusCore.Utilities;
using OctoPlusCore.VersionChecking;
using OctoPlusCore.VersionChecking.GitHub;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OctoPlus.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            System.Console.WriteLine("Starting in console mode...");
            var initResult = CheckConfigurationAndInit().GetAwaiter().GetResult();
            if (!initResult.Item1.Success) 
            {
                System.Console.Write(string.Join(Environment.NewLine, initResult.Item1.Errors));
                return;
            }
            var container = initResult.Item2;

            var app = new CommandLineApplication();
            app.Name = "OctoPlus";
            app.HelpOption("-?|-h|--help");
            

            Parser.Default.ParseArguments<OctoPlusDeployLatestOptions, OctoPlusListEnvironmentsOptions>(args)
                .MapResult(
                    (OctoPlusDeployLatestOptions opts) => DeployLatest(container, opts), 
                    (OctoPlusListEnvironmentsOptions opts) => ListEnvironments(container, opts), 
                    errs => 1);
        }

        public static async Task<Tuple<ConfigurationLoadResult, IServiceProvider>> CheckConfigurationAndInit() 
        {
            var log = LoggingProvider.GetLogger<Program>();
            log.Info("Attempting IoC configuration...");
            var container = IoC();
            log.Info("Attempting configuration load...");
            var configurationLoadResult = await ConfigurationProvider.LoadConfiguration(ConfigurationProviderTypes.Json);
            if (!configurationLoadResult.Success) 
            {
                log.Error("Failed to load config.");
                return new Tuple<ConfigurationLoadResult, IServiceProvider>(configurationLoadResult, container.BuildServiceProvider());
            }
            log.Info("OctoPlus started...");
            OctopusHelper.Init(configurationLoadResult.Configuration.OctopusUrl, configurationLoadResult.Configuration.ApiKey);
            container.AddSingleton<IOctopusHelper>(OctopusHelper.Default);
            container.AddSingleton<IConfiguration>(configurationLoadResult.Configuration);
            log.Info("Set configuration in IoC");
            return new Tuple<ConfigurationLoadResult, IServiceProvider>(configurationLoadResult, container.BuildServiceProvider());
        }

        private static IServiceCollection IoC() 
        {
            return new ServiceCollection()
            .AddLogging()
            .AddSingleton<ConfigurationImplementation, JsonConfigurationProvider>()
            .AddSingleton<IOctopusHelper, OctopusHelper>()
            .AddSingleton<IDeployer, Deployer>()
            .AddTransient<IChangeLogProvider, TeamCity>()
            .AddTransient<IWebRequestHelper, WebRequestHelper>()
            .AddTransient<IVersionCheckingProvider, GitHubVersionChecker>()
            .AddTransient<IVersionChecker, VersionChecker>()
            .AddTransient<IConsoleDoJob, ConsoleDoJob>();
        }

        private static int ListEnvironments(IServiceProvider container, OctoPlusListEnvironmentsOptions consoleOptions) 
        {
            System.Console.WriteLine("Environments: ");
            var envs = OctopusHelper.Default.GetEnvironments().GetAwaiter().GetResult();
            foreach(var env in envs)
            {
                System.Console.WriteLine($"{(env.Id)} : {(env.Name)}");
            }
            return 0;
        }
        private static int DeployLatest(IServiceProvider container, OctoPlusDeployLatestOptions consoleOptions) 
        {
            System.Console.WriteLine("Using profile at path " + consoleOptions.ProfileFile);
            var doJob = container.GetService<IConsoleDoJob>();
            doJob.StartJob(consoleOptions.ProfileFile, consoleOptions.ReleaseMessage, consoleOptions.ReleaseVersion,
                consoleOptions.ForceDeploymentIfSamePackage).GetAwaiter().GetResult();
            return 0;
        }
    }
}
