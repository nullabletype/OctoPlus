using System;
using System.Collections.Generic;
using System.Text;

namespace OctoPlusCore.Models 
{
    public class TaskDetails 
    {
        public int PercentageComplete { get; set; }
        public TaskStatus State { get; set; }
        public string TimeLeft { get; set; }
        public string TaskId { get; set; }
        public Dictionary<string, string> Links { get; set; }
    }
}
