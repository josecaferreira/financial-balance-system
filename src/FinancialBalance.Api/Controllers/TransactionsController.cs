using FinancialBalance.Application.Common;
using FinancialBalance.Application.Transactions.Commands.CancelTransaction;
using FinancialBalance.Application.Transactions.Commands.CreateTransaction;
using FinancialBalance.Application.Transactions.Queries.GetTransaction;
using FinancialBalance.Application.Transactions.Queries.ListTransactions;
using FinancialBalance.Domain.Accounts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FinancialBalance.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/transactions")]
[Produces("application/json")]
[EnableRateLimiting("fixed")]
public sealed class TransactionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TransactionsController(IMediator mediator)
        => _mediator = mediator;

    /// <summary>List transactions with optional filters.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TransactionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] Guid accountId,
        [FromQuery] TransactionType? type = null,
        [FromQuery] TransactionCategory? category = null,
        [FromQuery] TransactionStatus? status = null,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var result = await _mediator.Send(
            new ListTransactionsQuery(accountId, type, category, status, from, to, page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>Get a single transaction.</summary>
    [HttpGet("{transactionId:guid}")]
    [ProducesResponseType(typeof(TransactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        [FromQuery] Guid accountId,
        Guid transactionId,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetTransactionQuery(accountId, transactionId), ct);
        return Ok(result);
    }

    /// <summary>Register a new transaction.</summary>
    [HttpPost]
    [Authorize(Policy = "CanWriteTransactions")]
    [ProducesResponseType(typeof(TransactionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create([FromBody] CreateTransactionCommand command, CancellationToken ct = default)
    {
        var result = await _mediator.Send(command, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Cancel a transaction.</summary>
    [HttpPatch("{transactionId:guid}/cancel")]
    [Authorize(Policy = "CanWriteTransactions")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Cancel(
        [FromQuery] Guid accountId,
        Guid transactionId,
        CancellationToken ct = default)
    {
        await _mediator.Send(new CancelTransactionCommand(accountId, transactionId), ct);
        return NoContent();
    }
}
