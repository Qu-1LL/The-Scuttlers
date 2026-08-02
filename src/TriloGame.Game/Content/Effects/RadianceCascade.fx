#if OPENGL
#define SV_POSITION POSITION
#define VS_SHADERMODEL vs_3_0
#define PS_SHADERMODEL ps_3_0
#else
#define SV_POSITION SV_POSITION
#define VS_SHADERMODEL vs_4_0_level_9_1
#define PS_SHADERMODEL ps_4_0_level_9_1
#endif

Texture2D Texture;
Texture2D TileGridTexture;
Texture2D TileEmissionColorTexture;
Texture2D EntityOccluderTexture;
Texture2D PreviousCascadeTexture;
Texture2D LightingTexture;
Texture2D EmissiveTexture;
Texture2D WaterMaskTexture;
Texture2D WaterNoiseTexture;

sampler2D TextureSampler = sampler_state
{
    Texture = <Texture>;
    AddressU = Clamp;
    AddressV = Clamp;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = None;
};

sampler2D TileGridSampler = sampler_state
{
    Texture = <TileGridTexture>;
    AddressU = Clamp;
    AddressV = Clamp;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = None;
};

sampler2D TileEmissionColorSampler = sampler_state
{
    Texture = <TileEmissionColorTexture>;
    AddressU = Clamp;
    AddressV = Clamp;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = None;
};

// Linear, unlike the other occluder masks: creatures are much smaller than a tile, so point
// sampling made each ray either fully hit or fully miss and their shadows came out speckled.
// Filtering gives fractional coverage at the silhouette edge instead.
sampler2D EntityOccluderSampler = sampler_state
{
    Texture = <EntityOccluderTexture>;
    AddressU = Clamp;
    AddressV = Clamp;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = None;
};

sampler2D PreviousCascadeSampler = sampler_state
{
    Texture = <PreviousCascadeTexture>;
    AddressU = Clamp;
    AddressV = Clamp;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = None;
};

sampler2D LightingSampler = sampler_state
{
    Texture = <LightingTexture>;
    AddressU = Clamp;
    AddressV = Clamp;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = None;
};

sampler2D WaterMaskSampler = sampler_state
{
    Texture = <WaterMaskTexture>;
    AddressU = Clamp;
    AddressV = Clamp;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = None;
};

// WRAP, unlike every other sampler here, and that is the whole point of it: the water surface
// scrolls this texture continuously in two directions to warp its wavefronts, so it has to tile
// seamlessly or a hard seam would sweep across every lake once per wrap.
sampler2D WaterNoiseSampler = sampler_state
{
    Texture = <WaterNoiseTexture>;
    AddressU = Wrap;
    AddressV = Wrap;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = None;
};

sampler2D EmissiveSampler = sampler_state
{
    Texture = <EmissiveTexture>;
    AddressU = Clamp;
    AddressV = Clamp;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = None;
};

// Explicit level-zero sampling, used everywhere a sampler is read inside the ray march.
//
// Every sampler in this file is MipFilter = None, so level 0 is exactly the level tex2D would have
// picked - but tex2D DERIVES that level from screen-space gradients, and a gradient instruction
// inside a loop that can break forces the compiler to fully unroll the loop (warnings X3553 and
// X3570). SampleRaySegment is a 64-iteration cell walk wrapping SampleSpriteOcclusion's 5-iteration
// sample loop, so unrolling produced ~320 inlined sampling blocks. That cost roughly four minutes of
// content-build time per shader edit, a 375 KB compiled effect, and - because an unrolled loop has
// no early exit - every ray paid for all 64 cells on the GPU even after it had terminated on the
// first wall it hit.
//
// Asking for the level explicitly removes the gradient, and with it the compiler's reason to unroll.
// Anything sampled once per pixel outside a loop can keep using tex2D; it costs nothing there.
float4 SampleLevelZero(sampler2D textureSampler, float2 uv)
{
    return tex2Dlod(textureSampler, float4(uv, 0.0, 0.0));
}

float2 CascadeResolution;
float2 HigherCascadeResolution;
float2 LightingFieldResolution;

// ---- Cascade geometry, entirely in WORLD units --------------------------------------------------
//
// The probe lattice used to be described in "source pixels" - a light-buffer coordinate space that
// sat between world and screen - and every consumer had to convert into and out of it. That space is
// gone. A cascade is now fully described by where its probe grid starts in the world, how far apart
// its probes are in the world, and where its ray interval begins and ends in the world.
//
// Notably absent: any zoom factor. Interval lengths are authored as a world distance
// (OreLightSettings.LightReachTiles), so a ray reaches the same physical distance at every zoom
// without probe spacing having to be scaled to compensate - which is what coupled the two together
// and dragged the lattice to several times the size of the screen.
float2 ProbeWorldOrigin;
float2 ProbeWorldSpacing;
float2 HigherProbeWorldOrigin;
float2 HigherProbeWorldSpacing;
// How many probes the higher cascade actually has, per axis. The merge needs it to clamp its lookup
// into the real lattice - see CascadeMergePixel.
float2 HigherProbeCount;
float IntervalWorldOrigin;
float IntervalWorldLength;

