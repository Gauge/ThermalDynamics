using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// **The boundary a fleet is solved in parallel across, checked as text.**
    ///
    /// <para>
    /// `D19`'s shape is *solve in parallel, apply on the game thread*, and what makes it safe is not
    /// the threading but the boundary: `ThermalGrid.SolveTick` touches nothing outside its own grid,
    /// and everything that reads or writes the game happens in `PrepareTick` or `PublishTick`. That
    /// is a property of the source rather than of a run — the harness cannot construct a game
    /// component at all, and a session is the only place a violation would show, as a crash on a
    /// worker thread that nobody can reproduce.
    /// </para>
    ///
    /// <para>
    /// So it is asserted the way this repository asserts the settings plumbing: by reading the file.
    /// A test that runs the parallel path is not possible here; a test that says *this method does
    /// not name the game* is, and it is the check that would have caught the mistake.
    /// </para>
    /// </summary>
    public class ParallelTickTests
    {

        private static string RepoRoot()
        {
            return Thermodynamics.Harness.ShippedBlocks.RepoRoot();
        }


        private static string Source(string file)
        {
            return File.ReadAllText(Path.Combine(
                RepoRoot(), "Thermodynamics", "Game", file));
        }


        private static string Body(string source, string signature)
        {
            int at = source.IndexOf(signature);
            Assert.True(at >= 0, "no method named " + signature);

            int open = source.IndexOf('{', at);
            Assert.True(open >= 0, signature + " has no body");

            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;

                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0) return source.Substring(open, i - open + 1);
                }
            }

            Assert.Fail(signature + " has no closing brace");
            return null;
        }


        private static string CodeOnly(string body)
        {
            body = Regex.Replace(body, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(body, @"//[^\n]*", " ");
        }

        private static readonly string[] Forbidden =
        {
            "MyAPIGateway", "Grid.", "Telemetry.FrameCost", "MyLog", "blocks[", "Terminal",
        };

        [Fact]

        public void TheSolveHalfNamesNothingThatBelongsToTheGame()
        {

            string body = CodeOnly(Body(Source("ThermalGridSimulation.cs"), "public void SolveTick()"));


            List<string> found = new List<string>();
            foreach (string name in Forbidden)
            {
                if (body.Contains(name)) found.Add(name);
            }

            Assert.True(found.Count == 0,
                "SolveTick names " + string.Join(", ", found.ToArray())
                + ", which a worker thread may not touch — move it into PrepareTick or PublishTick");
        }

        [Fact]

        public void TheSchedulerPreparesAndPublishesAroundTheFanOut()
        {

            string scheduler = CodeOnly(Source("ThermalGridScheduler.cs"));

            int prepare = scheduler.IndexOf("PrepareTick");
            int fanOut = scheduler.IndexOf("MyAPIGateway.Parallel");
            int publish = scheduler.IndexOf("PublishTick");

            Assert.True(prepare >= 0, "the scheduler no longer prepares a grid before solving it");
            Assert.True(fanOut >= 0, "the scheduler no longer fans the solve out");
            Assert.True(publish >= 0, "the scheduler no longer publishes after solving");

            Assert.True(prepare < fanOut && fanOut < publish,
                "the scheduler's three phases are out of order: a grid must be prepared on the game"
                + " thread, solved anywhere, and published on the game thread");
        }

        [Fact]

        public void TheParallelPathShipsOff()
        {
            string settings = File.ReadAllText(Path.Combine(
                RepoRoot(), "Thermodynamics", "Settings.cs"));

            Assert.Contains("public bool ParallelGrids = false;", settings);
        }
    }
}
