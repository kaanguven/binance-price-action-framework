using System;
using System.Collections.Generic;

namespace Project.model
{
    public class MarketStructureRequest
    {
        public string Symbol { get; set; } = string.Empty;
        public string Timeframe { get; set; } = "1d";
        public int Limit { get; set; } = 500;
        public DateTime? Since { get; set; } = null;
        public int ZigZagLength { get; set; } = 7;
        public float FibFactor { get; set; } = 0.33f;
        // Not including visualization or box management inputs like show_zigzag, text_size, delete_boxes
        // as they are less relevant for a backend API service's data output.
        // Colors are also for UI.
    }

    public class MarketStructureResponse
    {
        public string Symbol { get; set; } = string.Empty;
        public string Timeframe { get; set; } = string.Empty;
        public string Exchange { get; set; } = string.Empty;
        public List<SwingPoint> AllSwingPoints { get; set; } = new List<SwingPoint>();
        public List<IdentifiedOrderBlock> OrderBlocks { get; set; } = new List<IdentifiedOrderBlock>();
        public List<IdentifiedBreakerBlock> BreakerBlocks { get; set; } = new List<IdentifiedBreakerBlock>();
        public List<MarketStructureBreak> MarketStructureBreaks { get; set; } = new List<MarketStructureBreak>();
    }

    public class SwingPoint
    {
        public DateTime Date { get; set; }
        public int Index { get; set; }
        public decimal Price { get; set; }
        public string Type { get; set; } = string.Empty; // "High" or "Low"
    }

    public class BaseBlockInfo
    {
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public DateTime CandleDate { get; set; } // Date of the candle forming the block
        public int CandleIndex { get; set; }     // Index of the candle forming the block
        public DateTime MsbDate { get; set; }     // Date of MSB that created/confirmed this block
        public int MsbIndex { get; set; }         // Index of MSB candle
        public string MsbDirection { get; set; } = string.Empty; // "Bullish" or "Bearish" MSB
        public bool IsMitigated { get; set; } = false; // True if price has violated this block according to script rules
    }
    
    public class IdentifiedOrderBlock : BaseBlockInfo
    {
        public string OrderBlockType { get; set; } = string.Empty; // "Bu-OB" (Bullish Order Block) or "Be-OB" (Bearish Order Block)
    }

    public class IdentifiedBreakerBlock : BaseBlockInfo
    {
        public string BreakerBlockType { get; set; } = string.Empty; // "Bu-BB", "Bu-MB", "Be-BB", "Be-MB"
    }

    public class MarketStructureBreak
    {
        public DateTime Date { get; set; } // Date of the candle confirming the MSB
        public int Index { get; set; }    // Index of the candle confirming the MSB
        public string Type { get; set; } = string.Empty; // "Bullish" (broke above high) or "Bearish" (broke below low)
        
        public SwingPoint BrokenSwingPoint { get; set; } = new SwingPoint(); // The H1 or L1 that was broken
        public SwingPoint PrecedingSwingPoint { get; set; } = new SwingPoint(); // The L0 before H1 break, or H0 before L1 break
        
        // Swing points at the moment of break for context (H0, L0, H1, L1 from script)
        public SwingPoint H0_at_break { get; set; } = new SwingPoint();
        public SwingPoint L0_at_break { get; set; } = new SwingPoint();
        public SwingPoint H1_at_break { get; set; } = new SwingPoint();
        public SwingPoint L1_at_break { get; set; } = new SwingPoint();
    }
} 