// Entity occluder mask, described entirely in world terms: the world position of texel 0, how many
// mask texels one world unit spans, and the mask's size. Snapped to a whole mask texel on the CPU so
// a point-sampled silhouette edge cannot flip between blocking and clear as the camera slides a
// fraction of a texel. Its quantum is far finer than any cascade's, which is why it stays a separate
// quantity - see GetOccluderMaskLayout in RadianceCascadeRenderer.cs.
float2 MaskWorldOrigin;
float2 MaskTexelsPerWorld;
float2 MaskResolution;
float2 ViewportResolution;
float2 TileGridOrigin;
float2 TileGridResolution;
float2 CameraOrigin;
float2 CameraViewCenter;
float2 CameraShakeOffset;
float RayDimension;
float HigherRayDimension;
float TileSize;
// The camera's live scale. Used ONLY by the composite, which is inherently screen-space - it has a
// screen pixel and needs the world position under it. The ray march and the merge no longer reference
// it at all, which is the point of item 1: a probe's geometry cannot drift relative to the camera if
// it never mentions the camera.
float CameraScale;
// The usable light range in tiles: how far the merged cascades can see, capped by OreLightRangeTiles.
// A constant now, not a per-zoom computation, because the reach is authored as a world distance.
float LightRangeTiles;
// Fraction of the usable range spent tapering out at the far edge.
float LightFadeFraction;
// Near-field flattening: brightness kept at the emitter, and the fraction of the range over which it
// ramps back to full.
float NearFieldFloor;
float NearFieldRampFraction;
// Uniform gain on gathered radiance before the tonemap, and the weight of the result on top of
// Ambient.
float LightGain;
float LitContribution;
// How far behind a short caster (a creature) its shadow still reaches, in tiles.
float ShortShadowTiles;
// Rays start AT the probe being lit, and probes sit densely enough that one commonly lands inside a
// creature's own sprite. Without a bias, that probe's very first samples are still inside the
// creature's own occluder mask, at near-zero distance, so shortFade is ~1 and the creature shadows
// itself on every side - a dark patch that reads as glued to its back rather than cast away from it.
// Occluder samples closer than this are excluded from the short-caster (entity) test only; tall
// occluders keep blocking at any distance, since walls legitimately sit flush against a probe.
float ShortShadowMinTiles;
// Strength of the specular highlight on open water. 0 disables it entirely.
float WaterSheenStrength;
// (WallTransmission is gone. How much light a cell stops is now carried per-cell in the tile grid's
// R channel as an opacity, so it varies by material and by sprite coverage instead of being one
// global constant. See SampleRaySegment.)
// ---- Water surface ------------------------------------------------------------------------------
//
// ElapsedSeconds drives every animated term. The per-layer wave frequencies, directions and speeds
// are deliberately NOT uniforms - they are authored constants in WaterHeight, because they have to
// stay in a specific non-harmonic relationship to each other and exposing them individually invites
// that relationship being broken from the outside. What is exposed here is the overall character.
float ElapsedSeconds;
// Steepness of the surface: how far the normal tilts for a given slope. 0 is a mirror-flat sheet.
float WaterWaveStrength;
// Global multiplier on the animation clock. The layers keep their relative speeds.
float WaterWaveSpeed;
// How far the scrolling noise displaces the point the waves are evaluated at, in tiles. This is what
// bends the wavefronts off their straight lines.
float WaterNoiseWarpTiles;
// Refraction: how far the scene under the surface is displaced, in SCREEN uv.
float WaterDistortionUv;
// Reflection: how far the lighting-field lookup is displaced, in tiles.
float WaterReflectionTiles;
// Weight of the slope-driven diffuse term, and of the narrow specular highlight on top of it.
float WaterDiffuseStrength;
float WaterSpecularStrength;
// Tightness of that highlight. Large soft highlights read as gelatin rather than water.
float WaterSpecularPower;
// Quantisation of the diffuse term, in bands. 0 leaves it a smooth gradient.
float WaterLightBands;
// Local disturbance rings - see GetWaterDisturbance for how these turn a drawn falloff into rings.
float WaterRippleRingFrequency;
float WaterRippleRingSpeed;
// Fresnel: reflectivity on flat water, and the remap that turns the wave slopes into a 0..1 range.
float WaterFresnelFloor;
float WaterFresnelScale;
float WaterFresnelPower;
// The surface's own albedo at a trough turned away from the light and at a crest turned toward it.
// A multiplier on the water texture already in the scene, so these straddle 1.
float WaterAlbedoLow;
float WaterAlbedoHigh;
// How much of the bed the surface hides where it is reflecting hardest.
float WaterRefractionLoss;
float HasHigherCascade;
float Ambient;
float LightingEnabled;
float3 OreLightColor;

float4x4 MatrixTransform;

struct VertexShaderInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TextureCoordinates : TEXCOORD0;
};

struct VertexShaderOutput
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TextureCoordinates : TEXCOORD0;
};

VertexShaderOutput SpriteVertexShader(VertexShaderInput input)
{
    VertexShaderOutput output;
    output.Position = mul(input.Position, MatrixTransform);
    output.Color = input.Color;
    output.TextureCoordinates = input.TextureCoordinates;
    return output;
}

float2 GetCascadePixel(float2 uv)
{
    return floor(uv * CascadeResolution);
}

float2 GetRayOffset(float2 pixel, float rayDimension)
{
    return fmod(pixel, rayDimension);
}

float2 GetProbeOffset(float2 pixel, float rayDimension)
{
    return floor(pixel / rayDimension);
}

float2 GetRayDirection(float2 rayOffset, float rayDimension)
{
    float rayCount = rayDimension * rayDimension;
    float rayIndex = (rayOffset.y * rayDimension) + rayOffset.x;
    float angle = ((rayIndex + 0.5) / rayCount) * 6.28318530718;
    return float2(cos(angle), sin(angle));
}

// A probe's world position. This is the ONLY conversion between a probe index and a world position
// anywhere in the system, and it is one line because the CPU sends the origin and spacing rather
// than the terms needed to reconstruct them.
//
// It used to be SourcePixelToWorld(probeIndex * ProbeSpacing) + ProbeWorldOffset, where
// SourcePixelToWorld rebuilt the position from CameraOrigin, CameraViewCenter, CameraShakeOffset,
// CameraScale, ViewportResolution and SourceResolution - six uniforms - and UpdateProbeLattice on
// the CPU had to replicate that exact expression in order to snap it. Two copies of one conversion,
// in two languages, which had to agree to the last term. Essentially every defect in this file's
// history was a place where they stopped agreeing.
// Note there is no half-probe phase: probe i sits at origin + i * spacing, so every probe lands on an
// exact multiple of its cascade's spacing.
//
// That is what makes the grids NEST. Spacing is quantised to powers of two, so a coarser cascade (or
// a coarser zoom rung) has spacing 2^n times a finer one - and multiples of 2^n * s are a subset of
// multiples of s. Every coarse probe therefore sits exactly on a fine probe, and changing spacing
// adds or removes probes without moving any of the ones that remain. With a half-probe phase the
// coarse positions land on half-integer multiples instead, which share nothing with the fine grid, so
// every spacing change moved every probe.
float2 GetProbeWorld(float2 probeOffset)
{
    return ProbeWorldOrigin + (probeOffset * ProbeWorldSpacing);
}

// World position -> texel in the entity occluder mask. A direct world-space map: the mask has its
// own world origin and its own texel density, and nothing here goes through the screen.
//
// It used to be WorldToSourcePixel(world - MaskWorldOffset) - i.e. the mask WAS the screen, in both
// extent and addressing. That coupling is what made a ray's occlusion answer depend on where the
// camera was pointing, since rays reach ~26 tiles while the screen at high zoom is barely 4. The
// mask now covers the view plus ShortShadowTiles of margin, which is the furthest a short caster can
// possibly matter, so the bounds test below is unreachable for anything that could contribute.
float2 WorldToMaskPixel(float2 worldPixel)
{
    return (worldPixel - MaskWorldOrigin) * MaskTexelsPerWorld;
}

float IsInsideMask(float2 maskPixel)
{
    return maskPixel.x >= 0.0 && maskPixel.y >= 0.0 &&
           maskPixel.x < MaskResolution.x && maskPixel.y < MaskResolution.y
        ? 1.0
        : 0.0;
}

