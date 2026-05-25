namespace StudentSkillsChallenge.Models;

/// <summary>
/// Represents a single individual competitor in the Student Skills Challenge.
///
/// Design rationale — why a separate Competitor class?
///   Team members and standalone individual competitors are fundamentally different
///   entities in the scoring model: a team member's scores roll up to their team,
///   whereas a standalone individual's scores stand alone on the individual leaderboard.
///   The <see cref="IsTeamMember"/> flag keeps that distinction explicit without
///   requiring two entirely separate class hierarchies.
///
/// Collection context:
///   All Competitor objects (both team-assigned and standalone) are held in a single
///   List&lt;Competitor&gt; in TournamentManager.  This lets us filter with LINQ in O(n)
///   time — cheap enough for the 20-entry maximum defined in the brief.
/// </summary>
public class Competitor
{
    // ─── System-wide capacity constraint (brief §Data Architecture) ──────────
    /// <summary>Maximum number of standalone individual competitors permitted.</summary>
    public const int MaxIndividualCompetitors = 20;

    // ─── Identity ─────────────────────────────────────────────────────────────

    /// <summary>Auto-assigned unique identifier (1-based for display friendliness).</summary>
    public int Id { get; }

    /// <summary>Full display name of the competitor.</summary>
    public string Name { get; }

    // ─── Team affiliation ─────────────────────────────────────────────────────

    /// <summary>
    /// True when this competitor belongs to a <see cref="Team"/>; false when they
    /// compete as a standalone individual on the individual leaderboard.
    /// </summary>
    public bool IsTeamMember { get; }

    /// <summary>
    /// The team this competitor belongs to, or null if they are a standalone individual.
    /// Nullable reference type forces callers to handle the null case explicitly.
    /// </summary>
    public Team? Team { get; internal set; }

    // ─── Scoring accumulator ─────────────────────────────────────────────────

    /// <summary>
    /// Read-only list of every individual-event placement recorded for this competitor.
    /// Using List&lt;Placement&gt; (rather than an array) allows O(1) amortised append
    /// and LINQ filtering, with no upper bound imposed here — the five-event cap is
    /// enforced at the point of recording a placement.
    /// </summary>
    public IReadOnlyList<Placement> Placements => _placements.AsReadOnly();
    private readonly List<Placement> _placements = new();

    /// <summary>
    /// Computed total points across all individual-event placements.
    /// Recalculated on demand (not cached) — with at most 5 placements per competitor
    /// this is negligibly cheap and avoids stale-cache bugs entirely.
    /// </summary>
    public int TotalPoints => _placements.Sum(p => p.PointsAwarded);

    // ─── Constructor ──────────────────────────────────────────────────────────

    /// <param name="id">Unique sequential identifier.</param>
    /// <param name="name">Competitor's display name. Must not be null or whitespace.</param>
    /// <param name="isTeamMember">
    ///   Pass <c>true</c> when registering this person as part of a team;
    ///   <c>false</c> when registering as a standalone individual.
    /// </param>
    public Competitor(int id, string name, bool isTeamMember = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Competitor name cannot be empty.", nameof(name));

        Id = id;
        Name = name;
        IsTeamMember = isTeamMember;
    }

    // ─── Internal mutation (called by TournamentManager only) ────────────────

    /// <summary>Appends a placement record; enforces the single-event cap per participant.</summary>
    internal void AddPlacement(Placement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);

        if (_placements.Any(p => p.Event.Id == placement.Event.Id))
            throw new InvalidOperationException(
                $"Competitor '{Name}' already has a placement for event '{placement.Event.Name}'.");

        _placements.Add(placement);
    }

    public override string ToString() => $"[{Id}] {Name}{(IsTeamMember ? $" ({Team?.Name})" : " (Individual)")}";
}
