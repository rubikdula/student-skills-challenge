using StudentSkillsChallenge.Services;
using StudentSkillsChallenge.UI;

namespace StudentSkillsChallenge;

// ─── Entry point ──────────────────────────────────────────────────────────────
// Compose the three service objects and hand control to the UI loop.
// All tournament data lives in TournamentManager; LeaderboardService reads it;
// ConsoleUI owns all console I/O and drives the interaction.
// ──────────────────────────────────────────────────────────────────────────────
class Program
{
    static void Main(string[] args)
    {
        var manager     = new TournamentManager();
        var leaderboard = new LeaderboardService(manager);
        var ui          = new ConsoleUI(manager, leaderboard);

        ui.Run();
    }
}
