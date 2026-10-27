using System;
using System.Collections.Generic;
using BunnyOS.Domain.Financial;
using Xunit;

namespace BunnyOS.Tests;

public class FinancialCalculationsTests
{
    [Fact]
    public void CalculateLineTotal_CorrectlyMultiplies_WithDecimalPrecision()
    {
        // 3 items @ 125.50
        var total = FinancialCalculations.CalculateLineTotal(125.50m, 3m);
        Assert.Equal(376.50m, total);
    }

    [Fact]
    public void CalculateSaleTotals_ComputesGrandTotal_AndDuesAccurately()
    {
        // Subtotal: 1000, Discount: 100, Tax: 50 => GrandTotal = 950
        // Paid: 500 => Due = 450
        var (grandTotal, due) = FinancialCalculations.CalculateSaleTotals(1000m, 100m, 50m, 500m);

        Assert.Equal(950.00m, grandTotal);
        Assert.Equal(450.00m, due);
    }

    [Fact]
    public void CalculateSaleTotals_WhenOverpaid_DueIsZero()
    {
        var (grandTotal, due) = FinancialCalculations.CalculateSaleTotals(500m, 50m, 0m, 600m);
        Assert.Equal(450.00m, grandTotal);
        Assert.Equal(0.00m, due);
    }

    [Fact]
    public void CalculateGrossProfit_AccuratelyEvaluatesCOGS()
    {
        // Item 1: Cost 80, Selling 120, Qty 10 => Profit = (120 - 80) * 10 = 400
        // Item 2: Cost 200, Selling 350, Qty 2 => Profit = (350 - 200) * 2 = 300
        var items = new List<(decimal UnitCost, decimal UnitSelling, decimal Quantity)>
        {
            (80m, 120m, 10m),
            (200m, 350m, 2m)
        };

        var gross = FinancialCalculations.CalculateGrossProfit(items);
        Assert.Equal(700.00m, gross);
    }

    [Fact]
    public void CalculateNetProfit_SubtractsOperatingExpenses()
    {
        // Gross profit: 700, Operating expenses: 250 => Net profit = 450
        var net = FinancialCalculations.CalculateNetProfit(700m, 250m);
        Assert.Equal(450.00m, net);
    }

    [Fact]
    public void CalculateInventoryValuation_SumsCostTimesQuantity()
    {
        var stock = new List<(decimal UnitCost, decimal QuantityOnHand)>
        {
            (50m, 20m), // 1000
            (120m, 15m) // 1800
        };

        var val = FinancialCalculations.CalculateInventoryValuation(stock);
        Assert.Equal(2800.00m, val);
    }
}
