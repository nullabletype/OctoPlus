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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OctoPlusCore.Configuration.Interfaces;
using OctoPlusCore.Octopus;
using OctoPlusCore.Configuration.Resources;
using OctoPlusCore.Logging;

namespace OctoPlusCore.Configuration
{
    public class JsonConfigurationProvider : ConfigurationImplementation
    {
        private const string ConfigurationFileName = "config.json";

        public override async Task<ConfigurationLoadResult> LoadConfiguration()
        {
            var loadResult = new ConfigurationLoadResult();
            if (!File.Exists(ConfigurationFileName))
            {
                var sampleConfig = this.GetSampleConfig();
                try
                {
                    File.WriteAllText(ConfigurationFileName,
                        JsonConvert.SerializeObject(sampleConfig, Formatting.Indented));
                    loadResult.Errors.Add(ConfigurationStrings.LoadNoFileFound);
                    return loadResult;
                }
                catch (Exception e)
                {
                    LoggingProvider.GetLogger<JsonConfigurationProvider>().Error("Failed to save sample config", e);
                }
                loadResult.Errors.Add(ConfigurationStrings.LoadNoFileFoundCantCreate);
                return loadResult;
            }

            var stringContent = File.ReadAllText(ConfigurationFileName);
            try
            {
                var config = JsonConvert.DeserializeObject<Configuration>(stringContent);
                await ValidateConfiguration(config, loadResult);
                if (loadResult.Success)
                {
                    loadResult.Configuration = config;
                    return loadResult;
                }
                return loadResult;
            }
            catch (Exception e)
            {
                LoggingProvider.GetLogger<JsonConfigurationProvider>().Error("Failed to parse config", e);
                loadResult.Errors.Add(ConfigurationStrings.LoadCouldntParseFile);
                return loadResult;
            }
        }

        private async Task ValidateConfiguration(IConfiguration config, ConfigurationLoadResult validationResult)
        {
            if (string.IsNullOrEmpty(config.OctopusUrl))
            {
                validationResult.Errors.Add(ConfigurationStrings.ValidationOctopusUrl);
            }

            if (string.IsNullOrEmpty(config.OctopusUrl)) {
                validationResult.Errors.Add(ConfigurationStrings.ValidationOctopusApiKey);
            }

            if (string.IsNullOrEmpty(config.ChannelSeedProjectName)) {
                validationResult.Errors.Add(ConfigurationStrings.ValidationSeedProject);
            }

            if (!validationResult.Errors.Any())
            {
                try
                {
                    var octoHelper = new OctopusHelper(config.OctopusUrl, config.ApiKey, null);
                    await octoHelper.GetEnvironments();
                    try 
                    {
                        if (!await octoHelper.ValidateProjectName(config.ChannelSeedProjectName)) 
                        {
                            validationResult.Errors.Add(ConfigurationStrings.ValidationSeedProjectNotValid);
                        }
                    } 
                    catch (Exception e) 
                    {
                        validationResult.Errors.Add(ConfigurationStrings.ValidationSeedProjectNotValid + ": " + e.Message);
                    }
                    
                }
                catch (Exception e)
                {
                    validationResult.Errors.Add(ConfigurationStrings.ValidationOctopusApiFailure + ": " + e.Message);
                }
            }
        }

    }
}