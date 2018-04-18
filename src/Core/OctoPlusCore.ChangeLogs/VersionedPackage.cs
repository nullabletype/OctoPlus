using OctoPlusCore.Models.Interfaces;

namespace OctoPlusCore.ChangeLogs {
    public class VersionedPackage : IVersionedPackage 
    {
        public string Id { get; set; }
        public string Message { get; set; }
        public string StepName { get; set; }
        public string Version { get; set; }
    }
}