float4 SampleTileCell(float2 tileCoordinate)
{
    float2 localCoordinate = tileCoordinate - TileGridOrigin;
    if (localCoordinate.x < 0.0 || localCoordinate.y < 0.0 ||
        localCoordinate.x >= TileGridResolution.x || localCoordinate.y >= TileGridResolution.y)
    {
        return float4(1.0, 0.0, 0.0, 1.0);
    }

    return SampleLevelZero(TileGridSampler, (localCoordinate + 0.5) / TileGridResolution);
}

// Per-cell emission colour, so each deposit lights the cave in its own hue instead of every
// ore sharing one global tint.
float3 SampleTileEmissionColor(float2 tileCoordinate)
{
    float2 localCoordinate = tileCoordinate - TileGridOrigin;
    if (localCoordinate.x < 0.0 || localCoordinate.y < 0.0 ||
        localCoordinate.x >= TileGridResolution.x || localCoordinate.y >= TileGridResolution.y)
    {
        return float3(0.0, 0.0, 0.0);
    }

    return SampleLevelZero(TileEmissionColorSampler, (localCoordinate + 0.5) / TileGridResolution).rgb;
}

float2 WorldToTileCoordinate(float2 worldPixel)
{
    return floor((worldPixel + (TileSize * 0.5)) / TileSize);
}

// World position under a full-screen UV. Used to phase the water ripple against the world rather
// than the screen, so the wave stays put on the lake instead of sliding when the camera pans.
float2 ScreenUvToWorld(float2 screenUv)
{
    float2 screenPixel = screenUv * ViewportResolution;
    return CameraOrigin + ((screenPixel - CameraViewCenter - CameraShakeOffset) / CameraScale);
}

// Screen UV -> lighting-field UV: invert GetProbeWorld and divide by the field's resolution.
//
// Field texel i holds cascade 0's probe i, which sits at ProbeWorldOrigin + (i + 0.5) * spacing. So
// for a world position w the continuous probe index is (w - origin)/spacing - 0.5, and its texel
// centre is at ((probeIndex + 0.5)) / resolution - the two halves cancel, which is why there is no
// half-texel term here. The linear sampler interpolates between neighbouring probes from there.
//
// This replaced a screenUV -> world -> source-pixel round trip multiplied by a LightingFieldUvScale
// that itself had to divide out a zoom factor. Three chances to disagree with how the ray march
// placed the same probe; now there is one expression and it is the inverse of the other one.
float2 GetLightingFieldUv(float2 screenUv)
{
    float2 probeIndex = (ScreenUvToWorld(screenUv) - ProbeWorldOrigin) / ProbeWorldSpacing;
    return (probeIndex + 0.5) / LightingFieldResolution;
}

// ---- Water surface ------------------------------------------------------------------------------
//
// (GetWaterRipple is gone. It summed two sines per axis and used them both to warp the reflection
// lookup and, through a scalar band, to brighten and darken it directly.
//
// Two things were wrong with that and neither is a tuning problem. The field was SEPARABLE - x
// depended only on world y and vice versa - so its level sets are straight lines and the surface
// reads as a moving grid, which is the most recognisable "fake water" tell there is. And driving
// brightness from the wave HEIGHT produces travelling light and dark stripes, because height is
// literally what the waves are made of; real water catches light on its SLOPE, which is a different
// function entirely and is what the normal below exists to compute.)

float WaterWave(float2 p, float2 direction, float frequency, float speed, float t)
{
    return sin((dot(p, normalize(direction)) * frequency) + (t * speed));
}

// Surface height at a point that has ALREADY been warped by noise. Four layers spanning very
// different scales:
//
//   swell  0.55 rad/tile - an ~11 tile wavelength, and slow. This is what stops the surface reading
//                          as a bathtub: with only small ripples every scale of motion is the same
//                          and the eye reads the whole thing as vibration rather than as a body of
//                          water. It contributes mostly through the normal, not through visible
//                          displacement.
//   medium 2.10 and 3.30 - the body of the motion, crossing at an angle so neither dominates.
//   chop   5.70          - fine detail that breaks up the medium wavefronts.
//
// The frequencies and speeds are deliberately not related by simple ratios. Harmonically related
// layers re-align on a short period and the field visibly repeats; these share no common period on
// any timescale a player will sit and watch.
float WaterHeight(float2 p, float t)
{
    return (WaterWave(p, float2(0.7, 0.3), 0.55, 0.25, t) * 0.70)
         + (WaterWave(p, float2(1.0, 0.25), 2.10, 1.20, t) * 0.45)
         + (WaterWave(p, float2(-0.3, 1.0), 3.30, 1.70, t) * 0.25)
         + (WaterWave(p, float2(0.8, -0.6), 5.70, 2.30, t) * 0.12);
}

// Two scrolling noise samples at unrelated scales, drifting in different directions, used to
// displace the position the waves are evaluated at.
//
// Pure sines have perfectly straight wavefronts, and once the layering above is right that is the
// strongest remaining tell. Warping the DOMAIN bends them without touching their amplitude, which is
// why this perturbs position rather than height.
//
// Evaluated once per pixel and reused across the gradient taps below. Re-evaluating it at each tap
// would be marginally more correct and three times the cost, and the difference cannot be seen: the
// warp varies over whole tiles while the gradient epsilon is a twentieth of one.
// The SCALES are the thing to be careful with here, and getting them wrong is what made lakes look
// like a repeating pattern. The texture wraps once per 1/scale tiles, so at the 0.12 and 0.31 this
// first used it repeated every 8.3 and 3.2 tiles - well inside a single lake, and with the albedo
// band driven off it that repeat was plainly visible. At 0.037 and 0.0911 the periods are 27 and 11
// tiles, and because their ratio is not a simple fraction the SUM does not line up again for far
// longer than either alone.
float2 GetWaterNoiseWarp(float2 tiles, float t)
{
    float a = SampleLevelZero(WaterNoiseSampler, (tiles * 0.0370) + float2(t * 0.015, t * 0.008)).r;
    float b = SampleLevelZero(WaterNoiseSampler, (tiles * 0.0911) + float2(-t * 0.020, t * 0.012)).r;
    return (((a * 0.7) + (b * 0.3)) - 0.5) * WaterNoiseWarpTiles;
}

// Local disturbance: concentric rings radiating from whatever is moving through the water.
//
// The mask's R channel carries a radial falloff drawn once per source on the CPU (see
// DrawWaterDisturbanceLayer), so its VALUE is a monotonic function of distance from that source - 1
// at the centre, 0 at the rim. Feeding that straight into a sine therefore produces rings without
// the shader ever being told where the centre is, which is what lets any number of sources exist at
// once instead of however many a uniform array could hold.
//
// The squared envelope makes the rings die out well inside the drawn disc, so there is no visible
// circular edge where the sprite stops.
float GetWaterDisturbance(float disturbance, float t)
{
    float ring = sin((disturbance * WaterRippleRingFrequency) - (t * WaterRippleRingSpeed));
    return ring * disturbance * disturbance;
}

