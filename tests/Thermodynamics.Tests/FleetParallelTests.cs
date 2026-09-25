using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **Whether one grid per thread is safe today**, which is the half of
    /// backlog.md `D19` a test can settle without a session.
    ///
    /// <para>
    /// The mod is not threaded and the intent is that it should be. Two things have to be true
    /// before that is a change rather than a gamble: a fleet stepped one grid per thread has to
    /// produce exactly what the same fleet stepped in order produces, and it has to keep producing
    /// it — which means nothing in the core may hold mutable state that two grids could share.
    /// Both are asserted here, and the second is the one that would rot silently: a static field
    /// added for a cache is invisible until two threads meet in it.
    /// </para>
    ///
    /// <para>
    /// **Bit-identical, not close.** This is `D8`'s shape applied to a change that has not been
    /// made yet: the sequential run is the oracle, and a parallel run that merely agrees to a
    /// tolerance is a run with a race in it that happened not to lose this time.
    /// </para>
    /// </summary>
    public class FleetParallelTests
    {
        private const int Grids = 6;

        private const int NodesEach = 400;

        private const int Steps = 8;


        private static ThermalSettings Settings()
        {

            ThermalSettings settings = new ThermalSettings();
            settings.MaxElementVisitsPerStep = 0;
            return settings.Derive();
        }


        private static float[][] Run(bool parallel)
        {

            ThermalSettings settings = Settings();
            List<ThermalSimulation> fleet = FleetParallelLab.Fleet(Grids, NodesEach, settings);

            EnvironmentState state =
                EnvironmentSolver.Solve(settings, fleet[0].Planet, Worlds.Shadow());

            if (parallel)
            {
                FleetParallelLab.StepParallel(fleet, settings, state, Steps, Grids);
            }
            else
            {
                FleetParallelLab.StepSequential(fleet, settings, state, Steps);
            }

            float[][] result = new float[fleet.Count][];
            for (int i = 0; i < fleet.Count; i++) result[i] = SolverAb.Temperatures(fleet[i]);
            return result;
        }

        [Fact]

        public void SteppingAFleetInParallelIsIdenticalToSteppingItInOrder()
        {

            float[][] sequential = Run(false);

            float[][] parallel = Run(true);

            Assert.Equal(sequential.Length, parallel.Length);

            int judged = 0;
            for (int g = 0; g < sequential.Length; g++)
            {
                Assert.Equal(sequential[g].Length, parallel[g].Length);
                Assert.True(sequential[g].Length > NodesEach / 2,
                    "grid " + g + " built " + sequential[g].Length + " nodes");

                for (int i = 0; i < sequential[g].Length; i++)
                {
                    judged++;
                    if (sequential[g][i] == parallel[g][i]) continue;

                    Assert.Fail( "grid " + g + " node " + i + " is " + parallel[g][i]
                        + " in parallel against " + sequential[g][i] + " in order");
                }
            }

            Assert.True(judged > Grids * NodesEach / 2,

                "only " + judged + " nodes were compared, so this agreed about almost nothing (`E8`)");
        }

        [Fact]

        public void TheFleetTheComparisonRunsOnActuallyMoves()
        {

            ThermalSettings settings = Settings();
            List<ThermalSimulation> fleet = FleetParallelLab.Fleet(2, NodesEach, settings);

            EnvironmentState state =
                EnvironmentSolver.Solve(settings, fleet[0].Planet, Worlds.Shadow());

            float[] before = SolverAb.Temperatures(fleet[0]);
            FleetParallelLab.StepSequential(fleet, settings, state, Steps);
            float[] after = SolverAb.Temperatures(fleet[0]);

            int moved = 0;
            float low = float.MaxValue;
            float high = float.MinValue;
            for (int i = 0; i < after.Length; i++)
            {
                if (after[i] != before[i]) moved++;
                if (after[i] < low) low = after[i];
                if (after[i] > high) high = after[i];
            }

            Assert.True(moved > after.Length / 10,
                "only " + moved + " of " + after.Length + " nodes moved in " + Steps + " steps");
            Assert.True(high - low > 100f,
                "the fleet spans " + (high - low).ToString("n1") + " K, which is not a gradient");
        }

        [Fact]

        public void EveryPieceOfStaticStateInTheCoreSaysWhyTwoGridsMayShareIt()
        {

            Assembly core = typeof(ThermalSolver).Assembly;

            Dictionary<string, string> reasons = new Dictionary<string, string>
            {
                { "Thermodynamics.Core.BlockMaterials.Table", "read-only table" },
                { "Thermodynamics.Core.ReferenceMaterials.Table", "read-only table" },
                { "Thermodynamics.Core.GroundTemperature.Grounds", "read-only table" },
                { "Thermodynamics.Core.WeatherResponse.Weathers", "read-only table" },
                { "Thermodynamics.Core.PlanetThermalDerivation.LevelTemperatures", "read-only table" },
                { "Thermodynamics.Core.Incandescence.Locus", "read-only table" },
                { "Thermodynamics.Core.FidelityEnds.All", "read-only table" },
                { "Thermodynamics.Core.ThermalSolver+SubstepProfile.DemandEdges", "read-only table" },
                { "Thermodynamics.Core.ThermalSolver+SubstepProfile.ProjectedCaps", "read-only table" },

                { "Thermodynamics.Core.ThermalSolver+StepPhaseProfile.Names", "read-only table" },

                { "Thermodynamics.Core.CellBitset.EmptyPrefix", "empty array" },
                { "Thermodynamics.Core.RoomMap.EmptyCells", "empty array" },
                { "Thermodynamics.Core.RoomMap.EmptyRanges", "empty array" },

                { "Thermodynamics.Core.RoomMapper.MinusX", "geometry constant" },
                { "Thermodynamics.Core.RoomMapper.PlusX", "geometry constant" },
                { "Thermodynamics.Core.RoomMapper.LateralFaces", "geometry constant" },

                { "Thermodynamics.Core.Face.Offsets", "geometry constant" },
                { "Thermodynamics.Core.Face.Normals", "geometry constant" },
                { "Thermodynamics.Core.Face.NamesByIndex", "geometry constant" },
                { "Thermodynamics.Core.BoxGeometry.PositiveFace", "geometry constant" },
                { "Thermodynamics.Core.BoxGeometry.NegativeFace", "geometry constant" },
                { "Thermodynamics.Core.BlockSurfaceBuilder.FaceMountBounds", "geometry constant" },
                { "Thermodynamics.Core.SolarOcclusionSampler.Corners", "geometry constant" },

                { "Thermodynamics.Core.BlockMaterials.Steel", "value constant" },
                { "Thermodynamics.Core.GroundTemperature.Neutral", "value constant" },
                { "Thermodynamics.Core.ReferenceMaterials.MildSteel", "value constant" },
                { "Thermodynamics.Core.ReferenceMaterials.Aluminium", "value constant" },
                { "Thermodynamics.Core.ReferenceMaterials.Copper", "value constant" },
                { "Thermodynamics.Core.ReferenceMaterials.SodaLimeGlass", "value constant" },
                { "Thermodynamics.Core.WeatherResponse.Calm", "value constant" },

                { "Thermodynamics.Core.BlockOrientation.rotatedAxes", "table built once, never written after" },
                { "Thermodynamics.Core.BlockOrientation.rotatedFaces", "table built once, never written after" },
                { "Thermodynamics.Core.BlockOrientation.legal", "table built once, never written after" },

                { "Thermodynamics.Core.GridMath.KeyByFace", "table built once, never written after" },

                { "Thermodynamics.Core.ShapeNormal.OneCellOffsets", "table built once, never written after" },
                { "Thermodynamics.Core.ShapeNormal.OneCellUnits", "table built once, never written after" },

                { "Thermodynamics.Core.ThermalValidation.Lock", "the lock itself" },

                { "Thermodynamics.Core.RoomMap.AllExternal", "shared empty default, never written" },

                { "Thermodynamics.Core.ThermalValidation.Said", "mutable, off the stepping path" },
                { "Thermodynamics.Core.ThermalValidation.Found", "mutable, off the stepping path" },
                { "Thermodynamics.Core.ThermalValidation.Writer", "mutable, off the stepping path" },
            };


            List<string> unexplained = new List<string>();
            int judged = 0;
            int explained = 0;

            foreach (Type type in core.GetTypes())
            {
                if (type.FullName == null) continue;
                if (!type.FullName.StartsWith("Thermodynamics.", StringComparison.Ordinal)) continue;

                FieldInfo[] fields = type.GetFields(
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                for (int i = 0; i < fields.Length; i++)
                {
                    FieldInfo field = fields[i];

                    if (field.IsLiteral) continue;

                    if (field.IsInitOnly && (field.FieldType.IsPrimitive || field.FieldType.IsEnum))
                    {
                        continue;
                    }

                    judged++;

                    string name = type.FullName + "." + field.Name;
                    if (reasons.ContainsKey(name))
                    {
                        explained++;
                        continue;
                    }

                    unexplained.Add(name + " : " + field.FieldType.Name);
                }
            }

            Assert.True(judged > 15,

                "only " + judged + " static fields were looked at, so this judged nothing (`E8`)");
            Assert.True(explained > 10,
                "only " + explained + " of the listed fields were found, so the list has gone stale"
                + " and this check is guarding names that no longer exist");
            Assert.True(unexplained.Count == 0,
                "static state in the core with no reason written down. Two grids on two threads"
                + " would share it, so each one is a decision:\n  "
                + string.Join("\n  ", unexplained));
        }
    }
}
