using System;
using System.Collections.Generic;

namespace BunnyOS.Application.DTOs;

#region Auth DTOs
public record RegisterRequest(string FullName, string Email, string? Phone = null, string Password = "", string BusinessName = "");
public record LoginRequest(string Email, string Password);
public record AuthResponse(string Token, Guid TenantId, Guid UserId, string FullName, string Role, string BusinessName, string CurrencySymbol);
public record BusinessSetupRequest(string BusinessName, string Tagline, string Phone, string Address, string Currency);
#endregion

#region Product DTOs
public record ProductDto(
    Guid Id,
    string Name,
    string Sku,
    Guid? CategoryId,
    string? CategoryName,
    decimal CostPrice,
    decimal SellingPrice,
    decimal StockQuantity,
    decimal LowStockThreshold,
    string Unit,
    bool IsLowStock);

public record CreateProductRequest(
    string Name,
    string Sku,
    Guid? CategoryId,
    decimal CostPrice,
    decimal SellingPrice,
    decimal StockQuantity,
    decimal LowStockThreshold,
    string Unit);

public record UpdateProductRequest(
    string Name,
    string Sku,
    Guid? CategoryId,
    decimal CostPrice,
    decimal SellingPrice,
    decimal LowStockThreshold,
    string Unit);

public record StockAdjustmentRequest(
    Guid ProductId,
    decimal QuantityChange,
    string Reason);
#endregion

#region Sales DTOs
public record SaleItemRequest(Guid ProductId, decimal Quantity, decimal? UnitPrice);
public record CreateSaleRequest(
    Guid? CustomerId,
    string? CustomerName,
    List<SaleItemRequest> Items,
    decimal DiscountAmount,
    decimal PaidAmount,
    string PaymentMethod,
    string? IdempotencyKey = null);

public record SaleDto(
    Guid Id,
    string InvoiceNumber,
    Guid? CustomerId,
    string? CustomerName,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal DueAmount,
    string PaymentMethod,
    string Status,
    DateTime CreatedAtUtc,
    List<SaleItemDto> Items);

public record SaleItemDto(
    Guid ProductId,
    string ProductName,
    decimal UnitCostPrice,
    decimal UnitSellingPrice,
    decimal Quantity,
    decimal TotalPrice);
#endregion

#region Customer & Supplier DTOs
public record CustomerDto(Guid Id, string Name, string Phone, string? Email, string? Address, decimal TotalPurchases, decimal TotalPaid, decimal CurrentDue);
public record CreateCustomerRequest(string Name, string Phone, string? Email, string? Address);
public record SupplierDto(Guid Id, string Name, string Phone, string? Company, string? Address, decimal TotalBilled, decimal TotalPaid, decimal CurrentDue);
public record CreateSupplierRequest(string Name, string Phone, string? Company, string? Address);
#endregion

#region Expense DTOs
public record ExpenseDto(Guid Id, string Category, decimal Amount, DateTime ExpenseDateUtc, string? Note, string PaymentMethod);
public record CreateExpenseRequest(string Category, decimal Amount, string? Note, string PaymentMethod, DateTime? ExpenseDateUtc);
#endregion

#region Report DTOs
public record DashboardSummaryDto(
    decimal TodaySales,
    decimal TodayExpenses,
    decimal GrossProfit,
    decimal NetProfit,
    decimal TotalCustomerDues,
    decimal TotalSupplierDues,
    int LowStockCount,
    List<SaleDto> RecentSales,
    List<ProductDto> LowStockProducts,
    string AiInsightText);

public record ProfitLossReportDto(
    DateTime StartDateUtc,
    DateTime EndDateUtc,
    decimal TotalRevenue,
    decimal CostOfGoodsSold,
    decimal GrossProfit,
    decimal TotalOperatingExpenses,
    decimal NetProfit,
    decimal NetProfitMarginPercent);

public record SalesTrendDto(string Period, decimal TotalSales, int TransactionCount);
#endregion

#region AI DTOs
public record AiExecutionRequest(string Command, bool? IsConfirmed = false);
public record AiExecutionResult(
    bool Success,
    string ToolName,
    string Message,
    object? Data,
    bool RequiresConfirmation,
    string? ConfirmationPrompt,
    string? TargetScreen);
#endregion