// Surface normal, by differencing the height field in TILE space.
//
// The epsilon is a world distance rather than a pixel or UV step for the same reason the waves are
// authored per tile: a screen-space epsilon would change the measured slope with the zoom, so the
// water would look rougher or smoother depending on how close the camera was.
//
// Dividing by the epsilon converts the difference into a true slope, which is what makes
// WaterWaveStrength mean "how steep" independently of the sampling distance chosen here.
float3 GetWaterNormal(float2 tiles, float2 screenUv, float t)
{
    const float epsilon = 0.05;
    float2 p = tiles + GetWaterNoiseWarp(tiles, t);

    // The same epsilon as a screen offset, so the disturbance rings - which live in screen space,
    // in the mask - are differentiated over the same distance as the waves and cannot end up with a
    // slope on a different scale from everything around them.
    float2 screenEpsilon = (epsilon * TileSize * CameraScale) / max(float2(1.0, 1.0), ViewportResolution);

    float height = WaterHeight(p, t)
        + GetWaterDisturbance(SampleLevelZero(WaterMaskSampler, screenUv).r, t);
    float heightX = WaterHeight(p + float2(epsilon, 0.0), t)
        + GetWaterDisturbance(SampleLevelZero(WaterMaskSampler, screenUv + float2(screenEpsilon.x, 0.0)).r, t);
    float heightY = WaterHeight(p + float2(0.0, epsilon), t)
        + GetWaterDisturbance(SampleLevelZero(WaterMaskSampler, screenUv + float2(0.0, screenEpsilon.y)).r, t);

    float2 slope = float2(heightX - height, heightY - height) / epsilon;
    return normalize(float3(-slope * WaterWaveStrength, 1.0));
}

// Which way light is arriving, taken as the gradient of the lighting field.
//
// A constant light vector is the usual choice and it is wrong for this game: the cave has no sun,
// only scattered deposits, so a fixed direction would put the highlight on the same side of every
// lake no matter which side the light was actually on. The field's gradient points at whatever is
// bright nearby, which for a top-down view IS the light direction.
//
// This is safe in a way the same trick was not for creature shadows (see the note above
// CompositePixel): nothing here feeds back into the field it reads, and a highlight that sits a
// little soft or a frame late is invisible, whereas a shadow pointed the wrong way is not.
//
// When the lighting is flat the gradient vanishes and the vector tends to straight up - and a light
// directly overhead produces almost no specular, which is the correct answer for "no direction to
// speak of" rather than a fallback that has to be invented.
float3 GetWaterLightDirection(float2 screenUv)
{
    float2 offset = 2.0 / max(float2(1.0, 1.0), ViewportResolution);
    float3 luma = float3(0.2126, 0.7152, 0.0722);
    float left = dot(tex2D(LightingSampler, GetLightingFieldUv(screenUv - float2(offset.x, 0.0))).rgb, luma);
    float right = dot(tex2D(LightingSampler, GetLightingFieldUv(screenUv + float2(offset.x, 0.0))).rgb, luma);
    float up = dot(tex2D(LightingSampler, GetLightingFieldUv(screenUv - float2(0.0, offset.y))).rgb, luma);
    float down = dot(tex2D(LightingSampler, GetLightingFieldUv(screenUv + float2(0.0, offset.y))).rgb, luma);
    return normalize(float3(float2(right - left, down - up) * 40.0, 1.0));
}

// Reflectivity as a property of the SURFACE ANGLE rather than a constant.
//
// Real water reflects almost nothing when looked into straight down and almost everything at a
// grazing angle. The view direction here is straight down, so normal.z IS the cosine of that angle:
// flat water reflects least and the faces of waves reflect most. Driving reflection from this is
// what makes it fluctuate ACROSS the surface with the waves, instead of sitting on the lake as an
// even sheet - which is what a constant reflectivity looks like, and why a uniform sheen reads as
// polished metal however far it is turned down.
//
// Schlick's exponent of 5 is unusable at this wave steepness. The slopes are gentle enough that
// normal.z stays above roughly 0.7 even on the steepest face, so (1 - z)^5 lands near 0.002 and the
// reflection vanishes altogether. The scale remaps the range the waves actually produce onto 0..1
// first; the power then shapes it. The floor keeps calm water from going completely matte.
float GetWaterReflectivity(float3 normal)
{
    float facing = saturate(1.0 - normal.z);
    float curve = pow(saturate(facing * WaterFresnelScale), max(0.01, WaterFresnelPower));
    return saturate(WaterFresnelFloor + ((1.0 - WaterFresnelFloor) * curve));
}

// (GetWaterShoreFoam is gone, and with it the shoreline term.
//
// It brightened a band wherever a water pixel had a non-water neighbour, which is the standard way
// to get foam without any extra CPU data. The problem is that it traces the mask's edge exactly, and
// that edge is a TILE boundary - the mask is drawn from whole water tiles - so what it produced was
// not foam but a clean bright line following the rim of every pool. Foam needs an irregular,
// noise-broken edge to read as foam; a band of uniform width reads as an outline, which is what it
// looked like.
//
// Removing it also takes four texture taps per water pixel with it. If it comes back it needs the
// noise field breaking up its width, not a lower strength - dimming an outline just gives a fainter
// outline.)

// (SampleSpriteOcclusion is gone. Creature occlusion has moved out of the ray march entirely and
// into the screen-space pass at the bottom of this file - see GetCreatureShadow.
//
// Two reasons. It never read: the march writes into a field whose probes are 0.2 tiles apart, about
// five samples across a trilobite, so a creature-sized silhouette could not survive the trip. And it
// actively broke the replacement, because the screen-space pass takes its light direction from that
// same field's gradient - so a creature's own blurred occlusion was bending the direction used to
// place its own sharp shadow. That feedback is what made shadows warp near bright light.
//
// It also cost 5 mask taps per DDA cell, per ray, per cascade, which was plausibly the single
// largest expense in the march. Full-height blockers are unaffected: they come from the world-space
// tile grid, which is where they always came from.)

