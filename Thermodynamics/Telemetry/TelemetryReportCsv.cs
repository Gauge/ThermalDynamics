using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Thermodynamics.Core;
using VRage.Utils;
using VRageMath;

namespace Thermodynamics
{
    public static partial class TelemetryReport
    {
        private static string BuildBlockTypeCsv()
        {

            StringBuilder sb = new StringBuilder(16 * 1024);
            sb.Append("subtype,type,placed,removed,live,peak_live,updates,sampled,");
            sb.Append("conductivity,specific_heat,emissivity,surface_area_scaler,");
            sb.Append("producer_waste,consumer_waste,critical_temperature,critical_scaler,");
            sb.Append("size_x,size_y,size_z,mass_mean,thermal_mass_mean,exposed_surfaces_mean,exposed_area_mean,");
            sb.Append("temp_min,temp_mean,temp_max,temp_sd,peak_temp,");
            sb.Append("dt_mean,conduction_w_mean,radiation_w_mean,convection_w_mean,solar_w_mean,friction_w_mean,heat_generation_w_mean,");
            sb.Append("power_produced_mean,power_consumed_mean,thrust_mean,");
            sb.Append("critical_updates,total_damage,");
            sb.Append("substep_demand_mean,substep_demand_max,substep_demand_peak,substep_demand_where\n");


            List<BlockTypeTelemetry> types = SortedBlockTypes();
            for (int i = 0; i < types.Count; i++)
            {
                BlockTypeTelemetry t = types[i];
                Core.BlockThermalProperties d = t.Definition;

                Csv(sb, t.Name);
                Csv(sb, t.DefinitionId.TypeId.ToString());
                Csv(sb, t.Placed);
                Csv(sb, t.Removed);
                Csv(sb, t.Live);
                Csv(sb, t.PeakLive);
                Csv(sb, t.TotalUpdates);
                Csv(sb, t.SampledUpdates);

                Csv(sb, d == null ? 0 : d.Conductivity);
                Csv(sb, d == null ? 0 : d.SpecificHeat);
                Csv(sb, d == null ? 0 : d.Emissivity);
                Csv(sb, d == null ? 0 : d.ExposedSurfaceMultiplier);
                Csv(sb, d == null ? 0 : d.ProducerWasteEnergy);
                Csv(sb, d == null ? 0 : d.ConsumerWasteEnergy);
                Csv(sb, d == null ? 0 : d.CriticalTemperature);
                Csv(sb, d == null ? 0 : d.OverheatDamagePerKelvin);

                Csv(sb, t.Size.X);
                Csv(sb, t.Size.Y);
                Csv(sb, t.Size.Z);
                Csv(sb, t.Mass.Mean);
                Csv(sb, t.ThermalMass.Mean);
                Csv(sb, t.ExposedSurfaces.Mean);
                Csv(sb, t.ExposedSurfaceArea.Mean);

                Csv(sb, t.Temperature.SafeMin);
                Csv(sb, t.Temperature.Mean);
                Csv(sb, t.Temperature.SafeMax);
                Csv(sb, t.Temperature.StdDev);
                Csv(sb, t.PeakTemperature == float.MinValue ? 0 : t.PeakTemperature);

                Csv(sb, t.DeltaTemperature.Mean);
                Csv(sb, t.ConductionWatts.Mean);
                Csv(sb, t.RadiationWatts.Mean);
                Csv(sb, t.ConvectionWatts.Mean);
                Csv(sb, t.SolarWatts.Mean);
                Csv(sb, t.FrictionWatts.Mean);
                Csv(sb, t.HeatGeneration.Mean);
                Csv(sb, t.EnergyProduction.Mean);
                Csv(sb, t.EnergyConsumption.Mean);
                Csv(sb, t.ThrustConsumption.Mean);

                Csv(sb, t.CriticalUpdates);
                Csv(sb, t.TotalDamage);

                Csv(sb, t.SubstepDemand.Mean);
                Csv(sb, t.SubstepDemand.SafeMax);
                Csv(sb, t.PeakSubstepDemand);
                CsvLast(sb, t.PeakSubstepDemandPosition);
            }

            return sb.ToString();
        }


