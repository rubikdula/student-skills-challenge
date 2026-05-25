using StudentSkillsChallenge.Models;

namespace StudentSkillsChallenge.Services;

/// <summary>
/// Central service class for the Student Skills Challenge.
/// Owns all tournament data and enforces every business rule defined in the brief.
///
/// Architectural responsibilities:
///   1. Registration  — Teams, team members, standalone individual competitors.
///   2. Event setup   — Exactly 5 events, each explicitly typed as Team or Individual.
///   3. Scoring       — Recording placements, calculating points, preventing duplicates.
///   4. Querying      — Read-only access to data for the UI/presentation layer.
///
/// Separation-of-concerns principle:
///   This class contains ALL business logic (validation, caps, duplication checks).
///   The Models (Competitor, Team, etc.) are pure data containers.
///   The UI layer (Phase 3) will call these methods and display the results — it will
///   never touch the backing lists directly, keeping rendering and logic independent.
///
/// Error strategy:
///   Methods return a (bool Success, string Message, T? Data) result tuple instead of
///   throwing exceptions into the UI.  This means the menu loop can display user-friendly
///   messages without try/catch boilerplate everywhere in the presentation layer.
///   Internal model methods (AddMember, AddPlacement) still throw — those are
///   programming errors, not user errors, and should never reach production UI.
/// </summary>
public class TournamentManager
{
    // ─── Backing stores ───────────────────────────────────────────────────────
    // Private lists are the single source of truth for all tournament data.
    // Public access is read-only (IReadOnlyList) to prevent accidental mutation
    // from outside this service.

    private readonly List<Team>             _teams       = new();
    private readonly List<Competitor>       _competitors = new();  // all competitors: team members + standalone individuals
    private readonly List<TournamentEvent>  _events      = new();

    // ─── Auto-increment ID counters ───────────────────────────────────────────
    // Simple integer counters ensure every entity gets a unique, sequential,
    // 1-based ID — friendly for display ("Competitor #3") and safe for lookup.

    private int _nextTeamId       = 1;
    private int _nextCompetitorId = 1;
    private int _nextEventId      = 1;

    // ─── Public read-only views ───────────────────────────────────────────────

    /// <summary>All registered teams (max 4).</summary>
    public IReadOnlyList<Team>            Teams       => _teams.AsReadOnly();

    /// <summary>All registered competitors — both team members and standalone individuals.</summary>
    public IReadOnlyList<Competitor>      Competitors => _competitors.AsReadOnly();

    /// <summary>Only standalone individual competitors (not assigned to any team).</summary>
    public IReadOnlyList<Competitor>      Individuals => _competitors.Where(c => !c.IsTeamMember).ToList().AsReadOnly();

    /// <summary>All five tournament events.</summary>
    public IReadOnlyList<TournamentEvent> Events      => _events.AsReadOnly();

    // =========================================================================
    // REGION 1 — TEAM REGISTRATION
    // =========================================================================

    /// <summary>
    /// Registers a new team with the given display name.
    ///
    /// Constraint enforced: no more than <see cref="Team.MaxTeams"/> (4) teams may exist.
    /// Edge case — duplicate names: allowed by design (two teams could share a name
    /// by mistake); the UI layer can warn about this, but the model does not forbid it
    /// because names are not used as identifiers — IDs are.
    /// </summary>
    /// <param name="name">Desired team name. Must not be null or whitespace.</param>
    /// <returns>Result tuple with the created Team on success, or an error message on failure.</returns>
    public (bool Success, string Message, Team? Data) RegisterTeam(string name)
    {
        // Guard: name must not be blank.
        if (string.IsNullOrWhiteSpace(name))
            return (false, "Team name cannot be empty.", null);

        // Guard: enforce the 4-team maximum from the brief.
        if (_teams.Count >= Team.MaxTeams)
            return (false, $"Cannot register team '{name}' — the maximum of {Team.MaxTeams} teams has been reached.", null);

        var team = new Team(_nextTeamId++, name.Trim());
        _teams.Add(team);

        return (true, $"Team '{team.Name}' registered successfully (ID: {team.Id}).", team);
    }

