using System.Security.Cryptography;

namespace TriloGame.Game.Shared.Utilities;

public sealed class XorShift64
{
    private const double OneOverTwoPow53 = 1d / 9007199254740992d;
    private ulong _state;

    public XorShift64()
        : this(GenerateRandomSeed())
    {
    }

    public XorShift64(ulong seed)
    {
        Seed(seed);
    }

    public ulong Next(ulong? modulo = null)
    {
        var value = NextRaw();
        if (!modulo.HasValue)
        {
            return value;
        }

        if (modulo.Value == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(modulo), "Modulo must be greater than zero.");
        }

        return value % modulo.Value;
    }

    // Use the high 53 bits so the result fits cleanly inside a double mantissa.
    public double NextFloat()
    {
        return (double)(Next() >> 11) * OneOverTwoPow53;
    }

    // Offset the sampled mantissa by half a step so the signed range stays open at both ends.
    public double NextSignedFloat()
    {
        return ((((double)(Next() >> 11)) + 0.5d) * (2d * OneOverTwoPow53)) - 1d;
    }

    public double NextFloatTo(double max)
    {
        if (double.IsNaN(max) || double.IsInfinity(max) || max < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(max), "Max must be a finite non-negative number.");
        }

        return NextFloat() * max;
    }

    public void Seed(ulong seed)
    {
        if (seed == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(seed), "XorShift64 requires a non-zero seed.");
        }

        _state = seed;
    }

    public ulong GetState() => _state;

    // Advance the generator with the exact xorshift64 step sequence requested by the project.
    private ulong NextRaw()
    {
        var x = _state;
        x ^= x << 13;
        x ^= x >> 7;
        x ^= x << 17;
        _state = x;
        return x;
    }

    private static ulong GenerateRandomSeed()
    {
        Span<byte> bytes = stackalloc byte[sizeof(ulong)];
        ulong seed;
        do
        {
            RandomNumberGenerator.Fill(bytes);
            seed = BitConverter.ToUInt64(bytes);
        }
        while (seed == 0);

        return seed;
    }
}
