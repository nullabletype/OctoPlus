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
using System.IO;
using Newtonsoft.Json;

namespace OctoPlusCore.Utilities
{
    public class StandardSerialiser
    {
        public static T DeserializeFromJsonNet<T>(string form, bool handleDerivedTypes = false)
        {
            T result = default(T);

            if (!string.IsNullOrEmpty(form))
            {
                var settings = new JsonSerializerSettings {DateFormatHandling = DateFormatHandling.IsoDateFormat};
                if (handleDerivedTypes)
                {
                    settings.TypeNameHandling = TypeNameHandling.All;
                }
                JsonSerializer serializer = JsonSerializer.Create(settings);

                using (var reader = new StringReader(form))
                {
                    try
                    {
                        result = serializer.Deserialize<T>(new JsonTextReader(reader));
                    }
                    catch
                    {
                        T val;

                        if (typeof(T).IsArray)
                        {
                            val = (T) Activator.CreateInstance(typeof(T), new[] {1});
                        }
                        else
                        {
                            val = (T) Activator.CreateInstance(typeof(T));
                        }

                        if (val is List<string>)
                        {
                            var list = val as List<string>;
                            list.Add(form);
                            return val;
                        }

                        return val;
                    }
                }
            }

            return result;
        }

        public static string SerializeToJsonNet<T>(T obj, bool handleDerivedTypes = false)
        {
            if (obj != null)
            {
                var settings = new JsonSerializerSettings {DateFormatHandling = DateFormatHandling.IsoDateFormat};
                if (handleDerivedTypes)
                {
                    settings.TypeNameHandling = TypeNameHandling.All;
                }
                JsonSerializer serializer = JsonSerializer.Create(settings);

                using (var textWriter = new StringWriter())
                {
                    serializer.Serialize(textWriter, obj);
                    return textWriter.ToString();
                }
            }

            return string.Empty;
        }
    }
}