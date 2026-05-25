using StudentSkillsChallenge.Models;
using StudentSkillsChallenge.Services;

namespace StudentSkillsChallenge.UI;

/// <summary>
/// Owns the entire console interaction loop for the Student Skills Challenge.
///
/// Architectural contract (strictly enforced):
///   ✓  All Console.ReadLine() / Console.Write() calls live here only.
///   ✓  Business logic delegates to TournamentManager.
///   ✓  Leaderboard strings come pre-built from LeaderboardService.
///   ✓  Input validation happens before any service call — services never
///       receive invalid data from this layer.
///   ✗  No scoring logic, no ranking math, no data mutation outside services.
/// </summary>
public class ConsoleUI
{
    private readonly TournamentManager   _manager;
    private readonly LeaderboardService  _leaderboard;

    private const string AppTitle = "STUDENT SKILLS CHALLENGE — SCORING SYSTEM";

    public ConsoleUI(TournamentManager manager, LeaderboardService leaderboard)
    {
        _manager     = manager     ?? throw new ArgumentNullException(nameof(manager));
        _leaderboard = leaderboard ?? throw new ArgumentNullException(nameof(leaderboard));
    }

    // =========================================================================
    // PUBLIC — MAIN LOOP
    // =========================================================================

    /// <summary>
    /// Starts the application loop. Returns only when the user selects Exit (10).
    /// The loop itself cannot crash: ReadInt handles every invalid menu input.
    /// </summary>
    public void Run()
    {
        Console.Title = AppTitle;

        while (true)
        {
            Console.Clear();
            ShowMainMenu();

            // ReadInt loops internally until a valid integer in range is entered.
            // An invalid choice (letters, blank, out-of-range) is caught here —
            // the switch block below is only reached with a guaranteed valid value.
            int choice = ReadInt("  Select option: ", 1, 10);

            switch (choice)
            {
                case  1: RegisterTeam();              break;
                case  2: AddTeamMember();             break;
                case  3: RegisterIndividual();        break;
                case  4: CreateEvent();               break;
                case  5: RecordPlacement();           break;
                case  6: ViewIndividualLeaderboard(); break;
                case  7: ViewTeamLeaderboard();       break;
                case  8: ViewEventResults();          break;
                case  9: SeedMockData();              break;
                case 10:
                    Console.Clear();
                    Console.WriteLine("\n  Goodbye!\n");
                    return;
            }
        }
    }

    // =========================================================================
    // PRIVATE — MENU ACTIONS (one method per option)
    // =========================================================================

    /// <summary>Option 1 — Register a new team; enforces the 4-team cap.</summary>
    private void RegisterTeam()
    {
        ShowHeader("REGISTER NEW TEAM");
        Console.WriteLine($"  Teams registered: {_manager.Teams.Count} / {Team.MaxTeams}\n");

        string name = ReadString("  Team name: ");
        var (success, message, _) = _manager.RegisterTeam(name);

        PrintResult(success, message);
        Pause();
    }

    /// <summary>Option 2 — Add a member to an existing team; enforces the 5-member cap.</summary>
    private void AddTeamMember()
    {
        ShowHeader("ADD MEMBER TO TEAM");

        if (_manager.Teams.Count == 0)
        {
            PrintResult(false, "No teams registered yet — use Option 1 first.");
            Pause();
            return;
        }

        ShowTeamList();

        int    teamId = ReadInt("  Enter Team ID: ", 1, int.MaxValue);
        string name   = ReadString("  Member name:   ");

        var (success, message, _) = _manager.AddTeamMember(teamId, name);
        PrintResult(success, message);
        Pause();
    }

    /// <summary>Option 3 — Register a standalone individual; enforces the 20-person cap.</summary>
    private void RegisterIndividual()
    {
        ShowHeader("REGISTER INDIVIDUAL COMPETITOR");
        Console.WriteLine($"  Individuals registered: {_manager.Individuals.Count} / {Competitor.MaxIndividualCompetitors}\n");

        string name = ReadString("  Competitor name: ");
        var (success, message, _) = _manager.RegisterIndividual(name);

        PrintResult(success, message);
        Pause();
    }

