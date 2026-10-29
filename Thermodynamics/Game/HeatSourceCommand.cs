using System;
using System.Globalization;
using Thermodynamics.Core;

namespace Thermodynamics
{
    public static class HeatSourceCommand
    {
        public enum Verb
        {
            Help,
            Place,
            Pulse,
            Set,
            Remove,
            List,
            Clear,
        }

        public struct Parsed
        {
            public Verb Verb;
            public float Watts;
            public float Seconds;
            public float Range;
            public int Id;

            public string Error;
        }

        public const float DefaultRange = 200f;

        public static Parsed Parse(string argument)
        {
            Parsed parsed = new Parsed { Range = DefaultRange };

            string[] parts = (argument ?? "").Trim()
                .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0 || parts[0] == "help")
            {
                parsed.Verb = Verb.Help;
                return parsed;
            }

            switch (parts[0])
            {
                case "list": parsed.Verb = Verb.List; return parsed;
                case "clear": parsed.Verb = Verb.Clear; return parsed;

                case "remove":
                    parsed.Verb = Verb.Remove;
                    if (parts.Length < 2 || !TryId(parts[1], out parsed.Id))
                    {
                        return Fail("usage: /thermal heat remove <id>");
                    }
                    return parsed;

                case "set":
                    parsed.Verb = Verb.Set;
                    if (parts.Length < 3 || !TryId(parts[1], out parsed.Id)
                        || !TryWatts(parts[2], out parsed.Watts))
                    {
                        return Fail("usage: /thermal heat set <id> <watts>");
                    }
                    return parsed;

                case "pulse":
                    parsed.Verb = Verb.Pulse;
                    if (parts.Length < 3 || !TryWatts(parts[1], out parsed.Watts)
                        || !TryWatts(parts[2], out parsed.Seconds))
                    {
                        return Fail("usage: /thermal heat pulse <watts> <seconds> [range]");
                    }
                    if (parsed.Seconds <= 0f) return Fail("a pulse needs a positive duration");
                    if (parsed.Watts <= 0f) return Fail("watts must be above zero");
                    if (parts.Length > 3) parsed.Range = RangeOr(parts[3], DefaultRange);
                    return parsed;
            }

            parsed.Verb = Verb.Place;
            if (!TryWatts(parts[0], out parsed.Watts)) return Fail("unrecognised: " + parts[0]);
            if (parsed.Watts <= 0f) return Fail("watts must be above zero");
            if (parts.Length > 1) parsed.Range = RangeOr(parts[1], DefaultRange);
            return parsed;
        }

        private static Parsed Fail(string message)
        {
            return new Parsed { Verb = Verb.Help, Range = DefaultRange, Error = message };
        }

        public static bool TryWatts(string text, out float watts)
        {
            watts = 0f;
            if (string.IsNullOrEmpty(text)) return false;

            float scale = 1f;
            char last = text[text.Length - 1];

            if (last == 'k' || last == 'K') scale = 1e3f;
            else if (last == 'm' || last == 'M') scale = 1e6f;
            else if (last == 'g' || last == 'G') scale = 1e9f;

            string number = scale > 1f ? text.Substring(0, text.Length - 1) : text;
            if (number.Length == 0) return false;

            float value;
            if (!float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                return false;
            }

            watts = value * scale;
            if (float.IsNaN(watts) || float.IsInfinity(watts))
            {
                watts = 0f;
                return false;
            }
            return true;
        }

        private static bool TryId(string text, out int id)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out id);
        }

        private static float RangeOr(string text, float fallback)
        {
            float range;
            return TryWatts(text, out range) && range > 0f ? range : fallback;
        }

        public static string Describe(float watts)
        {
            return Units.Watts(watts, 2, CultureInfo.InvariantCulture);
        }

        public static string Help()
        {
            return "heat commands:\n"
                + "  /thermal heat <watts> [range]           place a steady source ahead of you\n"
                + "  /thermal heat pulse <watts> <s> [range] place one that expires\n"
                + "  /thermal heat set <id> <watts>          dial one up or down\n"
                + "  /thermal heat remove <id>               remove one\n"
                + "  /thermal heat list                      what is registered\n"
                + "  /thermal heat clear                     remove everything\n"
                + "  watts accept k, M and G suffixes: 5M is five megawatts";
        }
    }
}
