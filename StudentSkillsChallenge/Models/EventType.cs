namespace StudentSkillsChallenge.Models;

/// <summary>
/// Classifies whether an event is contested by teams or individual competitors.
/// Using an enum (rather than a string or bool) gives us compile-time safety:
/// the compiler will reject any value that is not explicitly defined here,
/// and a switch statement can be exhaustively checked.
/// </summary>
public enum EventType
{
    /// <summary>
    /// All five members of a registered team compete together and share the placement.
    /// A Team event score is attributed to the team as a whole, not to any individual.
    /// </summary>
    Team,

    /// <summary>
    /// A single registered competitor competes under their own name.
    /// Individual event scores accumulate to the individual's personal leaderboard total.
    /// </summary>
    Individual
}
