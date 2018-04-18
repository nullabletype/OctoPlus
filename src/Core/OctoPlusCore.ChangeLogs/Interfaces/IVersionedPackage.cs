namespace OctoPlusCore.Models.Interfaces {
    public interface IVersionedPackage {
        string Id { get; }
        string Message { get; }
        string StepName { get; }
        string Version { get; }
    }
}