using Draygo.BlockExtensionsAPI;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Drawing;
using VRage.Game;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;

namespace Thermodynamics
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class PlanetManager : MySessionComponentBase
    {
        public static readonly PlanetDefinition NullDef = new PlanetDefinition();

        public class Planet
        {
            public MyPlanet Entity;
            public Vector3D Position;
            public MyGravityProviderComponent GravityComponent;
            private PlanetDefinition definition = NullDef;
            internal Presentation.ThermalVisionClimateCache<Core.PlanetThermalProperties> SkyClimate;

            public PlanetDefinition Definition() 
            {
                if (definition == NullDef && Entity.Generator != null)
                {
                    PlanetDefinition read = PlanetDefinition.GetDefinition(Entity.Generator.Id);
                    if (read == null) return null;

                    definition = read;

                    MyLog.Default.Info($"[{Settings.Name}] updated planet definition: {Entity.DisplayName}"
                        + $" ({definition.Supplied})");
                }

                return definition == NullDef ? null : definition;
            }
        }

        private static List<Planet> Planets = new List<Planet>();

        public static void CopyPlanets(List<Planet> target) { target.Clear(); target.AddRange(Planets); }

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            Planets.Clear();
            MyAPIGateway.Entities.OnEntityAdd += AddPlanet;
            MyAPIGateway.Entities.OnEntityRemove += RemovePlanet;
        }

        protected override void UnloadData()
        {
            if (MyAPIGateway.Entities != null)
            {
                MyAPIGateway.Entities.OnEntityAdd -= AddPlanet;
                MyAPIGateway.Entities.OnEntityRemove -= RemovePlanet;
            }
            Planets.Clear();
            base.UnloadData();
        }

        private void AddPlanet(IMyEntity ent)
        {
            if (ent is MyPlanet)
            {
                MyPlanet entity = ent as MyPlanet;

                Planets.Add(new Planet()
                {
                    Entity = entity,
                    Position = entity.PositionComp.WorldMatrixRef.Translation,
                    GravityComponent = entity.Components.Get<MyGravityProviderComponent>(),
                });
            }
        }

        private void RemovePlanet(IMyEntity ent)
        {
            if (!(ent is MyPlanet)) return;
            for (int i = Planets.Count - 1; i >= 0; i--)
                if (Planets[i].Entity.EntityId == ent.EntityId) Planets.RemoveAt(i);
        }


        public static Planet GetClosestPlanet(Vector3D position) 
        {
            Planet current = null;
            double distance = double.MaxValue;
            for (int i = 0; i < Planets.Count; i++) 
            {
                Planet p = Planets[i];
                double d = (p.Position - position).LengthSquared();
                if (d < distance) 
                {
                    current = p;
                    distance = d;
                }
            }

            return current;
        }
    }
}
