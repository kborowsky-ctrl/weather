using System.Text;
using WeatherWizard.Models;

namespace WeatherWizard.Services;

public static class NwsAlertDetailFormatter
{
    public static string FormatAll(IReadOnlyList<WeatherAlertItem> alerts)
    {
        if (alerts.Count == 0)
            return "No alert details available.";

        var parts = new List<string>();
        foreach (var alert in alerts)
        {
            var body = FormatOne(alert);
            if (body.Length > 0)
                parts.Add(body);
        }

        return parts.Count == 0
            ? "No alert text was provided for the active alert(s)."
            : string.Join("\n\n" + new string('─', 40) + "\n\n", parts);
    }

    public static string FormatOne(WeatherAlertItem alert)
    {
        var title = !string.IsNullOrWhiteSpace(alert.Event)
            ? alert.Event.Trim()
            : !string.IsNullOrWhiteSpace(alert.Summary)
                ? alert.Summary.Trim()
                : "Weather Alert";

        var sb = new StringBuilder();
        sb.AppendLine(title);

        if (!string.IsNullOrWhiteSpace(alert.AreaDesc))
            sb.AppendLine($"Area: {alert.AreaDesc.Trim()}");

        if (!string.IsNullOrWhiteSpace(alert.Headline))
            sb.AppendLine(alert.Headline.Trim());

        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(alert.Description))
            sb.AppendLine(alert.Description.Trim());

        if (!string.IsNullOrWhiteSpace(alert.Instruction))
        {
            sb.AppendLine();
            sb.AppendLine("PRECAUTIONARY/PREPAREDNESS ACTIONS...");
            sb.AppendLine();
            sb.AppendLine(alert.Instruction.Trim());
        }

        return sb.ToString().Trim();
    }
}
