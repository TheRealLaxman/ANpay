namespace ANpay.Api.DTOs;

public class TransactionDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    public decimal Fee { get; set; }
    public decimal? ExchangeRate { get; set; }
    public string Channel { get; set; } = "App";
    public Guid? BranchId { get; set; }
    public string? EmployeeId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? DestinationWalletName { get; set; }
}

public class TransactionHistoryDto
{
    public List<TransactionDto> Transactions { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
