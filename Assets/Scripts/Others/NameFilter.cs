using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

// Keeps offensive names off the public leaderboard. Checked when a name is typed, and again when
// names are shown (in case one was posted by a modified client).
// ponytail: small hand-made word list; swap in a proper list or service if the game gets big.
public static class NameFilter
{
    // Blocked anywhere in the name (after leetspeak is undone).
    private static readonly string[] Roots =
    {
        "fuck", "fuk", "shit", "cunt", "nigg", "nigga", "fagg", "whore", "slut", "bitch", "bastard", "asshole",
        "rapist", "porn", "pussy", "penis", "vagina", "retard", "kike", "tranny", "nazi", "hitler", "molest",
        "twat", "wank", "jizz", "dildo", "boob", "kkk", "heil",
    };

    // Blocked only as a whole word, because they hide inside normal words ("class", "peacock", "torpedo").
    private static readonly string[] Words =
    {
        "ass", "dick", "cock", "tit", "tits", "cum", "sex", "anal", "spic", "chink", "pedo", "fag", "kys", "rape",
    };

    public static bool IsBlocked(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        string letters = Normalize(name);
        string collapsed = Regex.Replace(letters, @"(.)\1+", "$1"); // "fuuuck" -> "fuck"
        if (Roots.Any(r => letters.Contains(r) || collapsed.Contains(r))) return true;

        // Words: split on spaces/symbols and on camelCase ("BigAss" -> big, ass).
        string spaced = Regex.Replace(name, "([a-z])([A-Z])", "$1 $2");
        return Regex.Split(spaced, "[^A-Za-z0-9@$!]+").Select(Normalize).Any(w => Words.Contains(w));
    }

    // For display: hide blocked names and strip rich-text tags.
    public static string Clean(string name)
    {
        if (name == null) return "";
        name = name.Replace("<", "").Replace(">", "");
        return IsBlocked(name) ? "Player" : name;
    }

    private static string Normalize(string s)
    {
        var sb = new StringBuilder();
        foreach (char raw in s.ToLowerInvariant())
        {
            char c = raw switch { '0' => 'o', '1' => 'i', '!' => 'i', '3' => 'e', '4' => 'a', '@' => 'a', '5' => 's', '$' => 's', '7' => 't', _ => raw };
            if (c >= 'a' && c <= 'z') sb.Append(c);
        }
        return sb.ToString();
    }
}
