using Microsoft.AspNetCore.Mvc;
using Project.model;
using Project.service;

namespace Project.controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class ElliottWaveController : ControllerBase
    {
        private readonly ElliottWaveService _elliottWaveService;
        
        public ElliottWaveController(ElliottWaveService elliottWaveService)
        {
            _elliottWaveService = elliottWaveService;
        }
        
        [HttpGet]
        public async Task<ActionResult<ElliottWaveResponse>> GetElliottWaves(
            [FromQuery] string symbol,
            [FromQuery] string timeframe = "1d",
            [FromQuery] int length1 = 4,
            [FromQuery] int length2 = 8,
            [FromQuery] int length3 = 16,
            [FromQuery] bool useLength1 = true,
            [FromQuery] bool useLength2 = true,
            [FromQuery] bool useLength3 = true,
            [FromQuery] decimal fibLevel1 = 0.5m,
            [FromQuery] decimal fibLevel2 = 0.618m,
            [FromQuery] decimal fibLevel3 = 0.764m,
            [FromQuery] decimal fibLevel4 = 0.854m,
            [FromQuery] int limit = 1000,
            [FromQuery] long? since = null)
        {
            try
            {
                var request = new ElliottWaveRequest
                {
                    Symbol = symbol,
                    Timeframe = timeframe,
                    Length1 = length1,
                    Length2 = length2,
                    Length3 = length3,
                    UseLength1 = useLength1,
                    UseLength2 = useLength2,
                    UseLength3 = useLength3,
                    FibLevel1 = fibLevel1,
                    FibLevel2 = fibLevel2,
                    FibLevel3 = fibLevel3,
                    FibLevel4 = fibLevel4,
                    Limit = limit,
                    Since = since.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(since.Value).DateTime : (DateTime?)null
                };
                
                var response = await _elliottWaveService.CalculateElliottWaves(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                return BadRequest($"Hata oluştu: {ex.Message}");
            }
        }
    }
}