// Marches one probe's interval and returns rgb = radiance gathered, a = 1 when the interval
// terminated on an occluder (so the merge knows no far-field light can pass through it).
float4 SampleRaySegment(float2 worldStart, float2 direction, float worldLength, float worldOriginDistance)
{
    float2 worldEnd = worldStart + (direction * worldLength);
    float2 worldDirection = worldEnd - worldStart;
    float2 safeDirection = float2(
        abs(worldDirection.x) < 0.0001 ? 0.0001 : worldDirection.x,
        abs(worldDirection.y) < 0.0001 ? 0.0001 : worldDirection.y);
    float2 cell = WorldToTileCoordinate(worldStart);
    float2 cellStep = float2(worldDirection.x < 0.0 ? -1.0 : 1.0, worldDirection.y < 0.0 ? -1.0 : 1.0);
    float2 boundary = (cell + (cellStep * 0.5)) * TileSize;
    float2 nextBoundary = (boundary - worldStart) / safeDirection;
    float2 deltaBoundary = abs(TileSize / safeDirection);
    float currentT = 0.0;
    float3 radiance = 0.0;
    // Fraction of light still able to travel along this ray. Partial occluders (short casters at
    // the edge of their range, soft sprite edges) reduce it instead of terminating the ray, which
    // is what produces graded shadow edges rather than a hard on/off silhouette.
    float transmittance = 1.0;

    // Both already world distances - there is no pixel-to-world conversion left to get wrong.
    float intervalWorldLength = worldLength;
    float originWorldDistance = worldOriginDistance;

    // Traverse tile cells instead of taking sparse screen samples so barriers cannot be skipped.
    //
    // The cell budget is 128 rather than 64 because the intervals widen with probe spacing at wide
    // zoom (see GetBaseIntervalWorld), so the top cascade's ray covers many more tiles there. The
    // range cut-off below is what bounds it: no ray marches past LightRangeTiles, so the worst case is
    // a diagonal ray crossing that range - about 94 cells at the widest rung. Raising the ceiling is
    // nearly free for short rays because the loop still exits the moment the interval is consumed.
    for (int cellStepIndex = 0; cellStepIndex < 128; cellStepIndex++)
    {
        if (currentT >= 1.0)
        {
            break;
        }

        float nextT = min(nextBoundary.x, nextBoundary.y);
        float segmentEnd = min(1.0, nextT);
        float sampleT = (currentT + segmentEnd) * 0.5;

        // The tile grid carries its own out-of-range fallback (SampleTileCell), so rays keep
        // marching through it past the screen edge instead of dying there - otherwise probes
        // near the bottom/right of the viewport lose most of their steps and both light and
        // shadow fade out well before the true edge of the visible area.
        float4 tileCell = SampleTileCell(cell);

        // Distance from the probe to this sample, in tiles, so falloff is zoom-invariant.
        float distanceTiles =
            (originWorldDistance + (sampleT * intervalWorldLength)) / max(1.0, TileSize);
        // A tile-sized emitter's solid angle already shrinks as ~1/distance, so gathering over all
        // directions gives an inherent 1/d falloff before any explicit term is applied. Stacking a
        // squared range falloff on top of that made light die within ~3 tiles no matter how large
        // LightRangeTiles was. So keep this term flat over most of the range and only ramp down
        // near the edge: the natural 1/d provides the shading, and the range genuinely controls
        // how far light carries.
        //
        // The range is whatever the cascades can SEE at this zoom, capped on the CPU. Tuning the
        // taper to a fixed tile count instead left it unreachable at normal zoom, so light ran at
        // full strength straight into the hierarchy's own reach and stopped there - a hard edge
        // whose radius in tiles changed with every zoom step.
        float rangeTiles = max(0.001, LightRangeTiles);
        float fadeBand = rangeTiles * max(0.01, LightFadeFraction);
        float rangeFade = saturate((rangeTiles - distanceTiles) / fadeBand);
        // Smoothstepped so the taper has no visible shoulder where it begins.
        rangeFade = rangeFade * rangeFade * (3.0 - (2.0 * rangeFade));
        // Partially offset that inherent 1/d concentration so the lit pool is broad and even
        // rather than a hot core with nothing around it: hold the near field back, leave the far
        // field untouched. This is what lowers peak brightness while extending useful reach.
        // Expressed as a fraction of the range, so it covers the same share of the lit pool at
        // every zoom rather than a fixed tile count that dominates when zoomed in.
        float nearFieldRamp = rangeTiles * max(0.001, NearFieldRampFraction);
        float nearFieldSoftening = saturate(NearFieldFloor + (distanceTiles / nearFieldRamp));
        // Both terms are fractions of a now zoom-invariant range, so this shapes the same physical
        // falloff at any zoom - no extra brightness compensation needed on top.
        float attenuation = rangeFade * nearFieldSoftening;

        // Past the range, rangeFade is already exactly zero - no light from here on can contribute, so
        // there is nothing to gain from walking further. Stopping is what bounds the cost of the
        // widened intervals: at the widest rung the top cascade's interval is 102 tiles long, but the
        // range caps at 64, so the ray retires there instead of iterating cells that add nothing.
        // Occlusion beyond the range is equally irrelevant, since no light can arrive through it.
        if (distanceTiles >= rangeTiles)
        {
            break;
        }

        // Emission is gathered through whatever transmittance survives up to this cell. A cell's
        // own occluder does not dim its own emission, so this is applied before the hit test.
        radiance += SampleTileEmissionColor(cell) * tileCell.b * attenuation * transmittance;

        // Full-height blockers, from the world-space tile grid, applied at EVERY distance rather
        // than only where the screen-space mask runs out. The grid is anchored to world tile
        // coordinates and now spans the march's whole reach, so a wall occludes by exactly the same
        // amount no matter where the camera is pointing - which is the property the screen-space
        // mask could never have.
        //
        // R carries the cell's OPACITY rather than a blocker flag, so the amount of light a cell stops
        // is a property of the CELL and not one global constant applied to everything that trips a
        // threshold. That is what lets a built wall and solid rock attenuate differently (see
        // LightingOccluderHeight.GetLightTransmission) and what lets a building's edge cells occlude
        // partially, in proportion to how much of them its sprite actually covers - which is what
        // stops a round radar on a 4x4 footprint casting a rectangle.
        //
        // Unconditional, because a clear cell stores exactly 0 and multiplying by 1 is a no-op. The
        // branch it replaced saved nothing: every lane in the wavefront had to evaluate it anyway.
        transmittance *= saturate(1.0 - tileCell.r);

        if (transmittance <= 0.02)
        {
            return float4(radiance, 1.0);
        }

        if (nextT >= 1.0)
        {
            break;
        }

        if (nextBoundary.x < nextBoundary.y)
        {
            currentT = nextBoundary.x;
            nextBoundary.x += deltaBoundary.x;
            cell.x += cellStep.x;
        }
        else
        {
            currentT = nextBoundary.y;
            nextBoundary.y += deltaBoundary.y;
            cell.y += cellStep.y;
        }
    }

    // Report how much of the ray was absorbed, not just whether it terminated: the merge scales
    // the far interval by (1 - alpha), so a partially shadowing short caster must pass on its
    // partial occlusion rather than reporting "clear".
    return float4(radiance, saturate(1.0 - transmittance));
}

