using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Thermodynamics.Harness;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>Pins the failed-unload incident: injected faults must not skip later cleanup or disappear from reports.</summary>
    public class SessionCleanupTests
    {
        [Fact]

        public void FailedTelemetryCannotPreventLightsAndDefinitionsFromUnloading()
        {

            var visited = new List<string>();

            var faults = new List<int>();

            var resetFailure = new NullReferenceException("injected telemetry reset failure");

            var lightFailure = new InvalidOperationException("injected light cleanup failure");

            var errors = new List<Exception>();
            SessionCleanup.Run(new Action[]
            {
                () => visited.Add("report"),
                () => { throw resetFailure; },
                () => visited.Add("callbacks"),
                () => { throw lightFailure; },
                () => visited.Add("definitions"),
                () => visited.Add("base unload"),
            }, (i, error) => { faults.Add(i); errors.Add(error); });
            Assert.Equal(new[] { "report", "callbacks", "definitions", "base unload" }, visited);
            Assert.Equal(new[] { 1, 3 }, faults);
            Assert.Same(resetFailure, errors[0]);
            Assert.Same(lightFailure, errors[1]);
        }

        [Fact]

        public void CleanAndRepeatedCleanupKeepsOrderWithoutReportingFaults()
        {
            int value = 9;
            var steps = new Action[] { () => value = 0, () => value += 1 };
            Action<int, Exception> unexpected = (i, error) => Assert.Fail(error.ToString());
            SessionCleanup.Run(steps, unexpected);
            Assert.Equal(1, value);
            SessionCleanup.Run(steps, unexpected);
            Assert.Equal(1, value);
            SessionCleanup.Run(new Action[0], unexpected);
        }

        [Fact]

        public void ActualSessionUnloadRoutesAllStagesThroughTheFailureBoundary()
        {
            string path = Path.Combine(ShippedBlocks.RepoRoot(), "Thermodynamics/Session.cs");
            var root = CSharpSyntaxTree.ParseText(File.ReadAllText(path)).GetRoot();
            var unload = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Single(m => m.Identifier.Text == "UnloadData");
            var statement = Assert.IsType<ExpressionStatementSyntax>(Assert.Single(unload.Body.Statements));
            var call = Assert.IsType<InvocationExpressionSyntax>(statement.Expression);
            Assert.Equal("SessionCleanup.Run", call.Expression.ToString());
            var steps = Assert.IsType<ArrayCreationExpressionSyntax>(call.ArgumentList.Arguments[0].Expression);
            var bodies = steps.Initializer.Expressions.Select(e => e.ToString()).ToArray();
            Assert.Contains("Telemetry.Reset", bodies);
            Assert.Contains("PlanetProbes.Reset", bodies);
            Assert.Contains("ThermalHeatSourceDebug.Reset", bodies);
            Assert.Contains("ThermalGlow.Clear", bodies);
            Assert.Contains("ThermalGrid.ResetCues", bodies);
            Assert.Contains("ThermalCharacters.Reset", bodies);
            Assert.Contains("ThermalApi.Unregister", bodies);
            Assert.Contains("UnregisterCommands", bodies);
            Assert.Contains(bodies, b => b.Contains("Definitions.UnloadData()"));
            Assert.Contains(bodies, b => b.Contains("base.UnloadData()"));
        }
    }
}