        private static string BuildRoomCsv()
        {

            StringBuilder sb = new StringBuilder();

            sb.Append("grid,grid_id,kind,index,anchor_x,anchor_y,anchor_z,cells,volume_m3,");
            sb.Append("vented,pressure,air_kg,temperature_k,links,game_airtight,game_oxygen,disagreement,");
            sb.Append("vents,vent_pressurised,oxygen_level,leak_faces,leaking_blocks\n");

            IList<GridTelemetry> grids = Telemetry.Grids;

            for (int g = 0; g < grids.Count; g++)
            {
                GridTelemetry record = grids[g];

                string name = Truncate(record.Name, 40);

                for (int i = 0; i < record.Rooms.Count; i++)
                {
                    RoomRow row = record.Rooms[i];

                    Csv(sb, name);
                    Csv(sb, record.EntityId);
                    Csv(sb, row.Kind);
                    Csv(sb, row.Index);

                    Csv(sb, row.AnchorX);
                    Csv(sb, row.AnchorY);
                    Csv(sb, row.AnchorZ);

                    Csv(sb, row.CellCount);
                    Csv(sb, row.Volume);

                    Csv(sb, row.Vented ? 1 : 0);
                    Csv(sb, row.Pressure);
                    Csv(sb, row.AirMass);
                    Csv(sb, row.TemperatureKelvin);
                    Csv(sb, row.LinkCount);
                    Csv(sb, row.GameAirtight ? 1 : 0);
                    Csv(sb, row.GameOxygen);
                    Csv(sb, row.Disagreement ? 1 : 0);

                    Csv(sb, Truncate(row.Vents ?? "", 120));
                    Csv(sb, row.VentPressurised ? 1 : 0);
                    Csv(sb, row.OxygenLevel);
                    Csv(sb, row.LeakCount);
                    CsvLast(sb, Truncate(row.LeakingBlocks ?? "", 200));
                }
            }

            return sb.ToString();
        }


