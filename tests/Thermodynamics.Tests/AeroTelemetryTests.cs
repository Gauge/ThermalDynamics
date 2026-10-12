using System.Globalization;
using Aerodynamics;
using VRageMath;

namespace Thermodynamics.Tests;

/// <summary>Checks the air-relative power diagnostic, bounded recording and portable CSV numbers.</summary>
[Trait("scope", "integration")]
public class AeroTelemetryTests
{
    [Fact]
    public void SignCheckUsesAirRelativeMotionIncludingTailwind()
    {
        Assert.False(AeroTelemetry.AddsAirRelativeEnergy(Vector3.Right * 20, Vector3.Right * 5));
        Assert.True(AeroTelemetry.AddsAirRelativeEnergy(Vector3.Left * 20, Vector3.Right * 5));
        Assert.False(AeroTelemetry.AddsAirRelativeEnergy(Vector3.Zero, Vector3.Zero));
    }

    [Fact]
    public void CaptureIsBoundedAndCsvUsesInvariantNumbers()
    {
        AeroTelemetry.Reset();
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            for (int i = 0; i < AeroTelemetry.Capacity + 3; i++)
                AeroTelemetry.Add(new AeroTelemetryRow { Status = "evaluated", Mass = 1.25f, Air = Vector3.Left, Drag = Vector3.Left * 2 });
            Assert.Equal(AeroTelemetry.Capacity, AeroTelemetry.Count);
            Assert.Equal(3, AeroTelemetry.Dropped);
            var lines = AeroTelemetry.Csv().Split('\n', System.StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(AeroTelemetry.Capacity + 1, lines.Length);
            Assert.Contains(",1.25,", lines[1]);
            Assert.Equal(lines[0].Split(',').Length, lines[1].Split(',').Length);
            var header = lines[0].TrimEnd('\r').Split(',');
            var row = lines[1].TrimEnd('\r').Split(',');
            int signColumn = System.Array.IndexOf(header, "drag_sign_error");
            Assert.True(signColumn >= 0);
            Assert.Equal("False", row[signColumn]);
        }
        finally { CultureInfo.CurrentCulture = culture; AeroTelemetry.Reset(); }
    }
}
