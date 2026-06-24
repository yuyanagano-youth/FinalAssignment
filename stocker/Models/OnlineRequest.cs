using System;
using System.Collections.Generic;
using System.Text;

namespace stocker.Models
{
    public class OnlineRequest
    {
        public string StockerId { get; set; } = string.Empty;

        public string ConnectionStatus { get; set; } = string.Empty;
    }
}
