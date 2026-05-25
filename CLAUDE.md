# CLAUDE.md - Student Skills Challenge Scoring System

## Project Overview
A C# Console Application designed to manage, calculate, and rank scoring data for a college "Student Skills Challenge". This project serves as the practical component for BTEC Unit 4: Programming (Learning Aims B & C).

## Technical Stack & Constraints
- **Language:** C# (.NET 8.0 LTS)
- **Application Type:** Console Application
- **Project path:** `StudentSkillsChallenge/StudentSkillsChallenge.csproj`
- **Data Architecture Constraints:**
  - Maximum 4 teams, strictly 5 members per team (20 team competitors total).
  - Maximum 20 individual competitors.
  - Exactly 5 distinct events (Sporting, Academic, Problem-Solving).
  - Support for "Single-Event Entry" (participants who only compete in 1 event).

## BTEC Target Criteria Tracker
- [ ] 4/B.P4: Comprehensive program design (Data structures, OOP architecture)
- [ ] 4/B.M2: Justification of design decisions (Why specific collections/approaches were used)
- [ ] 4/C.P6: Working, robust implementation meeting all client needs
- [ ] 4/C.M3: Code optimization for efficiency and performance
- [ ] 4/BC.D2: Evaluation of final application against the client brief

---

## Core Architecture Decisions (Phase 1)

### Class Hierarchy
| Class | File | Purpose |
|---|---|---|
| `EventType` | `Models/EventType.cs` | Enum — compile-time-safe tag: `Team` or `Individual` |
| `Competitor` | `Models/Competitor.cs` | One person; may be standalone or a team member |
| `Team` | `Models/Team.cs` | Group of exactly 5 Competitors; holds team-event placements |
| `TournamentEvent` | `Models/TournamentEvent.cs` | One of the 5 events; typed by EventType |
| `Placement` | `Models/Placement.cs` | Junction record: participant + event + rank + points |

### Why These Classes?
- **`EventType` enum** — Using an enum (not a string/bool) gives compile-time exhaustiveness checking. A `switch` on EventType will warn if a new case is added but not handled.
- **`Competitor` vs `Team` as separate classes** — Team members and standalone individuals have different aggregation rules. A team member's placements do not appear on the individual leaderboard. Keeping them in one `Competitor` class with an `IsTeamMember` flag avoids code duplication while the flag cleanly partitions the two leaderboards.
- **`TournamentEvent` naming** — The identifier `Event` collides with C#'s `event` keyword concept; `TournamentEvent` is unambiguous.
- **`Placement` as a junction record** — Rather than storing rank integers directly on Competitor/Team, a Placement object links an entity to an event with a frozen point value. This makes the scoring model an append-only ledger and means Single-Event Entry is free: `TotalPoints = placements.Sum(p => p.PointsAwarded)` works correctly with 1 entry or 5.

## Data Structures & Storage Strategy

| Structure | Used For | Why This Choice |
|---|---|---|
| `List<Competitor>` | All competitors (team + individual) | Dynamic append, LINQ-filterable; max 20+20 entries is trivially small |
| `List<Team>` | Registered teams | Dynamic append; max 4 entries; cap enforced by `Team.MaxTeams` constant |
| `List<TournamentEvent>` | All events | Dynamic append; cap enforced by `TournamentEvent.MaxEvents = 5` |
| `List<Placement>` | Per-entity placement ledger | Append-only; LINQ Sum gives total; no index needed |
| `Dictionary<int,int>` | `Placement.PointsTable` rank→points | O(1) lookup; easy to adjust point values in one place |
| Constants (`const int`) | `MaxTeams`, `MaxMembers`, `MaxEvents`, `MaxIndividualCompetitors` | Single source of truth; compiler catches magic-number drift |

### Capacity enforcement pattern
Capacity limits live as `const int` fields on the class they govern, and are checked inside `internal` mutation methods (`AddMember`, `AddPlacement`, `RecordPlacement`). Public surfaces expose only `IReadOnlyList<T>` views, so external code can never bypass the guards.

### Encapsulation pattern
Backing collections are `private readonly List<T>` fields. Public properties expose `IReadOnlyList<T>` via `.AsReadOnly()`. All mutation methods are `internal` — accessible only within the assembly (i.e., from `TournamentManager` in Phase 2) and not from the UI layer.

## Agreed Points Distribution Matrix

| Rank | Points | Rationale |
|---|---|---|
| 1st | 10 | Clear winner reward |
| 2nd | 8 | Step of 2 — meaningful gap without making lower places worthless |
| 3rd | 6 | — |
| 4th | 4 | — |
| 5th | 2 | All placed competitors earn at least 2 pts (positive score, motivating) |
| 6th+ | 0 | Unplaced but still recorded |

