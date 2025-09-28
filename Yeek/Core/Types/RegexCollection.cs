using System.Text.RegularExpressions;
using Yeek.Security.Model;

namespace Yeek.Core.Types;

public static partial class RegexCollection
{
    [GeneratedRegex("^midi-([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})-rev-(\\d+)")]
    public static partial Regex QuickLinkRevRegex();

    public static List<(string href, string name)> GetQuickLinksForItem(Ticket ticket)
    {
        var list = new List<(string href, string name)>();

        {
            var quickLinkRevRegex = QuickLinkRevRegex();
            var matches = quickLinkRevRegex.Matches(ticket.Header);

            if (matches.Count > 0)
            {
                list.Add(($"/{matches.First().Groups[1]}/history?rev={matches.First().Groups[2]}", "Reported MIDI"));
            }
        }

        list.Add(($"/moderation/users/{ticket.ReporteeId}", $"User: {ticket.Reportee?.DisplayName}"));

        return list;
    }

    public static List<(string href, string name)> GetQuickLinksForItem(User user)
    {
        var list = new List<(string href, string name)>();

        list.Add(($"/?search=uploadedby%3A{user.Id}&sortby=recent", $"MIDIs uploaded by {user.DisplayName}"));

        return list;
    }
}