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
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace OctoPlus.Utilities {
    public interface IWebRequestHelper
    {
        T GetXmlWebRequestWithBasicAuth<T>(string url, string username, string password);
    }

    class WebRequestHelper : IWebRequestHelper
    {

        public T GetXmlWebRequestWithBasicAuth<T>(string url, string username, string password) {
            WebRequest request = WebRequest.Create(url);
            request.Credentials = new NetworkCredential(username, password);
            request.PreAuthenticate = true;
            var response = request.GetResponse();
            using (Stream stream = response.GetResponseStream()) {
                var serializer = new XmlSerializer(typeof(T));
                if (stream != null) {
                    var document = (T)serializer.Deserialize(stream);
                    return document;
                }
            }
            return default(T);
        }

    }
}