    /// <summary>Option 4 — Define one of the five tournament events.</summary>
    private void CreateEvent()
    {
        ShowHeader("CREATE TOURNAMENT EVENT");
        Console.WriteLine($"  Events created: {_manager.Events.Count} / {TournamentEvent.MaxEvents}\n");

        string name = ReadString("  Event name: ");

        // Event type — compile-safe: only Team or Individual can be passed to AddEvent.
        Console.WriteLine("\n  Event Type:");
        Console.WriteLine("    1.  Team Event");
        Console.WriteLine("    2.  Individual Event");
        int typeChoice = ReadInt("  Select (1-2): ", 1, 2);
        EventType type = typeChoice == 1 ? EventType.Team : EventType.Individual;

        // Category — three standard options plus a free-text fallback.
        Console.WriteLine("\n  Category:");
        Console.WriteLine("    1.  Sporting");
        Console.WriteLine("    2.  Academic");
        Console.WriteLine("    3.  Problem-Solving");
        Console.WriteLine("    4.  Other (type your own)");
        int catChoice = ReadInt("  Select (1-4): ", 1, 4);
        string category = catChoice switch
        {
            1 => "Sporting",
            2 => "Academic",
            3 => "Problem-Solving",
            _ => ReadString("  Category name: ")
        };

        var (success, message, _) = _manager.AddEvent(name, type, category);
        PrintResult(success, message);
        Pause();
    }

    /// <summary>
    /// Option 5 — Record a placement result for a team or individual in an event.
    ///
    /// Flow:
    ///   1. Show available events → user picks event ID.
    ///   2. Branch on EventType:
    ///      Team      → show teams → pick team ID → enter rank.
    ///      Individual → show individuals → pick competitor ID → enter rank.
    ///   3. For Team events, validate the team roster is complete (5/5) before
    ///      forwarding to TournamentManager — incomplete teams cannot score.
    /// </summary>
    private void RecordPlacement()
    {
        ShowHeader("RECORD EVENT PLACEMENT");

        if (_manager.Events.Count == 0)
        {
            PrintResult(false, "No events created yet — use Option 4 first.");
            Pause();
            return;
        }

        // ── Step 1: show events and pick one ─────────────────────────────────
        Console.WriteLine("  Available Events:\n");
        foreach (var e in _manager.Events)
            Console.WriteLine($"    [{e.Id}] {e.Name,-28} ({e.Category} — {e.Type})");

        Console.WriteLine();
        int eventId = ReadInt("  Enter Event ID: ", 1, int.MaxValue);

        // Look up the chosen event before continuing.
        var evt = _manager.Events.FirstOrDefault(e => e.Id == eventId);
        if (evt is null)
        {
            PrintResult(false, $"No event found with ID {eventId}.");
            Pause();
            return;
        }

        Console.WriteLine($"\n  Selected: [{evt.Id}] {evt.Name}  ({evt.Type} Event)");
        Console.WriteLine();

        // ── Step 2: branch on event type ─────────────────────────────────────
        if (evt.Type == EventType.Team)
        {
            if (_manager.Teams.Count == 0)
            {
                PrintResult(false, "No teams registered yet — use Option 1 first.");
                Pause();
                return;
            }

            ShowTeamList();
            int teamId = ReadInt("  Enter Team ID: ", 1, int.MaxValue);

            // UI-level guard: team must be complete before it can earn a placement.
            // This enforces the confirmed brief requirement (exactly 5 members).
            var team = _manager.Teams.FirstOrDefault(t => t.Id == teamId);
            if (team is not null && !team.IsComplete)
            {
                PrintResult(false,
                    $"Team '{team.Name}' only has {team.MemberCount}/{Team.MaxMembers} members. " +
                    "Complete the roster (Option 2) before recording placements.");
                Pause();
                return;
            }

            int rank = ReadInt("  Finishing position (rank): ", 1, 99);
            var (success, message, _) = _manager.RecordTeamPlacement(teamId, eventId, rank);
            PrintResult(success, message);
        }
        else // Individual event
        {
            if (_manager.Individuals.Count == 0)
            {
                PrintResult(false, "No individual competitors registered yet — use Option 3 first.");
                Pause();
                return;
            }

            ShowIndividualList();
            int competitorId = ReadInt("  Enter Competitor ID: ", 1, int.MaxValue);
            int rank         = ReadInt("  Finishing position (rank): ", 1, 99);

            var (success, message, _) = _manager.RecordIndividualPlacement(competitorId, eventId, rank);
            PrintResult(success, message);
        }

        Pause();
    }

