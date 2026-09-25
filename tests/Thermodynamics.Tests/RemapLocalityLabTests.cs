using System.Collections.Generic;
using Thermodynamics.Harness;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The remap-locality lab is the instrument that decides the change-local remap candidate on
    /// redesign.md, so what is pinned is the instrument's honesty: the
    /// diff sees change where a mutation made one, sees a *lot* of change where a mutation opened
    /// a room — the case where a big answer is the right answer — and a full pass's visited count
    /// dwarfs the changed count for a skin change, which is the asymmetry the whole evaluation
    /// rests on. Every claim is on a hull the lab proves has compartments first (`E8`).
    /// </summary>
    public class RemapLocalityLabTests
    {
        private static List<RemapLocalityLab.Row> rows;


        private static List<RemapLocalityLab.Row> Rows()
        {
            if (rows == null) rows = RemapLocalityLab.Run(8000);
            return rows;
        }

        [Fact]

        public void EveryMutationChangesSomethingAndVisitsTheBox()
        {
            foreach (RemapLocalityLab.Row row in Rows())
            {
                Assert.True(row.CellsChanged > 0, row.Mutation + " changed nothing; the lab's own guard should have thrown");
                Assert.True(row.CellsVisited > row.BoxCells / 2,
                    row.Mutation + " visited " + row.CellsVisited + " cells of a " + row.BoxCells
                    + "-cell box; a full reflood walks the box and then some, so the instrument is not measuring a full pass");
            }
        }

        [Fact]

        public void ASkinChangeMovesAlmostNothingOfAFullPass()
        {

            RemapLocalityLab.Row skin = Find("skin add");
            Assert.True(skin.CellsChanged * 100 < skin.CellsVisited,
                "adding one skin block changed " + skin.CellsChanged + " cells of " + skin.CellsVisited
                + " visited; if this ratio is real the change-local candidate answers itself");
        }

        [Fact]

        public void RemovingARoomBoundaryMovesAtLeastTheRoom()
        {

            RemapLocalityLab.Row boundary = Find("room boundary remove");

            Assert.True(boundary.CellsChanged >= 2,
                "removing a room's boundary changed only " + boundary.CellsChanged + " cells");
            Assert.True(boundary.RoomsBefore > 0, "the fixture has no rooms, so the boundary mutation judged nothing");
        }

        [Fact]

        public void RoomRenumberingAloneReadsAsNoChange()
        {

            RemapLocalityLab.Row buried = Find("buried remove");
            Assert.True(buried.CellsChanged < 50,
                "removing one buried block read as " + buried.CellsChanged
                + " changed cells; the canonical room naming is letting renumbering read as change");
        }


        private static RemapLocalityLab.Row Find(string mutation)
        {
            foreach (RemapLocalityLab.Row row in Rows())
            {
                if (row.Mutation == mutation) return row;
            }
            throw new Xunit.Sdk.XunitException("no row for " + mutation);
        }
    }
}
