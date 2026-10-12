using Thermodynamics.Fixtures;
using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Harness
{
    public static class Battery
    {
        public class Scenario
        {
            public string Name;

            public Func<float, EnvironmentSample> Environment;

            public ShipLoad.State Load;

            public float Seconds = 1800f;

            public string Question;

            public ShipLoad.State Then;

            public Func<ShipAssembly, float> ThenAfterSeconds;
        }

        private const float ThickAir = 1f;
        private const float ThinAir = 0.4f;


        public static List<Scenario> All()
        {

            List<Scenario> scenarios = new List<Scenario>();


            scenarios.Add(new Scenario
            {
                Name = "vacuum-shadow",
                Question = "the cold reference: nothing in, nothing out but radiation",
                Environment = t => Worlds.Shadow(),
                Load = ShipLoad.State.Idle,
            });

            scenarios.Add(new Scenario
            {
                Name = "vacuum-sunlit",
                Question = "external heating at its worst: one face held to the sun",
                Environment = t => Worlds.Space(Vector3.Forward),
                Load = ShipLoad.State.Idle,
            });

            scenarios.Add(new Scenario
            {
                Name = "orbit-cycling",
                Question = "thermal inertia: does a ship average the day or chase it",
                Environment = t => ((int)(t / 900f) % 2) == 0 ? Worlds.Space(Vector3.Forward) : Worlds.Shadow(),
                Load = ShipLoad.State.Idle,
            });

            scenarios.Add(new Scenario
            {
                Name = "surface-hot-noon",
                Question = "external heating in air: hot ground, high sun, still",
                Environment = t => Worlds.PlanetSurface(ThickAir, 0.5f),
                Load = ShipLoad.State.Idle,
            });

            scenarios.Add(new Scenario
            {
                Name = "surface-cold-night",
                Question = "external cooling in air: does anything freeze below a usable floor",
                Environment = t => Worlds.PlanetSurface(ThinAir, 0f),
                Load = ShipLoad.State.Idle,
            });

            scenarios.Add(new Scenario
            {
                Name = "surface-windy",
                Question = "forced convection at rest: how much wind alone is worth",
                Environment = t => Worlds.PlanetSurface(ThickAir, 0.5f, 60f),
                Load = ShipLoad.State.Idle,
            });

            scenarios.Add(new Scenario
            {
                Name = "underground",
                Question = "buried: no sun, no wind, rock ambient",
                Environment = t => Worlds.Underground(ThickAir),
                Load = ShipLoad.State.Idle,
            });


            scenarios.Add(new Scenario
            {
                Name = "idle",
                Question = "G1: does a parked ship survive doing nothing",
                Environment = t => Worlds.Shadow(),
                Load = ShipLoad.State.Idle,
            });

            scenarios.Add(new Scenario
            {
                Name = "full-electrical",
                Question = "G2: every consumer at rating, reactors supplying what they ask",
                Environment = t => Worlds.Shadow(),
                Load = ShipLoad.State.Full,
            });

            scenarios.Add(new Scenario
            {
                Name = "jump-charge",
                Question = "G8: the ship charges its drives, finishes, and holds — the real event",
                Environment = t => Worlds.Shadow(),
                Load = ShipLoad.State.Full,
                Then = ShipLoad.State.Charged,
                ThenAfterSeconds = ShipLoad.ChargeSeconds,
            });

            scenarios.Add(new Scenario
            {
                Name = "full-electrical-charged",
                Question = "the same load with the jump drives charged rather than charging",
                Environment = t => Worlds.Shadow(),
                Load = ShipLoad.State.Charged,
            });

            scenarios.Add(new Scenario
            {
                Name = "all-peak",
                Question = "where heat concentrates when everything runs at once",
                Environment = t => Worlds.Shadow(),
                Load = ShipLoad.State.Everything,
            });

            for (int face = 0; face < Face.Count; face++)
            {
                int direction = face;
                scenarios.Add(new Scenario
                {
                    Name = "burn-" + ShipLoad.DirectionName(direction),
                    Question = "hot spots with thrusters firing " + ShipLoad.DirectionName(direction),
                    Environment = t => Worlds.Shadow(),
                    Load = ShipLoad.State.Burn(direction),
                });
            }


            float[] speeds = { 50f, 100f, 300f };
            foreach (float speed in speeds)
            {
                float knots = speed;
                scenarios.Add(new Scenario
                {
                    Name = "flight-" + (int)speed,
                    Question = "friction against airflow at " + (int)speed + " m/s: which wins",
                    Environment = t => Worlds.Flight(ThickAir, knots),
                    Load = ShipLoad.State.Burn(Face.Forward),
                    Seconds = 3600f,
                });
            }


            scenarios.Add(new Scenario
            {
                Name = "storm-300",
                Question = "wind alone at a raised speed limit: a parked hull in a 300 m/s gale",
                Environment = t => Worlds.Storm(ThickAir, 300f),
                Load = ShipLoad.State.Idle,
                Seconds = 3600f,
            });

            scenarios.Add(new Scenario
            {
                Name = "storm-parked",
                Question = "wind alone: a parked hull in a 100 m/s gale heats exactly as flight-100 does",
                Environment = t => Worlds.Storm(ThickAir, 100f),
                Load = ShipLoad.State.Idle,
                Seconds = 3600f,
            });

            scenarios.Add(new Scenario
            {
                Name = "flight-headwind",
                Question = "composition: 40 of wind against 60 of speed trips a threshold neither reaches alone",
                Environment = t => Worlds.WindAndMotion(ThickAir, 40f, Vector3.Forward, Vector3.Backward * 60f),
                Load = ShipLoad.State.Burn(Face.Forward),
                Seconds = 3600f,
            });

            scenarios.Add(new Scenario
            {
                Name = "flight-downwind",
                Question = "composition: 80 of speed in a 60 m/s tailwind is 20 of airflow — no friction at full throttle",
                Environment = t => Worlds.WindAndMotion(ThickAir, 60f, Vector3.Forward, Vector3.Forward * 80f),
                Load = ShipLoad.State.Burn(Face.Forward),
                Seconds = 3600f,
            });

            scenarios.Add(new Scenario
            {
                Name = "reentry",
                Question = "the leading face at terminal speed in thick air",
                Environment = t => Worlds.Flight(ThickAir, 200f),
                Load = ShipLoad.State.Idle,
                Seconds = 1200f,
            });


            scenarios.Add(new Scenario
            {
                Name = "recovery",
                Question = "G5: from a full burn, throttled to idle, does it come back",
                Environment = t => Worlds.Shadow(),
                Load = ShipLoad.State.Everything,
                Seconds = 1200f,
            });

            return scenarios;
        }


        public static ScenarioOutcome Run(Blueprints.Ship ship, Scenario scenario,
            ThermalSettings settings = null)
        {

            return Run(ship, scenario, settings, -1f);
        }


        public static ScenarioOutcome RunForSeconds(Blueprints.Ship ship, Scenario scenario,
            float seconds, ThermalSettings settings = null)
        {

            return Run(ship, scenario, settings, seconds);
        }


        private static ScenarioOutcome Run(Blueprints.Ship ship, Scenario scenario,
            ThermalSettings settings, float fixedSeconds)
        {
            ShipAssembly assembly = ship.Build(settings ?? new HistoricalThermalSettings());
            assembly.CollectDiagnostics(true);

            ShipLoad.Apply(assembly, scenario.Load);


            AssemblyRunner runner = new AssemblyRunner(assembly);
            runner.Environment = scenario.Environment;
            runner.Integrity = GameBlocks.IntegrityOf;

            if (scenario.Name == "recovery")
            {
                float half = fixedSeconds > 0f ? fixedSeconds * 0.5f : scenario.Seconds;
                Advance(runner, half, fixedSeconds);
                ShipLoad.Apply(assembly, ShipLoad.State.Idle);
                Advance(runner, fixedSeconds > 0f ? fixedSeconds - half : scenario.Seconds,
                    fixedSeconds);
            }

            else if (scenario.Then != null)
            {
                float first = scenario.ThenAfterSeconds == null
                    ? scenario.Seconds
                    : scenario.ThenAfterSeconds(assembly);

                float ceiling = fixedSeconds > 0f ? fixedSeconds : scenario.Seconds;
                if (first > ceiling) first = ceiling;
                if (first > 0f) runner.Run(first);

                ShipLoad.Apply(assembly, scenario.Then);
                Advance(runner, ceiling - first, fixedSeconds);
            }
            else
            {
                Advance(runner, fixedSeconds > 0f ? fixedSeconds : scenario.Seconds, fixedSeconds);
            }

            ScenarioOutcome outcome = ScenarioOutcome.Read(assembly, ship.Name, scenario.Name);
            outcome.WorkshopId = ship.WorkshopId;
            outcome.SecondsToCritical = runner.SecondsToCritical;
            outcome.SecondsToFirstLoss = runner.SecondsToFirstLoss;
            outcome.RunSeconds = runner.ElapsedSeconds;

            Settle(outcome, runner);
            return outcome;
        }

        public const float SettleWithin = 0.25f;

        public const float Chunk = 60f;


        private static void Advance(AssemblyRunner runner, float seconds, float fixedSeconds)
        {
            if (seconds <= 0f) return;

            if (fixedSeconds > 0f)
            {
                float done = 0f;
                while (done < seconds)
                {
                    float chunk = Math.Min(Chunk, seconds - done);
                    runner.Run(chunk);
                    done += chunk;
                }

                return;
            }

            RunUntilSettled(runner, seconds);
        }


        private static void RunUntilSettled(AssemblyRunner runner, float ceiling)
        {
            float previous = float.NaN;
            float lastStep = float.NaN;
            float elapsed = 0f;

            while (elapsed < ceiling)
            {
                float chunk = Math.Min(Chunk, ceiling - elapsed);
                runner.Run(chunk);
                elapsed += chunk;

                float hottest = runner.Hottest[runner.Hottest.Count - 1];
                if (!float.IsNaN(previous))
                {
                    float step = hottest - previous;
                    if (Math.Abs(step) <= SettleWithin) return;
                    if (Extrapolates && Converged(step, lastStep)) return;
                    lastStep = step;
                }

                previous = hottest;
            }
        }


        public static bool Converged(float step, float lastStep)
        {
            if (float.IsNaN(lastStep) || lastStep == 0f || step == 0f) return false;

            if ((step > 0f) != (lastStep > 0f)) return false;

            float ratio = Math.Abs(step) / Math.Abs(lastStep);
            if (ratio >= ConvergedRatio) return false;

            float remaining = Math.Abs(step) * ratio / (1f - ratio);
            return remaining <= SettleWithin;
        }

        public const float ConvergedRatio = 0.9f;

        public static readonly bool Extrapolates =
            (System.Environment.GetEnvironmentVariable("THERMAL_SETTLE_EXTRAPOLATE") ?? "") == "1";


        public const float SettledWithinOfFinal = 5f;


        public static float SettleSeconds(IList<float> samples, float final)
        {
            if (samples == null || samples.Count < 2) return -1f;

            for (int i = 1; i < samples.Count; i++)
            {
                if (Math.Abs(samples[i] - final) <= SettledWithinOfFinal) return (i + 1) * Chunk;
            }

            return -1f;
        }


        private static void Settle(ScenarioOutcome outcome, AssemblyRunner runner)
        {
            List<float> samples = runner.Hottest;
            if (samples.Count < 2) return;

            float fastest = 0f;

            for (int i = 1; i < samples.Count; i++)
            {
                float rate = Math.Abs(samples[i] - samples[i - 1]) / Chunk;
                if (rate > fastest) fastest = rate;
            }


            outcome.SecondsToSettle = SettleSeconds(samples, outcome.PeakKelvin);
            outcome.PeakRateKelvinPerSecond = fastest;

            BulkDrift(outcome, runner);
        }

        public const int BulkWindow = 3;


        private static void BulkDrift(ScenarioOutcome outcome, AssemblyRunner runner)
        {
            List<float> samples = runner.Bulk;
            if (samples.Count < BulkWindow + 1) return;

            float first = samples[samples.Count - 1 - BulkWindow];
            float last = samples[samples.Count - 1];

            outcome.BulkDriftKelvinPerSecond = (last - first) / (BulkWindow * Chunk);
            outcome.BulkDriftWatts = outcome.ThermalMass * outcome.BulkDriftKelvinPerSecond;
        }
    }
}
