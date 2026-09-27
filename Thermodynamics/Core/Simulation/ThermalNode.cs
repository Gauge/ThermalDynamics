using System;
using System.Collections.Generic;
using VRageMath;

namespace Thermodynamics.Core
{
    public class ThermalNode
    {
        public readonly BlockInstance Block;

        public int Index = -1;

        public float Temperature;

        public float ThermalMass { get; private set; }

        public float HeldCoolantCapacity
        {
            get { return heldCoolantCapacity; }
            set
            {
                float clamped = value > 0f ? value : 0f;
                if (clamped == heldCoolantCapacity) return;
                heldCoolantCapacity = clamped;
                RefreshThermalMass();
            }
        }

        private float heldCoolantCapacity;

        private long exposedFaces;

        private const int FaceBits = 10;

        private const long FaceMask = (1L << FaceBits) - 1L;

        public const int MaxExposedPerFace = (int)FaceMask;


        public int GetExposedFaces(int face)
        {
            return (int)((exposedFaces >> (face * FaceBits)) & FaceMask);
        }


        public void SetExposedFaces(int face, int count)
        {
            if (count < 0) count = 0;
            if (count > MaxExposedPerFace) count = MaxExposedPerFace;

            int shift = face * FaceBits;
            exposedFaces = (exposedFaces & ~(FaceMask << shift)) | ((long)count << shift);
        }

        public int TotalExposedFaces { get; private set; }

        public float ExposedArea { get; private set; }

        public float RadiationCoefficient { get; private set; }

        public float HeatGenerationWatts { get; private set; }

        public bool StateDirty = true;

        public bool PendingLinks;

        public int LinkCount;


        private NodeDiagnostics diagnostics;

        public NodeDiagnostics Diagnostics
        {
            get { return diagnostics; }
        }



        public float LastConductionWatts
        {
            get { return diagnostics == null ? 0f : diagnostics.Conduction; }
            set
            {
                if (diagnostics == null)
                {
                    if (value == 0f) return;


                    diagnostics = new NodeDiagnostics();
                }

                diagnostics.Conduction = value;
            }
        }

        public float LastRadiationWatts
        {
            get { return diagnostics == null ? 0f : diagnostics.Radiation; }
            set
            {
                if (diagnostics == null)
                {
                    if (value == 0f) return;


                    diagnostics = new NodeDiagnostics();
                }

                diagnostics.Radiation = value;
            }
        }

        public float LastConvectionWatts
        {
            get { return diagnostics == null ? 0f : diagnostics.Convection; }
            set
            {
                if (diagnostics == null)
                {
                    if (value == 0f) return;


                    diagnostics = new NodeDiagnostics();
                }

                diagnostics.Convection = value;
            }
        }

        public float LastSolarWatts
        {
            get { return diagnostics == null ? 0f : diagnostics.Solar; }
            set
            {
                if (diagnostics == null)
                {
                    if (value == 0f) return;


                    diagnostics = new NodeDiagnostics();
                }

                diagnostics.Solar = value;
            }
        }

        public float LastFrictionWatts
        {
            get { return diagnostics == null ? 0f : diagnostics.Friction; }
            set
            {
                if (diagnostics == null)
                {
                    if (value == 0f) return;


                    diagnostics = new NodeDiagnostics();
                }

                diagnostics.Friction = value;
            }
        }

        public DragProfile Drag
        {
            get { return Block.Drag; }
            set { Block.Drag = value; }
        }

        public float LastHeatSourceWatts
        {
            get { return diagnostics == null ? 0f : diagnostics.HeatSource; }
            set
            {
                if (diagnostics == null)
                {
                    if (value == 0f) return;


                    diagnostics = new NodeDiagnostics();
                }

                diagnostics.HeatSource = value;
            }
        }

        public float LastRoomWatts
        {
            get { return diagnostics == null ? 0f : diagnostics.Room; }
            set
            {
                if (diagnostics == null)
                {
                    if (value == 0f) return;


                    diagnostics = new NodeDiagnostics();
                }

                diagnostics.Room = value;
            }
        }

        public float LastDeltaTemperature;

        private readonly float cellFaceArea;

        public float HeatTimeScale
        {
            get { return heatTimeScale; }
            set
            {
                heatTimeScale = value > 0f ? value : 1f;
                RefreshThermalMass();
            }
        }

        private float heatTimeScale = 1f;


        public ThermalNode(BlockInstance block, float gridSize, float initialTemperature)
            : this(block, gridSize, initialTemperature, 1f)
        {
        }


        public ThermalNode(BlockInstance block, float gridSize, float initialTemperature, float heatTimeScale)
        {
            if (block == null) throw new ArgumentNullException("block");

            Block = block;
            Temperature = initialTemperature;
            this.heatTimeScale = heatTimeScale > 0f ? heatTimeScale : 1f;
            cellFaceArea = gridSize * gridSize * Math.Max(0f, block.Thermal.ExposedSurfaceMultiplier);

            RefreshThermalMass();
            RefreshExposure();
            RefreshHeatGeneration();
        }

        public BlockThermalProperties Thermal
        {
            get { return Block.Thermal; }
        }

        public float CellFaceArea
        {
            get { return cellFaceArea; }
        }


        public void RefreshThermalMass()
        {
            float mass = Math.Max(0f, Block.Mass);

            float capacity = (((Thermal.SpecificHeat * mass) + heldCoolantCapacity) / heatTimeScale);
            ThermalMass = Math.Max(ThermalConstants.MinimumThermalMass, capacity);
            StateDirty = true;
        }


        public void RefreshExposure()
        {
            int total = 0;
            for (int i = 0; i < Face.Count; i++)
            {

                total += GetExposedFaces(i);
            }

            SetTotalAndDerived(total);
        }


        public bool SetExposedFaces(int[] countsByFace)
        {
            if (countsByFace == null || countsByFace.Length < Face.Count)
            {
                throw new ArgumentException("countsByFace must have at least six entries");
            }

            long packed = 0L;
            int total = 0;

            for (int face = 0; face < Face.Count; face++)
            {
                int count = countsByFace[face];
                if (count < 0) count = 0;
                if (count > MaxExposedPerFace) count = MaxExposedPerFace;

                packed |= (long)count << (face * FaceBits);
                total += count;
            }

            if (exposedFaces == packed && TotalExposedFaces == total) return false;

            exposedFaces = packed;
            SetTotalAndDerived(total);
            return true;
        }


        private void SetTotalAndDerived(int total)
        {
            TotalExposedFaces = total;
            ExposedArea = total * cellFaceArea;
            RadiationCoefficient = Thermal.Emissivity * ThermalConstants.StefanBoltzmann * ExposedArea;
            StateDirty = true;
        }


        public void RefreshHeatGeneration()
        {
            float produced = Math.Max(0f, Block.PowerProducedWatts) * Thermal.ProducerWasteEnergy;
            float consumed = (Math.Max(0f, Block.PowerConsumedWatts) + Math.Max(0f, Block.ThrustWatts))
                * Thermal.ConsumerWasteEnergy;
            HeatGenerationWatts = produced + consumed + Math.Max(0f, Thermal.HeatSourceWatts);
            StateDirty = true;
        }

        public float Energy
        {
            get { return Temperature * ThermalMass; }
        }


        public override string ToString()
        {
            return Block.Name + " T=" + Temperature.ToString("n2") + "K";
        }
    }
}
