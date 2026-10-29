using System;
using System.Collections.Generic;
using Thermodynamics.Core;
using VRage.ModAPI;
using VRageMath;

namespace Thermodynamics
{
    public static class ThermalHeatSources
    {
        public class HeatSource
        {
            public int Id;

            public IMyEntity Entity;

            public Vector3D Position;

            public float Watts;

            public float Range;

            public bool Alive = true;

            public Vector3D WorldPosition
            {
                get { return Entity == null ? Position : Entity.WorldMatrix.Translation; }
            }
        }

        private static readonly List<HeatSource> Sources = new List<HeatSource>();
        private static int nextId = 1;

        public static int Count
        {
            get { return Sources.Count; }
        }

        public static IList<HeatSource> All
        {
            get { return Sources; }
        }

        public static int Add(IMyEntity entity, float watts, float range)
        {
            if (entity == null || entity.MarkedForClose || entity.Closed) return 0;
            return Register(entity, Vector3D.Zero, watts, range);
        }

        public static int Add(Vector3D position, float watts, float range)
        {
            return Register(null, position, watts, range);
        }

        private static int Register(IMyEntity entity, Vector3D position, float watts, float range)
        {
            if (!(watts > 0f) || float.IsInfinity(watts)
                || !(range > 0f) || float.IsInfinity(range)
                || !HeatSourceMath.ValidPosition(position)) return 0;

            HeatSource source = new HeatSource();
            source.Id = nextId++;
            source.Entity = entity;
            source.Position = position;
            source.Watts = watts;
            source.Range = range;

            Sources.Add(source);
            return source.Id;
        }

        public static bool Update(int id, float watts)
        {
            if (float.IsNaN(watts) || float.IsInfinity(watts)) return false;
            for (int i = 0; i < Sources.Count; i++)
            {
                if (Sources[i].Id != id) continue;
                Sources[i].Watts = Math.Max(0f, watts);
                return true;
            }
            return false;
        }

        public static bool Remove(int id)
        {
            for (int i = 0; i < Sources.Count; i++)
            {
                if (Sources[i].Id != id) continue;
                Sources[i].Alive = false;
                Sources.RemoveAt(i);
                return true;
            }
            return false;
        }

        public static void Clear()
        {
            Sources.Clear();
        }

        public static int Sample(Vector3D gridCentre, ref MatrixD worldToLocal, ref HeatSourceState[] buffer)
        {
            if (Sources.Count == 0) return 0;

            int written = 0;

            for (int i = Sources.Count - 1; i >= 0; i--)
            {
                HeatSource source = Sources[i];

                if (source.Entity != null && (source.Entity.MarkedForClose || source.Entity.Closed))
                {
                    source.Alive = false;
                    Sources.RemoveAt(i);
                    continue;
                }

                if (source.Watts <= 0f) continue;

                Vector3D position = source.WorldPosition;
                float irradiance = HeatSourceMath.Irradiance(
                    position, source.Watts, source.Range, gridCentre);

                if (irradiance <= 0f) continue;

                Vector3 local = HeatSourceMath.Direction(
                    position, gridCentre, ref worldToLocal);

                if (buffer == null || written >= buffer.Length)
                {
                    HeatSourceState[] grown = new HeatSourceState[Math.Max(4, written * 2)];
                    if (buffer != null) Array.Copy(buffer, grown, written);
                    buffer = grown;
                }

                buffer[written] = new HeatSourceState(local, irradiance);
                written++;
            }

            return written;
        }
    }
}
