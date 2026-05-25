namespace StudentSkillsChallenge.Models;

/// <summary>
/// Represents one of the five distinct competitive events in the Student Skills Challenge.
///
/// Design rationale — why TournamentEvent instead of Event?
///   "Event" is a reserved keyword concept in C# (used by the event/delegate system).
///   Naming this class TournamentEvent avoids any ambiguity for readers and prevents
///   accidental conflicts if the codebase is extended with .NET event handlers later.
///
/// Immutability:
///   Once an event is created (name, type, category) it should not change — placements
///   recorded against it depend on its identity being stable.  All properties are
///   therefore init-only after construction.  The Placements collection is append-only
///   via <see cref="RecordPlacement"/> and exposed as a read-only view.
///
/// Collection context:
///   The system holds exactly five TournamentEvent objects in a
///   List&lt;TournamentEvent&gt; inside TournamentManager.  A List (rather than a
///   fixed array) is used so event objects can be added one at a time during setup,
///   but the count is capped at <see cref="MaxEvents"/> before a new entry is accepted.
/// </summary>
public class TournamentEvent
{
    // ─── System-wide capacity constraint (brief §Data Architecture) ──────────
    /// <summary>Exactly five events are permitted system-wide.</summary>
    public const int MaxEvents = 5;

    // ─── Identity ─────────────────────────────────────────────────────────────

    /// <summary>Auto-assigned unique identifier (1-based).</summary>
    public int Id { get; }

    /// <summary>Human-readable event name (e.g., "100m Sprint", "Coding Quiz").</summary>
    public string Name { get; }

    /// <summary>
    /// Whether this event is scored at the team level or the individual level.
    /// This is the primary branch point throughout all scoring and display logic.
    /// </summary>
    public EventType Type { get; }

    /// <summary>
    /// Broad category label for display purposes (e.g., "Sporting", "Academic",
    /// "Problem-Solving"). Stored as a string rather than a second enum to allow
    /// arbitrary descriptive text without needing to update the enum for every
    /// new category name.
    /// </summary>
    public string Category { get; }

    // ─── Placement ledger ─────────────────────────────────────────────────────

    /// <summary>All placements recorded for this event, ordered by rank ascending.</summary>
    public IReadOnlyList<Placement> Placements => _placements.AsReadOnly();
    private readonly List<Placement> _placements = new();

    // ─── Constructor ──────────────────────────────────────────────────────────

    /// <param name="id">Unique sequential identifier.</param>
    /// <param name="name">Event display name. Must not be null or whitespace.</param>
    /// <param name="type">Team or Individual — determines which leaderboard scores flow to.</param>
    /// <param name="category">Descriptive category (e.g., "Sporting", "Academic", "Problem-Solving").</param>
    public TournamentEvent(int id, string name, EventType type, string category)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Event name cannot be empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Event category cannot be empty.", nameof(category));

        Id = id;
        Name = name;
        Type = type;
        Category = category;
    }

    // ─── Internal mutation ────────────────────────────────────────────────────

    /// <summary>Appends a placement to this event's ledger.</summary>
    internal void RecordPlacement(Placement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        _placements.Add(placement);
    }

    public override string ToString() => $"[{Id}] {Name} ({Category} — {Type})";
}
