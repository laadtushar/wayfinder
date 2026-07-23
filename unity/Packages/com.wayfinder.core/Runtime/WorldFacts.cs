using System;
using System.Collections.Generic;

namespace Wayfinder.Core
{
    /// Real physical facts about a world, for the bridge nav readout. Engine-free
    /// and committed as data (sourced in docs/data-sources.md), so the "command a
    /// starship" readout shows true numbers, not invented ones. Surface gravity is
    /// duplicated here from the WorldPackage's authoritative value for a
    /// self-contained, testable sheet; the two must agree (Moon 1.62, Mars 3.72).
    public readonly struct WorldFacts
    {
        public readonly string Body;          // "Mars - Jezero crater"
        public readonly float Gravity;        // m/s^2
        public readonly int MeanTempC;        // representative mean surface temp
        public readonly string TempNote;      // the swing / why
        public readonly float SolarDayHours;  // length of one solar day
        public readonly string Distance;      // e.g. "384,400 km from Earth"

        public WorldFacts(string body, float gravity, int meanTempC, string tempNote,
            float solarDayHours, string distance)
        {
            Body = body; Gravity = gravity; MeanTempC = meanTempC; TempNote = tempNote;
            SolarDayHours = solarDayHours; Distance = distance;
        }

        /// Surface gravity as a fraction of Earth's (9.807 m/s^2).
        public float GEarth => Gravity / 9.807f;

        /// A solar day rendered for humans: long lunar days in Earth-days,
        /// short Martian sols in hours + minutes.
        public string SolarDayText
        {
            get
            {
                if (SolarDayHours >= 48f)
                    return (SolarDayHours / 24f).ToString("0.0") + " Earth days";
                int h = (int)SolarDayHours;
                int m = (int)Math.Round((SolarDayHours - h) * 60f);
                return h + " h " + m + " m";
            }
        }

        /// The four stat rows for the readout panel (label: value).
        public string[] ReadoutLines() => new[]
        {
            "Gravity:   " + Gravity.ToString("0.00") + " m/s2  (" + GEarth.ToString("0.00") + " g)",
            "Mean temp: " + MeanTempC + " C  (" + TempNote + ")",
            "Solar day: " + SolarDayText,
            "Distance:  " + Distance,
        };
    }

    /// Lookup of real facts by world id. Data source of record; cite additions in
    /// docs/data-sources.md.
    public static class WorldFactSheet
    {
        static readonly Dictionary<string, WorldFacts> Table = new Dictionary<string, WorldFacts>
        {
            ["moon-tranquillity"] = new WorldFacts(
                "Earth's Moon - Sea of Tranquillity", 1.62f, -20,
                "+120 to -130 day-night", 708.7f, "384,400 km from Earth"),
            ["moon-shackleton"] = new WorldFacts(
                "Earth's Moon - south pole", 1.62f, -180,
                "polar shadow, near -230 C",
                708.7f, "384,400 km from Earth"),
            ["mars-jezero"] = new WorldFacts(
                "Mars - Jezero crater", 3.72f, -55,
                "-125 to +20 across a sol", 24.66f, "1.52 AU from the Sun"),
            ["mars-olympus"] = new WorldFacts(
                "Mars - Olympus Mons", 3.72f, -80,
                "22 km up, thin cold air", 24.66f, "1.52 AU from the Sun"),
            ["mars-valles"] = new WorldFacts(
                "Mars - Valles Marineris", 3.72f, -50,
                "warmer, deep canyon floor", 24.66f, "1.52 AU from the Sun"),
        };

        public static bool TryGet(string worldId, out WorldFacts facts)
            => Table.TryGetValue(worldId ?? "", out facts);

        /// True if we hold real facts for this world.
        public static bool Has(string worldId) => worldId != null && Table.ContainsKey(worldId);
    }
}
