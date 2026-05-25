using System.Text;
using StudentSkillsChallenge.Models;

namespace StudentSkillsChallenge.Services;

/// <summary>
/// Calculates and formats all leaderboard views for the Student Skills Challenge.
///
/// Separation-of-concerns contract:
///   This class never calls Console.WriteLine. Every public method returns a
///   pre-formatted string that the UI layer (Phase 4) can print as-is.
///   This keeps rendering concerns entirely out of the scoring logic, making
///   both layers independently testable and easier to document.
///
/// Dependency:
///   Constructed with a <see cref="TournamentManager"/> reference so it always
///   reads live data.  No data is copied or cached here — leaderboards are
///   regenerated on each call, which is correct and negligibly cheap at this scale.
/// </summary>
public class LeaderboardService
{
    private readonly TournamentManager _manager;

    // ─── Column widths for table formatting ───────────────────────────────────
    // Defined as constants so adjusting the layout requires changing one number,
    // not hunting through multiple string-pad calls.
    private const int ColRank       = 4;
    private const int ColName       = 24;
    private const int ColPoints     = 8;
    private const int ColEvents     = 7;

    public LeaderboardService(TournamentManager manager)
    {
        _manager = manager ?? throw new ArgumentNullException(nameof(manager));
    }

    // =========================================================================
    // PUBLIC — INDIVIDUAL LEADERBOARD
    // =========================================================================

    /// <summary>
    /// Builds and returns a formatted individual leaderboard string.
    ///
    /// Aggregation: uses <see cref="Competitor.TotalPoints"/>, which sums every
    /// recorded <see cref="Placement.PointsAwarded"/> for that competitor.
    /// A competitor with only one placement (single-event entry) accumulates
    /// exactly that placement's points — no penalty, no special case.
    /// A competitor with no placements scores 0 and appears at the bottom.
    ///
    /// Tie-handling: Standard Competition Ranking (1-2-2-4), applied by
    /// <see cref="Apply1224Ranking"/>.
    /// </summary>
    public string FormatIndividualLeaderboard()
    {
        var individuals = _manager.Individuals
            .OrderByDescending(c => c.TotalPoints)
            .ToList();

        var sb = new StringBuilder();
        AppendTitle(sb, "INDIVIDUAL LEADERBOARD");
        AppendTableHeader(sb, "Competitor", "Events");

        if (individuals.Count == 0)
        {
            AppendEmptyRow(sb, "No individual competitors registered.");
            AppendDivider(sb);
            return sb.ToString();
        }

        int[] ranks = Apply1224Ranking(individuals.Select(c => c.TotalPoints).ToList());

        for (int i = 0; i < individuals.Count; i++)
        {
            var c = individuals[i];
            AppendDataRow(sb,
                rank:   ranks[i],
                name:   c.Name,
                points: c.TotalPoints,
                extra:  c.Placements.Count.ToString()   // how many events entered
            );
        }

        AppendDivider(sb);
        return sb.ToString();
    }

    // =========================================================================
    // PUBLIC — TEAM LEADERBOARD
    // =========================================================================

    /// <summary>
    /// Builds and returns a formatted team leaderboard string.
    ///
    /// Aggregation: uses <see cref="Team.TotalPoints"/>, which sums every team-event
    /// <see cref="Placement.PointsAwarded"/> recorded against the team object.
    /// This is consistent with the Phase 1/2 design where team placements are stored
    /// on the Team, not on individual members.
    ///
    /// A team with only one placement (single-event entry) accumulates exactly that
    /// placement's points.  A team with no placements scores 0 and appears at the bottom.
    ///
    /// Tie-handling: same 1-2-2-4 algorithm as the individual leaderboard.
    /// </summary>
    public string FormatTeamLeaderboard()
    {
        var teams = _manager.Teams
            .OrderByDescending(t => t.TotalPoints)
            .ToList();

        var sb = new StringBuilder();
        AppendTitle(sb, "TEAM LEADERBOARD");
        AppendTableHeader(sb, "Team", "Events");

        if (teams.Count == 0)
        {
            AppendEmptyRow(sb, "No teams registered.");
            AppendDivider(sb);
            return sb.ToString();
        }

        int[] ranks = Apply1224Ranking(teams.Select(t => t.TotalPoints).ToList());

        for (int i = 0; i < teams.Count; i++)
        {
            var t = teams[i];
            AppendDataRow(sb,
                rank:   ranks[i],
                name:   $"{t.Name} ({t.MemberCount}/{Team.MaxMembers})",
                points: t.TotalPoints,
                extra:  t.Placements.Count.ToString()
            );
        }

        AppendDivider(sb);
        return sb.ToString();
    }