    // =========================================================================
    // REGION 2 — TEAM MEMBER REGISTRATION
    // =========================================================================

    /// <summary>
    /// Adds a new competitor to an existing team by team ID.
    ///
    /// Constraint enforced: each team must have exactly <see cref="Team.MaxMembers"/> (5) members.
    ///   — If the team is already full this call is rejected.
    ///   — The member is added to both the team's own roster AND the master _competitors list,
    ///     so they are discoverable by ID from a single collection.
    ///
    /// Single-event entry note: team members score through their team's placements.
    /// A team with only one placement recorded is a valid single-event-entry team.
    /// </summary>
    /// <param name="teamId">ID of the target team.</param>
    /// <param name="memberName">Display name for the new member.</param>
    public (bool Success, string Message, Competitor? Data) AddTeamMember(int teamId, string memberName)
    {
        // Guard: name must not be blank.
        if (string.IsNullOrWhiteSpace(memberName))
            return (false, "Member name cannot be empty.", null);

        // Guard: team must exist.
        var team = FindTeamById(teamId);
        if (team is null)
            return (false, $"No team found with ID {teamId}.", null);

        // Guard: team must not already be full.
        if (team.IsComplete)
            return (false, $"Team '{team.Name}' already has {Team.MaxMembers} members — the roster is full.", null);

        // Create the competitor flagged as a team member (IsTeamMember = true).
        var member = new Competitor(_nextCompetitorId++, memberName.Trim(), isTeamMember: true);

        // AddMember links the competitor to the team and sets Competitor.Team internally.
        team.AddMember(member);

        // Also register in the master list so ID-based lookups work universally.
        _competitors.Add(member);

        return (true, $"'{member.Name}' added to team '{team.Name}' ({team.MemberCount}/{Team.MaxMembers} members).", member);
    }

    // =========================================================================
    // REGION 3 — INDIVIDUAL COMPETITOR REGISTRATION
    // =========================================================================

    /// <summary>
    /// Registers a standalone individual competitor (not part of any team).
    ///
    /// Constraint enforced: no more than <see cref="Competitor.MaxIndividualCompetitors"/> (20)
    /// standalone individuals may be registered.
    ///
    /// Single-event entry note: an individual with only 1 placement recorded is
    /// perfectly valid. TotalPoints = Sum of placements, which equals that one
    /// placement's points. No special handling required — the design handles it naturally.
    /// </summary>
    /// <param name="name">Competitor's display name.</param>
    public (bool Success, string Message, Competitor? Data) RegisterIndividual(string name)
    {
        // Guard: name must not be blank.
        if (string.IsNullOrWhiteSpace(name))
            return (false, "Competitor name cannot be empty.", null);

        // Guard: count only standalone individuals (IsTeamMember = false) for the 20-cap.
        // Team members are NOT counted against this limit — they have their own 4×5=20 cap.
        int individualCount = _competitors.Count(c => !c.IsTeamMember);
        if (individualCount >= Competitor.MaxIndividualCompetitors)
            return (false, $"Cannot register '{name}' — the maximum of {Competitor.MaxIndividualCompetitors} individual competitors has been reached.", null);

        // isTeamMember = false: this person appears on the individual leaderboard only.
        var competitor = new Competitor(_nextCompetitorId++, name.Trim(), isTeamMember: false);
        _competitors.Add(competitor);

        return (true, $"Individual competitor '{competitor.Name}' registered (ID: {competitor.Id}).", competitor);
    }

    // =========================================================================
    // REGION 4 — EVENT MANAGEMENT
    // =========================================================================

