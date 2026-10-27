using System;
using System.Linq;
using System.Threading.Tasks;
using BunnyOS.Ai;
using BunnyOS.Domain.Entities;
using BunnyOS.Infrastructure.Persistence;
using BunnyOS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BunnyOS.Tests;

public class AiToolSecurityTests
{
    [Theory]
    [InlineData("আজকের বিক্রি কত?", "get_dashboard_summary")]
    [InlineData("আজকের মোট লাভ কত?", "get_dashboard_summary")]
    [InlineData("স্টক কমে গেছে এমন পণ্যগুলো দেখাও", "get_low_stock_products")]
    [InlineData("কম স্টক পণ্য তালিকা", "get_low_stock_products")]
    [InlineData("আজকের খরচ ৫০০ টাকা হিসেবে যোগ করো", "create_expense")]
    [InlineData("ইনভেন্টরি পেজ খুলে দাও", "navigate_to_screen")]
    [InlineData("পণ্য সার্চ করো চাল", "search_products")]
    public void IntentClassifier_CorrectlyIdentifies_BanglaCommands(string command, string expectedTool)
    {
        var classified = BanglaIntentClassifier.ClassifyIntent(command);
        Assert.Equal(expectedTool, classified);
    }

    [Fact]
    public async Task SensitiveTool_RequiresConfirmation_BeforeRecordingTransaction()
    {
        var tenantContext = new TenantContext();
        var options = new DbContextOptionsBuilder<BunnyDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var tenantId = Guid.NewGuid();
        tenantContext.SetTenant(tenantId);

        using var db = new BunnyDbContext(options, tenantContext);
        var registry = new AiToolRegistry(db);
        var engine = new BunnyAiEngine(registry);

        // Step 1: Send unconfirmed command
        var unconfirmedResult = await engine.ProcessCommandAsync("অফিসের জন্য ৫০০ টাকা খরচ যোগ করো", isConfirmed: false);

        Assert.False(unconfirmedResult.Success);
        Assert.True(unconfirmedResult.RequiresConfirmation);
        Assert.NotNull(unconfirmedResult.ConfirmationPrompt);

        // Verify database was NOT modified
        var countBefore = await db.Expenses.CountAsync();
        Assert.Equal(0, countBefore);

        // Step 2: Confirm action
        var confirmedResult = await engine.ProcessCommandAsync("অফিসের জন্য ৫০০ টাকা খরচ যোগ করো", isConfirmed: true);

        Assert.True(confirmedResult.Success);
        Assert.False(confirmedResult.RequiresConfirmation);

        // Verify record was persisted
        var countAfter = await db.Expenses.CountAsync();
        Assert.Equal(1, countAfter);
        var expense = await db.Expenses.FirstAsync();
        Assert.Equal(500m, expense.Amount);
    }
}
