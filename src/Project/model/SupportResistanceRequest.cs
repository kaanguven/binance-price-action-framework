using System.ComponentModel.DataAnnotations;

namespace Project.model
{
    public class SupportResistanceRequest
    {
        [Required(ErrorMessage = "Symbol is required")]
        [RegularExpression(@"^[A-Za-z0-9]+$", ErrorMessage = "Symbol must only contain letters and numbers")]
        public string Symbol { get; set; }
        
        [Required(ErrorMessage = "Interval is required")]
        [RegularExpression(@"^(1m|3m|5m|15m|30m|1h|2h|4h|6h|8h|12h|1d|3d|1w|1M)$", 
            ErrorMessage = "Invalid interval. Valid values: 1m, 3m, 5m, 15m, 30m, 1h, 2h, 4h, 6h, 8h, 12h, 1d, 3d, 1w, 1M")]
        public string Interval { get; set; } = "1h";
        
        [Range(0.1, 100, ErrorMessage = "Multiplicative Factor must be between 0.1 and 100")]
        public float MultiplicativeFactor { get; set; } = 8.0f;
        
        [Range(5, 500, ErrorMessage = "ATR Length must be between 5 and 500")]
        public int AtrLength { get; set; } = 50;
        
        [Range(1, 20, ErrorMessage = "Extend Last must be between 1 and 20")]
        public int ExtendLast { get; set; } = 4;

        [Range(10, 1000, ErrorMessage = "Limit must be between 10 and 1000")]
        public int Limit { get; set; } = 500;
    }
} 