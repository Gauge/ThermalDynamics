using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using VRage.Scripting;
using VRage.Game;
using VRage.Game.Components;
using VRageRender.Messages;
using VRage.Utils;
if (args.Length != 1) throw new ArgumentException("Expected game Bin64 directory");
var compiler = MyScriptCompiler.Static;
var paths = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")).Split(Path.PathSeparator)
    .Concat(new[] {typeof(MyVoxelMaterialDefinition).Assembly.Location, typeof(MyTextureChange).Assembly.Location,
        typeof(VRageMath.Vector3).Assembly.Location, typeof(MyStringId).Assembly.Location,
        typeof(VRage.MyVRage).Assembly.Location}).Distinct().ToArray();
compiler.AddReferencedAssemblies(paths);
compiler.AddReferencedAssemblies(Directory.GetFiles(args[0], "*.dll").Where(x => !Path.GetFileName(x).StartsWith("System.") && !Path.GetFileName(x).StartsWith("Microsoft.") && !Path.GetFileName(x).Contains("XmlSerializers")).Where(x => { try { using var f = File.OpenRead(x); using var pe = new System.Reflection.PortableExecutable.PEReader(f); return pe.HasMetadata; } catch { return false; } }).ToArray());
new SpaceEngineers.Game.GameWhitelistBootstrap().InitIlChecker();

var type = typeof(MyScriptCompiler).Assembly.GetType("VRage.Scripting.Analyzers.WhitelistDiagnosticAnalyzer", true);
var analyzer = (DiagnosticAnalyzer)Activator.CreateInstance(type, BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,
    null, new object[]{compiler.Whitelist, MyWhitelistTarget.ModApi}, null);
