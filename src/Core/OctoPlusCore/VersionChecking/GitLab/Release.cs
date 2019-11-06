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


using Newtonsoft.Json;

namespace OctoPlusCore.VersionChecking.GitLab
{
    internal class Release : IRelease
    {

        public string Url { get { return "https://gitlab.com/nullabletype/OctoPlus/releases"; } set { } }

        [JsonProperty("name")]
        public string Name { get; set; }

        public string CurrentVersion { get; set; }

        [JsonProperty("tag_name")]
        public string TagName { get; set; }

        public bool PreRelease { get { return true; } set { } }

        [JsonProperty("description")]
        public string ChangeLogMarkDown { get; set; }

        public string ChangeLog 
        {
            get 
            {
                var md = new MarkdownDeep.Markdown 
                {
                    SummaryLength = -1
                };
                return md.Transform(ChangeLogMarkDown);
            }
            set { }
        }

        public IAsset[] Assets { get => throw new System.NotImplementedException(); set => throw new System.NotImplementedException(); }
    }
}