float4 RadianceCascadePixel(VertexShaderOutput input) : COLOR0
{
    float2 pixel = GetCascadePixel(input.TextureCoordinates);
    float2 rayOffset = GetRayOffset(pixel, RayDimension);
    float2 probeOffset = GetProbeOffset(pixel, RayDimension);
    float2 direction = GetRayDirection(rayOffset, RayDimension);
    // Packed pixel -> probe index -> world, then march in world units. The packed texture is pure
    // storage from here on; its coordinates never enter the geometry.
    float2 worldStart = GetProbeWorld(probeOffset) + (direction * IntervalWorldOrigin);
    return SampleRaySegment(worldStart, direction, IntervalWorldLength, IntervalWorldOrigin);
}

// A ray's identity is its ANGULAR index: angle = (rayIndex + 0.5) / rayCount * 2pi. The
// rayDimension x rayDimension arrangement is only a storage layout for that 1D index. Ray
// count quadruples per cascade, so the four children of angular index i are 4i..4i+3, and
// their storage coordinates come from re-packing those 1D indices with the higher cascade's
// row width. (Doubling the 2D offset per axis is NOT equivalent - it scrambles the angles.)
float2 GetHigherRayOffset(float rayIndex, float subIndex)
{
    float higherIndex = (rayIndex * 4.0) + subIndex;
    return float2(
        fmod(higherIndex, HigherRayDimension),
        floor(higherIndex / HigherRayDimension));
}

float4 SampleHigherProbe(float2 probeOffset, float2 rayOffset)
{
    float3 radiance = 0.0;
    float occlusion = 0.0;
    float rayIndex = (rayOffset.y * RayDimension) + rayOffset.x;
    for (int directionOffset = 0; directionOffset < 4; directionOffset++)
    {
        float2 higherRayOffset = GetHigherRayOffset(rayIndex, directionOffset);
        float2 packedPixel = (probeOffset * HigherRayDimension) + higherRayOffset;
        float4 sampleValue = SampleLevelZero(PreviousCascadeSampler, (packedPixel + 0.5) / HigherCascadeResolution);
        radiance += sampleValue.rgb;
        occlusion += sampleValue.a;
    }

    // Average both channels over the four children so energy stays consistent per cascade.
    return float4(radiance * 0.25, occlusion * 0.25);
}

float4 CascadeMergePixel(VertexShaderOutput input) : COLOR0
{
    float4 lower = tex2D(TextureSampler, input.TextureCoordinates);
    if (HasHigherCascade < 0.5)
    {
        return lower;
    }

    float2 pixel = GetCascadePixel(input.TextureCoordinates);
    float2 rayOffset = GetRayOffset(pixel, RayDimension);
    float2 probeOffset = GetProbeOffset(pixel, RayDimension);
    float2 direction = GetRayDirection(rayOffset, RayDimension);

    // Sample the higher cascade around THIS PROBE'S OWN POSITION - not around the far end of its ray.
    //
    // This is the ring fix. Every cascade measures its interval from its own probe (see
    // SampleRaySegment's worldStart), and IntervalWorldOrigin(k+1) is by construction exactly
    // IntervalWorldOrigin(k) + IntervalWorldLength(k). So looking the higher cascade up at the ray's
    // endpoint applied that distance TWICE: cascade k covered [t_k, t_k+1] measured from P, while the
    // probes being interpolated sat at P + dir*t_k+1 and started their own rays another t_k+1 beyond
    // themselves. Nothing covered [t_k+1, 2*t_k+1] from P - a dark annulus around every emitter, at
    // every cascade boundary, at fixed WORLD radii. That is why it only resolved on screen when
    // zoomed in: at this layout the innermost gap is 0.30-0.60 tiles, about 24 screen pixels at the
    // default zoom and 100 at the tightest.
    //
    // Interpolating around P instead makes the higher cascade's probes approximately co-located with
    // this one, so their interval genuinely continues this ray where it stopped. The residual error
    // is that a higher probe sits up to half its spacing away from P, which is exactly the
    // approximation the bilinear interpolation below exists to smooth over.
    //
    // It also removes the reason the coarse cascades had to be widened for coverage: the lookup is
    // now at P, which is inside the lattice by construction, rather than a whole look-ahead outside
    // it.
    float2 probeWorld = GetProbeWorld(probeOffset);
    float2 higherProbeCoordinate = (probeWorld - HigherProbeWorldOrigin) / HigherProbeWorldSpacing;

    // A merge position can legitimately fall outside the higher cascade's lattice: the look-ahead is
    // a fixed world distance (a quarter of the total reach - 6.4 tiles here) while the lattice only
    // overhangs the screen by what the packed alignment provides, which is 2.4 tiles per side at the
    // default zoom and 0.57 at the tightest. So outward-facing rays routinely aim past the edge.
    //
    // What must happen out there is that the far field contributes NOTHING, and does so continuously.
    //
    // Two wrong answers were tried. Left alone, SampleHigherProbe turns an out-of-range probe index
    // into a packed UV outside 0..1 and the sampler resolves it to the texture border - which happens
    // to be near-zero radiance, so the result was accidentally about right, but it flipped
    // discontinuously as a world point crossed the boundary. Clamping the COORDINATE instead made it
    // continuous but substituted the edge probe's real radiance, which is not small: every outward
    // ray past the edge started injecting a large spurious far-field term, and that is what wrecked
    // the wall lighting.
    //
    // Fading to zero over the last probe gives both properties at once. It is still an approximation
    // - the far field is simply missing near the lattice edge rather than wrong - but it is
    // continuous in space AND in time, so a lattice step cannot make it jump. The proper repair is to
    // size the coarse cascades so the look-ahead fits inside them, at which point this fade stops
    // being reached in normal use and becomes a safety net.
    float2 outsideProbes = max(max(-higherProbeCoordinate, higherProbeCoordinate - (HigherProbeCount - 1.0)), 0.0);
    float edgeFade = saturate(1.0 - max(outsideProbes.x, outsideProbes.y));

    float2 maxBase = max(0.0, HigherProbeCount - 2.0);
    float2 clampedCoordinate = clamp(higherProbeCoordinate, 0.0, HigherProbeCount - 1.0);
    float2 higherProbeBase = min(floor(clampedCoordinate), maxBase);
    float2 interpolation = saturate(clampedCoordinate - higherProbeBase);

    // Smoothstep the interpolation weights.
    //
    // Plain bilinear is only C0: its value is continuous across a patch boundary but its GRADIENT is
    // not, so every probe row and column leaves a crease. Normally the probes are dense enough that
    // the creases are sub-pixel, but probe spacing is screen-relative while the light range is a fixed
    // 25.6 tiles - so zoomed fully out, cascade 3 sits at 12.8 tiles per probe and an isolated
    // deposit's whole pool spans about four of them. Four samples reconstructed with straight,
    // grid-aligned creases is a box around the ore, which is exactly what it looks like.
    //
    // Smoothstepping makes the reconstruction C1: the gradient matches across boundaries, so the
    // creases disappear. It adds no detail - the far field is genuinely that coarse out there - but it
    // stops the coarseness reading as an axis-aligned edge, which is the part that looks like a bug
    // rather than like soft light.
    interpolation = interpolation * interpolation * (3.0 - (2.0 * interpolation));

    float4 topLeft = SampleHigherProbe(higherProbeBase, rayOffset);
    float4 topRight = SampleHigherProbe(higherProbeBase + float2(1.0, 0.0), rayOffset);
    float4 bottomLeft = SampleHigherProbe(higherProbeBase + float2(0.0, 1.0), rayOffset);
    float4 bottomRight = SampleHigherProbe(higherProbeBase + float2(1.0, 1.0), rayOffset);
    float4 upper = lerp(
        lerp(topLeft, topRight, interpolation.x),
        lerp(bottomLeft, bottomRight, interpolation.x),
        interpolation.y);
    // Both channels fade: the occlusion term has to go with the radiance, or a merge position past
    // the edge would report the far interval as BLOCKED while contributing no light, which reads as
    // spurious shadow rather than as absent bounce.
    upper *= edgeFade;

    // Radiance along this direction is the near interval PLUS whatever the far interval
    // contributes through the near interval's remaining transmittance. Returning only the
    // far term (as this did before) throws away all near-field light: an emitter sitting
    // right next to a probe contributes nothing, so an enclosed light cannot illuminate its
    // own room and the field ends up dominated by distant unoccluded directions - which
    // reads as one global "sun" direction instead of light radiating from each deposit.
    float transmittance = saturate(1.0 - lower.a);
    return float4(lower.rgb + (upper.rgb * transmittance), saturate(lower.a + (upper.a * transmittance)));
}

