using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedModels
{
    public class GradeEvent
    {
        public string StudentName { get; set; }
        public string Subject { get; set; }
        public int Grade { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
