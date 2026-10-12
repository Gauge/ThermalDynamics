using Thermodynamics.Fixtures;
using System;
using System.Collections.Generic;
using System.Text;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Harness
{
    public static class ClientInputLab
    {
        public class Degradation
        {
            public string Name = "none";

            public string[] Scenarios;

            public string Because = "";

            public float StaleSeconds;

            public float HitchEverySeconds;

            public float HitchLosesSeconds = 5f;

            public float EnvironmentLagSeconds;

            public float SunAngleDegrees;

            public float PowerLagSeconds;

            public float PowerErrorShare;

            public float AirDensityError;

            public float PositionLagSeconds;

            public bool MissesWeather;

            public float ThrustErrorShare;

            public float SpeedErrorShare;

            public float OcclusionWrongEverySeconds;

            public float OcclusionWrongForSeconds = 3f;

            public float MassErrorShare;

            public float BlocksOffShare;

            public float RoomPressureError;

            public float RoomMapLagSeconds;

            public int BuildOrderSeed;

            public float BlocksMissingShare;

            public float BlocksMissingSeconds;

            public float SimSpeedError;

            public float MissingHeatSourceIrradiance;

            public bool OnDefaultSettings;
        }

        public class Result
        {
            public string Name;
            public string Because;

            public ClientDriftLab.Correction Protocol = ClientDriftLab.Correction.None;

            public int Blocks;
            public string Scenario;

            public bool NotExercised;

            public float PeakKelvin;

            public float StandingKelvin;

            public float SecondsMisreading;

            public int PeakDisagreeing;

            public float BytesPerSecond;

            public int PeakBlocksSent;

            public int PeakMissing;

            public int Rooms;

            public int PeakServerCritical;
        }

        public const float SampleSeconds = 5f;

        public const int SmallestHullWithACompartment = 600;

        public const float LoadPeriodSeconds = 120f;

        public const float LoadMultiplier = 3f;

        public const float LoadedWatts = Census.ProducerWatts * LoadMultiplier;

        public const float IdleWatts = Census.ProducerWatts * LoadMultiplier * 0.1f;

        public const float ThrustWatts = LoadedWatts;


        public static Result Measure(Degradation degradation, ClientDriftLab.Correction protocol,
            string scenario = "planet", float seconds = 600f, int blocks = 2000,
            float loadPeriodSeconds = LoadPeriodSeconds)
        {

            Degradation how = degradation ?? new Degradation();
            ClientDriftLab.Correction fix = protocol ?? ClientDriftLab.Correction.None;

            seconds = LabClock.Seconds(seconds);
            if (loadPeriodSeconds < 1e8f) loadPeriodSeconds = LabClock.Seconds(loadPeriodSeconds);


            ThermalSettings world = new HistoricalThermalSettings().Derive();


            ThermalSettings served = new HistoricalThermalSettings();
            served.HeatTimeScale = world.HeatTimeScale * 0.5f;
            served = served.Derive();

            ThermalSettings serverWorld = how.OnDefaultSettings ? served : world;
            ThermalSettings clientWorld = how.OnDefaultSettings ? world : serverWorld;

            ThermalSimulation server = Hulls.DrivenPastCritical(serverWorld, blocks);
            ThermalSimulation client = Hulls.DrivenPastCritical(clientWorld, blocks, how.BuildOrderSeed);

            Align(client, server);

            Reweigh(client, how.MassErrorShare);

            Result result = new Result
            {
                Name = how.Name,
                Because = how.Because,
                Protocol = fix,
                Blocks = server.Solver.Nodes.Count,
                Scenario = scenario,
                NotExercised = how.Scenarios != null && Array.IndexOf(how.Scenarios, scenario) < 0,
            };


            result.Rooms = Pressurise(server, 1f);

            bool asksAboutRooms = how.RoomPressureError > 0f || how.RoomMapLagSeconds > 0f;
            if (asksAboutRooms && result.Rooms <= 0)
            {
                throw new InvalidOperationException(
                    "'" + how.Name + "' degrades a room on a " + blocks + "-block hull that has"
                    + " none, so it would measure nothing; ask for at least "
                    + SmallestHullWithACompartment);
            }

            float clientPressure = 1f - how.RoomPressureError;
            Pressurise(client, clientPressure);

            float warm = 120f;
            Drive(server, LoadedWatts);
            Drive(client, LoadedWatts);

            bool flying = scenario == "burn";
            if (flying)
            {
                Census.DriveThrust(server, ThrustWatts);
                Census.DriveThrust(client, ThrustWatts * (1f + how.ThrustErrorShare));
            }

            Silence(client, how.BlocksOffShare);

            Advance(server, Sample(scenario, 0f, how, false), warm);
            Advance(client, Sample(scenario, 0f, how, true), warm);

            if (how.StaleSeconds > 0f)
            {
                float[] stale = GridState.Temperatures(client);
                Advance(server, Sample(scenario, 0f, how, false), how.StaleSeconds);
                GridState.Restore(client, stale);
            }


            List<BlockInstance> withheld = Withhold(client, how.BlocksMissingShare);
            bool blocksPending = withheld.Count > 0;
            if (blocksPending) Pressurise(client, clientPressure);

            bool mapPending = how.RoomMapLagSeconds > 0f;
            if (mapPending) Unmap(client);


            List<StoredTemperature> selection = new List<StoredTemperature>();

            List<StoredTemperature> received = new List<StoredTemperature>();

            List<float> disagreements = new List<float>();

            float tick = fix.IntervalSeconds > 0f
                ? Math.Min(SampleSeconds, fix.IntervalSeconds) : SampleSeconds;
            if (tick < serverWorld.StepSeconds) tick = serverWorld.StepSeconds;

            float elapsed = 0f;
            float sinceUpdate = float.MaxValue;
            float sinceSample = 0f;
            float sinceHitch = 0f;
            float owed = 0f;
            float clientOwed = 0f;

            float servedWatts = float.NaN;
            float clientWatts = float.NaN;

            while (elapsed < seconds)
            {
                float now = elapsed;


                servedWatts = Retune(server, servedWatts, Watts(now, loadPeriodSeconds));
                float wasClientWatts = clientWatts;

                clientWatts = Retune(client, clientWatts,
                    Watts(now - how.PowerLagSeconds, loadPeriodSeconds) * (1f + how.PowerErrorShare));

                if (clientWatts != wasClientWatts) Silence(client, how.BlocksOffShare);

                Advance(server, Sample(scenario, now, how, false), tick);

                if (owed > 0f)
                {
                    owed -= tick;
                }
                else
                {
                    clientOwed += tick * (1f + how.SimSpeedError);

                    clientOwed -= Advance(client,
                        Sample(scenario, now - how.EnvironmentLagSeconds, how, true), clientOwed);
                }

                elapsed += tick;

                if (mapPending && elapsed >= how.RoomMapLagSeconds)
                {
                    mapPending = false;
                    Remap(client, clientPressure);
                }

                if (blocksPending && how.BlocksMissingSeconds > 0f
                    && elapsed >= how.BlocksMissingSeconds)
                {
                    blocksPending = false;
                    Deliver(client, withheld);

                    if (mapPending) Unmap(client); else Pressurise(client, clientPressure);

                    clientWatts = float.NaN;
                }

                sinceUpdate += tick;
                sinceSample += tick;
                sinceHitch += tick;

                if (how.HitchEverySeconds > 0f && sinceHitch >= how.HitchEverySeconds)
                {
                    sinceHitch = 0f;
                    owed += how.HitchLosesSeconds;
                }

                if (sinceSample >= SampleSeconds)
                {
                    float worst;
                    int over;
                    int absent;

                    int disagreeing = Compare(server, client, out worst, out over, out absent);

                    if (over > result.PeakServerCritical) result.PeakServerCritical = over;
                    if (absent > result.PeakMissing) result.PeakMissing = absent;
                    disagreements.Add(worst);
                    if (worst > result.PeakKelvin) result.PeakKelvin = worst;
                    if (disagreeing > result.PeakDisagreeing) result.PeakDisagreeing = disagreeing;
                    if (disagreeing > 0) result.SecondsMisreading += sinceSample;

                    sinceSample = 0f;
                }

                if (fix.IntervalSeconds > 0f && sinceUpdate >= fix.IntervalSeconds)
                {
                    sinceUpdate = 0f;

                    int inBand = server.ExportHotTail(fix.BandKelvin, fix.MaxBlocks, selection);
                    byte[] packet = HotTailCodec.Encode(selection);

                    if (HotTailCodec.TryDecode(packet, received))
                    {
                        client.ImportHotTail(received);
                        result.BytesPerSecond += packet.Length;
                        if (selection.Count > result.PeakBlocksSent) result.PeakBlocksSent = selection.Count;
                    }

                    if (inBand < 0) return result;
                }
            }

            result.BytesPerSecond = elapsed > 0f ? result.BytesPerSecond / elapsed : 0f;

            result.StandingKelvin = FinalThird(disagreements);
            return result;
        }


        private static float FinalThird(IList<float> series)
        {
            if (series == null || series.Count == 0) return 0f;

            int from = series.Count - Math.Max(1, series.Count / 3);
            double total = 0d;
            for (int i = from; i < series.Count; i++) total += series[i];

            return (float)(total / (series.Count - from));
        }


        public static float Watts(float seconds)
        {

            return Watts(seconds, LoadPeriodSeconds);
        }


        public static float Watts(float seconds, float periodSeconds)
        {
            if (seconds < 0f) seconds = 0f;
            if (periodSeconds <= 0f) return LoadedWatts;

            int half = (int)(seconds / periodSeconds);
            return half % 2 == 0 ? LoadedWatts : IdleWatts;
        }


        private static float Retune(ThermalSimulation simulation, float current, float wanted)
        {
            if (current == wanted) return current;
            Census.DriveCensus(simulation, wanted);
            return wanted;
        }


        private static EnvironmentSample Sample(string scenario, float seconds, Degradation how,
            bool onClient)
        {

            EnvironmentSample sample = Built(scenario, seconds, how, onClient);

            if (!onClient && how.MissingHeatSourceIrradiance > 0f)
            {
                sample.HeatSources = new[]
                {

                    new HeatSourceState(new Vector3(0f, -1f, 0f), how.MissingHeatSourceIrradiance),
                };
                sample.HeatSourceCount = 1;
            }

            return sample;
        }


        private static EnvironmentSample Built(string scenario, float seconds, Degradation how,
            bool onClient)
        {
            if (seconds < 0f) seconds = 0f;

            if (scenario == "shadow") return Worlds.Shadow();

            if (scenario == "burn")
            {
                float speed = FlyingSpeed;
                if (onClient) speed *= 1f + how.SpeedErrorShare;
                if (speed < 0f) speed = 0f;

                return Worlds.Flight(1f, speed);
            }

            if (scenario == "sunlit")
            {
                float angle = seconds * (float)(Math.PI / 600d);
                if (onClient) angle += Degrees(how.SunAngleDegrees);

                EnvironmentSample sunlit = Worlds.Space(new Vector3(
                    (float)Math.Cos(angle), (float)Math.Sin(angle), 0.2f));

                if (onClient && Disagreeing(how, seconds))
                {
                    sunlit.IsSolarOccluded = true;
                    sunlit.SolarOcclusion = 1f;
                }

                return sunlit;
            }

            float dayLength = 1200f;
            float timeOfDay = 0.25f + (seconds / dayLength);
            if (onClient) timeOfDay += Degrees(how.SunAngleDegrees) / (float)(2d * Math.PI);

            float air = 1f;
            if (onClient) air = Math.Max(0f, Math.Min(1f, air - how.AirDensityError));

            if (scenario == "descent")
            {
                float at = seconds;
                if (onClient) at -= how.PositionLagSeconds;

                EnvironmentSample descending = Worlds.PlanetSurface(air, timeOfDay);
                Descend(ref descending, at);
                Weather(ref descending, how, onClient);
                return descending;
            }

            EnvironmentSample surface = Worlds.PlanetSurface(air, timeOfDay);
            Weather(ref surface, how, onClient);
            return surface;
        }

        public const float DescentFromMetres = 8000f;
        public const float DescentMetresPerSecond = 10f;


        private static void Descend(ref EnvironmentSample sample, float seconds)
        {
            if (seconds < 0f) seconds = 0f;

            float altitude = DescentFromMetres - (seconds * DescentMetresPerSecond);
            if (altitude < 0f) altitude = 0f;

            sample.Altitude = altitude;
            sample.Radius = Worlds.EarthlikeRadius + altitude;

            float thinning = (float)Math.Exp(-altitude / 5500d);
            sample.AirDensity *= thinning;
        }


        private static void Weather(ref EnvironmentSample sample, Degradation how, bool onClient)
        {
            if (!how.MissesWeather || onClient) return;

            sample.Weather = WeatherResponse.For("Snow");
            sample.WeatherIntensity = 1f;
        }

        public const float HeatSourceIrradiance = 100f;

        public const float FlyingSpeed = 100f;


        private static bool Disagreeing(Degradation how, float seconds)
        {
            if (how.OcclusionWrongEverySeconds <= 0f) return false;
            if (how.OcclusionWrongForSeconds <= 0f) return false;
            if (seconds < 0f) seconds = 0f;

            float into = seconds % how.OcclusionWrongEverySeconds;
            return into < how.OcclusionWrongForSeconds;
        }


        private static float Degrees(float degrees)
        {
            return (float)(degrees * Math.PI / 180d);
        }


        private static void Reweigh(ThermalSimulation simulation, float share)
        {
            if (share == 0f) return;

            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            float scale = Math.Max(0.01f, 1f + share);

            for (int i = 0; i < nodes.Count; i++)
            {
                nodes[i].Block.Mass = nodes[i].Block.Mass * scale;
                nodes[i].RefreshThermalMass();
            }
        }


        private static void Silence(ThermalSimulation simulation, float share)
        {
            if (share <= 0f) return;

            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            int period = share >= 1f ? 1 : (int)Math.Round(1f / share);
            if (period <= 0) period = 1;

            int producer = 0;

            for (int i = 0; i < nodes.Count; i++)
            {
                if (!Census.IsProducer(nodes[i])) continue;

                if (producer++ % period == 0)
                {
                    nodes[i].Block.PowerProducedWatts = 0f;
                    nodes[i].Block.ThrustWatts = 0f;
                    nodes[i].RefreshHeatGeneration();
                }
            }
        }


        private static int Pressurise(ThermalSimulation simulation, float level)
        {
            if (level < 0f) level = 0f;
            if (level > 1f) level = 1f;

            IList<RoomAirNode> air = simulation.RoomAir;
            int filled = 0;

            for (int i = 0; i < air.Count; i++)
            {
                if (simulation.SetRoomPressure(air[i].Anchor, level)) filled++;
            }

            return filled;
        }


        private static void Unmap(ThermalSimulation simulation)
        {
            Pressurise(simulation, 0f);
            simulation.Solver.RefreshExposure(new RoomMap());
        }


        private static void Remap(ThermalSimulation simulation, float level)
        {
            simulation.Solver.RefreshExposure(simulation.Rooms.Map);
            Pressurise(simulation, level);
        }


        private static void Drive(ThermalSimulation simulation, float watts)
        {
            Census.DriveCensus(simulation, watts);
        }


        private static float Advance(ThermalSimulation simulation, EnvironmentSample environment,
            float seconds)
        {
            if (seconds <= 0f) return 0f;

            float step = simulation.Settings.StepSeconds;

            int steps = (int)((seconds / step) + 1e-3f);
            for (int i = 0; i < steps; i++) simulation.StepExact(1, environment);

            return steps * step;
        }


        private static int Compare(ThermalSimulation server, ThermalSimulation client,
            out float worstKelvin, out int serverOverCritical, out int missing)
        {
            IList<ThermalNode> mine = server.Solver.Nodes;

            worstKelvin = 0f;
            serverOverCritical = 0;
            missing = 0;
            int disagreeing = 0;

            for (int i = 0; i < mine.Count; i++)
            {
                ThermalNode ours = mine[i];
                float critical = ours.Thermal.CriticalTemperature;
                bool over = critical > 0f && ours.Temperature > critical;
                if (over) serverOverCritical++;

                ThermalNode yours = client.Solver.GetNodeAt(ours.Block.Position);
                if (yours == null)
                {
                    missing++;
                    continue;
                }

                float difference = Math.Abs(ours.Temperature - yours.Temperature);
                if (difference > worstKelvin) worstKelvin = difference;

                if (critical <= 0f) continue;
                if (over != yours.Temperature > critical) disagreeing++;
            }

            return disagreeing;
        }


        private static void Align(ThermalSimulation client, ThermalSimulation server)
        {
            IList<ThermalNode> mine = server.Solver.Nodes;

            for (int i = 0; i < mine.Count; i++)
            {
                ThermalNode yours = client.Solver.GetNodeAt(mine[i].Block.Position);
                if (yours != null) yours.Temperature = mine[i].Temperature;
            }
        }


        private static List<BlockInstance> Withhold(ThermalSimulation simulation, float share)
        {

            List<BlockInstance> held = new List<BlockInstance>();
            if (share <= 0f) return held;

            IList<ThermalNode> nodes = simulation.Solver.Nodes;
            int period = share >= 1f ? 1 : (int)Math.Round(1f / share);
            if (period <= 0) period = 1;

            for (int i = 0; i < nodes.Count; i++)
            {
                if (i % period == 0) held.Add(nodes[i].Block);
            }

            if (held.Count >= nodes.Count) held.RemoveAt(held.Count - 1);

            for (int i = 0; i < held.Count; i++) simulation.RemoveBlock(held[i]);

            simulation.RebuildAll();
            return held;
        }


        private static void Deliver(ThermalSimulation simulation, List<BlockInstance> held)
        {
            if (held == null || held.Count == 0) return;

            for (int i = 0; i < held.Count; i++) simulation.AddBlock(held[i]);

            simulation.RebuildAll();
            held.Clear();
        }


        public static List<Degradation> All()
        {
            List<Degradation> cases = new List<Degradation>
            {
                new Degradation
                {
                    Name = "none",
                    Because = "the control: two identical clients, which must agree exactly",
                },
                new Degradation
                {
                    Name = "stale join",
                    Because = "joined holding the server's state from 60 s ago, as a save does",
                    StaleSeconds = 60f,
                },
                new Degradation
                {
                    Name = "hitching",
                    Because = "loses 5 s of simulated time every 30, which is a rate difference in lumps",
                    HitchEverySeconds = 30f,
                    HitchLosesSeconds = 5f,
                },
                new Degradation
                {
                    Name = "environment lag",
                    Because = "samples the environment the server had 2 s ago",
                    EnvironmentLagSeconds = 2f,
                },
                new Degradation
                {
                    Name = "sun angle",
                    Because = "its replicated orientation puts the sun 5 degrees off",
                    SunAngleDegrees = 5f,
                },
                new Degradation
                {
                    Name = "power lag",
                    Because = "block power reaches it 2 s late, so a throttle change arrives late",
                    PowerLagSeconds = 2f,
                },
                new Degradation
                {
                    Name = "power error",
                    Because = "block power arrives 5 % out, standing in for what replication rounds",
                    PowerErrorShare = 0.05f,
                },
                new Degradation
                {
                    Name = "thrust error",
                    Because = "the engine predicts thrust rather than sending it, so its 20 % is a guess",
                    Scenarios = new[] { "burn" },
                    ThrustErrorShare = 0.2f,
                },
                new Degradation
                {
                    Name = "speed error",
                    Because = "its predicted velocity is 20 % out, which is 1.7x the friction heat",
                    Scenarios = new[] { "burn" },
                    SpeedErrorShare = 0.2f,
                },
                new Degradation
                {
                    Name = "position lag",
                    Because = "places the ship where it was 5 s ago, which on a descent is a"
                        + " different ambient — position is predicted, not replicated",
                    Scenarios = new[] { "descent" },
                    PositionLagSeconds = 5f,
                },
                new Degradation
                {
                    Name = "wrong weather",
                    Because = "the server is in snow and the client has not been told — world state"
                        + " with no prediction behind it, so patience does not fix it",
                    Scenarios = new[] { "planet", "descent" },
                    MissesWeather = true,
                },
                new Degradation
                {
                    Name = "wrong shadow",
                    Because = "its own raycast puts the hull in shade for 3 s of every 30, in full sun",
                    OcclusionWrongEverySeconds = 30f,
                    OcclusionWrongForSeconds = 3f,
                },
                new Degradation
                {
                    Name = "mass error",
                    Because = "its mass rota is 20 % behind the server's, so its capacities are wrong",
                    MassErrorShare = 0.2f,
                },
                new Degradation
                {
                    Name = "blocks off",
                    Because = "a tenth of its producers are on the wrong side of their own switch",
                    BlocksOffShare = 0.1f,
                },
                new Degradation
                {
                    Name = "thinner air",
                    Because = "its gas system says the hull is in 20 % less air than the server's",
                    AirDensityError = 0.2f,
                },
                new Degradation
                {
                    Name = "room pressure",
                    Because = "its gas system reports the compartments a fifth emptier than the server's",
                    RoomPressureError = 0.2f,
                },
                new Degradation
                {
                    Name = "room map lag",
                    Because = "its flood fill has not landed for 60 s, so it has no rooms at all yet",
                    RoomMapLagSeconds = 60f,
                },
                new Degradation
                {
                    Name = "build order",
                    Because = "received the same blocks in a different order, which is no difference at all",
                    BuildOrderSeed = 20260824,
                },
                new Degradation
                {
                    Name = "blocks missing",
                    Because = "a tenth of the hull has not reached it for 60 s, as a subgrid attaching late",
                    BlocksMissingShare = 0.1f,
                    BlocksMissingSeconds = 60f,
                },
                new Degradation
                {
                    Name = "slow clock",
                    Because = "runs 10 % fewer simulation ticks a second, so its thermal clock is slow",
                    SimSpeedError = -0.1f,
                },
                new Degradation
                {
                    Name = "missing source",
                    Because = "another mod registered a heat source it never heard about",
                    MissingHeatSourceIrradiance = HeatSourceIrradiance,
                },
                new Degradation
                {
                    Name = "wrong settings",
                    Because = "never received the world's settings, so it is running other physics",
                    OnDefaultSettings = true,
                },
            };

            Degradation everything = new Degradation
            {
                Name = "all at once",
                Because = "the union of every case above, which is what a bad client is",
            };

            for (int i = 0; i < cases.Count; i++)
            {
                Degradation one = cases[i];
                if (one.StaleSeconds > everything.StaleSeconds) everything.StaleSeconds = one.StaleSeconds;
                if (one.HitchEverySeconds > 0f) everything.HitchEverySeconds = one.HitchEverySeconds;
                if (one.HitchLosesSeconds > everything.HitchLosesSeconds) everything.HitchLosesSeconds = one.HitchLosesSeconds;
                if (one.EnvironmentLagSeconds > everything.EnvironmentLagSeconds) everything.EnvironmentLagSeconds = one.EnvironmentLagSeconds;
                if (one.SunAngleDegrees > everything.SunAngleDegrees) everything.SunAngleDegrees = one.SunAngleDegrees;
                if (one.ThrustErrorShare > everything.ThrustErrorShare) everything.ThrustErrorShare = one.ThrustErrorShare;
                if (one.SpeedErrorShare > everything.SpeedErrorShare) everything.SpeedErrorShare = one.SpeedErrorShare;
                if (one.OcclusionWrongEverySeconds > 0f)
                {
                    everything.OcclusionWrongEverySeconds = one.OcclusionWrongEverySeconds;
                    everything.OcclusionWrongForSeconds = one.OcclusionWrongForSeconds;
                }
                if (one.MassErrorShare > everything.MassErrorShare) everything.MassErrorShare = one.MassErrorShare;
                if (one.BlocksOffShare > everything.BlocksOffShare) everything.BlocksOffShare = one.BlocksOffShare;
                if (one.PowerLagSeconds > everything.PowerLagSeconds) everything.PowerLagSeconds = one.PowerLagSeconds;
                if (one.PowerErrorShare > everything.PowerErrorShare) everything.PowerErrorShare = one.PowerErrorShare;
                if (one.AirDensityError > everything.AirDensityError) everything.AirDensityError = one.AirDensityError;
                if (one.RoomPressureError > everything.RoomPressureError) everything.RoomPressureError = one.RoomPressureError;
                if (one.RoomMapLagSeconds > everything.RoomMapLagSeconds) everything.RoomMapLagSeconds = one.RoomMapLagSeconds;
                if (one.BuildOrderSeed != 0) everything.BuildOrderSeed = one.BuildOrderSeed;
                if (Math.Abs(one.SimSpeedError) > Math.Abs(everything.SimSpeedError)) everything.SimSpeedError = one.SimSpeedError;
                if (one.MissingHeatSourceIrradiance > everything.MissingHeatSourceIrradiance) everything.MissingHeatSourceIrradiance = one.MissingHeatSourceIrradiance;
                if (one.BlocksMissingShare > everything.BlocksMissingShare)
                {
                    everything.BlocksMissingShare = one.BlocksMissingShare;
                    everything.BlocksMissingSeconds = one.BlocksMissingSeconds;
                }
                if (one.OnDefaultSettings) everything.OnDefaultSettings = true;
            }

            cases.Add(everything);
            return cases;
        }


        private static string NeedsScenario(Result result)
        {

            List<Degradation> cases = All();
            for (int i = 0; i < cases.Count; i++)
            {
                if (cases[i].Name == result.Name && cases[i].Scenarios != null)
                {
                    return string.Join(" or ", cases[i].Scenarios);
                }
            }
            return "another scenario";
        }


        public static string Report(IList<Result> results)
        {

            StringBuilder text = new StringBuilder();
            text.AppendLine("Each input a client drives its own simulation from, degraded, with the");
            text.AppendLine("correction off and on.");
            text.AppendLine();

            int compartments = results.Count > 0 ? results[0].Rooms : 0;
            text.AppendLine("Sealed compartments on the hull, holding air on the server: "
                + compartments.ToString("n0"));
            text.AppendLine();
            text.AppendLine("degradation         fix      peak K   standing K   misreading    worst  absent   B/s");

            int judged = 0;
            int skipped = 0;

            for (int i = 0; i < results.Count; i++)
            {
                Result result = results[i];
                bool off = result.Protocol == null || result.Protocol.IntervalSeconds <= 0f;
                bool anythingFailed = result.PeakServerCritical > 0 && !result.NotExercised;
                if (anythingFailed) judged++;
                if (result.NotExercised) skipped++;

                if (result.NotExercised)
                {
                    text.AppendLine(string.Format("{0,-20}{1,-9}{2}",
                        result.Name,
                        off ? "off" : result.Protocol.IntervalSeconds.ToString("n0") + " s"
                            + (result.Protocol.WholeHullOnJoin ? "+j" : ""),

                        "not exercised on " + result.Scenario + " — needs " + NeedsScenario(result)));
                    continue;
                }

                text.AppendLine(string.Format(
                    "{0,-20}{1,-9}{2,8}{3,13}{4,13}{5,9}{6,8}{7,6}",
                    result.Name,
                    off ? "off" : result.Protocol.IntervalSeconds.ToString("n0") + " s"
                        + (result.Protocol.WholeHullOnJoin ? "+j" : ""),
                    result.PeakKelvin.ToString("n1"),
                    result.StandingKelvin.ToString("n2"),
                    anythingFailed ? result.SecondsMisreading.ToString("n0") + " s" : "nothing hot",
                    anythingFailed ? result.PeakDisagreeing.ToString("n0") : "-",
                    result.PeakMissing.ToString("n0"),
                    result.BytesPerSecond.ToString("n0")));
            }

            if (skipped > 0)
            {
                text.AppendLine();
                text.AppendLine("**" + skipped + " of " + results.Count + " rows name a scenario"
                    + " this run is not**, so their degradation could not act. They are said rather");
                text.AppendLine("than scored: on the wrong scenario every column would be the"
                    + " control's, which reads as an input");
                text.AppendLine("that does not matter (E8). Run them with --scenario.");
            }

            if (judged < results.Count - skipped)
            {
                text.AppendLine();
                text.AppendLine("**" + (results.Count - skipped - judged) + " of "
                    + (results.Count - skipped)
                    + " rows had no block past critical on the server**, so their readout columns");
                text.AppendLine("judged nothing and are printed as 'nothing hot' rather than as zero (E8). The");
                text.AppendLine("kelvin columns are still real; the readout columns are not.");
            }

            text.AppendLine();
            text.AppendLine("standing K is the mean disagreement over the final third of the run, and it is the");
            text.AppendLine("column that separates a perturbation from a bias: a one-off wrong state decays to");
            text.AppendLine("nothing whatever it peaked at, and a wrong input settles somewhere and stays. worst");
            text.AppendLine("is the largest number of blocks the two put on opposite sides of critical at once,");
            text.AppendLine("and absent is the largest number the server had that the client did not — a block a");
            text.AppendLine("client has not been told about is not one it is wrong about, so it is counted rather");
            text.AppendLine("than scored.");
            text.AppendLine();
            text.AppendLine("Every magnitude here is a knob this lab turns rather than a figure measured from a");
            text.AppendLine("session; what the table answers is which inputs bias, which perturb, and whether");
            text.AppendLine("the correction reaches each.");

            return text.ToString();
        }
    }
}