    /// <summary>Option 6 — Print the pre-formatted individual leaderboard and wait.</summary>
    private void ViewIndividualLeaderboard()
    {
        Console.Clear();
        Console.WriteLine(_leaderboard.FormatIndividualLeaderboard());
        Pause();
    }

    /// <summary>Option 7 — Print the pre-formatted team leaderboard and wait.</summary>
    private void ViewTeamLeaderboard()
    {
        Console.Clear();
        Console.WriteLine(_leaderboard.FormatTeamLeaderboard());
        Pause();
    }

    /// <summary>Option 8 — Pick an event by ID, then print its pre-formatted results.</summary>
    private void ViewEventResults()
    {
        ShowHeader("VIEW EVENT RESULTS");

        if (_manager.Events.Count == 0)
        {
            PrintResult(false, "No events created yet — use Option 4 first.");
            Pause();
            return;
        }

        Console.WriteLine("  Events:\n");
        foreach (var e in _manager.Events)
            Console.WriteLine($"    [{e.Id}] {e.Name,-28} ({e.Type})");

        Console.WriteLine();
        int eventId = ReadInt("  Enter Event ID: ", 1, int.MaxValue);

        Console.Clear();
        Console.WriteLine(_leaderboard.FormatEventResults(eventId));
        Pause();
    }

    // =========================================================================
    // PRIVATE — SEED MOCK DATA (Option 9)
    // =========================================================================