float4 LightingFieldPixel(VertexShaderOutput input) : COLOR0
{
    float2 fieldPixel = floor(input.TextureCoordinates * LightingFieldResolution);
    float3 radiance = 0.0;
    for (int y = 0; y < 8; y++)
    {
        for (int x = 0; x < 8; x++)
        {
            float2 packedPixel = (fieldPixel * RayDimension) + float2(x, y);
            radiance += SampleLevelZero(TextureSampler, (packedPixel + 0.5) / CascadeResolution).rgb;
        }
    }

    return float4(radiance / 64.0, 1.0);
}

// Reinhard compression applied to LUMINANCE, with the colour's ratios carried through unchanged.
//
// Per-channel Reinhard - x / (x + 1) on each channel independently - desaturates as it compresses,
// because each channel approaches 1 on its own and their ratios collapse together. With LightGain at
// 20 almost everything visible sits well into the saturating region, so the shared ore amber
// (1.00 : 0.82 : 0.62) came out as (1.00 : 0.98 : 0.95) - white. The hue only survived in the dim
// fringes, which is exactly why the raw LightingField debug view looked correctly coloured while the
// composite did not.
//
// Compressing the luminance and scaling the colour by that same factor preserves the ratios exactly,
// so a bright light reads as a bright AMBER rather than a bright white.
float3 TonemapRadiance(float3 radiance)
{
    float luminance = dot(radiance, float3(0.2126, 0.7152, 0.0722));
    float compressed = luminance / (1.0 + luminance);
    float3 tinted = radiance * (compressed / max(luminance, 0.00001));

    // Chroma-preserving compression can push a single channel past 1 even though the luminance is in
    // range - (10, 0, 0) maps to (3.2, 0, 0) - and clipping that would give a hard-edged filled disc,
    // the precise artefact the per-channel curve was originally chosen to avoid. So desaturate toward
    // the compressed luminance by exactly the amount needed to bring the peak back to 1, and no more.
    // Below that threshold the hue is preserved perfectly; above it, only the core is affected.
    float peak = max(tinted.r, max(tinted.g, tinted.b));
    float desaturate = peak > 1.0 ? (peak - 1.0) / max(peak - compressed, 0.00001) : 0.0;
    return lerp(tinted, compressed.xxx, saturate(desaturate));
}

// (The screen-space creature shadow is gone, along with the LightingDirection pass that steered it.
// It swept the occluder mask along the light direction and took the maximum, which is the right
// shape in principle but comes out as a row of overlapping copies once sampled at discrete steps -
// visible as repeating segments on anything thinner than the step, and curved because the direction
// was sampled per pixel. Casters now extrude their own silhouette in the scene layer instead; see
// DrawProjectedShadow in WorldSceneRenderer.cs.)

float4 CompositePixel(VertexShaderOutput input) : COLOR0
{
    float4 scene = tex2D(TextureSampler, input.TextureCoordinates);
    float3 cascadeLight = tex2D(LightingSampler, GetLightingFieldUv(input.TextureCoordinates)).rgb;
    float3 directEmission = tex2D(EmissiveSampler, input.TextureCoordinates).rgb;
    // Ray radiance is no longer scaled by RayDimension^2 (that made cascade 4 carry 256x the
    // energy of cascade 0 and broke the merge), so apply one uniform gain here instead, then
    // tonemap with a Reinhard-style curve. That curve never fully saturates, which keeps the
    // glow a soft gradient rather than a hard-edged filled disc.
    // The gain is what sets where light drops below visibility, so it is the knob for how wide the
    // lit pool reads; the core is already saturating and barely responds to it.
    float3 litRadiance = cascadeLight * LightingEnabled * LightGain;
    float3 cascadeResponse = TonemapRadiance(litRadiance);
    float3 lightFactor = Ambient + (cascadeResponse * LitContribution);
    float3 color = (scene.rgb * saturate(lightFactor)) + (directEmission * 0.17);
    return float4(color, scene.a);
}

