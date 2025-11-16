using Microsoft.AspNetCore.Mvc;
using EMVBlacklist.API.Services;
using EMVBlacklist.Shared.Models;

namespace EMVBlacklist.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BlacklistController : ControllerBase
{
    private readonly BlacklistService _blacklistService;
    private readonly ILogger<BlacklistController> _logger;

    public BlacklistController(BlacklistService blacklistService, ILogger<BlacklistController> logger)
    {
        _blacklistService = blacklistService;
        _logger = logger;
    }

    [HttpGet("initial")]
    public ActionResult<InitialLoadResponse> GetInitialLoad([FromQuery] FilterType? filterType = null)
    {
        try
        {
            if (filterType.HasValue)
            {
                _blacklistService.SwitchFilter(filterType.Value);
            }

            var response = _blacklistService.GetInitialLoad();
            _logger.LogInformation(
                "Initial load requested. Filter: {FilterType}, Size: {Size} bytes, Count: {Count}",
                response.FilterType,
                response.FilterData.Length,
                response.TotalCount
            );

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting initial load");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("deltas")]
    public ActionResult<DeltaResponse> GetDeltas([FromBody] DeltaRequest request)
    {
        try
        {
            var response = _blacklistService.GetDeltas(request.LastTimestamp, request.Count);

            _logger.LogInformation(
                "Delta requested. LastTimestamp: {LastTimestamp}, Count: {Count}, Returned: {Returned}",
                request.LastTimestamp,
                request.Count,
                response.Deltas.Count
            );

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting deltas");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("validate")]
    public ActionResult<ValidationResponse> Validate([FromBody] ValidationRequest request)
    {
        try
        {
            var response = _blacklistService.Validate(request);

            _logger.LogInformation(
                "Validation requested. Tests: {Tests}, Correct: {Correct}, FP: {FP}, FN: {FN}, Accuracy: {Accuracy:P2}",
                response.TotalTests,
                response.CorrectMatches,
                response.FalsePositives,
                response.FalseNegatives,
                response.Accuracy
            );

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("status")]
    public ActionResult<object> GetStatus()
    {
        try
        {
            var filter = _blacklistService.GetFilter();
            var metadata = filter.GetMetadata();

            return Ok(new
            {
                FilterType = filter.FilterType.ToString(),
                Count = filter.Count,
                Metadata = metadata
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting status");
            return StatusCode(500, "Internal server error");
        }
    }
}
