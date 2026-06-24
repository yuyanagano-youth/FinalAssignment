using System;
using System.Collections.Generic;
using System.Text;

namespace stocker.Models
{
    public class CommandResponse
    {
        public string StockerId { get; set; }= string.Empty;

        public string? JobId { get; set; }

        public string? JobStatus { get; set; }
    }
}