var cases = new Dictionary<string,string> {
 ["extinguisher-projectile-hit"] = "public class Probe { public void Start() { Sandbox.ModAPI.MyAPIGateway.Projectiles.AddOnHitInterceptor(0, Hit); } public void Stop() { Sandbox.ModAPI.MyAPIGateway.Projectiles.RemoveOnHitInterceptor(Hit); } private void Hit(ref Sandbox.ModAPI.MyProjectileInfo projectile, ref Sandbox.ModAPI.MyProjectileHitInfo hit) { if (!Sandbox.ModAPI.MyAPIGateway.Multiplayer.IsServer) return; var ammo=projectile.ProjectileAmmoDefinition.Id; var weapon=projectile.WeaponDefinition.Id; var owner=projectile.OwnerEntity; var player=projectile.OwningPlayer; var origin=projectile.Origin; var range=projectile.MaxTrajectory; var entity=hit.HitEntity; var position=hit.HitPosition; var normal=hit.HitNormal; var grid=entity as VRage.Game.ModAPI.IMyCubeGrid; if (grid!=null) { var cell=grid.WorldToGridInteger(position); var block=grid.GetCubeBlock(cell); } } }",

 ["aero-profile-snapshot"] = "public class Probe { private object gate=new object(); public System.Collections.Generic.Dictionary<string,object> Read(VRageMath.Vector3[] directions,float[] damping) { lock(gate) { return new System.Collections.Generic.Dictionary<string,object> { { \"Schema\",1 }, { \"Directions\",(VRageMath.Vector3[])directions.Clone() }, { \"DampingMoments\",(float[])damping.Clone() } }; } } public void Bind(System.Collections.Generic.Dictionary<string,System.Delegate> api) { var read=api[\"GetGridAeroProfile\"] as System.Func<VRage.Game.ModAPI.IMyCubeGrid,System.Collections.Generic.Dictionary<string,object>>; var register=api[\"SetGridAeroProfileConsumer\"] as System.Func<VRage.Game.ModAPI.IMyCubeGrid,long,bool,bool>; } }",


 ["aero-grid-inputs"] = "public class Probe { public void Read(Sandbox.Game.Entities.MyCubeGrid g, Sandbox.Game.Entities.MyPlanet p) { var centre=g.Physics.CenterOfMassWorld; var local=VRageMath.Vector3D.Transform(centre,g.PositionComp.WorldMatrixNormalizedInv); var orientation=g.WorldMatrix.GetOrientation(); var density=p.GetAirDensity(centre); g.Physics.AddForce(VRage.Game.Components.MyPhysicsForceType.APPLY_WORLD_FORCE, VRageMath.Vector3.Zero, null, null); g.Physics.AddForce(VRage.Game.Components.MyPhysicsForceType.APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE, null, null, VRageMath.Vector3.Zero); } }",

 ["aero-physics-interface"] = "public class Probe { public void Read(VRage.Game.Components.MyPhysicsComponentBase p) { var centre=p.CenterOfMassWorld; var mass=p.Mass; var velocity=p.LinearVelocity; var spin=p.AngularVelocity; var live=p.Enabled && p.HasRigidBody && !p.IsStatic; p.AddForce(VRage.Game.Components.MyPhysicsForceType.APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE, velocity, centre, spin, null, true, false); } }",

 ["aero-physics-inertia"] = "public class Probe { public VRageMath.Matrix Read(VRage.Game.Components.MyPhysicsComponentBase p) { return p.RigidBody.InertiaTensor; } }",

 ["thermal-celestial"] = "public class Probe { public double Read(Sandbox.Game.Entities.MyPlanet p) { var sun=Sandbox.Game.MyVisualScriptLogicProvider.GetSunDirection(); var position=p.PositionComp.WorldMatrixRef.Translation; var up=p.WorldMatrix.Up; var radius=p.AverageRadius; return radius+sun.X+position.X+up.X; } }",

 ["thermal-local-frustum"] = "public class Probe { public bool Check(VRageMath.MatrixD grid, VRageMath.BoundingBoxD box) { var camera=Sandbox.ModAPI.MyAPIGateway.Session.Camera; var frustum=new VRageMath.BoundingFrustumD(grid*VRageMath.MatrixD.Invert(camera.WorldMatrix)*camera.ProjectionMatrix); return frustum.Contains(box)!=VRageMath.ContainmentType.Disjoint; } }",

 ["occlusion-interface"] = "public class Probe { public bool Read(Sandbox.Game.Entities.MyCubeGrid source, VRageMath.Vector3I position) { VRage.Game.ModAPI.IMyCubeGrid grid=source; var block=grid.GetCubeBlock(position); return block!=null && block.IsFullIntegrity && block.BuildLevelRatio>=1 && !block.HasDeformation && block.BlockDefinition.Context!=null && block.BlockDefinition.Context.IsBaseGame && block.BlockDefinition.Id.SubtypeName==\"LargeBlockArmorBlock\"; } }",

 ["occlusion-concrete-rejected"] = "public class Probe { public bool Read(Sandbox.Game.Entities.MyCubeGrid grid, VRageMath.Vector3I position) { var block=grid.GetCubeBlock(position); return block!=null && block.IsFullIntegrity && block.BuildLevelRatio>=1 && !block.HasDeformation; } }",


 ["voxel-base-builder"] = "public class Probe { public VRage.Game.MyVoxelMaterialDefinition Make(VRage.Game.MyVoxelMaterialDefinition source, VRage.Game.MyObjectBuilder_DefinitionBase builder) { var copy = new VRage.Game.MyVoxelMaterialDefinition(); copy.Index = source.Index; copy.Init(builder, source.Context); return copy; } }",

 ["voxel-xml-base"] = "public class Probe { public VRage.Game.MyVoxelMaterialDefinition Make(VRage.Game.MyVoxelMaterialDefinition source, string xml) { var data = Sandbox.ModAPI.MyAPIGateway.Utilities.SerializeFromXML<VRage.Game.MyObjectBuilder_Definitions>(xml); VRage.Game.MyObjectBuilder_DefinitionBase builder = data.VoxelMaterials[0]; var copy = new VRage.Game.MyVoxelMaterialDefinition(); copy.Index = source.Index; copy.Init(builder, source.Context); return copy; } }",


 ["model-binary-reader"] = "public class Probe { public void Read() { var reader = Sandbox.ModAPI.MyAPIGateway.Utilities.ReadBinaryFileInGameContent(\"Models/test.mwm\"); reader.ReadString(); reader.BaseStream.Position = 0; reader.Dispose(); } }",

 ["grid-render-data"] = "public class Probe { public void Read(Sandbox.Game.Entities.MyCubeGrid grid) { var data = ((Sandbox.Game.Components.MyRenderComponentCubeGrid)grid.Render).RenderData; } }",

 ["model-materials"] = "public class Probe { public void Read(VRage.Game.Models.MyModel model) { var meshes = model.GetMeshList(); } }",

 ["voxel-enumeration"] = "public class Probe { public void Read() { var materials = Sandbox.Definitions.MyDefinitionManager.Static.GetVoxelMaterialDefinitions(); } }",

 ["voxel-copy"] = "public class Probe { public void Save(VRage.Game.MyVoxelMaterialDefinition live, VRage.Game.MyVoxelMaterialDefinition saved) { saved.RenderParams = live.RenderParams; } public void Restore(VRage.Game.MyVoxelMaterialDefinition live, VRage.Game.MyVoxelMaterialDefinition saved) { live.RenderParams = saved.RenderParams; live.UpdateVoxelMaterial(); } }",

 ["voxel-direct"] = "public class Probe { public void Edit(VRage.Game.MyVoxelMaterialDefinition live) { live.RenderParams.TextureSets[0].ColorMetalY = \"test\"; } }",

 ["entity-override"] = "public class Probe { public void Apply(VRage.Game.Components.MyRenderComponentBase r, System.Collections.Generic.Dictionary<VRage.Utils.MyStringId, VRageRender.Messages.MyTextureChange> changes) { r.UpdateRenderTextureChanges(changes); } }",

 ["voxel-builder"] = "public class Probe { public VRage.Game.MyVoxelMaterialDefinition Make(VRage.Game.MyVoxelMaterialDefinition source, Medieval.ObjectBuilders.Definitions.MyObjectBuilder_Dx11VoxelMaterialDefinition builder) { var copy = new VRage.Game.MyVoxelMaterialDefinition(); copy.Index = source.Index; copy.Init(builder, source.Context); return copy; } }"
};
bool failed = false;
foreach(var pair in cases) {
 var compilation = CSharpCompilation.Create("Probe", new[]{ CSharpSyntaxTree.ParseText(pair.Value, new CSharpParseOptions(LanguageVersion.CSharp6))},
    ((System.Collections.Generic.List<MetadataReference>)typeof(MyScriptCompiler).GetField("m_metadataReferences", BindingFlags.Instance|BindingFlags.NonPublic).GetValue(compiler)), new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
 var errors = compilation.GetDiagnostics().Where(x=>x.Severity==DiagnosticSeverity.Error).ToArray();
 var diagnostics = await compilation.WithAnalyzers(ImmutableArray.Create(analyzer)).GetAnalyzerDiagnosticsAsync();
 Console.WriteLine(pair.Key + ": compiler="+errors.Length+", whitelist="+diagnostics.Length);
 foreach(var d in errors.Concat(diagnostics)) Console.WriteLine(d);
 bool rejected = pair.Key == "aero-physics-inertia" || pair.Key == "occlusion-concrete-rejected" || pair.Key == "voxel-direct" || pair.Key == "voxel-builder" || pair.Key == "model-materials" || pair.Key == "grid-render-data";
 if (errors.Length != 0 || diagnostics.Any(x => x.Id != "ProhibitedMemberRule")
     || (rejected ? diagnostics.Length == 0 : diagnostics.Length != 0)) failed = true;
}

return failed ? 1 : 0;
