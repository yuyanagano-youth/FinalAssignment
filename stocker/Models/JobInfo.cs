using System;
using System.Collections.Generic;
using System.Text;
using stocker.Enums;

namespace stocker.Models
{
    public class JobInfo
    {
        public string? JobId { get; set; }

        public string? Command { get; set;}

        public string? CarrierId { get; set;}

        public string? Source { get; set;}

        public string? Destination { get; set; }
    }
}
