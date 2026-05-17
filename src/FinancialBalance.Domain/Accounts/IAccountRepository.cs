using FinancialBalance.Domain.Shared;

namespace FinancialBalance.Domain.Accounts;

public interface IAccountRepository : IRepository<Account>
{
    Task<Account?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<bool> ExistsByCodeAsync(string code, CancellationToken ct = default);
    Task<(IReadOnlyList<Account> Items, int TotalCount)> ListAsync(
        bool? isActive, int page, int pageSize, CancellationToken ct = default);
}
