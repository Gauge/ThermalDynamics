using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Thermodynamics.Core;
using Thermodynamics.Harness;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// A one-cell `BlockInstance` is built without walking its cells, and is held to the walk over
    /// every one of the twenty-four orientations, for a solid block, a partly mounted one, a door
    /// shut and a door open — the same cell, the same live bits, the same structural bits (`D8`).
    /// The uniform-bits short cut in the surface rotation is covered by the same comparison, since
    /// the solid block takes it and the partly mounted one cannot.
    /// </summary>
    public class BlockInstanceOneCellTests
    {

        private static IEnumerable<BlockModel> Models()
        {
            yield return Catalog.LightArmor();
            BlockModel partly = BlockModel.Solid("partly mounted", Vector3I.One, 100f, Catalog.LightArmor().Thermal);
            int state = CellSurface.SelfAirtightMask;
            state = CellSurface.WithSelfMount(state, 0, true);
            state = CellSurface.WithSelfMount(state, 2, true);
            partly.SetLocalSurface(Vector3I.Zero, state);
            yield return partly;
            yield return Catalog.SlideDoor();
        }

        [Fact]

        public void TheTwoSurfaceLayersShareOneArrayExactlyWhenTheyCannotDiffer()
        {

            BlockInstance armour = new BlockInstance(Catalog.LightArmor(), Vector3I.Zero, BlockOrientation.Identity);
            Assert.Same(armour.StructuralSurfaces, armour.SelfSurfaces);


            BlockInstance door = new BlockInstance(Catalog.SlideDoor(), Vector3I.Zero, BlockOrientation.Identity);
            Assert.True(door.HasStateDependentSealing);
            Assert.True(door.IsSealedByDoorState);
            Assert.Same(door.StructuralSurfaces, door.SelfSurfaces);

            int[] structuralBefore = door.StructuralSurfaces;
            int[] liveBefore = door.SelfSurfaces;
            door.IsSealedByDoorState = false;
            door.RefreshSurfaces();

            Assert.Same(structuralBefore, door.StructuralSurfaces);
            Assert.NotSame(liveBefore, door.SelfSurfaces);
            Assert.NotSame(door.StructuralSurfaces, door.SelfSurfaces);
            Assert.Equal(structuralBefore, door.StructuralSurfaces);
            Assert.NotEqual(liveBefore, door.SelfSurfaces);

            door.IsSealedByDoorState = true;
            door.RefreshSurfaces();
            Assert.Same(door.StructuralSurfaces, door.SelfSurfaces);
            Assert.Equal(liveBefore, door.SelfSurfaces);

            int[] armourBefore = armour.SelfSurfaces;
            armour.RefreshSurfaces();
            Assert.Equal(armourBefore, armour.SelfSurfaces);
            Assert.Same(armour.StructuralSurfaces, armour.SelfSurfaces);


            BlockInstance multi = new BlockInstance(Catalog.LightArmorCube(2), Vector3I.Zero, BlockOrientation.Identity);
            Assert.True(multi.Model.CellCount > 1,
                "the multi-cell case needs a block with more than one cell to say anything");
            int[] multiBefore = multi.StructuralSurfaces;
            multi.RefreshSurfaces();
            Assert.NotSame(multiBefore, multi.StructuralSurfaces);
            Assert.Equal(multiBefore, multi.StructuralSurfaces);
        }

        [Fact]

        public void NothingWritesThroughABlocksSurfaceArrays()
        {
            string root = ShippedBlocks.RepoRoot();
            string[] properties = { "SelfSurfaces", "StructuralSurfaces", "Cells" };

            List<string> writes = new List<string>();

            foreach (string folder in new[] { "Thermodynamics", "tests" })
            {
                foreach (string path in Directory.GetFiles(Path.Combine(root, folder), "*.cs",
                    SearchOption.AllDirectories))
                {
                    string relative = path.Substring(root.Length).TrimStart('/', '\\').Replace('\\', '/');
                    if (relative.Contains("/obj/") || relative.Contains("/bin/")) continue;

                    SyntaxNode tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot();

                    foreach (AssignmentExpressionSyntax assignment in tree.DescendantNodes()
                        .OfType<AssignmentExpressionSyntax>())
                    {
                        ElementAccessExpressionSyntax element =
                            assignment.Left as ElementAccessExpressionSyntax;
                        if (element == null) continue;

                        MemberAccessExpressionSyntax member =
                            element.Expression as MemberAccessExpressionSyntax;
                        if (member == null) continue;
                        if (Array.IndexOf(properties, member.Name.Identifier.Text) < 0) continue;

                        writes.Add(relative + ":"
                            + (assignment.GetLocation().GetLineSpan().StartLinePosition.Line + 1));
                    }
                }
            }

            Assert.True(writes.Count == 0,
                "these write through a block's surface or cell array, which one-cell blocks share"
                + " per model and orientation — the write would land on every other block of that"
                + " model and orientation in the session: "
                + string.Join(", ", writes.ToArray()));
        }

        [Fact]

        public void OneCellBlocksOfAModelAndOrientationShareOneSurfaceArray()
        {
            BlockModel model = Catalog.LightArmor();


            BlockInstance here = new BlockInstance(model, Vector3I.Zero, BlockOrientation.Identity);

            BlockInstance there = new BlockInstance(model, new Vector3I(9, 4, 7), BlockOrientation.Identity);

            Assert.Same(here.StructuralSurfaces, there.StructuralSurfaces);
            Assert.Equal(Vector3I.Zero, here.Cells[0]);
            Assert.Equal(new Vector3I(9, 4, 7), there.Cells[0]);
            Assert.NotSame(here.Cells, there.Cells);


            List<int[]> seen = new List<int[]>();
            int distinct = 0;
            foreach (BlockOrientation orientation in PipeFitter.AllOrientations())
            {

                BlockInstance block = new BlockInstance(model, Vector3I.Zero, orientation);

                BlockInstance twin = new BlockInstance(model, new Vector3I(3, 3, 3), orientation);

                Assert.Same(block.StructuralSurfaces, twin.StructuralSurfaces);

                bool known = false;
                for (int i = 0; i < seen.Count; i++)
                {
                    if (ReferenceEquals(seen[i], block.StructuralSurfaces)) known = true;
                }
                if (!known)
                {
                    seen.Add(block.StructuralSurfaces);
                    distinct++;
                }
            }

            Assert.True(distinct > 1,
                "every orientation was handed the same array, so the cache is keyed on nothing");
        }

        [Fact]

        public void AOneCellBlockIsBuiltAsTheCellWalkWouldBuildIt()
        {
            int compared = 0;
            int differingBits = 0;
            foreach (BlockModel model in Models())
            {
                foreach (BlockOrientation orientation in PipeFitter.AllOrientations())
                {
                    foreach (bool shut in new[] { true, false })
                    {

                        Vector3I at = new Vector3I(-4, 7, 2);

                        BlockInstance fast = new BlockInstance(model, at, orientation);
                        fast.IsSealedByDoorState = shut;
                        fast.RefreshSurfaces();


                        BlockInstance walked = new BlockInstance(model, at, orientation);
                        walked.IsSealedByDoorState = shut;
                        walked.RefreshSurfaces();
                        walked.BuildGridSurfacesWalkingTheCells();

                        Assert.NotSame(fast.StructuralSurfaces, walked.StructuralSurfaces);
                        Assert.NotSame(fast.SelfSurfaces, walked.SelfSurfaces);

                        Assert.Equal(1, fast.CellCount);
                        Assert.Equal(walked.Cells[0], fast.Cells[0]);
                        Assert.Equal(at, fast.Cells[0]);
                        Assert.True(walked.SelfSurfaces[0] == fast.SelfSurfaces[0],
                            model.Name + " " + orientation + (shut ? " shut" : " open") + ": live bits "
                            + fast.SelfSurfaces[0] + " by the short path and " + walked.SelfSurfaces[0] + " by the walk");
                        Assert.Equal(walked.StructuralSurfaces[0], fast.StructuralSurfaces[0]);
                        if (fast.SelfSurfaces[0] != fast.StructuralSurfaces[0]) differingBits++;
                        compared++;
                    }
                }
            }

            Assert.Equal(3 * 24 * 2, compared);
            Assert.True(differingBits > 0, "no case had a door open, so the live half was never exercised");
        }
    }
}
