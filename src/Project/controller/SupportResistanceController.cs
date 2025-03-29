using Microsoft.AspNetCore.Mvc;
using Project.model;
using Project.service;
using System;
using System.Threading.Tasks;

namespace Project.controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class SupportResistanceController : ControllerBase
    {
        private readonly SupportResistanceService _supportResistanceService;

        public SupportResistanceController(SupportResistanceService supportResistanceService)
        {
            _supportResistanceService = supportResistanceService;
        }

        [HttpGet("dynamic")]
        public async Task<ActionResult<SupportResistanceResponse>> GetDynamicSupportResistance([FromQuery] SupportResistanceRequest request)
        {
            if (string.IsNullOrEmpty(request.Symbol))
                return BadRequest("Symbol is required");

            if (string.IsNullOrEmpty(request.Interval))
                return BadRequest("Interval is required");

            try
            {
                var response = await _supportResistanceService.CalculateSupportResistance(
                    request.Symbol, 
                    request.Interval,
                    request.MultiplicativeFactor,
                    request.AtrLength,
                    request.ExtendLast,
                    request.Limit);
                
                if (response.Levels == null || response.Levels.Count == 0)
                    return NotFound("No support/resistance levels found for this symbol");

                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                // Log the exception
                Console.WriteLine($"Error calculating support/resistance: {ex.Message}");
                return StatusCode(500, "An error occurred while calculating support/resistance levels");
            }
        }
    }
} 