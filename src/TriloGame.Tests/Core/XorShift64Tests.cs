using TriloGame.Game.Shared.Utilities;

namespace TriloGame.Tests.Core;

public sealed class XorShift64Tests
{
    [Fact]
    public void Constructor_WithSeed_PreservesInitialStateUntilFirstNextCall()
    {
        var random = new XorShift64(33333UL);

        Assert.Equal(33333UL, random.GetState());

        var next = random.Next();

        Assert.Equal(36066124148337UL, next);
        Assert.Equal(next, random.GetState());
    }

    [Fact]
    public void Next_WithModulo_ReturnsTheModuloButKeepsTheFullProgressedState()
    {
        var random = new XorShift64(33333UL);

        var next = random.Next(100UL);

        Assert.Equal(37UL, next);
        Assert.Equal(36066124148337UL, random.GetState());
    }

    [Fact]
    public void Seed_ResetsTheStateWithoutAdvancingIt()
    {
        var random = new XorShift64(33333UL);
        random.Next();

        random.Seed(33333UL);

        Assert.Equal(33333UL, random.GetState());
        Assert.Equal(36066124148337UL, random.Next());
    }

    [Fact]
    public void ParameterlessConstructor_StartsWithANonZeroState()
    {
        var random = new XorShift64();

        Assert.NotEqual(0UL, random.GetState());
    }

    [Fact]
    public void FloatHelpers_StayInsideTheirRequestedRanges()
    {
        var random = new XorShift64(33333UL);

        for (var index = 0; index < 1024; index++)
        {
            var zeroToOne = random.NextFloat();
            var signed = random.NextSignedFloat();
            var zeroToMax = random.NextFloatTo(7.5d);

            Assert.True(zeroToOne >= 0d && zeroToOne < 1d);
            Assert.True(signed > -1d && signed < 1d);
            Assert.True(zeroToMax >= 0d && zeroToMax < 7.5d);
        }
    }

    [Fact]
    public void ZeroSeed_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new XorShift64(0UL));

        var random = new XorShift64(33333UL);
        Assert.Throws<ArgumentOutOfRangeException>(() => random.Seed(0UL));
    }
}
