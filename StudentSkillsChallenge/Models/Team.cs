namespace StudentSkillsChallenge.Models;

/// <summary>
/// Represents one of the four competing teams in the Student Skills Challenge.
///
/// Design rationale — why a Team class separate from a List of Competitors?
///   The brief imposes two hard constraints that need active enforcement:
///   (a) no more than four teams exist system-wide, and
///   (b) each team must have exactly five members.
///   Embedding both checks inside this class keeps validation close to the data
///   and prevents the rest of the application from creating invalid state.
///
/// Collection context:
///   Members are stored in a List&lt;Competitor&gt; (max 5) rather than a fixed array
///   so we can append members one at a time during the registration flow without
///   knowing all five names upfront.  The MaxMembers constant acts as the guard.
///
///   Team-event Placements are stored in a separate List&lt;Placement&gt; on this object
///   (mirroring the design on Competitor) so that score aggregation can be written
///   identically for both entity types.
/// </summary>
public class Team
{
    // ─── System-wide capacity constraints (brief §Data Architecture) ─────────
    /// <summary>Maximum number of teams permitted in the tournament.</summary>
    public const int MaxTeams = 4;

    /// <summary>Exact number of members every team must have before it is considered complete.</summary>
    public const int MaxMembers = 5;

    // ─── Identity ─────────────────────────────────────────────────────────────

    /// <summary>Auto-assigned unique identifier (1-based).</summary>
    public int Id { get; }

    /// <summary>Display name for this team (e.g., "Team Alpha").</summary>
    public string Name { get; }

    // ─── Membership ───────────────────────────────────────────────────────────

    /// <summary>
    /// Read-only view of registered members.  Callers can iterate and query but
    /// cannot mutate the list directly — all writes go through <see cref="AddMember"/>.
    /// </summary>
    public IReadOnlyList<Competitor> Members => _members.AsReadOnly();
    private readonly List<Competitor> _members = new();

    /// <summary>True once the team roster reaches exactly <see cref="MaxMembers"/> members.</summary>
    public bool IsComplete => _members.Count == MaxMembers;

    /// <summary>Current headcount (0–5).</summary>
    public int MemberCount => _members.Count;

    // ─── Scoring accumulator ─────────────────────────────────────────────────

    /// <summary>
    /// All team-event placements recorded for this team.
    /// See <see cref="Competitor.Placements"/> for the equivalent individual structure.
    /// </summary>
    public IReadOnlyList<Placement> Placements => _placements.AsReadOnly();
    private readonly List<Placement> _placements = new();

    /// <summary>
    /// Sum of points from every team-event placement.
    /// Computed on demand — at most 5 entries, so no caching needed.
    /// </summary>
    public int TotalPoints => _placements.Sum(p => p.PointsAwarded);

    // ─── Constructor ──────────────────────────────────────────────────────────

    /// <param name="id">Unique sequential identifier.</param>
    /// <param name="name">Team display name. Must not be null or whitespace.</param>
    public Team(int id, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Team name cannot be empty.", nameof(name));

        Id = id;
        Name = name;
    }

    // ─── Internal mutation ────────────────────────────────────────────────────

    /// <summary>
    /// Registers a competitor as a member of this team.
    /// Throws if the roster is already full or if the competitor is already on a team.
    /// </summary>
    internal void AddMember(Competitor competitor)
    {
        ArgumentNullException.ThrowIfNull(competitor);

        if (IsComplete)
            throw new InvalidOperationException(
                $"Team '{Name}' already has the maximum of {MaxMembers} members.");

        if (competitor.Team is not null)
            throw new InvalidOperationException(
                $"Competitor '{competitor.Name}' is already assigned to team '{competitor.Team.Name}'.");

        _members.Add(competitor);
        competitor.Team = this;
    }

    /// <summary>Appends a team-event placement; enforces the one-entry-per-event rule.</summary>
    internal void AddPlacement(Placement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);

        if (_placements.Any(p => p.Event.Id == placement.Event.Id))
            throw new InvalidOperationException(
                $"Team '{Name}' already has a placement for event '{placement.Event.Name}'.");

        _placements.Add(placement);
    }

    public override string ToString() => $"[{Id}] {Name} ({MemberCount}/{MaxMembers} members)";
}