float4 LightingAddPixel(VertexShaderOutput input) : COLOR0
{
    float2 screenUv = input.TextureCoordinates;
    float water = tex2D(WaterMaskSampler, screenUv).a;
    float3 cascadeLight = tex2D(LightingSampler, GetLightingFieldUv(screenUv)).rgb;
    float3 directEmission = tex2D(EmissiveSampler, screenUv).rgb;
    // Ray radiance is no longer scaled by RayDimension^2 (that made cascade 4 carry 256x the
    // energy of cascade 0 and broke the merge), so apply one uniform gain here instead, then
    // tonemap with a Reinhard-style curve. That curve never fully saturates, which keeps the
    // glow a soft gradient rather than a hard-edged filled disc.
    // The gain is what sets where light drops below visibility, so it is the knob for how wide the
    // lit pool reads; the core is already saturating and barely responds to it.
    float3 litRadiance = cascadeLight * LightingEnabled * LightGain;
    float3 cascadeResponse = TonemapRadiance(litRadiance);

    float2 refraction = float2(0.0, 0.0);
    float3 waterAdd = float3(0.0, 0.0, 0.0);
    // Multiplier on the surface's own colour. 1 everywhere that is not water, so the term below can
    // be applied unconditionally.
    float waterAlbedo = 1.0;

    // Branch rather than multiply the result out at the end. This is a full-screen pass and the
    // block below is twelve sines, a normalize and about a dozen taps; water is a small fraction of
    // a typical view, so paying for it everywhere would be most of the cost of the whole composite.
    if (water > 0.002)
    {
        float t = ElapsedSeconds * WaterWaveSpeed;
        float2 tiles = ScreenUvToWorld(screenUv) / max(1.0, TileSize);
        float3 normal = GetWaterNormal(tiles, screenUv, t);

        // Refraction of what lies under the surface. Screen-space deliberately, unlike the wave
        // geometry: this is the offset between where the bed IS and where it APPEARS through a
        // sloped surface, which is a property of looking through the water rather than a distance
        // in the world. Kept small - past roughly 0.015 the bed stops reading as seen through water
        // and starts reading as heat haze, which is the single most common way this effect is
        // overdone.
        refraction = normal.xy * WaterDistortionUv * water;

        // Reflection: the lighting field sampled through the normal, so the mirrored light bends
        // with the surface. Converted from a tile displacement the same way everything else in this
        // file converts world to field, so it slides the same physical distance at every zoom.
        float2 reflectionOffset =
            ((normal.xy * WaterReflectionTiles * TileSize) / ProbeWorldSpacing) / LightingFieldResolution;
        float3 reflectedResponse = TonemapRadiance(
            tex2D(LightingSampler, GetLightingFieldUv(screenUv) + reflectionOffset).rgb
                * LightingEnabled * LightGain);

        // Lighting from the SLOPE, never from the height - see the note on GetWaterRipple's removal.
        float3 lightDirection = GetWaterLightDirection(screenUv);
        float diffuse = saturate(dot(normal, lightDirection));

        // Quantised, because everything around this is pixel art and a perfectly smooth ramp across
        // a lake reads as a different medium from the tiles it sits in. WaterLightBands = 0 leaves
        // it smooth.
        float bands = max(0.0, WaterLightBands);
        diffuse = bands >= 1.0 ? (floor(diffuse * bands) / bands) : diffuse;

        // Narrow specular. The view direction is straight down in a top-down game, so the reflected
        // vector's z component IS its alignment with the viewer and no explicit dot is needed.
        float3 reflected = reflect(-lightDirection, normal);
        float specular = pow(saturate(reflected.z), max(1.0, WaterSpecularPower));

        // Torn into streaks rather than left as a smooth blob, reusing the noise that warps the
        // waves. An unbroken highlight across a whole lake reads as a lit sheet; real ones are
        // broken up by structure far finer than the waves carrying them.
        specular *= smoothstep(0.55, 0.95, diffuse + (GetWaterNoiseWarp(tiles, t).x * 6.0));

        // How reflective this particular patch of surface is, which varies across every wave.
        float reflectivity = GetWaterReflectivity(normal);

        // The surface's OWN albedo, varying with slope. This is what gives the water body between
        // its highlights: a trough turned away from the light is a darker, deeper colour, a crest
        // turned toward it a lighter one. Taken from the slope via diffuse and never from the
        // height, or it degenerates into travelling stripes.
        //
        // Because diffuse has already been quantised, so is this - the surface reads as bands of
        // flat colour rather than a smooth gradient, which is the point for pixel art.
        waterAlbedo = WaterAlbedoLow + (diffuse * (WaterAlbedoHigh - WaterAlbedoLow));

        // Loose energy conservation, and the other half of driving reflectivity from the waves:
        // where the surface reflects hardest it also hides more of what lies under it. The two
        // trade off across every wave instead of simply summing, which is what stops a strongly
        // reflecting crest from also being a brightly lit one.
        waterAlbedo *= 1.0 - (reflectivity * WaterRefractionLoss);
        waterAlbedo = lerp(1.0, waterAlbedo, water);

        // Water returns light more sharply than rough rock does. Squaring the response narrows it
        // into a highlight, which reads as a wet sheen rather than a uniformly brighter tile.
        // Weighted by reflectivity, so the reflection appears on wave faces rather than everywhere.
        float3 sheen = reflectedResponse * reflectedResponse * WaterSheenStrength * water * reflectivity;

        // EVERY water term is gated by cascadeResponse. Without that gate the surface generates its
        // own highlights out of the wave field alone and a lake in an unlit chamber glows - which
        // is how a water shader most easily wrecks a scene whose whole premise is that it is dark.
        waterAdd = sheen
            + (cascadeResponse * water
                * ((diffuse * WaterDiffuseStrength) + (specular * WaterSpecularStrength * reflectivity)));
    }

    // Sampled through the refraction offset, which is zero everywhere that is not water. Level-zero
    // rather than tex2D because the coordinate is now computed inside a branch, and a gradient
    // instruction there is undefined; these targets have no mips, so level 0 is what tex2D would
    // have chosen anyway.
    float3 scene = SampleLevelZero(TextureSampler, screenUv + refraction).rgb;

    // The contribution is drawn with an additive blend state; keep alpha non-zero on
    // backends that fold source alpha into the color contribution.
    // waterAlbedo is 1 off the water, so this multiply is a no-op everywhere else. It scales the
    // surface's own colour rather than adding to it, which is the difference between the waves
    // shading the water and the waves lighting it.
    return float4(
        (scene * waterAlbedo * cascadeResponse * LitContribution) + waterAdd + (directEmission * 0.17),
        1.0);
}

float4 TileGridDebugPixel(VertexShaderOutput input) : COLOR0
{
    float2 screenPixel = input.TextureCoordinates * ViewportResolution;
    float2 worldPixel = CameraOrigin + ((screenPixel - CameraViewCenter - CameraShakeOffset) / CameraScale);
    float2 cell = WorldToTileCoordinate(worldPixel);
    float4 value = SampleTileCell(cell);
    return float4(value.rgb, 1.0);
}

technique RadianceCascade
{
    pass Pass1
    {
        VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
        PixelShader = compile PS_SHADERMODEL RadianceCascadePixel();
    }
}

technique CascadeMerge
{
    pass Pass1
    {
        VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
        PixelShader = compile PS_SHADERMODEL CascadeMergePixel();
    }
}

technique LightingField
{
    pass Pass1
    {
        VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
        PixelShader = compile PS_SHADERMODEL LightingFieldPixel();
    }
}

technique Composite
{
    pass Pass1
    {
        VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
        PixelShader = compile PS_SHADERMODEL CompositePixel();
    }
}

technique LightingAdd
{
    pass Pass1
    {
        VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
        PixelShader = compile PS_SHADERMODEL LightingAddPixel();
    }
}

technique TileGridDebug
{
    pass Pass1
    {
        VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
        PixelShader = compile PS_SHADERMODEL TileGridDebugPixel();
    }
}
