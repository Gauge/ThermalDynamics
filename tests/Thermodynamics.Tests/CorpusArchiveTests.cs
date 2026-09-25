using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Which entry of a legacy workshop archive is the blueprint.
    ///
    /// <para>
    /// A blueprint published before Steam's current UGC system arrives as a single
    /// <c>*_legacy.bin</c>, a zip holding the <c>bp.sbc</c> and a thumbnail. **Three of the 3,904 in
    /// the corpus have lost the front of their entry names** — <c>p.sbc</c>, <c>.sbc</c>,
    /// <c>humb.png</c> — and an exact test for <c>bp.sbc</c> skipped all three without a word, which
    /// is three blueprints the corpus has held and never read. See tools/corpus/README.md.
    /// </para>
    /// </summary>
    public class CorpusArchiveTests
    {
        [Theory]
        [InlineData("bp.sbc")]
        [InlineData("p.sbc")]
        [InlineData(".sbc")]
        [InlineData("BP.SBC")]

        public void EveryShapeOfBlueprintEntryIsFound(string name)
        {
            Assert.True(Blueprints.IsLegacyBlueprintEntry(name));
        }

        [Theory]
        [InlineData("thumb.png")]
        [InlineData("humb.png")]
        [InlineData("umb.png")]
        [InlineData("")]
        [InlineData(null)]

        public void NothingElseIsMistakenForOne(string name)
        {
            Assert.False(Blueprints.IsLegacyBlueprintEntry(name));
        }
    }
}
