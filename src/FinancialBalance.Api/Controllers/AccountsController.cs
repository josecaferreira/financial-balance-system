using FinancialBalance.Application.Accounts.Commands.CreateAccount;
using FinancialBalance.Application.Accounts.Queries.GetAccount;
using FinancialBalance.Application.Accounts.Queries.GetAccountBalance;
using FinancialBalance.Application.Accounts.Queries.ListAccounts;
using FinancialBalance.Application.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FinancialBalance.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/accounts")]
[Produces("application/json")]
[EnableRateLimiting("fixed")]
public sealed class AccountsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AccountsController(IMediator mediator)
        => _mediator = mediator;

    /// <summary>List all accounts.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AccountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] bool? isActive = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var result = await _mediator.Send(new ListAccountsQuery(isActive, page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>Get account by ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAccountQuery(id), ct);
        return Ok(result);
    }

    /// <summary>Get current balance for an account.</summary>
    [HttpGet("{id:guid}/balance")]
    [ProducesResponseType(typeof(AccountBalanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBalance(Guid id, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetAccountBalanceQuery(id), ct);
        return Ok(result);
    }

    /// <summary>Create a new account.</summary>
    [HttpPost]
    [Authorize(Policy = "CanManageAccounts")]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create([FromBody] CreateAccountCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }
}
