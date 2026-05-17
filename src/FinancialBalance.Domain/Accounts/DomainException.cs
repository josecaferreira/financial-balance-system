namespace FinancialBalance.Domain.Accounts;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
