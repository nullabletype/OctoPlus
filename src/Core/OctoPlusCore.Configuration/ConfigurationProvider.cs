using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace OctoPlusCore.Configuration 
{
    public static class ConfigurationProvider 
    {
        public async static Task<ConfigurationLoadResult> LoadConfiguration(ConfigurationProviderTypes type) 
        {
            if (type == ConfigurationProviderTypes.Json) 
            {
                return await new JsonConfigurationProvider().LoadConfiguration();
            }

            return null;
        }
    }

    public enum ConfigurationProviderTypes 
    {
        Json
    }
}
