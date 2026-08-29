namespace WeatherWizard.Services;

/// <summary>
/// Meteorological seasons (DJF/MAM/JJA/SON) and when the seasonal-outlook indicator is shown:
/// from 4 weeks before the next season starts through 2 weeks after that season's CPC issuance.
/// </summary>
public static class SeasonalOutlookWindow
{
    public sealed record SeasonTarget(
        string Code,
        string DisplayName,
        DateOnly SeasonStart,
        DateOnly WindowStart,
        DateOnly WindowEnd,
        DateOnly ExpectedIssuance);

    public static SeasonTarget? TryGetActiveTarget(DateOnly today)
    {
        foreach (var candidate in NextCandidates(today))
        {
            if (today >= candidate.WindowStart && today <= candidate.WindowEnd)
                return candidate;
        }

        return null;
    }

    public static IEnumerable<SeasonTarget> NextCandidates(DateOnly today)
    {
        // Check this year and next for each meteorological season start.
        for (var year = today.Year; year <= today.Year + 1; year++)
        {
            yield return Build("MAM", "Spring", new DateOnly(year, 3, 1));
            yield return Build("JJA", "Summer", new DateOnly(year, 6, 1));
            yield return Build("SON", "Fall", new DateOnly(year, 9, 1));
            yield return Build("DJF", "Winter", new DateOnly(year, 12, 1));
        }
    }

    private static SeasonTarget Build(string code, string displayName, DateOnly seasonStart)
    {
        // Lead-1 for this season is issued mid-month of the prior calendar month.
        var issueMonth = seasonStart.AddMonths(-1);
        var issuance = ThirdThursday(issueMonth.Year, issueMonth.Month);
        var windowStart = seasonStart.AddDays(-28);
        var windowEnd = issuance.AddDays(14);
        return new SeasonTarget(code, displayName, seasonStart, windowStart, windowEnd, issuance);
    }

    public static DateOnly ThirdThursday(int year, int month)
    {
        var d = new DateOnly(year, month, 1);
        var thursdays = 0;
        while (true)
        {
            if (d.DayOfWeek == DayOfWeek.Thursday)
            {
                thursdays++;
                if (thursdays == 3)
                    return d;
            }

            d = d.AddDays(1);
        }
    }

    /// <summary>CPC <c>valid_seas</c> like "SON 2026" or "DJF 2026/27".</summary>
    public static bool MatchesValidSeas(string? validSeas, SeasonTarget target)
    {
        if (string.IsNullOrWhiteSpace(validSeas))
            return false;

        var s = validSeas.Trim();
        if (!s.StartsWith(target.Code, StringComparison.OrdinalIgnoreCase))
            return false;

        var y = target.SeasonStart.Year;
        if (target.Code == "DJF")
        {
            // Winter labeled with Dec year or "YYYY/YY".
            return s.Contains(y.ToString(), StringComparison.Ordinal)
                   || s.Contains($"{y}/", StringComparison.Ordinal)
                   || s.Contains((y + 1).ToString(), StringComparison.Ordinal);
        }

        return s.Contains(y.ToString(), StringComparison.Ordinal);
    }
}
