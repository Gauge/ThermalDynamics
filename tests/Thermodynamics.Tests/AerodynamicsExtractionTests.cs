using System;
using System.Collections.Generic;
using System.IO;
using Aerodynamics.Core;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;
namespace Thermodynamics.Tests
{
    /// <summary>Exercises the real Thermal API geometry serializer against the extracted mod's decoder and verifies standalone ownership.</summary>
    public class AerodynamicsExtractionTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ExactSizedGeometryCellsRetainLayoutAndCallerOwnership(bool empty)
        {
            var source = new GridModel(2.5f);
            if (!empty)
            {
                source.Add(BlockModel.Solid("one", Vector3I.One, 1f,
                    new BlockThermalProperties()), Vector3I.Zero);
                source.Add(BlockModel.Solid("four", new Vector3I(2, 2, 1), 4f,
                    new BlockThermalProperties()), new Vector3I(3, 0, 0));
            }
            var data = GridGeometrySnapshot.Copy(source, 42);
            var cells = (Vector3I[])data["Cells"];
            var starts = (int[])data["CellStarts"];
            Assert.Equal(empty ? new[] { 0 } : new[] { 0, 1, 5 }, starts);
            Assert.Equal(empty ? 0 : 5, cells.Length);
            Assert.Equal(42L, data["Revision"]);
            for (int i = 0; i < source.BlockCount; i++)
                for (int c = 0; c < source.Blocks[i].CellCount; c++)
                    Assert.Equal(source.Blocks[i].Cells[c], cells[starts[i] + c]);
            if (empty) return;
            cells[1] = new Vector3I(999);
            Assert.NotEqual(cells[1], source.Blocks[1].Cells[0]);
            var next = GridGeometrySnapshot.Copy(source, 43);
            Assert.Equal(source.Blocks[1].Cells[0], ((Vector3I[])next["Cells"])[1]);
        }

        [Fact]
        public void DetachedApiGeometryPreservesTheFlightProfile()
        {
            GridModel source=AeroFlightLab.Shuttlecock();
            var data=GridGeometrySnapshot.Copy(source,42);
            AeroGrid decoded=AeroGrid.Decode(data);
            Assert.Equal(42L,data["Revision"]);
            var expected=AeroProfile.Build(source,26);var actual=AeroProfile.Build(decoded,26);
            foreach(Vector3 air in new[]{new Vector3(2,3,70),new Vector3(-4,90,1)})
            {
                var a=AeroForces.Evaluate(expected,Matrix.Identity,air,Vector3.Zero,1.225f,expected.Mass,1f/60f);
                var b=AeroForces.Evaluate(actual,Matrix.Identity,air,Vector3.Zero,1.225f,actual.Mass,1f/60f);
                Assert.Equal(a.Force,b.Force);Assert.Equal(a.Restoring,b.Restoring);
            }
            Vector3I cell=decoded.Blocks[0].Cells[0];
            ((Vector3I[])data["Cells"])[0]=new Vector3I(999);
            ((float[])data["FaceWeights"])[0]=0;
            Assert.Equal(cell,decoded.Blocks[0].Cells[0]);
            Assert.NotEqual(0f,decoded.Blocks[0].Drag[0]);
            Assert.NotEqual(new Vector3I(999),source.Blocks[0].Cells[0]);
        }
        [Fact]
        public void MalformedApiGeometryIsRejected()
        {
            var data=GridGeometrySnapshot.Copy(AeroCalibrationLab.Box(2,2,2),1);
            ((int[])data["CellStarts"])[1]=-1;
            Assert.Throws<ArgumentException>(()=>AeroGrid.Decode(data));
        }
        [Fact]
        public void NoProviderMeansNoAerodynamicHeat()
        {
            var settings=new ThermalSettings { SurfaceHeating=null,EnableEnvironment=false,EnableSolarHeat=false,EnableConduction=false,EnableDamage=false };
            settings.Derive();var builder=GridBuilder.Large();builder.Place(Catalog.HeavyArmor(),Vector3I.Zero);
            var simulation=builder.BuildSimulation(settings,293.15f);simulation.Planet=PlanetThermalProperties.Default();
            simulation.StepExact(4,Worlds.Flight(1f,200f));Assert.Equal(0f,simulation.FrictionWatts);
        }
        [Fact]
        public void ProductionModsHaveNoAssemblyOrPrivateRuntimeDependency()
        {
            string root=ShippedBlocks.RepoRoot();
            string project=File.ReadAllText(Path.Combine(root,"../Aerodynamics/Aerodynamics.csproj"));
            Assert.DoesNotContain("ProjectReference",project);
            foreach(string file in Directory.GetFiles(Path.Combine(root,"../Aerodynamics/Source"),"*.cs",SearchOption.AllDirectories))
            {
                if(file.Contains("/obj/")||file.Contains("/bin/"))continue;
                string source=File.ReadAllText(file);Assert.DoesNotContain("using Thermodynamics",source);Assert.DoesNotContain("GetAs<ThermalGrid>",source);
            }
            Assert.False(Directory.Exists(Path.Combine(root,"Thermodynamics/Core/Aerodynamics")) && Directory.GetFiles(Path.Combine(root,"Thermodynamics/Core/Aerodynamics"),"*.cs").Length>0);
        }
    }
}
