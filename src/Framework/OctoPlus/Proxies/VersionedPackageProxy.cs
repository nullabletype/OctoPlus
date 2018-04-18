using OctoPlusCore;
using OctoPlusCore.Models.Interfaces;

namespace OctoPlus.Proxies {
    public class VersionedPackageProxy : IVersionedPackage 
    {
        private PackageFull package;

        public VersionedPackageProxy(PackageFull package) {
            this.package = package;
        }
        public string Id { get => this.package.Id; }
        public string Message { get => this.package.Message; }
        public string StepName { get => this.package.StepName; }
        public string Version { get => this.package.Version; }
    }
}
