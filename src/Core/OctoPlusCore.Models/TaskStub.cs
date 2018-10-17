using System.Collections.Generic;

namespace OctoPlusCore.Models 
{
    public class TaskStub 
    {
        public TaskStatus State { get; set; }
        public string TaskId { get; set; }
        public bool IsComplete { get; set; }
        public bool FinishedSuccessfully { get; set; }
        public bool HasWarningsOrErrors { get; set; }
        public string ErrorMessage { get; set; }
        public Dictionary<string, string> Links { get; set; }
    }
}