        private static string BuildSurfaceCsv()
        {

            StringBuilder sb = new StringBuilder();

            sb.Append("grid,grid_id,block,subtype,cell_x,cell_y,cell_z,size_x,size_y,size_z,");
            sb.Append("face,face_cells,exposed,sealed,mounted,interior,");
            sb.Append("sun_dot,sun_lit_fraction,solar_w,temperature_k,exposed_area_m2,windward_dot,block_friction_w\n");

            IList<GridTelemetry> grids = Telemetry.Grids;

            for (int g = 0; g < grids.Count; g++)
            {
                GridTelemetry record = grids[g];

                string gridName = Truncate(record.Name, 40);

                for (int i = 0; i < record.Surfaces.Count; i++)
                {
                    SurfaceRow row = record.Surfaces[i];

                    Csv(sb, gridName);
                    Csv(sb, record.EntityId);
                    Csv(sb, Truncate(row.Block, 40));
                    Csv(sb, Truncate(row.Subtype, 40));

                    Csv(sb, row.Cell.X);
                    Csv(sb, row.Cell.Y);
                    Csv(sb, row.Cell.Z);
                    Csv(sb, row.Size.X);
                    Csv(sb, row.Size.Y);
                    Csv(sb, row.Size.Z);

                    Csv(sb, Face.Name(row.Face));
                    Csv(sb, row.Cells);
                    Csv(sb, row.Exposed);
                    Csv(sb, row.Sealed);
                    Csv(sb, row.Mounted);
                    Csv(sb, row.Interior);

                    Csv(sb, row.SunDot);
                    Csv(sb, row.SunLitFraction);
                    Csv(sb, row.SolarWatts);
                    Csv(sb, row.Temperature);
                    Csv(sb, row.ExposedArea);
                    Csv(sb, row.WindwardDot);
                    CsvLast(sb, row.BlockFrictionWatts);
                }
            }

            return sb.ToString();
        }
        private static string BuildEnvironmentCsv()
        {

            StringBuilder sb = new StringBuilder();

            sb.Append("time_s,grid,grid_id,planet,altitude_surface_m,altitude_sealevel_m,latitude_deg,");
            sb.Append("sun_elevation_deg,air_density,atmosphere_factor,ambient_k,ambient_c,underground,depth_m,");
            sb.Append("solar_w,solar_occlusion,convection_coeff,wind_speed,wind_bearing_deg,wind_ceiling,");
            sb.Append("wind_agl_m,wind_burial,wind_band_share,wind_profile,wind_heating,wind_speedup,wind_shelter,wind_channel_deg,grid_speed,");
            sb.Append("weather,weather_intensity,weather_ambient_k,game_comfort,");
            sb.Append("surface_material,grid_mean_k,grid_peak_k\n");

            IList<GridTelemetry> grids = Telemetry.Grids;

            for (int g = 0; g < grids.Count; g++)
            {
                GridTelemetry record = grids[g];

                string name = Truncate(record.Name, 40);

                for (int i = 0; i < record.Environment.Count; i++)
                {
                    EnvironmentRow row = record.Environment[i];

                    Csv(sb, row.Seconds);
                    Csv(sb, name);
                    Csv(sb, record.EntityId);
                    Csv(sb, Truncate(row.Planet ?? "", 40));

                    Csv(sb, row.AltitudeSurface);
                    Csv(sb, row.AltitudeSealevel);
                    Csv(sb, row.LatitudeDegrees);
                    Csv(sb, row.SunElevationDegrees);

                    Csv(sb, row.AirDensity);
                    Csv(sb, row.AtmosphereFactor);
                    Csv(sb, row.AmbientKelvin);
                    Csv(sb, ThermalConstants.KelvinToCelsius(row.AmbientKelvin));
                    Csv(sb, row.Underground ? 1 : 0);
                    Csv(sb, row.Depth);

                    Csv(sb, row.SolarEnergy);
                    Csv(sb, row.SolarOcclusion);
                    Csv(sb, row.ConvectionCoefficient);
                    Csv(sb, row.WindSpeed);
                    Csv(sb, row.WindBearingDegrees);
                    Csv(sb, row.WindCeiling);

                    Csv(sb, row.WindHeightAboveGround);
                    Csv(sb, row.WindBurial);
                    Csv(sb, row.WindBandShare);
                    Csv(sb, row.WindProfileFactor);
                    Csv(sb, row.WindHeating);
                    Csv(sb, row.WindSpeedUp);
                    Csv(sb, row.WindShelter);
                    Csv(sb, row.WindChannelDegrees);
                    Csv(sb, row.GridSpeed);

                    Csv(sb, Truncate(row.Weather ?? "", 32));
                    Csv(sb, row.WeatherIntensity);
                    Csv(sb, row.WeatherAmbientOffset);
                    Csv(sb, row.GameComfort);

                    Csv(sb, Truncate(row.SurfaceMaterial ?? "", 32));
                    Csv(sb, row.GridMeanKelvin);
                    CsvLast(sb, row.GridPeakKelvin);
                }
            }

            return sb.ToString();
        }


