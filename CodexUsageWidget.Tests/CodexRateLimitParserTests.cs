using System.Globalization;
using System.Text.Json;
using CodexUsageWidget.Core;

namespace CodexUsageWidget.Tests;

[TestClass]
public sealed class CodexRateLimitParserTests
{
    [TestMethod]
    public void Parse_ConvertsUsedPercentToRemainingPercent()
    {
        using JsonDocument document = JsonDocument.Parse("""
            {
              "rateLimits": {
                "primary": {
                  "usedPercent": 41,
                  "windowDurationMins": 300,
                  "resetsAt": 1781265283
                },
                "secondary": {
                  "usedPercent": 38,
                  "windowDurationMins": 10080,
                  "resetsAt": 1781767568
                },
                "credits": {
                  "hasCredits": true,
                  "unlimited": false,
                  "balance": "1183.9833500000"
                },
                "planType": "plus"
              }
            }
            """);

        UsageSnapshot snapshot = CodexRateLimitParser.Parse(
            document.RootElement,
            DateTimeOffset.UnixEpoch);

        Assert.AreEqual(59, snapshot.Primary?.RemainingPercent);
        Assert.AreEqual(62, snapshot.Secondary?.RemainingPercent);
        Assert.AreEqual(1183.9833500000m, snapshot.Credits);
        Assert.AreEqual("plus", snapshot.PlanType);
    }

    [TestMethod]
    public void Parse_AllowsMissingWindowsAndCredits()
    {
        using JsonDocument document = JsonDocument.Parse("""
            {
              "rateLimits": {
                "primary": null,
                "secondary": null,
                "credits": null,
                "planType": null
              }
            }
            """);

        UsageSnapshot snapshot = CodexRateLimitParser.Parse(
            document.RootElement,
            DateTimeOffset.UnixEpoch);

        Assert.IsNull(snapshot.Primary);
        Assert.IsNull(snapshot.Secondary);
        Assert.IsNull(snapshot.Credits);
        Assert.IsNull(snapshot.PlanType);
    }

    [TestMethod]
    public void WeeklyLimit_UsesPrimaryWithTheCurrentWeeklyOnlySchema()
    {
        var weekly = new RateLimitWindow(36, 10080, 1784966671);
        var snapshot = new UsageSnapshot(
            weekly,
            null,
            null,
            "plus",
            DateTimeOffset.UnixEpoch);

        Assert.AreEqual(weekly, snapshot.WeeklyLimit);
    }

    [TestMethod]
    public void WeeklyLimit_UsesLongestWindowFromLegacyCachedData()
    {
        var fiveHours = new RateLimitWindow(41, 300, 1781265283);
        var weekly = new RateLimitWindow(38, 10080, 1781767568);
        var snapshot = new UsageSnapshot(
            fiveHours,
            weekly,
            null,
            "plus",
            DateTimeOffset.UnixEpoch);

        Assert.AreEqual(weekly, snapshot.WeeklyLimit);
    }

    [TestMethod]
    public void RemainingPercent_IsClamped()
    {
        Assert.AreEqual(0, new RateLimitWindow(150, null, null).RemainingPercent);
        Assert.AreEqual(100, new RateLimitWindow(-20, null, null).RemainingPercent);
    }

    [TestMethod]
    public void LowRemainingThreshold_IsStrictlyBelowTwentyPercent()
    {
        Assert.IsFalse(new RateLimitWindow(80, null, null).IsLowRemaining);
        Assert.IsTrue(new RateLimitWindow(81, null, null).IsLowRemaining);
        Assert.IsTrue(new RateLimitWindow(100, null, null).IsLowRemaining);
    }

    [TestMethod]
    public void FormatReset_ConvertsUtcToRequestedTimeZone()
    {
        var parisSummer = TimeZoneInfo.CreateCustomTimeZone(
            "Test Paris",
            TimeSpan.FromHours(2),
            "Test Paris",
            "Test Paris");
        var reset = new DateTimeOffset(2026, 6, 12, 11, 54, 0, TimeSpan.Zero);

        string text = UsageFormatting.FormatReset(
            reset,
            isWeekly: false,
            CultureInfo.InvariantCulture,
            parisSummer);

        Assert.AreEqual("Reinitialisation : 13:54", text);
    }
}
