using Microsoft.AspNetCore.Mvc;
using Project.model;
using Project.service;
using System.Threading.Tasks;

namespace Project.controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class CryptoController : ControllerBase
    {
        private readonly CryptoService _cryptoService;

        public CryptoController(CryptoService cryptoService)
        {
            _cryptoService = cryptoService;
        }

        [HttpGet("fetchOhlcv")] 
        public async Task<ActionResult<OHLCResponse>> GetOHLCData([FromQuery] OHLCRequest request)
        {
            if (string.IsNullOrEmpty(request.Symbol))
                return BadRequest("Symbol is required");

            var response = await _cryptoService.FetchOHLCAsync(request);
            
            if (response.Candles == null || response.Candles.Count == 0)
                return NotFound("No OHLC data found for this symbol");

            return Ok(response);
        }

        [HttpGet("breakerBlocks")]
        public async Task<ActionResult<BreakerBlocksResponse>> GetBreakerBlocks([FromQuery] OHLCRequest request)
        {
            if (string.IsNullOrEmpty(request.Symbol))
                return BadRequest("Symbol is required");

            var response = await _cryptoService.FetchBreakerBlocksAsync(request);
            
            if (response.BreakerBlocks == null || response.BreakerBlocks.Count == 0)
                return NotFound("No breaker blocks found for this symbol");

            return Ok(response);
        }

        [HttpGet("liquidityZones")]
        public async Task<ActionResult<LiquidityResponse>> GetLiquidityZones([FromQuery] OHLCRequest request)
        {
            if (string.IsNullOrEmpty(request.Symbol))
                return BadRequest("Symbol is required");

            var response = await _cryptoService.FetchLiquidityZonesAsync(request);
            
            if (response.LiquidityZones == null || response.LiquidityZones.Count == 0)
                return NotFound("No liquidity zones found for this symbol");

            return Ok(response);
        }
    }
}