    /// <summary>
    /// Populates the system with a complete, realistic sample tournament state:
    ///   • 4 teams with full 5-member rosters (20 team competitors)
    ///   • 6 individual competitors
    ///   • 5 events (mix of Team/Individual, all three categories)
    ///   • Placements that demonstrate tie-breaking and single-event entries
    ///
    /// Expected leaderboard highlights after seeding:
    ///   Individual: Jordan Blake &amp; Casey Rowe tied 1st (18 pts) — shows 1-2-2-4
    ///   Team:       Team Alpha &amp; Team Beta tied 1st (18 pts)     — shows 1-2-2-4
    ///   Single-event: Team Delta (Relay only, 4 pts), Niamh Quinn (2 events, 14 pts)
    /// </summary>
    private void SeedMockData()
    {
        ShowHeader("[DEBUG] SEED MOCK TOURNAMENT DATA");

        // Guard: if system is already at full capacity, seeding would all fail silently.
        if (_manager.Teams.Count == Team.MaxTeams &&
            _manager.Events.Count == TournamentEvent.MaxEvents)
        {
            PrintResult(false, "Mock data already loaded — system is at full capacity.");
            Pause();
            return;
        }

        Console.WriteLine("  This will instantly populate a complete sample tournament:\n");
        Console.WriteLine("    • 4 teams (5 members each = 20 team competitors)");
        Console.WriteLine("    • 6 individual competitors");
        Console.WriteLine("    • 5 events (Sporting / Academic / Problem-Solving)");
        Console.WriteLine("    • Placements including ties and single-event entries\n");
        Console.Write("  Proceed? (y/n): ");

        string? confirm = Console.ReadLine();
        if (!string.Equals(confirm?.Trim(), "y", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("\n  Seeding cancelled.");
            Pause();
            return;
        }

        Console.WriteLine();

        // ── Register Teams ────────────────────────────────────────────────────
        var (_, _, t1) = _manager.RegisterTeam("Team Alpha");
        var (_, _, t2) = _manager.RegisterTeam("Team Beta");
        var (_, _, t3) = _manager.RegisterTeam("Team Gamma");
        var (_, _, t4) = _manager.RegisterTeam("Team Delta");

        // ── Roster: Team Alpha ────────────────────────────────────────────────
        if (t1 is not null)
        {
            _manager.AddTeamMember(t1.Id, "Sarah Mitchell");
            _manager.AddTeamMember(t1.Id, "James Carter");
            _manager.AddTeamMember(t1.Id, "Emily Brooks");
            _manager.AddTeamMember(t1.Id, "Daniel Foster");
            _manager.AddTeamMember(t1.Id, "Laura Hughes");
        }

        // ── Roster: Team Beta ─────────────────────────────────────────────────
        if (t2 is not null)
        {
            _manager.AddTeamMember(t2.Id, "Ryan Davies");
            _manager.AddTeamMember(t2.Id, "Sophie Clarke");
            _manager.AddTeamMember(t2.Id, "Marcus Webb");
            _manager.AddTeamMember(t2.Id, "Chloe Turner");
            _manager.AddTeamMember(t2.Id, "Ethan Price");
        }

        // ── Roster: Team Gamma ────────────────────────────────────────────────
        if (t3 is not null)
        {
            _manager.AddTeamMember(t3.Id, "Hannah Moore");
            _manager.AddTeamMember(t3.Id, "Noah Baker");
            _manager.AddTeamMember(t3.Id, "Lily Evans");
            _manager.AddTeamMember(t3.Id, "Jack Harris");
            _manager.AddTeamMember(t3.Id, "Mia Stewart");
        }

        // ── Roster: Team Delta ────────────────────────────────────────────────
        if (t4 is not null)
        {
            _manager.AddTeamMember(t4.Id, "Oliver Lewis");
            _manager.AddTeamMember(t4.Id, "Amelia White");
            _manager.AddTeamMember(t4.Id, "Benjamin Scott");
            _manager.AddTeamMember(t4.Id, "Isabella Hall");
            _manager.AddTeamMember(t4.Id, "William Green");
        }

        // ── Individual Competitors ────────────────────────────────────────────
        var (_, _, c1) = _manager.RegisterIndividual("Jordan Blake");
        var (_, _, c2) = _manager.RegisterIndividual("Casey Rowe");
        var (_, _, c3) = _manager.RegisterIndividual("Morgan Shaw");
        var (_, _, c4) = _manager.RegisterIndividual("Alex Cole");
        var (_, _, c5) = _manager.RegisterIndividual("Reece Flynn");
        var (_, _, c6) = _manager.RegisterIndividual("Niamh Quinn");

        // ── Events ────────────────────────────────────────────────────────────
        var (_, _, e1) = _manager.AddEvent("100m Sprint",            EventType.Individual, "Sporting");
        var (_, _, e2) = _manager.AddEvent("Relay Race",             EventType.Team,       "Sporting");
        var (_, _, e3) = _manager.AddEvent("Maths Quiz",             EventType.Individual, "Academic");
        var (_, _, e4) = _manager.AddEvent("Bridge Building",        EventType.Team,       "Problem-Solving");
        var (_, _, e5) = _manager.AddEvent("General Knowledge Quiz", EventType.Individual, "Academic");

        // ── Team Placements ───────────────────────────────────────────────────
        // Relay Race: all 4 teams placed (Alpha 1st, Beta 2nd, Gamma 3rd, Delta 4th)
        if (t1 is not null && t2 is not null && t3 is not null && t4 is not null && e2 is not null)
        {
            _manager.RecordTeamPlacement(t1.Id, e2.Id, 1);
            _manager.RecordTeamPlacement(t2.Id, e2.Id, 2);
            _manager.RecordTeamPlacement(t3.Id, e2.Id, 3);
            _manager.RecordTeamPlacement(t4.Id, e2.Id, 4);
        }

        // Bridge Building: only 3 teams placed — Team Delta intentionally skipped
        // to demonstrate the "single-event entry" scenario on the team leaderboard.
        if (t1 is not null && t2 is not null && t3 is not null && e4 is not null)
        {
            _manager.RecordTeamPlacement(t2.Id, e4.Id, 1);   // Beta 1st → 18 total (ties Alpha)
            _manager.RecordTeamPlacement(t1.Id, e4.Id, 2);   // Alpha 2nd → 18 total
            _manager.RecordTeamPlacement(t3.Id, e4.Id, 3);   // Gamma 3rd → 12 total
            // Team Delta: Relay only → 4 total (single-event entry)
        }

        // ── Individual Placements ─────────────────────────────────────────────
        // 100m Sprint: 5 competitors placed; Niamh Quinn not entered (single-event example)
        if (c1 is not null && c2 is not null && c3 is not null &&
            c4 is not null && c5 is not null && e1 is not null)
        {
            _manager.RecordIndividualPlacement(c1.Id, e1.Id, 1);  // Jordan 1st  → +10
            _manager.RecordIndividualPlacement(c2.Id, e1.Id, 2);  // Casey  2nd  → +8
            _manager.RecordIndividualPlacement(c3.Id, e1.Id, 3);  // Morgan 3rd  → +6
            _manager.RecordIndividualPlacement(c4.Id, e1.Id, 4);  // Alex   4th  → +4
            _manager.RecordIndividualPlacement(c5.Id, e1.Id, 5);  // Reece  5th  → +2
        }

        // Maths Quiz: 4 competitors; Morgan and Reece not entered
        if (c1 is not null && c2 is not null && c4 is not null && c6 is not null && e3 is not null)
        {
            _manager.RecordIndividualPlacement(c2.Id, e3.Id, 1);  // Casey  1st  → +10
            _manager.RecordIndividualPlacement(c1.Id, e3.Id, 2);  // Jordan 2nd  → +8
            _manager.RecordIndividualPlacement(c6.Id, e3.Id, 3);  // Niamh  3rd  → +6
            _manager.RecordIndividualPlacement(c4.Id, e3.Id, 4);  // Alex   4th  → +4
        }

        // General Knowledge Quiz: 3 competitors; Jordan, Casey, Alex not entered
        if (c3 is not null && c5 is not null && c6 is not null && e5 is not null)
        {
            _manager.RecordIndividualPlacement(c3.Id, e5.Id, 1);  // Morgan 1st  → +10
            _manager.RecordIndividualPlacement(c6.Id, e5.Id, 2);  // Niamh  2nd  → +8
            _manager.RecordIndividualPlacement(c5.Id, e5.Id, 3);  // Reece  3rd  → +6
        }

        // ── Summary ───────────────────────────────────────────────────────────
        PrintResult(true, "Mock tournament data seeded successfully.");
        Console.WriteLine();
        Console.WriteLine("  ── EXPECTED LEADERBOARD HIGHLIGHTS ─────────────────");
        Console.WriteLine("  Individual totals (use Option 6 to verify):");
        Console.WriteLine("    18 pts — Jordan Blake  (Sprint 1st + Maths 2nd)");
        Console.WriteLine("    18 pts — Casey Rowe    (Sprint 2nd + Maths 1st)  ← 1-2-2-4 TIE");
        Console.WriteLine("    16 pts — Morgan Shaw   (Sprint 3rd + GK 1st)");
        Console.WriteLine("    14 pts — Niamh Quinn   (Maths 3rd + GK 2nd)");
        Console.WriteLine("     8 pts — Alex Cole     (Sprint 4th + Maths 4th)");
        Console.WriteLine("     8 pts — Reece Flynn   (Sprint 5th + GK 3rd)    ← 1-2-2-4 TIE");
        Console.WriteLine();
        Console.WriteLine("  Team totals (use Option 7 to verify):");
        Console.WriteLine("    18 pts — Team Alpha    (Relay 1st + Bridge 2nd)");
        Console.WriteLine("    18 pts — Team Beta     (Relay 2nd + Bridge 1st) ← 1-2-2-4 TIE");
        Console.WriteLine("    12 pts — Team Gamma    (Relay 3rd + Bridge 3rd)");
        Console.WriteLine("     4 pts — Team Delta    (Relay 4th only)         ← single-event");

        Pause();
    }

    // =========================================================================
    // PRIVATE — DISPLAY HELPERS
    // =========================================================================

    private void ShowMainMenu()
    {
        // Status bar: live counts from TournamentManager — updates every render.
        int teamCount  = _manager.Teams.Count;
        int indvCount  = _manager.Individuals.Count;
        int evtCount   = _manager.Events.Count;

        Console.WriteLine();
        Console.WriteLine("  ╔═══════════════════════════════════════════════╗");
        Console.WriteLine("  ║  STUDENT SKILLS CHALLENGE — SCORING SYSTEM  ║");
        Console.WriteLine("  ╚═══════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine(
            $"  Teams: {teamCount}/{Team.MaxTeams}   " +
            $"Individuals: {indvCount}/{Competitor.MaxIndividualCompetitors}   " +
            $"Events: {evtCount}/{TournamentEvent.MaxEvents}");
        Console.WriteLine();
        Console.WriteLine("  ─── REGISTRATION ────────────────────────────────");
        Console.WriteLine("    1.  Register a Team");
        Console.WriteLine("    2.  Add Member to a Team");
        Console.WriteLine("    3.  Register Individual Competitor");
        Console.WriteLine("    4.  Create Tournament Event");
        Console.WriteLine("  ─── SCORING ─────────────────────────────────────");
        Console.WriteLine("    5.  Record Event Placement");
        Console.WriteLine("  ─── LEADERBOARDS ────────────────────────────────");
        Console.WriteLine("    6.  View Individual Leaderboard");
        Console.WriteLine("    7.  View Team Leaderboard");
        Console.WriteLine("    8.  View Results for a Specific Event");
        Console.WriteLine("  ─── SYSTEM ──────────────────────────────────────");
        Console.WriteLine("    9.  [DEBUG] Seed Mock Tournament Data");
        Console.WriteLine("   10.  Exit Application");
        Console.WriteLine("  ─────────────────────────────────────────────────");
        Console.WriteLine();
    }

    /// <summary>Prints the team roster with ID, name, and completion status.</summary>
    private void ShowTeamList()
    {
        Console.WriteLine("  Registered Teams:\n");
        foreach (var t in _manager.Teams)
        {
            string status = t.IsComplete
                ? $"COMPLETE  ({t.TotalPoints} pts)"
                : $"{t.MemberCount}/{Team.MaxMembers} members";
            Console.WriteLine($"    [{t.Id}]  {t.Name,-20}  {status}");
        }
        Console.WriteLine();
    }

    /// <summary>Prints individual competitors with ID, name, and current point total.</summary>
    private void ShowIndividualList()
    {
        Console.WriteLine("  Individual Competitors:\n");
        foreach (var c in _manager.Individuals)
            Console.WriteLine($"    [{c.Id}]  {c.Name,-24}  {c.TotalPoints} pts  ({c.Placements.Count} events)");
        Console.WriteLine();
    }

    private static void ShowHeader(string title)
    {
        Console.Clear();
        Console.WriteLine();
        Console.WriteLine($"  ── {title} ──────────────────────────────");
        Console.WriteLine();
    }

    // =========================================================================
    // PRIVATE — INPUT / OUTPUT HELPERS
    // =========================================================================

    /// <summary>
    /// Safe integer reader — loops until a valid whole number within [min, max] is entered.
    ///
    /// Validation approach:
    ///   int.TryParse() is used instead of int.Parse() because TryParse returns false
    ///   for non-numeric and empty input rather than throwing a FormatException.
    ///   This eliminates any path to an unhandled exception from user input.
    ///
    ///   The range check (value >= min &amp;&amp; value &lt;= max) runs inside the same
    ///   if-condition — both guards must pass before the value is returned.
    /// </summary>
    private static int ReadInt(string prompt, int min = 1, int max = int.MaxValue)
    {
        while (true)
        {
            Console.Write(prompt);
            string? raw = Console.ReadLine();

            if (int.TryParse(raw, out int value) && value >= min && value <= max)
                return value;

            // Build a context-specific hint so the error message is always actionable.
            string rangeHint = max == int.MaxValue
                ? $"a number ≥ {min}"
                : $"a number between {min} and {max}";

            Console.WriteLine($"  [!] Invalid input. Please enter {rangeHint}.");
        }
    }

    /// <summary>
    /// Safe string reader — loops until the user enters a non-empty, non-whitespace string.
    /// Trims surrounding whitespace before returning.
    /// </summary>
    private static string ReadString(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            string? raw = Console.ReadLine();

            if (!string.IsNullOrWhiteSpace(raw))
                return raw.Trim();

            Console.WriteLine("  [!] Input cannot be empty. Please try again.");
        }
    }

    /// <summary>
    /// Displays a colour-coded result line: green ✓ for success, red ✗ for failure.
    /// Console colour is always reset after printing — cannot leak into subsequent output.
    /// </summary>
    private static void PrintResult(bool success, string message)
    {
        Console.WriteLine();
        Console.ForegroundColor = success ? ConsoleColor.Green : ConsoleColor.Red;
        Console.WriteLine($"  {(success ? "✓" : "✗")}  {message}");
        Console.ResetColor();
    }

    /// <summary>Blocks until any key is pressed; used after every data view so output isn't lost.</summary>
    private static void Pause()
    {
        Console.WriteLine();
        Console.Write("  Press any key to return to the main menu...");
        Console.ReadKey(intercept: true);
    }
}
