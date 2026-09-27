using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Harness
{
    /// <summary>Rated full-power thruster vectors in the saved grid frames; this is not a flight-feasibility scorer.</summary>
    public static class AeroPopulationInputs
    {
        public struct Thruster
        {
            public int Grid;
            public Vector3I Cell;
            public string Definition;
            public Vector3 Direction;
            public float Nominal;
            public float Effective;
        }

        public static List<Thruster> Read(Blueprints.Ship ship, float influence, bool atmosphere)
        {
            if (ship == null || ship.Grids.Count == 0 || !ship.IsVanilla)
                throw new ArgumentException("Flight inputs require a nonempty vanilla blueprint.", "ship");
            List<Thruster> result = new List<Thruster>();
            for (int i = 0; i < ship.Grids.Count; i++)
            {
                Blueprints.Grid grid = ship.Grids[i];
                if (grid.Frame == null || !grid.Frame.Valid)
                    throw new InvalidDataException("Grid " + i + ": " + (grid.Frame == null ? "missing frame" : grid.Frame.Error));
                foreach (BlockInstance block in grid.Builder.Grid.Blocks)
                {
                    GameBlocks.Definition definition;
                    if (!GameBlocks.ByModelName().TryGetValue(block.Name, out definition))
                        throw new InvalidDataException("No installed definition for " + block.Name);
                    if (definition.TypeId != "Thrust") continue;
                    Vector3 local = -(Vector3)block.Orientation.Rotate(Vector3I.Forward);
                    result.Add(new Thruster
                    {
                        Grid = i, Cell = block.Position, Definition = block.Name,
                        Direction = (Vector3)Vector3D.TransformNormal(local, grid.Frame.World),
                        Nominal = definition.ThrustNewtons,
                        Effective = definition.EffectiveThrust(influence, atmosphere)
                    });
                }
            }
            return result;
        }

        /// <summary>Machine-readable sea-level inputs for the independent directional-thrust optimizer.</summary>
        public static string Json(string path)
        {
            List<Blueprints.Ship> ships = Blueprints.Read(path);
            if (ships.Count != 1) throw new InvalidDataException("Expected one readable blueprint: " + path);
            Blueprints.Ship ship = ships[0];
            List<Thruster> thrust = Read(ship, 1f, true);
            string digest;
            using (SHA256 hash = SHA256.Create())
            using (Stream stream = File.OpenRead(path))
                digest = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            return JsonSerializer.Serialize(new
            {
                schema = 1, kind = "aero-population-inputs", blueprint = Path.GetFullPath(path),
                blueprint_sha256 = digest, harness_mvid = typeof(AeroPopulationInputs).Assembly.ManifestModule.ModuleVersionId,
                relative_air_density = 1f, atmosphere = true, grids = ship.Grids.Count,
                mass_kg = ship.Grids.Sum(g => g.Builder.Grid.Blocks.Sum(b => (double)b.Mass)),
                headings = Enumerable.Range(0, Face.Count).Select(face => new
                {
                    name = ShipLoad.DirectionName(face),
                    direction = Components(Vector3D.TransformNormal((Vector3D)Face.Offsets[face], ship.Grids[0].Frame.World))
                }),
                thrusters = thrust.Select(t => new
                {
                    grid = t.Grid, definition = t.Definition, direction = Components(t.Direction),
                    nominal_n = t.Nominal, effective_n = t.Effective
                })
            });
        }

        private static double[] Components(Vector3D v) { return new[] { v.X, v.Y, v.Z }; }

        public static string Report(string path)
        {
            List<Blueprints.Ship> ships = Blueprints.Read(path);
            if (ships.Count != 1) throw new InvalidDataException("Expected one readable blueprint: " + path);
            List<Thruster> thrust = Read(ships[0], 1f, true);
            StringBuilder text = new StringBuilder();
            text.AppendLine("Aerodynamic population INPUT AUDIT; no P1/P2 verdict");
            text.AppendLine("Blueprint: " + Path.GetFullPath(path));
            using (SHA256 hash = SHA256.Create())
            using (Stream stream = File.OpenRead(path))
                text.AppendLine("Blueprint SHA256: " + BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant());
            text.AppendLine("Harness MVID: " + typeof(AeroPopulationInputs).Assembly.ManifestModule.ModuleVersionId);
            text.AppendLine("Relative air density 1; atmosphere present; unlimited fuel/power; multiplier one.");
            text.AppendLine("World-space unit force directions; nominal/effective ratings in N; functional blocks assumed.");
            text.AppendLine("grid,cell_x,cell_y,cell_z,definition,direction_x,direction_y,direction_z,nominal_n,effective_n");
            foreach (Thruster t in thrust)
                text.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4},{5:R},{6:R},{7:R},{8:R},{9:R}",
                    t.Grid, t.Cell.X, t.Cell.Y, t.Cell.Z, t.Definition, t.Direction.X, t.Direction.Y, t.Direction.Z, t.Nominal, t.Effective));
            text.AppendLine("Grids inspected: " + ships[0].Grids.Count + "; thrusters: " + thrust.Count);
            return text.ToString();
        }
    }
}
