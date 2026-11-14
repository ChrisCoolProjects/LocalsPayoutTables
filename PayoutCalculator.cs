using System.Text.RegularExpressions;
using DLLTesting.StartGGLibrary;

namespace DLLTesting
{
    internal static class PayoutCalculator
    {
        // The games we run every week: BBCF, UNI (UNI2/UNICLR/...), Xrd (GGXrdR2), Granblue (GBVSR), P4AU.
        // Matched as whole tags so e.g. "Universe" or "community" don't count as UNI.
        private static readonly Regex MainGameTag = new(
            @"\b(BBCF|UNI(\d|CLR|ST|EL)?\b|(GG)?Xrd|GBVS|Granblue|P4AU)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Events that mention a main game but aren't its bracket (e.g. 'QB51 UNI2 "2v2 Main Swap" Headcount').
        private static readonly string[] SideEventWords = { "headcount", "2v2", "side bracket" };

        /// <summary>True for a regular weekly bracket; false for dinner headcounts, side brackets and other extras.</summary>
        public static bool IsMainEvent(Event e) =>
            e.NumEntrants > 0
            && MainGameTag.IsMatch(e.Name)
            && !SideEventWords.Any(w => e.Name.Contains(w, StringComparison.OrdinalIgnoreCase));

        public static List<double> CalcPayout(int numAttendees)
        {
            double prizePool = numAttendees >= 11 ? 50 : 0;
            prizePool += (numAttendees * 5);
            List<double> payouts = numAttendees switch
            {
                < 5 => new List<double> { .75, .25 },                                    // Top 2 Payout
                >= 5 and < 20 => new List<double> { .50, .30, .20 },                     // Top 3 Payout
                >= 20 and < 30 => new List<double> { .50, .25, .15, .10 },               // Top 4 Payout
                >= 30 and < 40 => new List<double> { .45, .20, .15, .10, .05, .05 },     // Top 6 Payout
                >= 40 => new List<double> { .42, .19, .14, .09, .05, .05, .03, .03 },    // Top 8 Payout
            };
            return payouts.Select(x => x * prizePool).ToList();
        }

        public static string IntToPodiumName(int numToConvert)
        {
            return numToConvert switch{
               1 => "1st",
               2 => "2nd",
               3 => "3rd",
               _ => $"{numToConvert}th"
            };
        }
    }
}
