// SPDX-License-Identifier: MIT
// SPDX-FileCopyrightText: 2026 Moritz Voss

using System.Text.Json;
using Godot;
using tweens.gd.Tests.Support;

namespace tweens.gd.Tests.Unit;

public class InterpolatorsTests
{
    [Theory]
    [InlineData(0, 10, 0.25f, 3)]
    [InlineData(0, 10, 0.75f, 8)]
    [InlineData(0, 10, 0.125f, 1)]
    [InlineData(0, -10, 0.25f, -3)]
    [InlineData(10, 0, 1.5f, -5)]
    [InlineData(int.MinValue, int.MaxValue, 0.5f, -1)]
    public void IntRoundsHalfAwayFromZero(int from, int to, float weight, int expected)
        => Assert.Equal(expected, Interpolators.Int(from, to, weight));

    [Fact]
    public void IntSaturatesOvershoot()
    {
        Assert.Equal(int.MaxValue, Interpolators.Int(0, int.MaxValue, 2));
        Assert.Equal(int.MinValue, Interpolators.Int(0, int.MinValue, 2));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void IntRejectsNonFiniteWeights(float weight)
        => Assert.Throws<ArgumentOutOfRangeException>(() => Interpolators.Int(0, 1, weight));

    [Fact]
    public void ScalarsAndVectorsInterpolateLinearlyAndExtrapolate()
    {
        Assert.Equal(2.5f, Interpolators.Float(0, 10, 0.25f));
        Assert.Equal(-5f, Interpolators.Float(0, 10, -0.5f));
        Assert.Equal(12.5, Interpolators.Double(10, 20, 0.25f));
        Assert.Equal(new Vector2(1, 2), Interpolators.Vector2(Vector2.Zero, new Vector2(2, 4), 0.5f));
        Assert.Equal(new Vector3(1, 2, 3), Interpolators.Vector3(Vector3.Zero, new Vector3(2, 4, 6), 0.5f));
        Assert.Equal(new Vector4(1, 2, 3, 4), Interpolators.Vector4(Vector4.Zero, new Vector4(2, 4, 6, 8), 0.5f));
        Assert.Equal(new Color(0.5f, 0.25f, 0, 1), Interpolators.Color(new Color(0, 0, 0, 1), new Color(1, 0.5f, 0, 1), 0.5f,
            ColorSpace.Srgb, AlphaMode.Straight));
        Assert.Equal(new Rect2(1, 2, 3, 4), Interpolators.Rect2(new Rect2(0, 0, 2, 4), new Rect2(2, 4, 4, 4), 0.5f));
    }

    [Fact]
    public void FiniteEndpointsNearOppositeLimitsInterpolateWithoutOverflow()
    {
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "conformance", "endpoints.json")));
        var test = data.RootElement.GetProperty("wide_interpolation");
        var single = test.GetProperty("float").GetSingle();
        var wide = test.GetProperty("double").GetDouble();
        foreach (var sample in test.GetProperty("weights").EnumerateArray())
        {
            var weight = sample.GetSingle();
            var expected = 2 * weight - 1;
            Values.AssertClose(expected, Interpolators.Float(-single, single, weight) / single);
            Values.AssertClose((double)expected, Interpolators.Double(-wide, wide, weight) / wide);
            Values.AssertClose(new Vector2(expected, 1), Interpolators.Vector2(new(-single, 1), new(single, 1), weight) / new Vector2(single, 1));
            Values.AssertClose(new Vector3(expected, 1, expected), Interpolators.Vector3(new(-single, 1, -single), new(single, 1, single), weight) / new Vector3(single, 1, single));
            Values.AssertClose(new Vector4(expected, 1, expected, 1), Interpolators.Vector4(new(-single, 1, -single, 1), new(single, 1, single, 1), weight) / new Vector4(single, 1, single, 1));
            var rect = Interpolators.Rect2(new(-single, 1, -single, 1), new(single, 1, single, 1), weight);
            Values.AssertClose(new Vector2(expected, 1), rect.Position / new Vector2(single, 1));
            Values.AssertClose(new Vector2(expected, 1), rect.Size / new Vector2(single, 1));
        }
    }

    [Fact]
    public void QuaternionsSlerpTheShortPathBetweenNormalizedEndpoints()
    {
        var from = new Quaternion(Vector3.Up, 0) * 3;
        var to = new Quaternion(Vector3.Up, 1) * 0.5f;
        var middle = Interpolators.Quaternion(from, to, 0.5f);
        Assert.True(middle.IsNormalized());
        Assert.True(middle.IsEqualApprox(new Quaternion(Vector3.Up, 0.5f)));
        Assert.True(Interpolators.Quaternion(from, -to, 0.5f).IsEqualApprox(middle));
    }

    [Fact]
    public void QuaternionsRejectZeroEndpoints()
    {
        Assert.Throws<ArgumentException>(() => Interpolators.Quaternion(default, Quaternion.Identity, 0.5f));
        Assert.Throws<ArgumentException>(() => Interpolators.Quaternion(Quaternion.Identity, default, 0.5f));
        Assert.Throws<ArgumentException>(() => Interpolators.Quaternion(new(float.MaxValue, 0, 0, 1), Quaternion.Identity, 0.5f));
        Assert.Throws<ArgumentException>(() => Interpolators.Quaternion(Quaternion.Identity, new(float.MaxValue, 0, 0, 1), 0.5f));
    }
}
