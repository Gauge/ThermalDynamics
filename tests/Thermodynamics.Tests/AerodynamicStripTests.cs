using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **What the aerodynamics strip left behind: heating from the air, and no force.** `K25` step 0
    /// removed the drag and lift the mod applied and the top-speed port, so that the force model can
    /// be rebuilt from its own surface profile rather than from the friction watts (see
    /// aerodynamics.md, Strip and rebuild).
    ///
    /// <para>
    /// **The friction row is pinned against the code it replaced** (`D8`). The strip took the
    /// Newtonian `sin²θ` shape factor off the row, which leaves exactly what the old code computed
    /// with `EnableShapeDrag` off. The figures below were taken from that code on 2026-09-24,
    /// before any of it was removed, and the rigs are checked to have exercised the row: both hulls
    /// take friction and both warm. With the shape factor on, which was the shipped default, the
    /// same rigs read 100,800 W and 82,215 W, so the strip raises their friction heating 1.71× and
    /// 2.10×; bounding that heating is `AR16`'s job.
    /// </para>
    ///
    /// <para>
    /// The new profile adapter owns every physics write. Source reach checks supplement the
    /// independent wrench and lifecycle rigs; they do not execute the engine or prove native handling.
    /// </para>
    /// </summary>
    public class AerodynamicStripTests
    {
        private static ThermalSimulation Built(bool wedge)
        {
            ThermalSettings settings = new ThermalSettings();
            settings.Derive();

            GridBuilder builder = GridBuilder.Large();
            for (int y = 0; y < 4; y++)
            {
                for (int x = 0; x < 4; x++)
                {
                    for (int z = 0; z < (wedge ? 4 - y : 4); z++)
                    {
                        builder.Place(Catalog.HeavyArmor(), new Vector3I(x, y, z));
                    }
                }
            }

            ThermalSimulation simulation = builder.BuildSimulation(settings, 293.15f);
            simulation.Planet = PlanetThermalProperties.Default();
            return simulation;
        }


        private static ulong Hash(ThermalSimulation simulation)
        {
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < simulation.Solver.Nodes.Count; i++)
            {
                uint bits = BitConverter.ToUInt32(BitConverter.GetBytes(simulation.Solver.Nodes[i].Temperature), 0);
                hash = (hash ^ bits) * 1099511628211UL;
            }
            return hash;
        }

        [Theory]
        [InlineData(false, 172800.02f, 11208179775237087901UL)]
        [InlineData(true, 172800.02f, 8631283692489809559UL)]

        public void TheFrictionRowIsWhatTheOldCodeComputedWithTheShapeTermOff(
            bool wedge, float friction, ulong temperatures)
        {
            ThermalSimulation simulation = Built(wedge);
            simulation.StepExact(200, Worlds.Flight(1f, 120f));

            float peak = 0f;
            for (int i = 0; i < simulation.Solver.Nodes.Count; i++)
            {
                peak = Math.Max(peak, simulation.Solver.Nodes[i].Temperature);
            }

            Assert.True(peak > 294.15f, "the hull did not warm, so the row was not exercised: " + peak + " K");
            Assert.Equal(friction, simulation.FrictionWatts);
            Assert.Equal(temperatures, Hash(simulation));
        }

        [Fact]

        public void TheProfileAdapterOwnsEveryPhysicsWrite()
        {
            string root = Path.Combine(ShippedBlocks.RepoRoot(), "Thermodynamics");
            Regex writes = new Regex(@"\.(AddForce|ApplyImpulse|ApplyForce)\s*\(|LinearVelocity\s*=[^=]");

            int read = 0;
            List<string> found = new List<string>();
            foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string relative = path.Substring(root.Length).Replace('\\', '/') + "/";
                if (relative.Contains("/bin/") || relative.Contains("/obj/")) continue;
                if (relative.Contains("/RichHudFramework/")) continue;
                read++;

                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (writes.IsMatch(lines[i])) found.Add(relative.TrimEnd('/') + ":" + (i + 1));
                }
            }

            Assert.True(read > 100, "read only " + read + " mod source files, which is not the mod");
            Assert.Equal(2, found.Count);
            Assert.All(found, line => Assert.Contains("ThermalGridAerodynamics.cs", line));
        }

        [Fact]

        public void TheApiReportsTheForceAppliedByTheAdapter()
        {
            string api = File.ReadAllText(Path.Combine(ShippedBlocks.ModRoot(), "ThermalApi.cs"));

            Match body = Regex.Match(api,
                @"MyTuple<Vector3, Vector3> GetGridAeroForces\(IMyCubeGrid grid\)\s*\{(.*?)\n        \}",
                RegexOptions.Singleline);

            Assert.True(body.Success, "GetGridAeroForces is no longer declared the way this reads it");
            Assert.Contains("thermals.AeroForceWorld", body.Groups[1].Value);
            Assert.Contains("Vector3.Zero", body.Groups[1].Value);
        }
    }
}
