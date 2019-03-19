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
using OctoPlusCore.Configuration.Interfaces;
using OctoPlusCore.Language;

namespace OctoPlusCore.Configuration
{
    public abstract class ConfigurationImplementation
    {
        protected ILanguageProvider languageProvider;

        public ConfigurationImplementation(ILanguageProvider languageProvider)
        {
            this.languageProvider = languageProvider;
        }
        public abstract Task<ConfigurationLoadResult> LoadConfiguration();

        protected virtual IConfiguration GetSampleConfig()
        {
            return new Configuration
            {
                ApiKey = languageProvider.GetString(LanguageSection.ConfigurationStrings, "SampleApiKey"),
                ChannelSeedProjectName = languageProvider.GetString(LanguageSection.ConfigurationStrings, "SampleChannelSeedAppName"),
                OctopusUrl = languageProvider.GetString(LanguageSection.ConfigurationStrings, "SampleOctopusUrl"),
                ProjectGroupFilterString = languageProvider.GetString(LanguageSection.ConfigurationStrings, "SampleProjectGroupFilterString"),
                DefaultChannel = "master"
            };
        }
    }
}