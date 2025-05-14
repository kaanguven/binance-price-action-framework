using Microsoft.AspNetCore.Mvc;
using Project.model;
using Project.service;
using System.Threading.Tasks;

namespace Project.controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class MarketStructureController : ControllerBase
    {
        private readonly IMarketStructureService _marketStructureService;

        public MarketStructureController(IMarketStructureService marketStructureService)
        {
            _marketStructureService = marketStructureService;
        }

        [HttpGet("calculate")]
        public async Task<ActionResult<MarketStructureResponse>> GetMarketStructure([FromQuery] MarketStructureRequest request)
        {
            if (string.IsNullOrEmpty(request.Symbol))
                return BadRequest("Symbol is required.");
            if (string.IsNullOrEmpty(request.Timeframe))
                return BadRequest("Timeframe is required.");
            if (request.ZigZagLength <= 1)
                return BadRequest("ZigZagLength must be greater than 1.");
            if (request.Limit <= request.ZigZagLength * 2)
                 return BadRequest($"Limit must be greater than ZigZagLength * 2 (currently {request.ZigZagLength * 2}).");

            try
            {
                var response = await _marketStructureService.CalculateMarketStructureAsync(request);
                return Ok(response);
            }
            catch (System.Exception ex)
            {
                // Log the exception (e.g., using ILogger)
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
    }
} 