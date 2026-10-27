using System;
using System.Collections.Generic;
using System.Linq;

namespace BunnyOS.Domain.Financial;

public static class FinancialCalculations
{
    public static decimal CalculateLineTotal(decimal unitPrice, decimal quantity)
    {
        if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity cannot be negative.");
        if (unitPrice < 0) throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price cannot be negative.");
        return Math.Round(unitPrice * quantity, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal CalculateSubtotal(IEnumerable<(decimal UnitPrice, decimal Quantity)> items)
    {
        return items.Sum(item => CalculateLineTotal(item.UnitPrice, item.Quantity));
    }

    public static (decimal GrandTotal, decimal DueAmount) CalculateSaleTotals(
        decimal subtotal,
        decimal discountAmount,
        decimal taxAmount,
        decimal paidAmount)
    {
        if (subtotal < 0) throw new ArgumentOutOfRangeException(nameof(subtotal), "Subtotal cannot be negative.");
        if (discountAmount < 0) throw new ArgumentOutOfRangeException(nameof(discountAmount), "Discount cannot be negative.");
        if (taxAmount < 0) throw new ArgumentOutOfRangeException(nameof(taxAmount), "Tax cannot be negative.");
        if (paidAmount < 0) throw new ArgumentOutOfRangeException(nameof(paidAmount), "Paid amount cannot be negative.");

        var grandTotal = Math.Max(0m, subtotal - discountAmount + taxAmount);
        grandTotal = Math.Round(grandTotal, 2, MidpointRounding.AwayFromZero);

        var due = Math.Max(0m, grandTotal - paidAmount);
        due = Math.Round(due, 2, MidpointRounding.AwayFromZero);

        return (grandTotal, due);
    }

    public static decimal CalculateGrossProfit(IEnumerable<(decimal UnitCost, decimal UnitSelling, decimal Quantity)> soldItems)
    {
        decimal gross = 0m;
        foreach (var (cost, selling, qty) in soldItems)
        {
            var lineProfit = (selling - cost) * qty;
            gross += lineProfit;
        }
        return Math.Round(gross, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal CalculateNetProfit(decimal grossProfit, decimal totalOperatingExpenses)
    {
        return Math.Round(grossProfit - totalOperatingExpenses, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal CalculateInventoryValuation(IEnumerable<(decimal UnitCost, decimal QuantityOnHand)> stockItems)
    {
        decimal valuation = 0m;
        foreach (var (cost, qty) in stockItems)
        {
            if (qty > 0)
            {
                valuation += cost * qty;
            }
        }
        return Math.Round(valuation, 2, MidpointRounding.AwayFromZero);
    }
}
