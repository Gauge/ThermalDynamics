using System.Collections.Generic;
using Thermodynamics.Core;
using VRage.Game.Components;
using VRageMath;

namespace Thermodynamics
{
    public partial class ThermalGrid
    {
        private const int AeroBuildBudget = 4096;
        private const int AeroCentreBudget = 256;
        private readonly object aeroLock = new object();
        private AeroRuntime aerodynamics;
        private long aeroInputRevision;

        public Vector3 AeroForceWorld { get; private set; }
        public Vector3 AeroRestoringWorld { get; private set; }
        public Vector3 AeroDampingWorld { get; private set; }
        public bool AeroGuarded { get; private set; }
        public bool AeroReady { get { return aerodynamics != null && aerodynamics.Ready; } }
        public int AeroBuildWork { get { return aerodynamics == null ? 0 : aerodynamics.BuildWork; } }
        public int AeroCentreWork { get { return aerodynamics == null ? 0 : aerodynamics.CentreWork; } }

        internal void InvalidateAerodynamics()
        {
            lock (aeroLock)
            {
                aeroInputRevision++;
                if (aerodynamics != null) aerodynamics.Reset();
            }
        }

        internal Dictionary<string, object> CopyAeroProfile()
        {
            lock (aeroLock) { return aerodynamics == null ? null : aerodynamics.CopyProfile(); }
        }

        internal bool SetAeroProfileConsumer(long consumer, bool enabled)
        {
            lock (aeroLock)
            {
                if (aerodynamics == null && enabled) aerodynamics = new AeroRuntime();
                if (aerodynamics != null) aerodynamics.SetConsumer(consumer, enabled);
                return true;
            }
        }

        private void ResetAerodynamics()
        {
            lock (aeroLock) { ResetAerodynamicsInternal(); }
        }

        private void ResetAerodynamicsInternal()
        {
            aerodynamics = null;
            AeroForceWorld = Vector3.Zero;
            AeroRestoringWorld = Vector3.Zero;
            AeroDampingWorld = Vector3.Zero;
            AeroGuarded = false;
        }

        private void TickAerodynamics()
        {
            lock (aeroLock) { TickAerodynamicsInternal(); }
        }

        private void TickAerodynamicsInternal()
        {
            AeroForceWorld = Vector3.Zero;
            AeroRestoringWorld = Vector3.Zero;
            AeroDampingWorld = Vector3.Zero;
            AeroGuarded = false;
            Settings settings = Settings.Instance;
            bool requested = aerodynamics != null && aerodynamics.Requested;
            if (settings == null || (!settings.EnableAeroForces && !requested))
            {
                if (aerodynamics != null) aerodynamics.Reset();
                return;
            }
            if (Grid == null || Grid.Physics == null
                || !Grid.Physics.Enabled || !Grid.Physics.HasRigidBody || (Grid.Physics.IsStatic && !requested))
            {
                if (aerodynamics != null) aerodynamics.Reset();
                return;
            }
            if (aerodynamics == null) aerodynamics = new AeroRuntime();
            Vector3D centreWorld = Grid.Physics.CenterOfMassWorld;
            Vector3 centre = (Vector3)Vector3D.Transform(centreWorld, Grid.PositionComp.WorldMatrixNormalizedInv);
            if (!aerodynamics.Prepare(Model, settings.EnableAeroForces, settings.AeroProfileDirections, aeroInputRevision,
                centre, AeroBuildBudget, AeroCentreBudget)) return;
            if (!settings.EnableAeroForces || Grid.Physics.IsStatic) return;

            PlanetManager.Planet planet = PlanetManager.GetClosestPlanet(centreWorld);
            if (planet == null || planet.Entity == null) return;
            float density = planet.Entity.GetAirDensity(centreWorld) * AeroForces.SeaLevelDensity;
            if (density <= 0f) return;
            Vector3 wind = settings.EnableWind ? LastSample.WindDirection * LastSample.WindSpeed : Vector3.Zero;
            Vector3 relative = wind - Grid.Physics.LinearVelocity;
            AeroWrench wrench = AeroForces.Evaluate(aerodynamics.Profile, Grid.WorldMatrix.GetOrientation(),
                relative, Grid.Physics.AngularVelocity, density, Grid.Physics.Mass, ThermalGridScheduler.FrameSeconds);
            AeroGuarded = wrench.Guarded;
            Vector3 torque = wrench.Restoring + wrench.Damping;

            // The engine's world-force path ignores torque; its world-angular path applies the tick
            // duration internally. Both arguments below are forces/moments, not pre-scaled impulses.
            if (wrench.Force.LengthSquared() > 0f)
            {
                Grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, wrench.Force, null, null);
                AeroForceWorld = wrench.Force;
            }
            if (torque.LengthSquared() > 0f)
                Grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE, null, null, torque);
            AeroRestoringWorld = wrench.Restoring;
            AeroDampingWorld = wrench.Damping;
        }
    }
}
