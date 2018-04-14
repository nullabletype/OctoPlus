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


using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using log4net.Appender;
using log4net.Config;
using log4net.Core;
using log4net.Layout;
using Octopus.Client;
using OctoPlusCore.Configuration;
using OctoPlusCore.Configuration.Interfaces;
using OctoPlus.StructureMap;
using StructureMap;
using OctoPlus.Configuration;

namespace OctoPlus.Startup
{
    public class BootStrap
    {
        public static async Task<InitResult> CheckConfigurationAndInit()
        {
            var container = StructureMap();
            var configurationProvider = container.GetInstance<ConfigurationProvider>();
            var result = await configurationProvider.LoadConfiguration();
            if (!result.Success)
            {
                return new InitResult
                {
                    container = container,
                    ConfigResult = result
                };
            }
            Log4net(result.Configuration.EnableTrace, false);
            var log = container.GetInstance<ILogger<BootStrap>>();
            log.Info("OctoPlus started...");
            var configuration = result.Configuration;
            var endpoint = new OctopusServerEndpoint(configuration.OctopusUrl, configuration.ApiKey);
            IOctopusAsyncClient client = null;
            Task.Run(async () => { client = await OctopusAsyncClient.Create(endpoint); }).Wait();
            log.Info("Created the octopus client");
            container.Configure(c =>
            {
                c.For<IConfiguration>().Use(result.Configuration).Singleton();
                c.For<IOctopusAsyncClient>().Use(client);
            });
            log.Info("Set configuration in structuremap");
            return new InitResult
            {
                container = container,
                ConfigResult = result
            };
        }

        private static IContainer StructureMap()
        {
            return new Container(c =>
            {
                c.AddRegistry<ConfigurationRegistry>();
                c.AddRegistry<WindowRegistry>();
                c.AddRegistry<UtilitiesRegistry>();
                c.AddRegistry<MiscRegistry>();
            });
        }

        private static void Log4net(bool trace, bool console)
        {
            var layout = new PatternLayout("%-4timestamp [%thread] %-5level %logger %ndc - %message%newline");
            var errorAppender = new RollingFileAppender {
                File = "errors.log",
                Layout = layout,
                Name = "ErrorLog",
                AppendToFile = true,
                ImmediateFlush = true,
                MaxSizeRollBackups = 1,
                MaximumFileSize = "5MB",
                RollingStyle = RollingFileAppender.RollingMode.Size,
                Threshold = Level.Error
            };

            layout.ActivateOptions();
            errorAppender.ActivateOptions();

            if (trace)
            {
                var traceAppender = new RollingFileAppender {
                    File = "trace.log",
                    Layout = layout,
                    Name = "TraceLog",
                    AppendToFile = true,
                    ImmediateFlush = true,
                    MaxSizeRollBackups = 1,
                    MaximumFileSize = "5MB",
                    RollingStyle = RollingFileAppender.RollingMode.Size,
                    Threshold = Level.Info
                };
                traceAppender.ActivateOptions();
                BasicConfigurator.Configure(traceAppender);
            }

            if (trace) {
                var consoleAppender = new ColoredConsoleAppender {
                    Layout = layout,
                    Name = "ConsoleLog",
                    Threshold = Level.Error,
                };
                consoleAppender.ActivateOptions();
                BasicConfigurator.Configure(consoleAppender);
            }

            BasicConfigurator.Configure(errorAppender);
        }
    }
}