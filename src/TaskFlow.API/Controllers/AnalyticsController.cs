using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Features.Analytics.DTOs;
using TaskFlow.Application.Features.Analytics.Interfaces;

namespace TaskFlow.API.Controllers;

[ApiController]
[Authorize]
[Route("api/analytics")]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _service;

    public AnalyticsController(
        IAnalyticsService service
    )
    {
        _service = service;
    }

    private Guid UserId =>
        Guid.Parse(
            User.FindFirstValue(
                ClaimTypes.NameIdentifier
            )!
        );

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery]
        AnalyticsPeriod period = AnalyticsPeriod.Week,
        [FromQuery]
        DateTime? referenceDate = null
    )
    {
        return Ok(
            await _service.GetAsync(
                UserId,
                period,
                referenceDate ?? DateTime.UtcNow
            )
        );
    }
}
