using System;
using System.Collections.Generic;
using System.Text;

namespace OctoPlusCore.Models
{
    public class Deployment {
        public string TaskId { get; set; }
        public string EnvironmentId { get; set; }
        public string ReleaseId { get; set; }
    }
}