    /// <summary>
    /// Defines a new tournament event.
    ///
    /// Constraint enforced: exactly <see cref="TournamentEvent.MaxEvents"/> (5) events are permitted.
    ///
    /// Type enforcement: every event must be explicitly declared as
    /// <see cref="EventType.Team"/> or <see cref="EventType.Individual"/>.
    /// This is checked at the enum level — the compiler prevents any other value.
    ///
    /// Single-event entry: having fewer participants/teams with a placement in
    /// a given event is not an error. The event simply has a shorter Placements list.
    /// </summary>
    /// <param name="name">Display name (e.g., "100m Sprint", "Maths Quiz").</param>
    /// <param name="type">Team or Individual — determines which leaderboard results flow to.</param>
    /// <param name="category">Broad category: "Sporting", "Academic", or "Problem-Solving".</param>
    public (bool Success, string Message, TournamentEvent? Data) AddEvent(string name, EventType type, string category)
    {
        // Guard: name and category must not be blank.
        if (string.IsNullOrWhiteSpace(name))
            return (false, "Event name cannot be empty.", null);
        if (string.IsNullOrWhiteSpace(category))
            return (false, "Event category cannot be empty.", null);

        // Guard: enforce the 5-event maximum.
        if (_events.Count >= TournamentEvent.MaxEvents)
            return (false, $"Cannot add event '{name}' — the maximum of {TournamentEvent.MaxEvents} events has been reached.", null);

        // Guard: prevent duplicate event names (case-insensitive) to avoid confusion.
        if (_events.Any(e => e.Name.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)))
            return (false, $"An event named '{name}' already exists.", null);

        var tournamentEvent = new TournamentEvent(_nextEventId++, name.Trim(), type, category.Trim());
        _events.Add(tournamentEvent);

