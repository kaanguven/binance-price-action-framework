using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Project.model
{
    public class SrLevel
    {
        [JsonPropertyName("y")]
        public float Y { get; set; }         // Price level
        
        [JsonPropertyName("area")]
        public float Area { get; set; }       // Secondary price level forming the area
        
        [JsonIgnore]
        public long X { get; set; }           // Timestamp in milliseconds
        
        [JsonPropertyName("date")]
        public DateTime Date => DateTimeOffset.FromUnixTimeMilliseconds(X).DateTime;
        
        [JsonPropertyName("isSupport")]
        public bool IsSupport { get; set; }   // If true, it's a support level; otherwise, it's resistance
    }

    public class SupportResistanceResponse
    {
        [JsonPropertyName("levels")]
        public List<SrLevel> Levels { get; set; } = new List<SrLevel>();
        
        [JsonPropertyName("symbol")]
        public string Symbol { get; set; }
        
        [JsonPropertyName("timeframe")]
        public string Timeframe { get; set; }
        
        [JsonPropertyName("calculationTime")]
        public DateTime CalculationTime { get; set; } = DateTime.UtcNow;
    }
} 