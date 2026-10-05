using BuildingBlocks.Security.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Test.Application.Interfaces;
using Test.Contracts.DTOs;

namespace Test.Api.Controllers;

[ApiController]
[Route("api/tests")]
[Authorize]
public class TestController : ControllerBase
{
    private readonly ITestService _testService;

    public TestController(ITestService testService)
    {
        _testService = testService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateSession(
        [FromBody] CreateTestSessionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = User.GetRequiredUserId();
            var session = await _testService.CreateSessionAsync(userId, request, cancellationToken);
            return CreatedAtAction(nameof(GetSessionById), new { id = session.Id }, session);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetSessionById(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        var session = await _testService.GetSessionByIdAsync(userId, id, cancellationToken);
        if (session == null)
        {
            return NotFound();
        }
        return Ok(session);
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> SubmitSession(
        Guid id,
        [FromBody] SubmitTestRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        var result = await _testService.SubmitSessionAsync(userId, id, request, cancellationToken);
        if (result == null)
        {
            return NotFound();
        }
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetSessions(
        [FromQuery] int skip = 0,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetRequiredUserId();
        var sessions = await _testService.GetUserSessionsAsync(userId, skip, take, cancellationToken);
        return Ok(sessions);
    }
}
