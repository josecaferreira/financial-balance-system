using FinancialBalance.Application.Reports.Queries.GetDailyReport;
using FinancialBalance.Application.Reports.Queries.GetDailyReportRange;
using FinancialBalance.Application.Reports.Queries.GetMonthlyReport;
using FinancialBalance.Application.Reports.Queries.GetMonthlyReportRange;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FinancialBalance.ReportingApi.Controllers;

[ApiController]
[Authorize(Policy = "CanViewReports")]
[Route("api/v1/reports")]
[Produces("application/json")]
[EnableRateLimiting("reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ReportsController(IMediator mediator)
        => _mediator = mediator;

    /// <summary>Get the daily balance summary for an account on a specific date.</summary>
    [HttpGet("daily")]
    [ProducesResponseType(typeof(DailyReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDaily(
        [FromQuery] Guid accountId,
        [FromQuery] DateOnly date,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetDailyReportQuery(accountId, date), ct);
        return Ok(result);
    }

    /// <summary>Get daily balance summaries for a date range (max 92 days).</summary>
    [HttpGet("daily/range")]
    [ProducesResponseType(typeof(IReadOnlyList<DailyReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDailyRange(
        [FromQuery] Guid accountId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetDailyReportRangeQuery(accountId, from, to), ct);
        return Ok(result);
    }

    /// <summary>Get the full monthly balance summary including daily breakdown.</summary>
    [HttpGet("monthly")]
    [ProducesResponseType(typeof(MonthlyReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMonthly(
        [FromQuery] Guid accountId,
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetMonthlyReportQuery(accountId, year, month), ct);
        return Ok(result);
    }

    /// <summary>Get monthly summaries for a range of months (max 12 months).</summary>
    [HttpGet("monthly/range")]
    [ProducesResponseType(typeof(IReadOnlyList<MonthlyReportSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetMonthlyRange(
        [FromQuery] Guid accountId,
        [FromQuery] int fromYear,
        [FromQuery] int fromMonth,
        [FromQuery] int toYear,
        [FromQuery] int toMonth,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(
            new GetMonthlyReportRangeQuery(accountId, fromYear, fromMonth, toYear, toMonth), ct);
        return Ok(result);
    }
}
