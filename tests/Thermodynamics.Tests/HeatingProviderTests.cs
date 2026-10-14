using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Exercises the shipped provider boundary without engine services: worker failures disable
    /// one registration and are handed off once; replacement and unload cannot lose new state.
    /// Successful callbacks preserve arguments/results and allocate nothing.
    /// </summary>
    public class HeatingProviderTests
    {
        private static Dictionary<string, Delegate> Methods(
            Func<float, float, float, float, float, float, float, float> heat = null,
            Func<float, bool> gate = null)
        {
            var result = new Dictionary<string, Delegate>();
            result["SurfaceHeating"] = heat ?? ((a, d, s, e, t, c, h) => a + d + s + e + t + c + h);
            if (gate != null) result["SurfaceHeatingGate"] = gate;
            return result;
        }

        private static float Heat(HeatingProviderRegistry registry)
        {
            return registry.Heat(1, 2, 3, 4, 5, 6, 7);
        }

        [Fact]
        public void RegistrationCopiesMethodsAndPreservesOwnershipAndCallbackArguments()
        {
            var registry = new HeatingProviderRegistry();
            var methods = Methods(gate: speed => speed == 37);
            Assert.False(registry.Register(0, methods));
            Assert.True(registry.Register(1, methods));
            methods.Clear();
            Assert.False(registry.Register(2, Methods()));
            Assert.False(registry.Register(2, null));
            Assert.False(registry.Register(1, new Dictionary<string, Delegate>()));
            Assert.False(registry.Active(36));
            Assert.True(registry.Active(37));
            Assert.Equal(28f, Heat(registry));
            Assert.True(registry.Register(1, null));
            Assert.Equal(0f, Heat(registry));
            Assert.True(registry.Register(2, Methods()));
            Assert.False(registry.Active(0));
            Assert.True(registry.Active(1));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GateAndHeatingFailuresDisableAllForwardingUntilTheMainThreadDrains(bool gate)
        {
            var registry = new HeatingProviderRegistry();
            var fault = new InvalidOperationException("provider fault");
            int calls = 0;
            registry.Register(1, gate
                ? Methods(gate: s => { calls++; throw fault; })
                : Methods((a, d, s, e, t, c, h) => { calls++; throw fault; }));
            if (gate) Assert.False(registry.Active(10));
            else Assert.Equal(0f, Heat(registry));
            Assert.False(registry.Active(10));
            Assert.Equal(0f, Heat(registry));
            Assert.Null(registry.Method<Delegate>("SurfaceHeating"));
            Assert.Equal(1, calls);
            Assert.True(registry.TryTakeFailure(out var context, out var error, out var unavailable));
            Assert.Equal(gate ? "ThermalApi.SurfaceHeatingGate" : "ThermalApi.SurfaceHeating", context);
            Assert.Same(fault, error);
            Assert.True(unavailable);
            Assert.False(registry.TryTakeFailure(out _, out _, out _));
            Assert.True(registry.Register(2, Methods()));
            Assert.Equal(28f, Heat(registry));
        }

        [Fact]
        public async Task SimultaneousWorkerFaultsProduceOneHandoff()
        {
            var registry = new HeatingProviderRegistry();
            using (var barrier = new Barrier(4))
            {
                registry.Register(1, Methods((a, d, s, e, t, c, h) =>
                {
                    barrier.SignalAndWait();
                    throw new InvalidOperationException("concurrent provider fault");
                }));
                var tasks = new Task<float>[4];
                for (int i = 0; i < tasks.Length; i++)
                    tasks[i] = Task.Factory.StartNew(() => Heat(registry), CancellationToken.None,
                        TaskCreationOptions.LongRunning, TaskScheduler.Default);
                float[] results = await Task.WhenAll(tasks);
                foreach (float result in results) Assert.Equal(0f, result);
            }
            Assert.True(registry.TryTakeFailure(out _, out _, out var unavailable));
            Assert.True(unavailable);
            Assert.False(registry.TryTakeFailure(out _, out _, out _));
        }

        [Fact]
        public void APendingOldFaultCannotRemoveOrAnnounceAReplacementAsUnavailable()
        {
            var registry = new HeatingProviderRegistry();
            registry.Register(1, Methods((a, d, s, e, t, c, h) => { throw new Exception("old"); }));
            Heat(registry);
            Assert.True(registry.Register(1, Methods()));
            Assert.True(registry.TryTakeFailure(out _, out _, out var unavailable));
            Assert.False(unavailable);
            Assert.Equal(28f, Heat(registry));
        }

        [Fact]
        public void ACallbackCanReplaceItselfBeforeThrowingWithoutDisablingItsSuccessor()
        {
            var registry = new HeatingProviderRegistry();
            registry.Register(1, Methods((a, d, s, e, t, c, h) =>
            {
                registry.Register(1, Methods());
                throw new Exception("retired");
            }));
            Assert.Equal(0f, Heat(registry));
            Assert.False(registry.TryTakeFailure(out _, out _, out _));
            Assert.Equal(28f, Heat(registry));
        }

        [Fact]
        public void UnloadDropsPendingFaultsAndReleasesOwnership()
        {
            var registry = new HeatingProviderRegistry();
            registry.Register(1, Methods(gate: s => { throw new Exception("pending"); }));
            registry.Active(1);
            registry.Clear();
            Assert.False(registry.TryTakeFailure(out _, out _, out _));
            Assert.False(registry.Active(1));
            Assert.True(registry.Register(2, Methods()));
        }

        [Fact]
        public void SuccessfulCallbacksAllocateNothing()
        {
            var registry = new HeatingProviderRegistry();
            registry.Register(1, Methods(gate: speed => speed > 0));
            Heat(registry);
            registry.Active(1);
            long before = GC.GetAllocatedBytesForCurrentThread();
            float sum = 0;
            for (int i = 0; i < 1000; i++)
                if (registry.Active(1)) sum += Heat(registry);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(28000f, sum);
            Assert.Equal(0, allocated);
        }
    }
}
