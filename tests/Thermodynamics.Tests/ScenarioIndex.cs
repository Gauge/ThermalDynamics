using System;
using System.Collections.Generic;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Resolves a walk's scenario names against the battery, failing loudly on a name the
    /// battery does not carry.
    ///
    /// A runner that skips an unknown name silently is how a misspelled scenario thins a
    /// dataset without an error (`E8`), so the resolution asserts rather than filters. Three
    /// corpus walks carried this as near-copies differing only in the walk's own name, which is
    /// now the argument.
    /// </summary>
    internal static class ScenarioIndex
    {

        public static List<Battery.Scenario> Resolve(string[] names, string walk)
        {
            Dictionary<string, Battery.Scenario> byName =
                new Dictionary<string, Battery.Scenario>(StringComparer.Ordinal);
            foreach (Battery.Scenario scenario in Battery.All()) byName[scenario.Name] = scenario;


            List<Battery.Scenario> chosen = new List<Battery.Scenario>();
            foreach (string name in names)
            {
                Assert.True(byName.ContainsKey(name),
                    walk + " asks for scenario '" + name + "' and the battery has no such case");
                chosen.Add(byName[name]);
            }

            return chosen;
        }
    }
}
