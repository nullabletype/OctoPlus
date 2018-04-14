using OctoPlusCore.Models.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace OctoPlusCore.ChangeLogs
{
    class VersionedProject : IVersionedProject 
    {
        public bool Checked { get; set; }
        public string LifeCycleId { get; set; }
        public string ProjectGroupId { get; set; }
        public string ProjectId { get; set; }
        public string ProjectName { get; set; }
    }
}