    // =========================================================================
    // PUBLIC — EVENT-SPECIFIC RESULTS
    // =========================================================================

    /// <summary>
    /// Builds and returns a formatted results view for one specific event.
    ///
    /// Dynamically branches on <see cref="TournamentEvent.Type"/>:
    ///   Team event    → lists teams by finishing rank.
    ///   Individual event → lists competitors by finishing rank.
    ///
    /// The data comes directly from the event's own <see cref="TournamentEvent.Placements"/>
    /// list, which is already in insertion order.  Results are re-sorted by
    /// <see cref="Placement.Rank"/> ascending so display order is always correct
    /// regardless of the order placements were recorded.
    /// </summary>
    /// <param name="eventId">ID of the event to display.</param>
    /// <returns>Formatted results string, or an error message string if not found.</returns>
    public string FormatEventResults(int eventId)
    {
        var evt = _manager.Events.FirstOrDefault(e => e.Id == eventId);

        if (evt is null)
            return $"[Error] No event found with ID {eventId}.";

        var sb = new StringBuilder();
        AppendTitle(sb, $"EVENT RESULTS — {evt.Name.ToUpper()}");
        sb.AppendLine($"  Category : {evt.Category}");
        sb.AppendLine($"  Type     : {evt.Type} Event");
        AppendDivider(sb);

        var placements = evt.Placements
            .OrderBy(p => p.Rank)
            .ToList();

        if (placements.Count == 0)
        {
            AppendEmptyRow(sb, "No placements recorded for this event yet.");
            AppendDivider(sb);
            return sb.ToString();
        }

        // Header adapts to event type
        AppendTableHeader(sb, evt.Type == EventType.Team ? "Team" : "Competitor", "Pts");

        foreach (var p in placements)
        {
            string participantName = evt.Type == EventType.Team
                ? p.Team!.Name
                : p.Competitor!.Name;

            string medal = p.Rank switch
            {
                1 => "1st",
                2 => "2nd",
                3 => "3rd",
                _ => $"{p.Rank}th"
            };

            AppendDataRow(sb,
                rank:   p.Rank,
                name:   $"{medal}  {participantName}",
                points: p.PointsAwarded,
                extra:  string.Empty
            );
        }

        AppendDivider(sb);
        return sb.ToString();
    }

    // =========================================================================
    // PRIVATE — 1-2-2-4 STANDARD COMPETITION RANKING ALGORITHM
    // =========================================================================

