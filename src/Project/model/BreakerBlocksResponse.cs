
namespace Project.model
{
    public class BreakerBlocksResponse
    {
        public string Symbol { get; set; } = string.Empty;
        public string Timeframe { get; set; } = string.Empty;
        public string Exchange { get; set; } = string.Empty;
        public List<BreakerBlock> BreakerBlocks { get; set; } = new List<BreakerBlock>();
    }

    public class BreakerBlock
    {
        public string Type { get; set; } 
        public DateTime Date { get; set; }
        public int Index { get; set; }
        public int CandleIndex { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Average { get; set; }
        public bool IsBroken { get; set; }
        public bool IsMitigated { get; set; }
    }
}