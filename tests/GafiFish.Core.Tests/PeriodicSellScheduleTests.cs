using GafiFish.Core.Scheduling;
using Xunit;

namespace GafiFish.Core.Tests;

public sealed class PeriodicSellScheduleTests
{
    [Fact]
    public void Poll_MarksSalePendingAfterTenMinutes()
    {
        var start = DateTimeOffset.UtcNow;
        var schedule = new PeriodicSellSchedule();

        schedule.Start(start);

        Assert.False(schedule.Poll(start.AddMinutes(9)));
        Assert.True(schedule.Poll(start.AddMinutes(10)));
        Assert.True(schedule.SalePending);
    }

    [Fact]
    public void SaleWaitsUntilFishingCycleIsSafe()
    {
        var start = DateTimeOffset.UtcNow;
        var schedule = new PeriodicSellSchedule();
        schedule.Start(start);
        schedule.Poll(start.AddMinutes(10));

        Assert.False(schedule.TryBeginSale(isSafeToSell: false));
        Assert.True(schedule.SalePending);
        Assert.True(schedule.TryBeginSale(isSafeToSell: true));
        Assert.True(schedule.SaleInProgress);
    }

    [Fact]
    public void CompleteSaleStartsNextTenMinuteInterval()
    {
        var start = DateTimeOffset.UtcNow;
        var schedule = new PeriodicSellSchedule();
        schedule.Start(start);
        schedule.Poll(start.AddMinutes(10));
        Assert.True(schedule.TryBeginSale(isSafeToSell: true));

        var completedAt = start.AddMinutes(10).AddSeconds(8);
        schedule.CompleteSale(completedAt);

        Assert.False(schedule.SalePending);
        Assert.Equal(completedAt.AddMinutes(10), schedule.NextDueAt);
    }

    [Fact]
    public void FailedSaleStopsSchedulerAndKeepsPendingRequestVisible()
    {
        var start = DateTimeOffset.UtcNow;
        var schedule = new PeriodicSellSchedule();
        schedule.Start(start);
        schedule.Poll(start.AddMinutes(10));
        Assert.True(schedule.TryBeginSale(isSafeToSell: true));

        schedule.FailSale();

        Assert.False(schedule.IsRunning);
        Assert.False(schedule.SaleInProgress);
        Assert.True(schedule.SalePending);
    }
}
