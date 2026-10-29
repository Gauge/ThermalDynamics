using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using Thermodynamics.Core;
using VRage.Game.ModAPI;
using VRageMath;

namespace Thermodynamics
{
    public static class ThermalBridges
    {
        private class Bridge
        {
            public ThermalBlock A;
            public ThermalBlock B;
            public float Conductance;
        }

        private static readonly List<Bridge> Bridges = new List<Bridge>();

        public static int Count
        {
            get { return Bridges.Count; }
        }

        public static void Rebuild(ThermalBlock baseBlock, IMyMechanicalConnectionBlock mechanical)
        {
            RemoveAllFor(baseBlock);

            if (mechanical == null || mechanical.Top == null) return;

            ThermalGrid topGrid = mechanical.Top.CubeGrid.GameLogic.GetAs<ThermalGrid>();
            if (topGrid == null) return;

            ThermalBlock top = topGrid.Get(mechanical.Top.SlimBlock.Min);
            if (top == null || top == baseBlock) return;

            float lattice = Math.Min(
                baseBlock.Grid.Model.GridSize,
                topGrid.Model.GridSize);

            float conductance = ConductionBuilder.Conductance(
                lattice, baseBlock.Instance, top.Instance, 1, Face.Axis(Face.Up));

            if (conductance <= 0f) return;

            Bridges.Add(new Bridge { A = baseBlock, B = top, Conductance = conductance });
        }

        public static void RemoveAllFor(ThermalBlock block)
        {
            for (int i = Bridges.Count - 1; i >= 0; i--)
            {
                if (Bridges[i].A == block || Bridges[i].B == block) Bridges.RemoveAt(i);
            }
        }

        public static void Clear()
        {
            Bridges.Clear();
        }

        public static void Update(float deltaSeconds)
        {
            if (Bridges.Count == 0 || deltaSeconds <= 0f) return;
            if (Settings.Instance == null || !Settings.Instance.EnableConduction) return;

            for (int i = Bridges.Count - 1; i >= 0; i--)
            {
                Bridge bridge = Bridges[i];

                ThermalNode a = LiveNode(bridge.A);
                ThermalNode b = LiveNode(bridge.B);
                if (a == null || b == null)
                {
                    Bridges.RemoveAt(i);
                    continue;
                }

                float difference = b.Temperature - a.Temperature;
                if (difference == 0f) continue;

                float watts = ThermalSolver.ClampExchange(
                    bridge.Conductance * difference, deltaSeconds, difference, a.ThermalMass, b.ThermalMass);

                float energy = watts * deltaSeconds;
                a.Temperature = Math.Max(ThermalConstants.MinimumTemperature, a.Temperature + (energy / a.ThermalMass));
                b.Temperature = Math.Max(ThermalConstants.MinimumTemperature, b.Temperature - (energy / b.ThermalMass));
            }
        }

        private static ThermalNode LiveNode(ThermalBlock bound)
        {
            if (bound == null || bound.SweepSlot < 0 || bound.Grid == null
                || bound.Grid.Grid == null || bound.Grid.Grid.MarkedForClose || bound.Grid.Grid.Closed) return null;
            return bound.Node;
        }
    }
}
