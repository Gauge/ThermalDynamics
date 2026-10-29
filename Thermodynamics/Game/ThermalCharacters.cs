using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using Thermodynamics.Core;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Interfaces;
using VRage.Utils;
using VRageMath;

namespace Thermodynamics
{
    public static class ThermalCharacters
    {

        private static readonly Dictionary<long, float> Interiors = new Dictionary<long, float>();

        private static readonly List<IMyPlayer> Players = new List<IMyPlayer>();

        public static int Overwhelmed { get; private set; }

        public static float HottestInterior { get; private set; }

        public static void Forget(long identityId)
        {
            Interiors.Remove(identityId);
        }

        public static void Reset()
        {
            Interiors.Clear();
            Players.Clear();
            Overwhelmed = 0;
            HottestInterior = 0f;
        }

        public static void Step(float seconds)
        {
            Overwhelmed = 0;
            HottestInterior = 0f;

            Settings settings = Settings.Instance;
            if (settings == null || !settings.EnableSuitDamage) return;
            if (MyAPIGateway.Session == null || !MyAPIGateway.Session.IsServer) return;
            if (MyAPIGateway.Players == null || seconds <= 0f) return;

            Players.Clear();
            try
            {
                MyAPIGateway.Players.GetPlayers(Players);
                ThermalSettings core = settings.AppliedCore;
                for (int i = 0; i < Players.Count; i++)
                {
                    Step(core, Players[i], seconds);
                }
            }
            finally
            {
                Players.Clear();
            }
        }

        private static void Step(ThermalSettings core, IMyPlayer player, float seconds)
        {
            if (player == null || core == null) return;

            IMyCharacter character = player.Character;
            if (character == null || character.IsDead)
            {
                if (player.IdentityId != 0L) Forget(player.IdentityId);
                return;
            }

            float roomKelvin;
            if (!RoomTemperature(character, out roomKelvin))
            {
                Forget(player.IdentityId);
                return;
            }

            float interior;
            if (!Interiors.TryGetValue(player.IdentityId, out interior))
            {
                interior = SuitThermal.ComfortKelvin;
            }

            IMyControllableEntity controllable = character as IMyControllableEntity;
            bool helmetOpen = controllable != null && !controllable.EnabledHelmet;

            SuitStepResult result = SuitThermal.Step(core, interior, roomKelvin, helmetOpen,
                character.SuitEnergyLevel > 0f, seconds);

            Interiors[player.IdentityId] = result.InteriorKelvin;

            if (result.Overwhelmed) Overwhelmed++;
            if (result.InteriorKelvin > HottestInterior) HottestInterior = result.InteriorKelvin;

            if (result.Damage <= 0f) return;

            IMyDestroyableObject destroyable = character as IMyDestroyableObject;
            if (destroyable != null) destroyable.DoDamage(result.Damage, ThermalGrid.ThermalDamage, true);
        }

        private static bool RoomTemperature(IMyCharacter character, out float kelvin)
        {
            kelvin = 0f;
            if (character == null) return false;

            Vector3D position = character.GetPosition();
            IList<ThermalGrid> grids = ThermalGrid.LiveGrids;

            for (int i = 0; i < grids.Count; i++)
            {
                ThermalGrid grid = grids[i];
                if (grid == null || grid.Grid == null || grid.Simulation == null) continue;
                if (grid.Grid.MarkedForClose) continue;
                if (grid.Grid.PositionComp.WorldAABB.Contains(position) == ContainmentType.Disjoint) continue;

                RoomAirNode air = grid.Simulation.GetRoomAir(
                    grid.Grid.WorldToGridInteger(position));
                if (air == null || air.Pressure <= 0f) continue;

                kelvin = air.Temperature;
                return true;
            }

            return false;
        }
    }
}
