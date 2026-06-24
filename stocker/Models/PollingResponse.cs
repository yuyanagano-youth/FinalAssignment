using System;
using System.Collections.Generic;
using System.Text;

namespace stocker.Models
{
    public class PollingResponse
    {
        public bool HasPendingJob { get; set; }

        public JobInfo? Job { get; set; }
    }
}
