using System.Collections.Generic;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// The rule that decides whether a problem is recorded at all.
    ///
    /// The defect these were written against: <c>Telemetry.Exception</c> filed through the same
    /// gate as every measurement hook, and telemetry is off by default. Every <c>catch</c> in the
    /// simulation adapter — twenty-two of them, including the guard around <c>ThermalGrid.Tick</c>
    /// whose own comment says an exception named there is worth more than a crash dump — therefore
    /// discarded its exception in an ordinary world, wrote nothing to any file, and left the grid
    /// running in whatever state the throw abandoned it in. A player's crash was unrecoverable
    /// after the fact by construction.
    ///
    /// The distinction the registry holds is between an *observation*, which costs something on
    /// every healthy frame and is rightly opt-in, and a *fault*, which costs nothing until the mod
    /// has already failed.
    /// </summary>
    public class AnomalyRegistryTests
    {
        private const bool Collecting = true;
        private const bool NotCollecting = false;
        private const bool Fault = true;
        private const bool Observation = false;


        private static AnomalyRegistry Registry(int maxKinds = 64)
        {
            return new AnomalyRegistry(maxKinds);
        }

        [Fact]

        public void AFaultIsRecordedWhileCollectionIsOff()
        {

            AnomalyRegistry registry = Registry();

            registry.Record("exception in ThermalGrid.Tick", "NullReferenceException", Fault, NotCollecting, 12.5d);

            Assert.Equal(1, registry.Count);
            Assert.Single(registry.Faults());
        }

        [Fact]

        public void AnObservationIsDroppedWhileCollectionIsOff()
        {

            AnomalyRegistry registry = Registry();

            registry.Record("temperature is NaN", "grid / block", Observation, NotCollecting, 12.5d);

            Assert.Equal(0, registry.Count);
        }

        [Fact]

        public void AnObservationIsRecordedWhileCollectionIsOn()
        {

            AnomalyRegistry registry = Registry();

            registry.Record("temperature is NaN", "grid / block", Observation, Collecting, 12.5d);

            Assert.Equal(1, registry.Count);
            Assert.Empty(registry.Faults());
        }

        [Fact]

        public void OnlyTheFirstOccurrenceOfAFaultAsksToBeLogged()
        {

            AnomalyRegistry registry = Registry();

            Assert.True(registry.Record("exception in Tick", "first", Fault, NotCollecting, 1d));
            Assert.False(registry.Record("exception in Tick", "second", Fault, NotCollecting, 2d));
            Assert.False(registry.Record("exception in Tick", "third", Fault, NotCollecting, 3d));

            AnomalyRecord record = registry.Faults()[0];
            Assert.Equal(3L, record.Count);
        }

        [Fact]

        public void AnObservationNeverAsksToBeLogged()
        {

            AnomalyRegistry registry = Registry();

            Assert.False(registry.Record("temperature is NaN", "first", Observation, Collecting, 1d));
        }

        [Fact]

        public void TheFirstAndLastExampleOfAKindAreBothKept()
        {

            AnomalyRegistry registry = Registry();

            registry.Record("exception in Tick", "first", Fault, NotCollecting, 1d);
            registry.Record("exception in Tick", "middle", Fault, NotCollecting, 2d);
            registry.Record("exception in Tick", "last", Fault, NotCollecting, 9d);

            AnomalyRecord record = registry.Faults()[0];

            Assert.Equal("first", record.FirstExample);
            Assert.Equal("last", record.LastExample);
            Assert.Equal(1d, record.FirstSeconds);
            Assert.Equal(9d, record.LastSeconds);
        }

        [Fact]

        public void TheRegistryIsBoundedAndSaysWhatItDropped()
        {

            AnomalyRegistry registry = Registry(2);

            registry.Record("a", "x", Fault, NotCollecting, 1d);
            registry.Record("b", "x", Fault, NotCollecting, 1d);
            registry.Record("c", "x", Fault, NotCollecting, 1d);
            registry.Record("d", "x", Observation, Collecting, 1d);

            Assert.Equal(2, registry.Count);
            Assert.Equal(2L, registry.KindsDropped);
        }

        [Fact]

        public void AKindSeenAsBothIsTreatedAsAFault()
        {

            AnomalyRegistry registry = Registry();

            registry.Record("mixed", "observation", Observation, Collecting, 1d);
            registry.Record("mixed", "fault", Fault, Collecting, 2d);

            Assert.Single(registry.Faults());
        }

        [Fact]

        public void ThereIsNoSummaryWhenNothingFailed()
        {

            AnomalyRegistry registry = Registry();

            registry.Record("temperature is NaN", "x", Observation, Collecting, 1d);

            Assert.Null(registry.FaultSummary(Collecting));
        }

        [Fact]

        public void TheSummaryCarriesEveryFaultItsCountAndHowToGetMore()
        {

            AnomalyRegistry registry = Registry();

            for (int i = 0; i < 7; i++)
            {
                registry.Record("exception in ThermalGrid.Tick", "boom", Fault, NotCollecting, i);
            }
            registry.Record("exception in ThermalGrid.Save", "bang", Fault, NotCollecting, 20d);

            string summary = registry.FaultSummary(NotCollecting);

            Assert.Contains("2 fault kind(s)", summary);
            Assert.Contains("7x exception in ThermalGrid.Tick", summary);
            Assert.Contains("1x exception in ThermalGrid.Save", summary);
            Assert.Contains("EnableTelemetry", summary);
        }

        [Fact]

        public void TheSummaryDoesNotAskForTelemetryThatIsAlreadyOn()
        {

            AnomalyRegistry registry = Registry();
            registry.Record("exception in Tick", "boom", Fault, Collecting, 1d);

            Assert.DoesNotContain("EnableTelemetry", registry.FaultSummary(Collecting));
        }

        [Fact]

        public void FaultsAreSummarisedInTheOrderTheyFirstAppeared()
        {

            AnomalyRegistry registry = Registry();

            registry.Record("second", "x", Fault, NotCollecting, 50d);
            registry.Record("first", "x", Fault, NotCollecting, 10d);

            List<AnomalyRecord> faults = registry.Faults();

            Assert.Equal("first", faults[0].Kind);
            Assert.Equal("second", faults[1].Kind);
        }

        [Fact]

        public void ClearingDropsEverythingIncludingTheDropCount()
        {

            AnomalyRegistry registry = Registry(1);

            registry.Record("a", "x", Fault, NotCollecting, 1d);
            registry.Record("b", "x", Fault, NotCollecting, 1d);
            registry.Clear();

            Assert.Equal(0, registry.Count);
            Assert.Equal(0L, registry.KindsDropped);
            Assert.Null(registry.FaultSummary(NotCollecting));
        }
    }
}