        private static string BuildGridCsv()
        {

            StringBuilder sb = new StringBuilder(8 * 1024);
            sb.Append("entity_id,name,grid_size,is_static,closed,lifetime_s,");
            sb.Append("peak_cells,mean_cells,peak_links,peak_rooms,peak_loops,");
            sb.Append("peak_external_cells,peak_solid_cells,peak_room_cells,max_leaked_cells,max_open_block_cells,");
            sb.Append("simulation_steps,node_updates,sampled_nodes,substeps_mean,clamped_steps,");
            sb.Append("peak_temperature,mean_hottest,critical_max,damage_events,total_damage,");
            sb.Append("ambient_min,ambient_mean,ambient_max,air_density_mean,wind_mean,wind_max,speed_max,");
            sb.Append("occluded_fraction,atmosphere_fraction,");
            sb.Append("blocks_added,blocks_removed,blocks_restored,rooms_restored,splits,merges,door_changes,surface_refreshes,");
            sb.Append("mapper_passes,loop_w_absorbed,loop_w_rejected,pump_lift_w,pump_draw_w,pump_cop,saves,loads,save_bytes,load_bytes,");
            sb.Append("sim_ms_total,sim_ms_max,topology_ms_total,mapping_ms_total,exposure_ms_total,solver_ms_total,solver_ms_max,");
            sb.Append("solar_ms_total,solar_ms_max,save_ms_total,load_ms_total,");
            sb.Append("ticks,first_tick_frame,last_tick_frame,");
            sb.Append("substeps_required_mean,substeps_required_max,floored_nodes_mean,");
            sb.Append("demand_uncapped,demand_configured,demand_conduction_share,");
            sb.Append("demand_room_air,demand_loop,environment_stiff_nodes,substep_driver,");

            int[] capColumns = ThermalSolver.SubstepProfile.ProjectedCaps;
            for (int c = 0; c < capColumns.Length; c++)
            {
                sb.Append("cap").Append(capColumns[c]).Append("_substeps,");
            }
            for (int c = 0; c < capColumns.Length; c++)
            {
                sb.Append("cap").Append(capColumns[c]).Append("_blocks");
                sb.Append(c == capColumns.Length - 1 ? "\n" : ",");
            }


            List<GridTelemetry> grids = SortedGrids();
            for (int i = 0; i < grids.Count; i++)
            {
                GridTelemetry g = grids[i];

                Csv(sb, g.EntityId);
                Csv(sb, g.Name);
                Csv(sb, g.GridSize);
                Csv(sb, g.IsStatic ? 1 : 0);
                Csv(sb, g.IsClosed ? 1 : 0);
                Csv(sb, g.LifetimeSeconds);

                Csv(sb, g.PeakCellCount);
                Csv(sb, g.CellCount.Mean);
                Csv(sb, g.NeighborLinks.SafeMax);
                Csv(sb, g.RoomCount.SafeMax);
                Csv(sb, g.CoolantLoops.SafeMax);

                Csv(sb, g.ExternalCells.SafeMax);
                Csv(sb, g.SolidCells.SafeMax);
                Csv(sb, g.RoomCells.SafeMax);
                Csv(sb, g.LeakedCells.SafeMax);
                Csv(sb, g.OpenBlockCells.SafeMax);

                Csv(sb, g.SimulationSteps);
                Csv(sb, g.NodeUpdates);
                Csv(sb, g.SampledNodes);
                Csv(sb, g.Substeps.Mean);
                Csv(sb, g.ClampedSteps);

                Csv(sb, g.PeakTemperature == float.MinValue ? 0 : g.PeakTemperature);
                Csv(sb, g.HottestBlockTemperature.Mean);
                Csv(sb, g.CriticalBlocks.SafeMax);
                Csv(sb, g.DamageEvents);
                Csv(sb, g.TotalDamage);

                Csv(sb, g.AmbientTemperature.SafeMin);
                Csv(sb, g.AmbientTemperature.Mean);
                Csv(sb, g.AmbientTemperature.SafeMax);
                Csv(sb, g.AirDensity.Mean);
                Csv(sb, g.WindSpeed.Mean);
                Csv(sb, g.WindSpeed.SafeMax);
                Csv(sb, g.Speed.SafeMax);

                Csv(sb, g.OccludedFraction);
                Csv(sb, g.AtmosphereFraction);

                Csv(sb, g.BlocksAdded);
                Csv(sb, g.BlocksRemoved);
                Csv(sb, g.BlocksRestored);
                Csv(sb, g.RoomsRestored);
                Csv(sb, g.Splits);
                Csv(sb, g.Merges);
                Csv(sb, g.DoorStateChanges);
                Csv(sb, g.SurfaceRecalcs);
                Csv(sb, g.MapperCompletions);
                Csv(sb, g.LoopWattsAbsorbed.Mean);
                Csv(sb, g.LoopWattsRejected.Mean);
                Csv(sb, g.HeatPumpLiftWatts.Mean);
                Csv(sb, g.HeatPumpPowerWatts.Mean);
                Csv(sb, g.HeatPumpCoefficient.Mean);
                Csv(sb, g.Saves);
                Csv(sb, g.Loads);
                Csv(sb, g.SaveBytes);
                Csv(sb, g.LoadBytes);

                Csv(sb, g.SimulationTime.TotalMilliseconds);
                Csv(sb, g.SimulationTime.MaxMilliseconds);
                Csv(sb, g.Profiler.Topology.TotalMilliseconds);
                Csv(sb, g.Profiler.RoomMapping.TotalMilliseconds);
                Csv(sb, g.Profiler.Exposure.TotalMilliseconds);
                Csv(sb, g.Profiler.Solver.TotalMilliseconds);
                Csv(sb, g.Profiler.Solver.MaxMilliseconds);
                Csv(sb, g.SolarTime.TotalMilliseconds);
                Csv(sb, g.SolarTime.MaxMilliseconds);
                Csv(sb, g.SaveTime.TotalMilliseconds);
                Csv(sb, g.LoadTime.TotalMilliseconds);

                Csv(sb, g.SimulationTime.Calls);
                Csv(sb, g.FirstTickFrame);
                Csv(sb, g.LastTickFrame);

                Csv(sb, g.RequiredSubsteps.Mean);
                Csv(sb, g.RequiredSubsteps.SafeMax);
                Csv(sb, g.FlooredNodes.Mean);

                ThermalSolver.SubstepProfile p = g.Profile;
                Csv(sb, p == null ? 0 : p.RequiredSubsteps);
                Csv(sb, p == null ? 0 : p.RequiredSubstepsInForce);
                Csv(sb, p == null ? 0 : p.WorstNodeConductionShare);
                Csv(sb, p == null ? 0 : p.WorstRoomAirDemand);
                Csv(sb, p == null ? 0 : p.WorstLoopDemand);
                Csv(sb, p == null ? 0 : p.EnvironmentDominatedNodes);
                Csv(sb, g.WorstSubstepBlock);

                int[] caps = ThermalSolver.SubstepProfile.ProjectedCaps;
                for (int c = 0; c < caps.Length; c++)
                {
                    Csv(sb, p == null ? 0 : p.CapRequiredSubsteps[c]);
                }
                for (int c = 0; c < caps.Length; c++)
                {
                    if (c == caps.Length - 1) CsvLast(sb, p == null ? 0 : p.CapNodesFloored[c]);

                    else Csv(sb, p == null ? 0 : p.CapNodesFloored[c]);
                }
            }

            return sb.ToString();
        }


        private static void Csv(StringBuilder sb, string value)
        {
            TelemetryFormat.AppendCsv(sb, value);
        }


        private static void Csv(StringBuilder sb, long value)
        {
            TelemetryFormat.AppendCsv(sb, value);
        }


        private static void Csv(StringBuilder sb, double value)
        {
            TelemetryFormat.AppendCsv(sb, value);
        }


        private static void CsvLast(StringBuilder sb, double value)
        {
            TelemetryFormat.AppendCsvLast(sb, value);
        }


        private static void CsvLast(StringBuilder sb, long value)
        {
            TelemetryFormat.AppendCsvLast(sb, value);
        }


        private static void CsvLast(StringBuilder sb, string value)
        {
            TelemetryFormat.AppendCsvLast(sb, value);
        }



    }
}
