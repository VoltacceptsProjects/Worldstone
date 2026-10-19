using System.Diagnostics;

namespace Diablerie.Engine.Utility
{
    // Shared per-frame time budget for work that is spread over several frames (level streaming, tile loading).
    // Outside of a sliced run (Active == false) there is no limit, so synchronous callers just finish the work.
    public static class TimeSlice
    {
        private static readonly Stopwatch clock = new Stopwatch();
        private static double budgetMs;
        private static bool active;

        public static bool Active => active;

        public static void Begin(double milliseconds)
        {
            budgetMs = milliseconds;
            active = true;
            clock.Restart();
        }

        public static void End()
        {
            active = false;
            clock.Stop();
        }

        public static double RemainingMs => active ? budgetMs - clock.Elapsed.TotalMilliseconds : double.MaxValue;

        public static bool Exceeded => active && clock.Elapsed.TotalMilliseconds >= budgetMs;
    }
}
