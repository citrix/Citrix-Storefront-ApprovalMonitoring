using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SFWorkflowService.Classes
{
    public class WorkflowApplicationRequest
    {
        public string type { get; set; }
        public string user { get; set; }
        public string application { get; set; }
        public string monitordeviceid { get; set; }
        public string manageremail { get; set; }
    }
}