    /// <summary>
    /// Assigns Standard Competition Ranking (also called 1-2-2-4 or "Olympic" ranking)
    /// to an already-sorted list of point values.
    ///
    /// ── How the algorithm works (pseudocode for design documentation) ──────
    ///
    ///   INPUT : scores[]  — point values sorted high→low  (e.g. [10, 8, 8, 5])
    ///   OUTPUT: ranks[]   — 1-based rank for each entry   (e.g. [ 1, 2, 2, 4])
    ///
    ///   STEP 1  Set i = 0  (outer cursor — walks through every entry)
    ///   STEP 2  While i < length of scores:
    ///     STEP 3    Set j = i  (inner cursor — finds the end of the tied group)
    ///     STEP 4    While j < length AND scores[j] == scores[i]:
    ///                   advance j by 1
    ///               (j now points ONE PAST the last tied entry)
    ///     STEP 5    Assign rank (i + 1) to every entry from index i up to j-1
    ///               WHY (i + 1)?  Because i is zero-based; adding 1 converts to
    ///               a 1-based rank.  All tied entries get the SAME rank — the rank
    ///               of the FIRST entry in their group.
    ///     STEP 6    Set i = j
    ///               WHY jump to j?  The entries between i and j-1 are all tied.
    ///               Jumping past the whole group creates the characteristic "gap"
    ///               (e.g., two 2nd-place entries mean no 3rd — next rank is 4th).
    ///   STEP 7  Return ranks[]
    ///
    /// ── Worked example ──────────────────────────────────────────────────────
    ///   scores = [10, 8, 8, 5]
    ///   i=0: scores[0]=10, j advances to 1 (10≠8 stops it). Group size=1. ranks[0]=1.  i→1
    ///   i=1: scores[1]=8,  j advances to 3 (8==8, then 8≠5 stops it). Group size=2. ranks[1]=ranks[2]=2. i→3
    ///   i=3: scores[3]=5,  j advances to 4 (end). Group size=1. ranks[3]=4. i→4
    ///   Result: [1, 2, 2, 4]  ✓
    ///
    /// ── Why not a simpler loop? ─────────────────────────────────────────────
    ///   A naive loop (rank = i + 1) would produce [1, 2, 3, 4] and split the tie,
    ///   which is unfair.  The two-cursor approach correctly "uses up" all tied
    ///   positions before moving to the next rank, matching the international
    ///   standard for sports competitions.
    /// </summary>
    /// <param name="sortedPointsDescending">
    ///   Point totals already sorted highest-first.
    ///   The caller is responsible for sorting — this method only assigns ranks.
    /// </param>
    /// <returns>
    ///   Integer array of the same length as the input, where each element is
    ///   the 1-based Standard Competition Rank for the corresponding entry.
    /// </returns>
    private static int[] Apply1224Ranking(IReadOnlyList<int> sortedPointsDescending)
    {
        int[] ranks = new int[sortedPointsDescending.Count];

        int i = 0;
        while (i < sortedPointsDescending.Count)
        {
            // STEP 3-4: find the boundary of the current tied group.
            int j = i;
            while (j < sortedPointsDescending.Count
                   && sortedPointsDescending[j] == sortedPointsDescending[i])
            {
                j++;
            }

            // STEP 5: assign the same rank to every member of the tied group.
            // Rank = i + 1 because i is zero-based and ranks are 1-based.
            int groupRank = i + 1;
            for (int k = i; k < j; k++)
                ranks[k] = groupRank;

            // STEP 6: jump past the entire tied group, creating the rank gap.
            i = j;
        }

        return ranks;
    }

    // =========================================================================
    // PRIVATE — FORMATTING HELPERS
    // =========================================================================
    // All table output is built through these helpers so column widths and
    // separator styles are defined in one place.

    private static void AppendTitle(StringBuilder sb, string title)
    {
        string line = new string('═', ColRank + ColName + ColPoints + ColEvents + 7);
        sb.AppendLine();
        sb.AppendLine($"  ╔{line}╗");
        sb.AppendLine($"  ║  {title.PadRight(line.Length - 2)}║");
        sb.AppendLine($"  ╚{line}╝");
    }

    private static void AppendTableHeader(StringBuilder sb, string nameLabel, string extraLabel)
    {
        AppendDivider(sb);
        sb.AppendLine(
            $"  | {"#".PadRight(ColRank)}" +
            $"| {nameLabel.PadRight(ColName)}" +
            $"| {"Points".PadLeft(ColPoints)}" +
            $"| {extraLabel.PadLeft(ColEvents)} |"
        );
        AppendDivider(sb);
    }

    private static void AppendDataRow(StringBuilder sb, int rank, string name, int points, string extra)
    {
        string rankStr   = rank.ToString().PadRight(ColRank);
        string nameStr   = name.Length > ColName ? name[..ColName] : name.PadRight(ColName);
        string pointsStr = points.ToString().PadLeft(ColPoints);
        string extraStr  = extra.PadLeft(ColEvents);

        sb.AppendLine($"  | {rankStr}| {nameStr}| {pointsStr}| {extraStr} |");
    }

    private static void AppendEmptyRow(StringBuilder sb, string message)
    {
        int width = ColRank + ColName + ColPoints + ColEvents + 7;
        sb.AppendLine($"  | {message.PadRight(width - 2)} |");
    }

    private static void AppendDivider(StringBuilder sb)
    {
        string seg1 = new string('-', ColRank + 1);
        string seg2 = new string('-', ColName + 1);
        string seg3 = new string('-', ColPoints + 1);
        string seg4 = new string('-', ColEvents + 2);
        sb.AppendLine($"  +-{seg1}+-{seg2}+-{seg3}+-{seg4}+");
    }
}
