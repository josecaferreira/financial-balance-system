namespace FinancialBalance.Application.Common;

public interface ICurrentUser
{
    Guid Id { get; }
    string Email { get; }
    IReadOnlyList<string> Roles { get; }
}
