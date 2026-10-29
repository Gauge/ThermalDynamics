using Thermodynamics.Core;
using VRageMath;

namespace Thermodynamics.Tests
{
    /// <summary>Two nodes retain private source values between slices and bound malformed public counts to the supplied buffer.</summary>
    public class HeatSourceSnapshotTests
    {
        private static ThermalSimulation TwoBlocks()
        {
            var settings=new ThermalSettings { EnableEnvironment=false, EnableSolarHeat=false,
                EnableWasteHeat=false, EnableDamage=false, EnableConduction=false,
                MaxSubsteps=1, MaxSubstepsPerBlock=0, HeatTimeScale=1f };
            settings.Derive();
            var simulation=new ThermalSimulation(settings,new GridModel(2.5f));
            var thermal=new BlockThermalProperties { Conductivity=0f,SpecificHeat=20f,Emissivity=.5f };
            var model=BlockModel.Solid("source snapshot",Vector3I.One,1f,thermal);
            for(int i=0;i<2;i++)
                simulation.AddBlock(new BlockInstance(model,new Vector3I(i*2,0,0),BlockOrientation.Identity),250f);
            simulation.RebuildAll();
            simulation.Solver.CollectDiagnostics=true;
            return simulation;
        }

        [Fact]
        public void ReusedSourceArrayCannotChangeAnInFlightStepButIsObservedByTheNextStep()
        {
            var whole=TwoBlocks(); var sliced=TwoBlocks();
            var sources=new[] { new HeatSourceState(Vector3.Up,100f) };
            var environment=EnvironmentState.Vacuum(2.7f);
            environment.HeatSources=sources; environment.HeatSourceCount=1;
            const float seconds=.0001f;
            whole.Solver.Step(seconds,environment);

            var solver=sliced.Solver;
            Assert.True(solver.BeginStep(seconds,environment));
            Assert.Equal(1,solver.LastSubsteps);
            Assert.Same(sources,solver.Environment.HeatSources);
            Assert.False(solver.AdvanceStep(2)); // Ordinary environment for both nodes.
            Assert.False(solver.AdvanceStep(1)); // One source/node pair.
            Assert.True(solver.Nodes[0].LastHeatSourceWatts>0f);
            Assert.Equal(0f,solver.Nodes[1].LastHeatSourceWatts);
            // The public environment remains the caller's state, never an alias
            // to the private array used by the suspended step.
            solver.Environment.HeatSources[0]=new HeatSourceState(Vector3.Up,200f);
            for(int advances=0;!solver.AdvanceStep(1);advances++) Assert.True(advances<32);
            AssertSameOutput(whole.Solver,solver);

            whole.Solver.Step(seconds,environment);
            solver.Step(seconds,environment);
            AssertSameOutput(whole.Solver,solver);

            // Direct Core hosts need not pass through EnvironmentSolver. Null,
            // negative and oversized counts must schedule only available data.
            int[] reported={int.MaxValue,-1,int.MaxValue};
            int[] valid={0,0,1};
            for(int sample=0;sample<reported.Length;sample++)
            {
                environment.HeatSources=sample==0 ? null : sources;
                environment.HeatSourceCount=reported[sample];
                var reference=environment; reference.HeatSourceCount=valid[sample];
                whole.Solver.Step(seconds,reference);
                Assert.True(solver.BeginStep(seconds,environment));
                Assert.Same(environment.HeatSources,solver.Environment.HeatSources);
                Assert.Equal(reported[sample],solver.Environment.HeatSourceCount);
                long spent=0;
                bool completed;
                int advances=0;
                do
                {
                    completed=solver.AdvanceStep(1);
                    spent+=solver.LastAdvanceWork;
                    Assert.True(advances++<32);
                } while(!completed);
                Assert.Equal(solver.StepWorkUnits,spent);
                AssertSameOutput(whole.Solver,solver);
            }
        }

        private static void AssertSameOutput(ThermalSolver expected,ThermalSolver actual)
        {
            Assert.Equal(expected.LastHeatGainWatts,actual.LastHeatGainWatts);
            Assert.Equal(expected.StepWorkUnits,actual.StepWorkUnits);
            for(int i=0;i<2;i++)
            {
                Assert.Equal(expected.Nodes[i].Temperature,actual.Nodes[i].Temperature);
                Assert.Equal(expected.Nodes[i].LastHeatSourceWatts,actual.Nodes[i].LastHeatSourceWatts);
            }
        }
    }
}
