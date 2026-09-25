using System;
using System.Collections.Generic;
using System.IO;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The auditor that reads a field dump.
    ///
    /// <para>Every check in it exists because a document claims something about a real world, and
    /// the auditor is what turns that claim into a count. So each test here builds the smallest CSV
    /// that could break one check, and asserts that the check breaks — a check that cannot fail is
    /// worse than no check, because it reads as evidence.</para>
    ///
    /// <para>A dump this repository can no longer produce is still audited, since the checks skip
    /// the columns a dump does not carry. That is tested too: a skip must never read as a pass.</para>
    /// </summary>
    public class DumpAuditTests : IDisposable
    {

        private readonly List<string> written = new List<string>();

        private readonly List<string> folders = new List<string>();


        public void Dispose()
        {
            foreach (string path in written)
            {
                try { File.Delete(path); }
                catch (IOException) { }
            }

            foreach (string folder in folders)
            {
                try { Directory.Delete(folder, true); }
                catch (IOException) { }
            }
        }

        private const string Header =
            "time_s,grid,grid_id,planet,altitude_surface_m,altitude_sealevel_m,latitude_deg," +
            "sun_elevation_deg,air_density,atmosphere_factor,ambient_k,ambient_c,underground," +
            "depth_m,solar_w,solar_occlusion,convection_coeff,wind_speed,wind_bearing_deg," +
            "wind_ceiling,wind_agl_m,wind_burial,wind_band_share,wind_profile,wind_heating,wind_speedup," +
            "wind_shelter,wind_channel_deg,grid_speed,weather,weather_intensity,weather_ambient_k," +
            "game_comfort,surface_material,grid_mean_k,grid_peak_k";



        private static Dictionary<string, string> Row()
        {
            Dictionary<string, string> row = new Dictionary<string, string>();

            foreach (string column in Header.Split(',')) row[column] = "0";

            row["time_s"] = "10";
            row["grid"] = "Test Grid";
            row["planet"] = "EarthLike";
            row["altitude_surface_m"] = "120";
            row["air_density"] = "0.9";
            row["ambient_k"] = "288";
            row["convection_coeff"] = "60";
            row["solar_w"] = "800";
            row["wind_ceiling"] = "70";
            row["wind_band_share"] = "0.2";
            row["wind_profile"] = "1.5";
            row["wind_speedup"] = "1";
            row["wind_shelter"] = "1";
            row["wind_speed"] = "21";
            row["wind_agl_m"] = "120";
            row["wind_burial"] = "1";
            row["weather"] = "Clear";
            row["surface_material"] = "Grass";
            row["grid_mean_k"] = "290";
            row["grid_peak_k"] = "300";

            return row;
        }

        private const string GridHeader =
            "entity_id,name,grid_size,peak_cells,mean_cells,simulation_steps,clamped_steps," +
            "ambient_min,ambient_mean,ambient_max,mean_hottest,sim_ms_total,sim_ms_max," +
            "topology_ms_total,mapping_ms_total,exposure_ms_total,solver_ms_total,solver_ms_max," +
            "loop_w_absorbed,loop_w_rejected,pump_lift_w,pump_draw_w,pump_cop";


        private static Dictionary<string, string> Grid()
        {
            Dictionary<string, string> row = new Dictionary<string, string>();
            foreach (string column in GridHeader.Split(',')) row[column] = "0";

            row["entity_id"] = "1";
            row["name"] = "Test Grid";
            row["grid_size"] = "Large";
            row["peak_cells"] = "900";
            row["mean_cells"] = "880";
            row["simulation_steps"] = "1200";
            row["clamped_steps"] = "3";
            row["ambient_min"] = "250";
            row["ambient_mean"] = "270";
            row["ambient_max"] = "290";
            row["sim_ms_total"] = "100";
            row["sim_ms_max"] = "2";
            row["topology_ms_total"] = "10";
            row["mapping_ms_total"] = "5";
            row["exposure_ms_total"] = "4";
            row["solver_ms_total"] = "70";
            row["solver_ms_max"] = "1.5";
            row["mean_hottest"] = "310";
            return row;
        }

        private const string TypeHeader =
            "subtype,type,placed,removed,live,conductivity,specific_heat,emissivity," +
            "critical_temperature,mass_mean,thermal_mass_mean,exposed_area_mean," +
            "temp_min,temp_mean,temp_max,peak_temp," +
            "substep_demand_mean,substep_demand_max,substep_demand_peak";


        private static Dictionary<string, string> Type()
        {
            Dictionary<string, string> row = new Dictionary<string, string>();
            foreach (string column in TypeHeader.Split(',')) row[column] = "0";

            row["subtype"] = "LargeBlockArmorBlock";
            row["type"] = "CubeBlock";
            row["placed"] = "500";
            row["removed"] = "20";
            row["live"] = "480";
            row["conductivity"] = "50";
            row["specific_heat"] = "466";
            row["emissivity"] = "0.9";
            row["critical_temperature"] = "868";
            row["temp_min"] = "280";
            row["temp_mean"] = "291";
            row["temp_max"] = "300";
            row["peak_temp"] = "312";
            row["substep_demand_mean"] = "1.9";
            row["substep_demand_max"] = "2.4";
            row["substep_demand_peak"] = "2.4";
            row["mass_mean"] = "3000";
            row["thermal_mass_mean"] = "6200";
            row["exposed_area_mean"] = "18.75";
            return row;
        }


        private string WriteDump(
            Dictionary<string, string>[] rows,
            Dictionary<string, string>[] grids = null,
            Dictionary<string, string>[] types = null)
        {
            string folder = Path.Combine(Path.GetTempPath(),
                "thermal-dump-" + Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(folder);
            folders.Add(folder);

            const string stamp = "20260820_175059";
            string environment = Path.Combine(folder, "Thermodynamics_Environment_" + stamp + ".csv");

            Write(environment, Header, rows);
            Write(Path.Combine(folder, "Thermodynamics_Grids_" + stamp + ".csv"),

                GridHeader, grids ?? new[] { Grid() });
            Write(Path.Combine(folder, "Thermodynamics_BlockTypes_" + stamp + ".csv"),

                TypeHeader, types ?? new[] { Type() });

            return environment;
        }


        private string Write(params Dictionary<string, string>[] rows)
        {

            return Write(Header, rows);
        }


        private string Write(string header, params Dictionary<string, string>[] rows)
        {
            string path = Path.Combine(Path.GetTempPath(),
                "thermal-dump-" + Guid.NewGuid().ToString("n") + ".csv");
            written.Add(path);

            Write(path, header, rows);
            return path;
        }


        private static void Write(string path, string header, Dictionary<string, string>[] rows)
        {
            string[] columns = header.Split(',');

            using (StreamWriter writer = new StreamWriter(path))
            {
                writer.WriteLine(header);
                foreach (Dictionary<string, string> row in rows)
                {
                    string[] fields = new string[columns.Length];
                    for (int i = 0; i < columns.Length; i++)
                    {
                        string value;
                        fields[i] = row.TryGetValue(columns[i], out value) ? value : "0";
                    }
                    writer.WriteLine(string.Join(",", fields));
                }
            }
        }


        private static DumpAudit.CheckResult Check(DumpAudit.Result result, string name)
        {
            foreach (DumpAudit.CheckResult check in result.Checks)
            {
                if (check.Name.StartsWith(name, StringComparison.Ordinal)) return check;
            }

            throw new Xunit.Sdk.XunitException("no check named " + name);
        }

        [Fact]

        public void APlausibleDumpPassesEveryCheck()
        {
            DumpAudit.Result result = DumpAudit.Run(WriteDump(new[] { Row(), Row() }));

            Assert.True(result.Passed);
            Assert.Equal(2, result.Rows);
            Assert.Equal(1, result.GridRows);
            Assert.Equal(1, result.BlockTypeRows);

            foreach (DumpAudit.CheckResult check in result.Checks)
            {
                Assert.False(check.Skipped, check.Name + " skipped on a full dump");
                Assert.Equal(0, check.Hits);
            }
        }

        [Fact]

        public void StagesLargerThanTheUpdateTheyNestInAreADefect()
        {

            Dictionary<string, string> grid = Grid();
            grid["sim_ms_total"] = "11.93";
            grid["topology_ms_total"] = "21.75";
            grid["mapping_ms_total"] = "12.36";
            grid["exposure_ms_total"] = "6.83";
            grid["solver_ms_total"] = "3.26";

            DumpAudit.Result result = DumpAudit.Run(
                WriteDump(new[] { Row() }, new[] { grid }));


            DumpAudit.CheckResult check = Check(result, "a grid's stages fit");
            Assert.Equal(1, check.Hits);
            Assert.True(check.Failed);
            Assert.Contains("11.93", check.Worst);
        }

        [Fact]

        public void AnUpdateLargerThanItsStagesIsNotADefect()
        {

            Dictionary<string, string> grid = Grid();
            grid["sim_ms_total"] = "100";
            grid["solver_ms_total"] = "40";

            Assert.True(DumpAudit.Run(WriteDump(new[] { Row() }, new[] { grid })).Passed);
        }

        [Fact]

        public void ASolverCallLongerThanTheUpdateAroundItIsADefect()
        {

            Dictionary<string, string> grid = Grid();
            grid["solver_ms_max"] = "5";
            grid["sim_ms_max"] = "2";

            Assert.Equal(1, Check(DumpAudit.Run(WriteDump(new[] { Row() }, new[] { grid })),
                "a grid's worst solver call").Hits);
        }

        [Fact]

        public void MoreClampedStepsThanStepsIsADefect()
        {

            Dictionary<string, string> grid = Grid();
            grid["clamped_steps"] = "1300";

            Assert.Equal(1, Check(DumpAudit.Run(WriteDump(new[] { Row() }, new[] { grid })),
                "clamped steps").Hits);
        }

        [Fact]

        public void AMeanOutsideItsOwnRangeIsADefect()
        {

            Dictionary<string, string> grid = Grid();
            grid["ambient_mean"] = "300";

            Assert.Equal(1, Check(DumpAudit.Run(WriteDump(new[] { Row() }, new[] { grid })),
                "a grid's minima").Hits);
        }

        [Fact]

        public void LiveBlocksThatDoNotFollowFromPlacedAndRemovedAreADefect()
        {

            Dictionary<string, string> type = Type();
            type["live"] = "500";

            Assert.Equal(1, Check(DumpAudit.Run(WriteDump(new[] { Row() }, null, new[] { type })),
                "live blocks").Hits);
        }

        [Fact]

        public void APeakBelowTheMaximumItBoundsIsADefect()
        {

            Dictionary<string, string> type = Type();
            type["peak_temp"] = "282.23";
            type["temp_max"] = "293.15";


            DumpAudit.CheckResult check = Check(
                DumpAudit.Run(WriteDump(new[] { Row() }, null, new[] { type })),
                "a block type's temperatures");

            Assert.Equal(1, check.Hits);
            Assert.Contains("temp_max", check.Worst);
        }

        [Fact]

        public void ATypeNothingSampledIsNotJudgedOnItsZeroes()
        {

            Dictionary<string, string> type = Type();
            type["temp_min"] = "0";
            type["temp_mean"] = "0";
            type["temp_max"] = "0";
            type["peak_temp"] = "0";

            DumpAudit.Result result = DumpAudit.Run(WriteDump(new[] { Row() }, null, new[] { type }));

            Assert.True(result.Passed);
            Assert.Equal(0, Check(result, "a block type's temperatures").Rows);
        }

        [Fact]

        public void APropertyTheSolverCannotDivideByIsADefect()
        {

            Dictionary<string, string> zeroHeat = Type();
            zeroHeat["specific_heat"] = "0";

            Assert.Equal(1, Check(DumpAudit.Run(WriteDump(new[] { Row() }, null, new[] { zeroHeat })),
                "every block type has properties").Hits);


            Dictionary<string, string> superBlack = Type();
            superBlack["emissivity"] = "1.4";

            Assert.Equal(1, Check(DumpAudit.Run(WriteDump(new[] { Row() }, null, new[] { superBlack })),
                "every block type has properties").Hits);
        }

        [Fact]

        public void APumpThatDrewNothingAndStillLiftedIsADefect()
        {

            Dictionary<string, string> idle = Grid();
            idle["pump_draw_w"] = "0";
            idle["pump_lift_w"] = "4258";

            Assert.Equal(1, Check(DumpAudit.Run(WriteDump(new[] { Row() }, new[] { idle })),
                "a pump that drew nothing").Hits);


            Dictionary<string, string> poor = Grid();
            poor["pump_draw_w"] = "20000";
            poor["pump_lift_w"] = "4258";
            poor["pump_cop"] = "0.213";

            Assert.True(DumpAudit.Run(WriteDump(new[] { Row() }, new[] { poor })).Passed);
        }

        [Fact]

        public void ANonsenseReadingIsCaughtInEveryTableNotJustTheClimate()
        {

            Dictionary<string, string> grid = Grid();
            grid["solver_ms_total"] = "-70";


            DumpAudit.CheckResult inGrids = Check(
                DumpAudit.Run(WriteDump(new[] { Row() }, new[] { grid })), "no grid reading");
            Assert.Equal(1, inGrids.Hits);
            Assert.Contains("Test Grid", inGrids.Worst);


            Dictionary<string, string> type = Type();
            type["thermal_mass_mean"] = "NaN";


            DumpAudit.CheckResult inTypes = Check(
                DumpAudit.Run(WriteDump(new[] { Row() }, null, new[] { type })), "no block type reading");
            Assert.Equal(1, inTypes.Hits);
            Assert.Contains("LargeBlockArmorBlock", inTypes.Worst);
        }

        [Fact]

        public void AGridsSignedColumnsAreLeftAlone()
        {

            Dictionary<string, string> grid = Grid();
            grid["mean_hottest"] = "-40";

            Assert.True(DumpAudit.Run(WriteDump(new[] { Row() }, new[] { grid })).Passed);
        }

        [Fact]

        public void AnEnvironmentCsvOnItsOwnSkipsTheChecksItCannotReach()
        {
            DumpAudit.Result result = DumpAudit.Run(Write(Row()));

            Assert.True(Check(result, "a grid's stages fit").Skipped);
            Assert.True(Check(result, "live blocks").Skipped);


            DumpAudit.CheckResult grids = Check(result, "no grid reading");
            Assert.True(grids.Skipped);
            Assert.Contains("grid CSV", grids.Missing);

            Assert.False(Check(result, "convection is reported").Skipped);
            Assert.True(result.Passed);
        }

        [Fact]

        public void AWindSlowerThanItsFactorsIsADefect()
        {

            Dictionary<string, string> row = Row();
            row["wind_speed"] = "10";

            DumpAudit.Result result = DumpAudit.Run(Write(row));

            DumpAudit.CheckResult check = Check(result, "wind decomposes");

            Assert.Equal(1, check.Hits);
            Assert.True(check.Failed);
            Assert.False(result.Passed);
            Assert.Contains("10.00", check.Worst);
        }

        [Fact]

        public void AMovingGridMayReadBelowItsFactors()
        {

            Dictionary<string, string> row = Row();
            row["wind_speed"] = "10";
            row["grid_speed"] = "45";

            DumpAudit.Result result = DumpAudit.Run(Write(row));

            DumpAudit.CheckResult check = Check(result, "wind decomposes");

            Assert.Equal(0, check.Hits);
            Assert.True(result.Passed);
        }

        [Fact]

        public void AWindFasterThanItsFactorsIsCountedAsSlopeWind()
        {

            Dictionary<string, string> row = Row();
            row["wind_speed"] = "24";

            DumpAudit.Result result = DumpAudit.Run(Write(row));

            Assert.Equal(0, Check(result, "wind decomposes").Hits);
            Assert.Equal(1, Check(result, "slope wind").Hits);
            Assert.True(result.Passed);
        }

        [Fact]

        public void RoundingInTheDumpIsNotADefect()
        {

            Dictionary<string, string> row = Row();
            row["wind_speed"] = "20.9994";

            Assert.True(DumpAudit.Run(Write(row)).Passed);
        }

        [Fact]

        public void AWindOverTheEnginesCeilingIsObservedRatherThanFailed()
        {

            Dictionary<string, string> row = Row();
            row["wind_profile"] = "6";
            row["wind_speed"] = "84";

            DumpAudit.Result result = DumpAudit.Run(Write(row));

            DumpAudit.CheckResult check = Check(result, "wind stays under");

            Assert.Equal(1, check.Hits);
            Assert.True(check.Observation);
            Assert.False(check.Failed);
            Assert.True(result.Passed);
        }

        [Fact]

        public void ConvectionAndAirMustAgreeInBothDirections()
        {

            Dictionary<string, string> vacuum = Row();
            vacuum["air_density"] = "0";
            vacuum["wind_ceiling"] = "0";
            vacuum["wind_speed"] = "0";

            Assert.Equal(1, Check(DumpAudit.Run(Write(vacuum)), "convection is reported").Hits);


            Dictionary<string, string> still = Row();
            still["convection_coeff"] = "0";

            Assert.Equal(1, Check(DumpAudit.Run(Write(still)), "convection is reported").Hits);
        }

        [Fact]

        public void WindOnAWhollyBuriedGridFails()
        {

            Dictionary<string, string> row = Row();
            row["depth_m"] = "12";
            row["underground"] = "1";
            row["wind_burial"] = "0";
            row["wind_speed"] = "4.3";

            DumpAudit.Result result = DumpAudit.Run(Write(row));

            Assert.False(result.Passed);
            Assert.True(Check(result, "a wholly buried grid").Hits > 0);
        }

        [Fact]

        public void OnlyAPositiveDepthOnAnUnburiedGridIsADefect()
        {

            Dictionary<string, string> flying = Row();
            flying["depth_m"] = "-120";

            Assert.True(DumpAudit.Run(Write(flying)).Passed);


            Dictionary<string, string> buried = Row();
            buried["depth_m"] = "46";
            buried["underground"] = "1";

            Assert.True(DumpAudit.Run(Write(buried)).Passed);


            Dictionary<string, string> confused = Row();
            confused["depth_m"] = "46";

            Assert.Equal(1, Check(DumpAudit.Run(Write(confused)), "a positive depth").Hits);
        }

        [Fact]

        public void AWeatherOffsetThatDoesNotScaleWithItsIntensityIsADefect()
        {

            Dictionary<string, string> half = Row();
            half["weather"] = "RainLight";
            half["weather_intensity"] = "0.5";
            half["weather_ambient_k"] = "-0.9";


            Dictionary<string, string> full = Row();
            full["weather"] = "RainLight";
            full["weather_intensity"] = "1";
            full["weather_ambient_k"] = "-1.8";

            Assert.True(DumpAudit.Run(Write(half, full)).Passed);


            Dictionary<string, string> stuck = Row();
            stuck["weather"] = "RainLight";
            stuck["weather_intensity"] = "1";
            stuck["weather_ambient_k"] = "-0.9";

            Assert.Equal(1, Check(DumpAudit.Run(Write(half, stuck)), "a weather's offset").Hits);
        }

        [Fact]

        public void TwoWeathersDoNotHaveToAgreeWithEachOther()
        {

            Dictionary<string, string> rain = Row();
            rain["weather"] = "RainLight";
            rain["weather_intensity"] = "1";
            rain["weather_ambient_k"] = "-1.8";


            Dictionary<string, string> storm = Row();
            storm["weather"] = "ThunderstormHeavy";
            storm["weather_intensity"] = "1";
            storm["weather_ambient_k"] = "-6";

            Assert.True(DumpAudit.Run(Write(rain, storm)).Passed);
        }

        [Fact]

        public void ANaNAndAnImpossibleNegativeAreBothCaught()
        {

            Dictionary<string, string> nan = Row();
            nan["grid_peak_k"] = "NaN";


            DumpAudit.CheckResult check = Check(DumpAudit.Run(Write(nan)), "no climate reading");
            Assert.Equal(1, check.Hits);
            Assert.Contains("grid_peak_k", check.Worst);


            Dictionary<string, string> negative = Row();
            negative["air_density"] = "-0.5";

            Assert.Equal(1, Check(DumpAudit.Run(Write(negative)), "no climate reading").Hits);
        }

        [Fact]

        public void ASignedColumnIsNotAnImpossibleNegative()
        {

            Dictionary<string, string> row = Row();
            row["latitude_deg"] = "-41";
            row["wind_bearing_deg"] = "-160";
            row["ambient_c"] = "-52";
            row["altitude_surface_m"] = "-169";

            Assert.True(DumpAudit.Run(Write(row)).Passed);
        }

        [Fact]

        public void AnOlderDumpSkipsWhatItCannotAnswerAndAuditsTheRest()
        {
            string header = "time_s,grid,planet,air_density,ambient_k,convection_coeff,"
                + "wind_speed,wind_ceiling,underground,depth_m,weather,weather_intensity,"
                + "weather_ambient_k,grid_mean_k,grid_peak_k";


            Dictionary<string, string> row = Row();
            DumpAudit.Result result = DumpAudit.Run(Write(header, row));


            DumpAudit.CheckResult decomposition = Check(result, "wind decomposes");
            Assert.True(decomposition.Skipped);
            Assert.False(decomposition.Failed);
            Assert.Equal(0, decomposition.Rows);
            Assert.Contains("wind_band_share", decomposition.Missing);

            Assert.False(Check(result, "convection is reported").Skipped);
            Assert.True(result.Passed);
        }

        [Fact]

        public void AnEmptyDumpIsReadWithoutCrashingAndClaimsNothing()
        {
            DumpAudit.Result result = DumpAudit.Run(Write(Header));

            Assert.Equal(0, result.Rows);
            Assert.True(result.Passed);

            foreach (DumpAudit.CheckResult check in result.Checks) Assert.Equal(0, check.Rows);
        }

        [Fact]

        public void TheReportNamesWhatItRead()
        {

            Dictionary<string, string> row = Row();
            row["wind_speed"] = "10";


            string path = Write(row);
            string report = DumpAudit.Report(DumpAudit.Run(path));

            Assert.Contains(Path.GetFileName(path), report);
            Assert.Contains("A DEFECT CHECK FAILED", report);
            Assert.Contains("wind decomposes", report);
        }

        [Fact]

        public void TheNewestDumpUnderAFolderIsTheOneAudited()
        {
            string folder = Path.Combine(Path.GetTempPath(),
                "thermal-dumps-" + Guid.NewGuid().ToString("n"));
            Directory.CreateDirectory(folder);

            try
            {
                string older = Path.Combine(folder, "Thermodynamics_Environment_20260101_000000.csv");
                string newer = Path.Combine(folder, "nested", "Thermodynamics_Environment_20260820_175059.csv");
                Directory.CreateDirectory(Path.GetDirectoryName(newer));

                File.WriteAllText(older, Header);
                File.WriteAllText(newer, Header);
                File.SetLastWriteTimeUtc(older, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                File.SetLastWriteTimeUtc(newer, new DateTime(2026, 8, 20, 17, 50, 59, DateTimeKind.Utc));

                Assert.Equal(newer, DumpAudit.Newest(folder));

                Assert.Equal(older, DumpAudit.Newest(older));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [Fact]

        public void APathWithNoDumpInItAnswersNothingRatherThanThrowing()
        {
            Assert.Null(DumpAudit.Newest(null));
            Assert.Null(DumpAudit.Newest(""));
            Assert.Null(DumpAudit.Newest(Path.Combine(Path.GetTempPath(), "thermal-absent-" + Guid.NewGuid().ToString("n"))));
        }
    }
}
