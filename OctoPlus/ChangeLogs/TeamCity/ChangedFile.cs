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


using System.Xml.Serialization;

namespace OctoPlus.ChangeLogs.TeamCity
{
    public class ChangedFile
    {

        [XmlAttribute("before-revision")]
        public string BeforeRevision { get; set; }

        [XmlAttribute("after-revision")]
        public string AfterRevision { get; set; }

        [XmlAttribute("changeType")]
        public string ChangeType { get; set; }

        [XmlAttribute("file")]
        public string File { get; set; }

        [XmlAttribute("relative-file")]
        public string RelativeFile { get; set; }
    }
}