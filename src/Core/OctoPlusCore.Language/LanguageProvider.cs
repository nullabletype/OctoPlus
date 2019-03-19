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
using System.Resources;
using System.Threading;

namespace OctoPlusCore.Language 
{
    public class LanguageProvider : ILanguageProvider 
    {
        private static Dictionary<LanguageSection, ResourceManager> resManLookUp;

        static LanguageProvider()
        {
            resManLookUp = new Dictionary<LanguageSection, ResourceManager>();
        }

        public string GetString(LanguageSection section, string key)
        {
            return GetResourceManager(section).GetString(key, Thread.CurrentThread.CurrentCulture);
        }

        private ResourceManager GetResourceManager(LanguageSection managerName) 
        {
            if (!resManLookUp.ContainsKey(managerName))
            {
                resManLookUp.Add(managerName, new ResourceManager(this.GetType().Namespace + ".Resources." + managerName, this.GetType().Assembly));
            }
            return resManLookUp[managerName];
        }

    }
}