        return (true, $"Event '{tournamentEvent.Name}' added (ID: {tournamentEvent.Id}, Type: {type}, Category: {category}).", tournamentEvent);
    }

    // =========================================================================
    // REGION 5 — PLACEMENT RECORDING (TEAM)
    // =========================================================================

    /// <summary>
    /// Records a finishing position for a team in a Team-type event.
    ///
    /// Points are calculated automatically from <see cref="Placement.PointsTable"/>
    /// (1st=10, 2nd=8, 3rd=6, 4th=4, 5th=2, 6th+=0).
    ///
    /// Duplicate prevention: if a placement for this team in this event already exists
    /// the call is rejected. This protects against accidentally running the record flow
    /// twice for the same result.
    ///
    /// Single-event entry: recording a placement for only ONE event is fine. Nothing
    /// in this method requires all 5 events to have placements — it is purely additive.
    /// </summary>
    /// <param name="teamId">ID of the team that placed.</param>
    /// <param name="eventId">ID of the event they placed in.</param>
    /// <param name="rank">Finishing position (1 = first place). Must be ≥ 1.</param>
    public (bool Success, string Message, Placement? Data) RecordTeamPlacement(int teamId, int eventId, int rank)
    {
        // Guard: rank must be a positive integer.
        if (rank < 1)
            return (false, "Rank must be 1 or greater.", null);

        // Guard: team must exist.
        var team = FindTeamById(teamId);
        if (team is null)
            return (false, $"No team found with ID {teamId}.", null);

        // Guard: event must exist.
        var evt = FindEventById(eventId);
        if (evt is null)
            return (false, $"No event found with ID {eventId}.", null);

        // Guard: event must be a Team-type event.
        // Recording a team in an Individual event is a logical error — reject it clearly.
        if (evt.Type != EventType.Team)
            return (false, $"Event '{evt.Name}' is an Individual event. Use RecordIndividualPlacement instead.", null);

        // Guard: duplicate check — has this team already been placed in this event?
        // Checked here (before creating the Placement object) so the error message is
        // user-friendly rather than an internal exception bubbling up from the model.
        if (team.Placements.Any(p => p.Event.Id == eventId))
            return (false, $"Team '{team.Name}' already has a recorded placement in '{evt.Name}'.", null);

        // Create the placement — points are calculated inside the Placement constructor
        // via Placement.GetPoints(rank), so this method never needs to know the scale.
        var placement = new Placement(evt, rank, team);

        // Register the placement on both the team and the event for bidirectional lookup.
        team.AddPlacement(placement);
        evt.RecordPlacement(placement);

        return (true, $"Recorded: {placement}", placement);
    }

    // =========================================================================
    // REGION 6 — PLACEMENT RECORDING (INDIVIDUAL)
    // =========================================================================

    /// <summary>
    /// Records a finishing position for a standalone individual competitor in an
    /// Individual-type event.
    ///
    /// Logic mirrors <see cref="RecordTeamPlacement"/> but operates on Competitor objects.
    ///
    /// Edge case — team member in an Individual event:
    ///   The brief does not explicitly permit or forbid this, but logically team members
    ///   are scored through their team.  This method rejects team members to prevent
    ///   double-counting on both leaderboards.
    /// </summary>
    /// <param name="competitorId">ID of the individual competitor.</param>
    /// <param name="eventId">ID of the Individual-type event.</param>
    /// <param name="rank">Finishing position (1 = first). Must be ≥ 1.</param>
    public (bool Success, string Message, Placement? Data) RecordIndividualPlacement(int competitorId, int eventId, int rank)
    {
        // Guard: rank must be a positive integer.
        if (rank < 1)
            return (false, "Rank must be 1 or greater.", null);

        // Guard: competitor must exist.
        var competitor = FindCompetitorById(competitorId);
        if (competitor is null)
            return (false, $"No competitor found with ID {competitorId}.", null);

        // Guard: team members are scored via their team, not individually.
        if (competitor.IsTeamMember)
            return (false, $"'{competitor.Name}' is a team member — record their result via RecordTeamPlacement using their team ID.", null);

        // Guard: event must exist.
        var evt = FindEventById(eventId);
        if (evt is null)
            return (false, $"No event found with ID {eventId}.", null);

        // Guard: event must be Individual-type.
        if (evt.Type != EventType.Individual)
            return (false, $"Event '{evt.Name}' is a Team event. Use RecordTeamPlacement instead.", null);

        // Guard: duplicate check — has this competitor already placed in this event?
        if (competitor.Placements.Any(p => p.Event.Id == eventId))
            return (false, $"'{competitor.Name}' already has a recorded placement in '{evt.Name}'.", null);

        var placement = new Placement(evt, rank, competitor);

        // Register on both the competitor and the event.
        competitor.AddPlacement(placement);
        evt.RecordPlacement(placement);

        return (true, $"Recorded: {placement}", placement);
    }

    // =========================================================================
    // REGION 7 — LEADERBOARD QUERIES
    // =========================================================================

    /// <summary>
    /// Returns teams sorted by total points descending (highest first).
    /// Teams with no placements appear at the bottom with 0 points.
    /// Safe to call at any time — works with 0 or 5 placements per team.
    /// </summary>
    public IReadOnlyList<Team> GetTeamLeaderboard() =>
        _teams.OrderByDescending(t => t.TotalPoints).ToList().AsReadOnly();

    /// <summary>
    /// Returns standalone individual competitors sorted by total points descending.
    /// Individuals with no placements appear at the bottom with 0 points.
    /// Safe with single-event entries — TotalPoints is simply the sum of however
    /// many placements exist (1 through 5).
    /// </summary>
    public IReadOnlyList<Competitor> GetIndividualLeaderboard() =>
        _competitors
            .Where(c => !c.IsTeamMember)
            .OrderByDescending(c => c.TotalPoints)
            .ToList()
            .AsReadOnly();

    // =========================================================================
    // REGION 8 — PRIVATE LOOKUP HELPERS
    // =========================================================================
    // These helpers centralise the "find by ID" pattern so it is not duplicated
    // in every registration or placement method.  They return null on miss so
    // callers can produce a clean user-facing message rather than a thrown exception.

    /// <summary>Finds a team by its numeric ID. Returns null if not found.</summary>
    private Team? FindTeamById(int id) =>
        _teams.FirstOrDefault(t => t.Id == id);

    /// <summary>Finds any competitor (team member or individual) by ID. Returns null if not found.</summary>
    private Competitor? FindCompetitorById(int id) =>
        _competitors.FirstOrDefault(c => c.Id == id);

    /// <summary>Finds an event by its numeric ID. Returns null if not found.</summary>
    private TournamentEvent? FindEventById(int id) =>
        _events.FirstOrDefault(e => e.Id == id);
}
