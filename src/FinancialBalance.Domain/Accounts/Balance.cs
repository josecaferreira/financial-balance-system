namespace FinancialBalance.Domain.Accounts;

public record Balance(
    decimal TotalIncoming,
    decimal TotalOutgoing,
    decimal Net,
    DateOnly Date,
    Guid AccountId)
{
    public static Balance Calculate(IEnumerable<Transaction> transactions, DateOnly date, Guid accountId)
    {
        var confirmed = transactions.Where(t => t.Status == TransactionStatus.Confirmed);
        var incoming = confirmed.Where(t => t.Type == TransactionType.Incoming).Sum(t => t.Amount);
        var outgoing = confirmed.Where(t => t.Type == TransactionType.Outgoing).Sum(t => t.Amount);
        return new Balance(incoming, outgoing, incoming - outgoing, date, accountId);
    }
}
