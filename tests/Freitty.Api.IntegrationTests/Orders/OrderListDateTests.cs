using Freitty.Domain.Orders;

namespace Freitty.Api.IntegrationTests.Orders;

// The API's clock stands at 12:00 on Wednesday 15 April 2026 in Toronto, the reporting zone; daylight saving time
// (UTC−4) is in effect from 8 March, so every instant below is local summer time.
public sealed class OrderListDateTests(PostgresFixture postgres) : IAsyncDisposable
{
    private static readonly TimeSpan TorontoSummerOffset = TimeSpan.FromHours(-4);

    private readonly CabinetApi api = new(postgres.ConnectionString);

    [Fact]
    public async Task TodayRunsFromLocalMidnightToTheNextOne()
    {
        string[] inside = await ScheduleAsync(Local(4, 15, 0, 0), Local(4, 15, 23, 30));
        await ScheduleAsync(Local(4, 14, 23, 59), Local(4, 16, 0, 0), Local(4, 16, 0, 30));

        string[] numbers = await api.ListNumbersAsync("?date=Today");

        Assert.Equivalent(inside, numbers, strict: true);
    }

    [Fact]
    public async Task ThisWeekRunsFromMondayToTheNextMonday()
    {
        string[] inside = await ScheduleAsync(Local(4, 13, 0, 0), Local(4, 13, 0, 30), Local(4, 19, 23, 30));
        await ScheduleAsync(Local(4, 12, 23, 30), Local(4, 20, 0, 0), Local(4, 20, 0, 30));

        string[] numbers = await api.ListNumbersAsync("?date=ThisWeek");

        Assert.Equivalent(inside, numbers, strict: true);
    }

    [Fact]
    public async Task Last30DaysEndWithTodayAndStartTwentyNineDaysEarlier()
    {
        string[] inside = await ScheduleAsync(Local(3, 17, 0, 0), Local(3, 17, 0, 30), Local(4, 15, 23, 30));
        await ScheduleAsync(Local(3, 16, 23, 30), Local(4, 16, 0, 0), Local(4, 16, 0, 30));

        string[] numbers = await api.ListNumbersAsync("?date=Last30Days");

        Assert.Equivalent(inside, numbers, strict: true);
    }

    [Fact]
    public async Task TodayIsTheDateInTorontoNotInUtc()
    {
        DateTimeOffset eveningInToronto = Local(4, 15, 21, 0);
        await using var lateApi = new CabinetApi(postgres.ConnectionString, eveningInToronto);
        Order today = lateApi.Orders.Ready(scheduledAt: Local(4, 15, 9, 0));
        Order tomorrow = lateApi.Orders.Ready(scheduledAt: Local(4, 16, 9, 0));
        await lateApi.SaveAsync(today, tomorrow);

        string[] numbers = await lateApi.ListNumbersAsync("?date=Today");

        Assert.Equal(today.Number, Assert.Single(numbers));
    }

    // Toronto moves its clocks forward at 02:00 on 8 March 2026: that day starts at UTC−5 and ends at UTC−4.
    [Fact]
    public async Task TodayFollowsTheClockChange()
    {
        DateTimeOffset startOfDay = new(2026, 3, 8, 0, 0, 0, TimeSpan.FromHours(-5));
        DateTimeOffset endOfDay = new(2026, 3, 9, 0, 0, 0, TorontoSummerOffset);
        await using var dayOfChange = new CabinetApi(postgres.ConnectionString, startOfDay.AddHours(12));
        string[] inside = await ScheduleAsync(dayOfChange, startOfDay, endOfDay.AddMinutes(-30));
        await ScheduleAsync(dayOfChange, startOfDay.AddMinutes(-1), endOfDay, endOfDay.AddMinutes(30));

        string[] numbers = await dayOfChange.ListNumbersAsync("?date=Today");

        Assert.Equivalent(inside, numbers, strict: true);
    }

    // Santiago moves its clocks from 00:00 to 01:00 on 6 September 2026, so that day begins at 01:00, UTC−3.
    [Fact]
    public async Task DayWhoseMidnightIsSkippedStartsAtTheFirstInstantOfTheDay()
    {
        DateTimeOffset startOfDay = new(2026, 9, 6, 1, 0, 0, TimeSpan.FromHours(-3));
        await using var santiago = new CabinetApi(
            postgres.ConnectionString,
            startOfDay.AddHours(11),
            "America/Santiago");
        string[] inside = await ScheduleAsync(santiago, startOfDay);
        await ScheduleAsync(santiago, startOfDay.AddMinutes(-1));

        string[] numbers = await santiago.ListNumbersAsync("?date=Today");

        Assert.Equivalent(inside, numbers, strict: true);
    }

    // The Azores move their clocks from 01:00 back to 00:00 on 25 October 2026, so midnight comes twice; the day
    // begins at the first, UTC+0.
    [Fact]
    public async Task DayWhoseMidnightIsRepeatedStartsAtTheFirstMidnight()
    {
        DateTimeOffset firstMidnight = new(2026, 10, 25, 0, 0, 0, TimeSpan.Zero);
        await using var azores = new CabinetApi(
            postgres.ConnectionString,
            firstMidnight.AddHours(12),
            "Atlantic/Azores");
        string[] inside = await ScheduleAsync(azores, firstMidnight, firstMidnight.AddMinutes(30));
        await ScheduleAsync(azores, firstMidnight.AddMinutes(-1));

        string[] numbers = await azores.ListNumbersAsync("?date=Today");

        Assert.Equivalent(inside, numbers, strict: true);
    }

    [Theory]
    [InlineData("Today")]
    [InlineData("ThisWeek")]
    [InlineData("Last30Days")]
    public async Task OrderWithoutScheduledDateMatchesNoPreset(string preset)
    {
        await api.SaveAsync(api.Orders.Draft());

        string[] numbers = await api.ListNumbersAsync($"?date={preset}");

        Assert.Empty(numbers);
    }

    public async ValueTask DisposeAsync()
    {
        await api.DisposeAsync();
    }

    private static DateTimeOffset Local(int month, int day, int hour, int minute)
    {
        return new DateTimeOffset(2026, month, day, hour, minute, 0, TorontoSummerOffset);
    }

    private Task<string[]> ScheduleAsync(params DateTimeOffset[] scheduledDates)
    {
        return ScheduleAsync(api, scheduledDates);
    }

    private static async Task<string[]> ScheduleAsync(CabinetApi target, params DateTimeOffset[] scheduledDates)
    {
        Order[] orders = scheduledDates
            .Select(scheduledAt => target.Orders.Ready(scheduledAt: scheduledAt))
            .ToArray();
        await target.SaveAsync(orders);
        return orders.Select(order => order.Number).ToArray();
    }
}
