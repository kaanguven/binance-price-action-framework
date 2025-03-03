using System;
using System.Collections.Generic;

namespace Project.model
{
    public class LiquidityResponse
    {
        public string Symbol { get; set; } = string.Empty;
        public string Timeframe { get; set; } = string.Empty;
        public string Exchange { get; set; } = string.Empty;
        public List<LiquidityZone> LiquidityZones { get; set; } = new List<LiquidityZone>();
    }

    public class LiquidityZone
    {
        public string Type { get; set; } // "Buyside" veya "Sellside"
        public DateTime Date { get; set; }
        public int Index { get; set; }  
        public int PivotIndex { get; set; }
        public decimal PivotPrice { get; set; }
        public decimal TopPrice { get; set; }
        public decimal BottomPrice { get; set; }
        public decimal AveragePrice { get; set; }
        public bool IsBreached { get; set; }
        public bool IsActive { get; set; }
    }
}