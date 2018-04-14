using System.Collections.Generic;

namespace OctoPlusCore.Models.Interfaces {
    public interface IVersionedProject {
        bool Checked { get; }
        string LifeCycleId { get; }
        string ProjectGroupId { get; }
        string ProjectId { get; }
        string ProjectName { get; }
    }
}