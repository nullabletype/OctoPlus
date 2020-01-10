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


using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using OctoPlus.Console.Commands;
using OctoPlusCore.ChangeLogs.Interfaces;
using OctoPlusCore.ChangeLogs.TeamCity;
using OctoPlusCore.Configuration;
using OctoPlusCore.Configuration.Interfaces;
using OctoPlusCore.Deployment;
using OctoPlusCore.Deployment.Interfaces;
using OctoPlusCore.Logging;
using OctoPlusCore.Logging.Interfaces;
using OctoPlusCore.Octopus;
using OctoPlusCore.Octopus.Interfaces;
using OctoPlusCore.Utilities;
using OctoPlusCore.VersionChecking;
using OctoPlusCore.VersionChecking.GitHub;
using System;
using System.Threading.Tasks;
using OctoPlus.Console.Commands.SubCommands;
using OctoPlus.Console.ConsoleTools;
using Microsoft.Extensions.Caching.Memory;
using System.Linq;
using System.IO;
using OctoPlusCore.VersionChecking.GitLab;
using OctoPlusCore.Language;
using OctoPlusCore.JobRunners.Interfaces;

namespace OctoPlus.Console
{
    class Program
    {
        static int Main(string[] args)
        {
            string cwd = Path.GetDirectoryName(System.Environment.GetCommandLineArgs()[0]);
            Directory.SetCurrentDirectory(cwd ?? ".");
            args = args.Select(a => a.Replace("action:", "--action")).ToArray();
            AppDomain.CurrentDomain.UnhandledException += HandleException;
            var initResult = CheckConfigurationAndInit().GetAwaiter().GetResult();
            if (!initResult.Item1.Success) 
            {
                System.Console.Write(string.Join(System.Environment.NewLine, initResult.Item1.Errors));
                return -1;
            }
            var container = initResult.Item2;

            var app = new CommandLineApplication();
            app.Name = "OctoPlus";
            app.HelpOption("-?|-h|--help");
            app.ThrowOnUnexpectedArgument = true;
            app.Conventions.UseConstructorInjection(container);

            var deployer = container.GetService<Deploy>();
            app.Command(deployer.CommandName, deploy => deployer.Configure(deploy));
            var promoter = container.GetService<Promote>();
            app.Command(promoter.CommandName, promote => promoter.Configure(promote));
            var environment = container.GetService<Commands.Environment>();
            app.Command(environment.CommandName, env => environment.Configure(env));
            var release = container.GetService<Release>();
            app.Command(release.CommandName, env => release.Configure(env));
            var variable = container.GetService<Variable>();
            app.Command(variable.CommandName, vari => variable.Configure(vari));

            app.OnExecute(() =>
            {
                app.ShowHelp();
            });
            
            return app.Execute(args);
        }

        private static void HandleException(object sender, UnhandledExceptionEventArgs e)
        {
            if(e.ExceptionObject is CommandParsingException)
            {
                var colorBefore = System.Console.ForegroundColor;
                System.Console.ForegroundColor = ConsoleColor.Red;
                System.Console.WriteLine($"Error: {(((CommandParsingException)e.ExceptionObject).Message)}");
                System.Console.ForegroundColor = colorBefore;
                System.Console.WriteLine();
                System.Console.WriteLine("Command wasn't recognised. Try -? for help if you're stuck.");
                System.Console.WriteLine();
                System.Environment.Exit(1);
            }
        }

        public static async Task<Tuple<ConfigurationLoadResult, IServiceProvider>> CheckConfigurationAndInit() 
        {
            var log = LoggingProvider.GetLogger<Program>();
            log.Info("Attempting IoC configuration...");
            var container = IoC();
            log.Info("Attempting configuration load...");
            var configurationLoadResult = await ConfigurationProvider.LoadConfiguration(ConfigurationProviderTypes.Json, new LanguageProvider()); //todo: fix this!
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

            var serviceProvider = container.BuildServiceProvider();
            //Temporary filth
            serviceProvider.GetService<IOctopusHelper>().SetCacheImplementation(serviceProvider.GetService<IMemoryCache>(), configurationLoadResult.Configuration.CacheTimeoutInSeconds);

            var versionChecker = serviceProvider.GetService<IVersionChecker>();
            var checkResult = await versionChecker.GetLatestVersion();

            if (checkResult.NewVersion) {
                ShowNewVersionMessage(checkResult, serviceProvider.GetService<ILanguageProvider>());
            }

            return new Tuple<ConfigurationLoadResult, IServiceProvider>(configurationLoadResult, serviceProvider);
        }

        private static void ShowNewVersionMessage(VersionCheckResult checkResult, ILanguageProvider languageProvider) {
            System.Console.WriteLine("-------------------------------------");
            System.Console.WriteLine(languageProvider.GetString(LanguageSection.UiStrings, "NewVersionAvailable"));
            System.Console.WriteLine(string.Format(languageProvider.GetString(LanguageSection.UiStrings, "CurrentVersion"), checkResult.Release.CurrentVersion));
            System.Console.WriteLine(string.Format(languageProvider.GetString(LanguageSection.UiStrings, "NewVersion"), checkResult.Release.TagName));
            if (checkResult.Release.Assets != null && checkResult.Release.Assets.Any())
            {
                foreach(var asset in checkResult.Release.Assets)
                System.Console.WriteLine(string.Format(languageProvider.GetString(LanguageSection.UiStrings, "DownloadAvailableHere"), asset.Name, asset.DownloadUrl));
            }
            else
            {
                System.Console.WriteLine(string.Format(languageProvider.GetString(LanguageSection.UiStrings, "UpdateAvailableHere"), checkResult.Release.Url));
            }
            if (!string.IsNullOrEmpty(checkResult.Release.ChangeLog)) {
                System.Console.WriteLine(languageProvider.GetString(LanguageSection.UiStrings, "ChangeLog"));
                System.Console.WriteLine(checkResult.Release.ChangeLog);
            }
            System.Console.WriteLine("-------------------------------------");
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
            .AddTransient<IJobRunner, ConsoleJobRunner>()
            .AddTransient<Deploy, Deploy>()
            .AddTransient<Promote, Promote>()
            .AddTransient<Release, Release>()
            .AddTransient<RenameRelease, RenameRelease>()
            .AddTransient<UpdateReleaseVariables, UpdateReleaseVariables>()
            .AddTransient<DeployWithProfile, DeployWithProfile>()
            .AddTransient<DeployWithProfileDirectory, DeployWithProfileDirectory>()
            .AddTransient<EnsureEnvironment, EnsureEnvironment>()
            .AddTransient<DeleteEnvironment, DeleteEnvironment>()
            .AddTransient<EnvironmentToTeam, EnvironmentToTeam>()
            .AddTransient<EnvironmentToLifecycle, EnvironmentToLifecycle>()
            .AddTransient<Variable, Variable>()
            .AddTransient<VariablesWithProfile, VariablesWithProfile>()
            .AddTransient<Commands.Environment, Commands.Environment>()
            .AddTransient<IUiLogger, ConsoleJobRunner>()
            .AddTransient<IProgressBar, ProgressBar>().AddMemoryCache()
            .AddTransient<ILanguageProvider, LanguageProvider>().AddMemoryCache()
            .AddTransient<DeployWithProfileDirectoryRunner, DeployWithProfileDirectoryRunner>();
        }
    }
}