**Key design property:** The even arithmetic sequence (−2 per rank) means every placement matters and no single event is pivotal enough to completely define the leaderboard. Single-event entries aggregate correctly because `TotalPoints` is a plain `Sum()` — there is no averaging, normalisation, or "did-not-enter" penalty.

---

## Feature Development Checklist
- [x] **Phase 1: Domain Models & Setup**
  - [x] Define core classes: `Competitor`, `Team`, `TournamentEvent`, `Placement`
  - [x] Implement robust validation properties (team size, event count, placement guards)
  - [x] `EventType` enum for compile-safe Team/Individual branching
  - [x] Points distribution table documented and justified
- [x] **Phase 2: Registration & System Management**
  - [x] `TournamentManager` service class (owns all lists, enforces global caps)
  - [x] Team and Individual registration flow
  - [x] Event definition (tagging events as Team or Individual)
  - [x] Placement recording with duplicate prevention and auto points
  - [x] Team + Individual leaderboard queries
- [ ] **Phase 3: Presentation Layer**
  - [ ] Main text-menu navigation loop (`ConsoleUI`)
  - [ ] Formatted scoreboard layouts
- [ ] **Phase 4: Presentation Layer**
  - [ ] Main text-menu navigation loop
  - [ ] Formatted scoreboard layouts

---

## Service Layer — TournamentManager (`Services/TournamentManager.cs`)

### Architecture
`TournamentManager` is the sole owner of all runtime data. UI layer calls its methods; it never exposes mutable collections. Result tuples `(bool Success, string Message, T? Data)` carry outcomes back to the UI without requiring try/catch at the menu level.

### Public Methods

| Method | Returns | Purpose |
|---|---|---|
| `RegisterTeam(name)` | `(bool, string, Team?)` | Create team; enforce ≤4 cap |
| `AddTeamMember(teamId, name)` | `(bool, string, Competitor?)` | Add member; enforce ≤5 per team |
| `RegisterIndividual(name)` | `(bool, string, Competitor?)` | Register standalone competitor; enforce ≤20 cap |
| `AddEvent(name, type, category)` | `(bool, string, TournamentEvent?)` | Define event; enforce ≤5 cap; block duplicate names |
| `RecordTeamPlacement(teamId, eventId, rank)` | `(bool, string, Placement?)` | Log team result; auto-calculate points; block duplicates |
| `RecordIndividualPlacement(competitorId, eventId, rank)` | `(bool, string, Placement?)` | Log individual result; auto-calculate points; block duplicates; block team members |
| `GetTeamLeaderboard()` | `IReadOnlyList<Team>` | Teams sorted by TotalPoints desc |
| `GetIndividualLeaderboard()` | `IReadOnlyList<Competitor>` | Standalone competitors sorted by TotalPoints desc |

### Read-Only Properties
`Teams`, `Competitors`, `Individuals`, `Events` — all `IReadOnlyList<T>` views.

### Single-Event Entry Strategy
No special code path. `TotalPoints` on both `Competitor` and `Team` is `placements.Sum(p => p.PointsAwarded)`. A list with 1 entry sums to that entry's points. A list with 0 entries sums to 0. Both appear on the leaderboard correctly without any null checks or "did-not-enter" logic.

### Validation Layers
1. **TournamentManager** — user-facing guards (cap exceeded, entity not found, wrong event type, duplicate placement). Returns `(false, message)`.
2. **Model internal methods** (`AddMember`, `AddPlacement`, `RecordPlacement`) — programming-error guards. Throw `InvalidOperationException`; should never be reached if layer 1 works correctly.

### Confirmed Design Decisions
| Decision | Rationale |
|---|---|
| Placement recording blocked for incomplete teams (< 5 members) | Brief mandates *exactly* 5 members per team. `Team.IsComplete` must be `true` before a team result is meaningful. Intentional — no change required. |
| Team members excluded from individual leaderboard | Team members score through their team's placements only. Prevents double-counting on both leaderboards. |
| Duplicate placement returns `(false, message)` not exception | User error, not programming error. UI layer handles the message without try/catch. |

---

## Development, Bug, & Optimization Log
| Ref # | Description of Issue / Defect | Fix / Optimization Applied |
|---|---|---|
| 001 | `Event` as a class name conflicts with C# keyword ecosystem | Renamed to `TournamentEvent` throughout |
| — | *(log issues here as testing progresses)* | — |
