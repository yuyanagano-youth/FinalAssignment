using System;
using System.Collections.Generic;
using System.Text;

namespace stocker.Models
{
    public class PollingRequest
    {
        public string StockerId { get; set; } = string.Empty;

        public string CurrentOperationState {  get; set; } = string.Empty;
    }
}
