using OctoPlusCore.Models;
using OctoPlusCore.Models.Interfaces;

namespace OctoPlus.Proxies 
{
    class VersionedProjectProxy : IVersionedProject
    {
        private Project project;

        public VersionedProjectProxy(Project project) 
        {
            this.project = project;
        }

        public bool Checked { get => this.project.Checked; }
        public string LifeCycleId { get => this.project.LifeCycleId; }
        public string ProjectGroupId { get => this.project.ProjectGroupId; }
        public string ProjectId { get => this.project.ProjectId; }
        public string ProjectName { get => this.project.ProjectName; }
    }
}
