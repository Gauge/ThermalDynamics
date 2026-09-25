using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The collection for tests that must not share a machine: the opt-in corpus walks, and every
    /// test whose assertion is about wall-clock time.
    ///
    /// <para>
    /// **Four walks across thirty-one workers on thirty-two cores measured seventeen times slower
    /// than running them one at a time**, and it hid behind a 93 % CPU reading because the cores
    /// were busy thrashing each other's cache. Each walk is already internally parallel over
    /// thousands of blueprints, so two of them at once are two thread pools competing for the same
    /// memory bandwidth rather than two jobs sharing a machine.
    /// </para>
    ///
    /// <para>
    /// **The isolation used to be project-wide**, and that is what this exists to replace:
    /// `xunit.runner.json` set `maxParallelThreads: 1` for every class in the suite, so every run
    /// paid three times its duration for the isolation of walks that most runs never execute —
    /// 53.1 s serial against 16.7 s at eight threads, with the same passing count either way.
    /// Stated as `O4` in rules.md; the row that asked for this is
    /// backlog.md `F8`.
    /// </para>
    ///
    /// <para>
    /// **A class belongs here when it walks the corpus or measures wall-clock time**, and the
    /// second half is not theoretical: `StaggerTests` compares two cache regimes a few per cent
    /// apart and has its own noise guard, which fired on the first parallel burn-in. A timing
    /// assertion on a contended machine is measuring the other tests.
    ///
    /// <para>
    /// Everything else runs in parallel with everything else, which is what the suite's own
    /// fixtures are built for: each test builds its own grid and shares no mutable state with
    /// another. Measured on this repository's 32-core machine, the whole suite is **1 m 41 s at one
    /// worker, 38 s at eight and 1 m 47 s at thirty-two** — one per core is no faster than serial,
    /// for the same reason the walks were not, so `maxParallelThreads` is a modest fixed number
    /// rather than a core count.
    /// </para>
    /// </para>
    /// </summary>
    [CollectionDefinition("alone", DisableParallelization = true)]
    public class AloneCollection
    {
    }
}
