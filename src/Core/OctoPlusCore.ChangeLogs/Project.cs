using System;
using System.Collections.Generic;
using System.Text;

namespace OctoPlusCore.ChangeLogs
{
    public class Project
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string WebUrl { get; set; }
        public List<BuildResult> Builds { get; set; }
    }
}
