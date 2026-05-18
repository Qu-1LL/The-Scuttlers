namespace TriloGame.Game.Shared.Utilities;

public static class PerlinNoise
{
    private static readonly int[] Permutation =
    [
        151, 160, 137, 91, 90, 15, 131, 13, 201, 95, 96, 53, 194, 233, 7, 225,
        140, 36, 103, 30, 69, 142, 8, 99, 37, 240, 21, 10, 23, 190, 6, 148,
        247, 120, 234, 75, 0, 26, 197, 62, 94, 252, 219, 203, 117, 35, 11, 32,
        57, 177, 33, 88, 237, 149, 56, 87, 174, 20, 125, 136, 171, 168, 68, 175,
        74, 165, 71, 134, 139, 48, 27, 166, 77, 146, 158, 231, 83, 111, 229, 122,
        60, 211, 133, 230, 220, 105, 92, 41, 55, 46, 245, 40, 244, 102, 143, 54,
        65, 25, 63, 161, 1, 216, 80, 73, 209, 76, 132, 187, 208, 89, 18, 169,
        200, 196, 135, 130, 116, 188, 159, 86, 164, 100, 109, 198, 173, 186, 3, 64,
        52, 217, 226, 250, 124, 123, 5, 202, 38, 147, 118, 126, 255, 82, 85, 212,
        207, 206, 59, 227, 47, 16, 58, 17, 182, 189, 28, 42, 223, 183, 170, 213,
        119, 248, 152, 2, 44, 154, 163, 70, 221, 153, 101, 155, 167, 43, 172, 9,
        129, 22, 39, 253, 19, 98, 108, 110, 79, 113, 224, 232, 178, 185, 112, 104,
        218, 246, 97, 228, 251, 34, 242, 193, 238, 210, 144, 12, 191, 179, 162, 241,
        81, 51, 145, 235, 249, 14, 239, 107, 49, 192, 214, 31, 181, 199, 106, 157,
        184, 84, 204, 176, 115, 121, 50, 45, 127, 4, 150, 254, 138, 236, 205, 93,
        222, 114, 67, 29, 24, 72, 243, 141, 128, 195, 78, 66, 215, 61, 156, 180
    ];

    private static readonly int[] Lookup = BuildLookup();

    // Sample two-dimensional Perlin noise at the requested coordinates.
    public static float Sample(float x, float y)
    {
        var xi = FastFloor(x) & 255;
        var yi = FastFloor(y) & 255;
        var xf = x - FastFloor(x);
        var yf = y - FastFloor(y);

        var u = Fade(xf);
        var v = Fade(yf);

        var aa = Lookup[Lookup[xi] + yi];
        var ab = Lookup[Lookup[xi] + yi + 1];
        var ba = Lookup[Lookup[xi + 1] + yi];
        var bb = Lookup[Lookup[xi + 1] + yi + 1];

        var x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1f, yf), u);
        var x2 = Lerp(Grad(ab, xf, yf - 1f), Grad(bb, xf - 1f, yf - 1f), u);

        return Lerp(x1, x2, v);
    }

    // Duplicate the permutation table so wrapped lookups can skip an extra modulo.
    private static int[] BuildLookup()
    {
        var lookup = new int[512];
        for (var index = 0; index < lookup.Length; index++)
        {
            lookup[index] = Permutation[index & 255];
        }

        return lookup;
    }

    // Floor negative values in the same way the original Perlin algorithm expects.
    private static int FastFloor(float value)
    {
        return value >= 0f ? (int)value : (int)value - 1;
    }

    // Smooth interpolation inputs with the classic quintic easing curve.
    private static float Fade(float t)
    {
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    // Blend between two samples using the provided interpolation factor.
    private static float Lerp(float a, float b, float t)
    {
        return a + (t * (b - a));
    }

    // Resolve the gradient contribution for one hashed lattice corner.
    private static float Grad(int hash, float x, float y)
    {
        return (hash & 3) switch
        {
            0 => x + y,
            1 => -x + y,
            2 => x - y,
            _ => -x - y
        };
    }
}
