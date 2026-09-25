using Thermodynamics.Core;
using Xunit;

namespace Thermodynamics.Tests
{
    /// <summary>
    /// Whether a room holds air, decided from the game's answers rather than this model's.
    ///
    /// Every one of those answers can veto air and none can insist on it — not because the two
    /// mistakes are different sizes, which was the reason written down here and is measured false,
    /// but because the game owns pressurisation and this model has no standing to overrule it.
    /// **Silence is not one of those answers**, and treating it as one is what `C22` corrected.
    /// </summary>
    public class RoomPressureTests
    {
        [Fact]

        public void AWorldWithoutPressurisationHasNoAirAnywhere()
        {
            Assert.Equal(0f, RoomPressure.Level(false, true, 1f), 5);
        }

        [Fact]

        public void ARoomTheGameDoesNotCallSealedHoldsNothing()
        {
            Assert.Equal(0f, RoomPressure.Level(true, false, 1f), 5);
        }

        [Fact]

        public void ASealedRoomHoldsWhatItsVentReports()
        {
            Assert.Equal(1f, RoomPressure.Level(true, true, 1f), 5);
            Assert.Equal(0.4f, RoomPressure.Level(true, true, 0.4f), 5);
        }

        [Fact]

        public void ReportingEmptyEmptiesTheRoomAndReportingNothingDoesNot()
        {
            Assert.Equal(0f, RoomPressure.Level(true, true, 0f), 5);

            Assert.Equal(RoomPressure.AssumedWhenUnanswered,
                RoomPressure.Level(true, true, RoomPressure.NotReported), 5);
            Assert.True(RoomPressure.AssumedWhenUnanswered > 0f);
            Assert.True(RoomPressure.NotReported < 0f);
        }

        [Fact]

        public void AnUnansweredRoomIsStillEmptyWhereSomethingElseSaidNo()
        {
            Assert.Equal(0f, RoomPressure.Level(false, true, RoomPressure.NotReported), 5);
            Assert.Equal(0f, RoomPressure.Level(true, false, RoomPressure.NotReported), 5);
            Assert.Equal(0f, RoomPressure.Level(false, false, RoomPressure.NotReported), 5);
        }

        [Fact]

        public void AnImpossibleReadingIsClampedRatherThanTrusted()
        {
            Assert.Equal(1f, RoomPressure.Level(true, true, 4f), 5);

            Assert.Equal(RoomPressure.AssumedWhenUnanswered,
                RoomPressure.Level(true, true, -3f), 5);
        }
    }

    /// <summary>
    /// What pressure does to the air itself, once decided.
    /// </summary>
    public class RoomAirPressureTests
    {

        private static RoomAirNode Room(float pressure)
        {

            RoomAirNode air = new RoomAirNode();
            air.RoomIndex = 0;
            air.CellCount = 8;
            air.Volume = 8f * 2.5f * 2.5f * 2.5f;
            air.Temperature = 293.15f;
            air.Pressure = pressure;
            air.AirDensity = 1.225f;
            air.RefreshThermalMass();
            return air;
        }

        [Fact]

        public void AnEmptyRoomHasNoAirAndOnlyTheSolverFloorOfHeatCapacity()
        {

            RoomAirNode air = Room(0f);

            Assert.False(air.HasAir);
            Assert.Equal(0f, air.AirMass, 5);

            Assert.Equal(ThermalConstants.MinimumThermalMass, air.ThermalMass, 6);
        }

        [Fact]

        public void HalfPressureIsHalfTheAirAndHalfTheHeatCapacity()
        {

            RoomAirNode full = Room(1f);

            RoomAirNode half = Room(0.5f);

            Assert.True(half.HasAir);
            Assert.Equal(full.AirMass * 0.5f, half.AirMass, 3);
            Assert.Equal(full.ThermalMass * 0.5f, half.ThermalMass, 3);
        }
    

        [Fact]

        public void ASealedEmptyRoomIsNotADisagreement()
        {
            Assert.False(RoomPressure.Disagrees(false, false, 0f));
            Assert.False(RoomPressure.Disagrees(false, false, RoomPressure.OxygenPresent));
        }

        [Fact]

        public void AirInTheGameAndNoneHereIsADisagreement()
        {
            Assert.True(RoomPressure.Disagrees(false, false, 1f));
            Assert.True(RoomPressure.Disagrees(false, false, 0.5f));
        }

        [Fact]

        public void ARoomThisModelHasFilledIsNeverADisagreement()
        {
            Assert.False(RoomPressure.Disagrees(true, false, 1f));
        }

        [Fact]

        public void AVentedRoomIsEmptyOnPurpose()
        {
            Assert.False(RoomPressure.Disagrees(false, true, 1f));
        }

        [Fact]

        public void WhatCouldNotBeMeasuredIsNotAFault()
        {
            Assert.False(RoomPressure.Disagrees(false, false, RoomPressure.NotReported));
            Assert.False(RoomPressure.Disagrees(false, false, -1f));
        }
    

        [Fact]

        public void AnUnansweredSealedRoomNeedsTheVentsRead()
        {
            Assert.True(RoomPressure.NeedsVentFallback(true, true, RoomPressure.NotReported));
        }

        [Fact]

        public void ARoomTheGameAnsweredForNeedsNoVents()
        {
            Assert.False(RoomPressure.NeedsVentFallback(true, true, 1f));
            Assert.False(RoomPressure.NeedsVentFallback(true, true, 0f));
        }

        [Fact]

        public void ARoomTheGameDoesNotSealNeedsNoVentsBecauseItsAnswerIsAlreadyZero()
        {
            Assert.False(RoomPressure.NeedsVentFallback(true, false, RoomPressure.NotReported));

            Assert.Equal(0f, RoomPressure.Level(true, false, 1f));
        }

        [Fact]

        public void AWorldWithoutPressurisationNeedsNoVentsAtAll()
        {
            Assert.False(RoomPressure.NeedsVentFallback(false, true, RoomPressure.NotReported));
            Assert.Equal(0f, RoomPressure.Level(false, true, 1f));
        }

        [Fact]

        public void TheFallbackIsNeededExactlyWhenTheAnswerIsStillOpen()
        {
            float[] levels = { RoomPressure.NotReported, 0f, 0.5f, 1f };

            for (int w = 0; w < 2; w++)
            {
                for (int g = 0; g < 2; g++)
                {
                    for (int i = 0; i < levels.Length; i++)
                    {
                        bool world = w == 1;
                        bool sealedByGame = g == 1;
                        bool needs = RoomPressure.NeedsVentFallback(world, sealedByGame, levels[i]);

                        if (!needs)
                        {
                            Assert.Equal(
                                RoomPressure.Level(world, sealedByGame, levels[i]),
                                RoomPressure.Level(world, sealedByGame,
                                    levels[i] < 0f ? RoomPressure.NotReported : levels[i]));
                        }
                        else
                        {
                            Assert.True(world && sealedByGame && levels[i] < 0f);
                        }
                    }
                }
            }
        }
    }
}
