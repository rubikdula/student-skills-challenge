namespace StudentSkillsChallenge.Models;

/// <summary>
/// Records the finishing position of one participant (a <see cref="Team"/> or
/// a <see cref="Competitor"/>) in one <see cref="TournamentEvent"/>, together
/// with the points that position is worth.
///
/// ─── Points Distribution Matrix ────────────────────────────────────────────
///
///   Rank  │ Points
///   ──────┼────────
///   1st   │  10
///   2nd   │   8
///   3rd   │   6
///   4th   │   4
///   5th   │   2
///   6th+  │   0  (entered but unplaced)
///
/// Justification for this scale (BTEC B.M2 design rationale):
///
///   1. Meaningful separation at the top.
///      The gap between 1st (10) and 2nd (8) is 2 points — the same as every
///      subsequent step — so winning is always worth more than second place
///      but not by an unfair margin that makes lower places irrelevant.
///
///   2. No zero-score for placed competitors.
///      All five scoring positions (1st–5th) receive at least 2 points.  This
///      keeps every placed participant on the leaderboard with a positive score,
///      which is motivating and makes the leaderboard easier to read.
///
///   3. Single-Event Entry is handled gracefully.
///      A competitor who enters only one event accumulates points from exactly
///      that one placement.  Because every placement stores its points directly
///      on the Placement record (not calculated at query time from a lookup
///      table), a sparse dataset — where most competitors have 0–1 placements
///      instead of all five — aggregates correctly with a simple Sum().
///      There is no division, no average, and no "did not enter" penalty to
///      manage, so the algorithm stays O(n) with no special cases.
///
///   4. Deterministic tie-breaking is feasible.
///      Equal totals produce equal rank.  If a tie-break rule is needed later
///      (e.g., count of 1st-place finishes), the raw Placement records held on
///      each Competitor and Team object supply the data without any schema change.
///
///   5. The scale is even and easy to explain to participants.
///      10 → 8 → 6 → 4 → 2 is a clean arithmetic sequence (step = 2).
///      Participants can mentally verify their own score, which supports the
///      transparency expected of an official college competition.
///
/// ─── Design notes ───────────────────────────────────────────────────────────
///
///   Placement is intentionally a pure data record — it owns no logic.
///   Points are calculated once at creation by the static <see cref="PointsTable"/>
///   and then stored, so leaderboard queries never touch the table again.
///
///   Either <see cref="Team"/> or <see cref="Competitor"/> will be non-null,
///   never both (enforced by the constructor overloads).  This models the
///   mutually-exclusive nature of team vs. individual event entries without
///   needing an inheritance hierarchy.
/// </summary>
public class Placement
{
    // ─── Static points lookup ─────────────────────────────────────────────────

    /// <summary>
    /// Maps finishing rank (1-based) to points awarded.
    /// Implemented as a Dictionary for O(1) lookup and easy future adjustment.
    /// Ranks beyond the table receive 0 points.
    /// </summary>
    public static readonly IReadOnlyDictionary<int, int> PointsTable =
        new Dictionary<int, int>
        {
            { 1, 10 },
            { 2,  8 },
            { 3,  6 },
            { 4,  4 },
            { 5,  2 }
        };

    /// <summary>Convenience method: looks up points for a given rank, returns 0 if unranked.</summary>
    public static int GetPoints(int rank) =>
        PointsTable.TryGetValue(rank, out int pts) ? pts : 0;

    // ─── Placement data ───────────────────────────────────────────────────────

    /// <summary>The event this placement belongs to.</summary>
    public TournamentEvent Event { get; }

    /// <summary>Finishing position within the event (1 = first place).</summary>
    public int Rank { get; }

    /// <summary>Points awarded for this rank, frozen at creation time.</summary>
    public int PointsAwarded { get; }

    // ─── Participant (exactly one of the two properties below will be non-null) ─

    /// <summary>The team that achieved this placement (null for Individual events).</summary>
    public Team? Team { get; }

    /// <summary>The individual competitor that achieved this placement (null for Team events).</summary>
    public Competitor? Competitor { get; }

    // ─── Constructors ─────────────────────────────────────────────────────────

    /// <summary>Creates a placement record for a <see cref="Models.Team"/> in a Team event.</summary>
    public Placement(TournamentEvent tournamentEvent, int rank, Team team)
    {
        ArgumentNullException.ThrowIfNull(tournamentEvent);
        ArgumentNullException.ThrowIfNull(team);

        if (tournamentEvent.Type != EventType.Team)
            throw new ArgumentException(
                $"Cannot record a team placement for Individual event '{tournamentEvent.Name}'.");

        if (rank < 1)
            throw new ArgumentOutOfRangeException(nameof(rank), "Rank must be 1 or greater.");

        Event = tournamentEvent;
        Rank = rank;
        PointsAwarded = GetPoints(rank);
        Team = team;
    }

    /// <summary>Creates a placement record for a <see cref="Models.Competitor"/> in an Individual event.</summary>
    public Placement(TournamentEvent tournamentEvent, int rank, Competitor competitor)
    {
        ArgumentNullException.ThrowIfNull(tournamentEvent);
        ArgumentNullException.ThrowIfNull(competitor);

        if (tournamentEvent.Type != EventType.Individual)
            throw new ArgumentException(
                $"Cannot record an individual placement for Team event '{tournamentEvent.Name}'.");

        if (rank < 1)
            throw new ArgumentOutOfRangeException(nameof(rank), "Rank must be 1 or greater.");

        Event = tournamentEvent;
        Rank = rank;
        PointsAwarded = GetPoints(rank);
        Competitor = competitor;
    }

    public override string ToString()
    {
        string participant = Team is not null ? Team.Name : Competitor!.Name;
        string medal = Rank switch { 1 => "Gold", 2 => "Silver", 3 => "Bronze", _ => $"{Rank}th" };
        return $"{medal} — {participant} in '{Event.Name}' (+{PointsAwarded} pts)";
    }
}
