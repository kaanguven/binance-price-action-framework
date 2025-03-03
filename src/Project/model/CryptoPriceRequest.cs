using System;

namespace Project.model
{
    public class OHLCRequest
    {
        public string Symbol { get; set; } = string.Empty;
        public string Timeframe { get; set; } = "1d"; 
        public int Limit { get; set; } = 500; 
        public DateTime? Since { get; set; } = null;

    }
}