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
Texture2D TallOccluderTexture;
Texture2D PreviousCascadeTexture;
Texture2D LightingTexture;
Texture2D EmissiveTexture;
Texture2D WaterMaskTexture;

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

sampler2D EntityOccluderSampler = sampler_state
{
    Texture = <EntityOccluderTexture>;
    AddressU = Clamp;
    AddressV = Clamp;
    MinFilter = Point;
    MagFilter = Point;
    MipFilter = None;
};

sampler2D TallOccluderSampler = sampler_state
{
    Texture = <TallOccluderTexture>;
    AddressU = Clamp;
    AddressV = Clamp;
    MinFilter = Point;
    MagFilter = Point;
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

sampler2D EmissiveSampler = sampler_state
{
    Texture = <EmissiveTexture>;
    AddressU = Clamp;
    AddressV = Clamp;
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = None;
};

float2 SourceResolution;
float2 CascadeResolution;
float2 HigherCascadeResolution;
float2 LightingFieldResolution;
// Screen UV -> lighting-field UV. The cascades are packed into a power-of-two-aligned target
// (PackedSize) that is LARGER than the actual light buffer (SourceResolution): e.g. a 1440x900
// window gives a 720x450 light buffer but a 768x512 packed size. The lighting field therefore
// covers 768x512 source pixels, so sampling it with raw 0..1 screen UV stretches it by
// PackedSize/SourceResolution and reads light from further down-right than it belongs - the
// light pattern appears dragged toward the top-left, with zero error at the top-left corner
// growing outward. Measured in tiles that error is negligible when zoomed in and severe when
// zoomed out, which is exactly how it presents.
float2 LightingFieldUvScale;
float2 ViewportResolution;
float2 TileGridOrigin;
float2 TileGridResolution;
float2 CameraOrigin;
float2 CameraViewCenter;
float2 CameraShakeOffset;
float RayDimension;
float HigherRayDimension;
float ProbeSpacing;
float HigherProbeSpacing;
float IntervalOrigin;
float IntervalLength;
float RaySteps;
float TileSize;
float CameraScale;
float LightRangeTiles;
// How far behind a short caster (a creature) its shadow still reaches, in tiles.
float ShortShadowTiles;
// Strength of the specular highlight on open water. 0 disables it entirely.
float WaterSheenStrength;
// Fraction of light a full-height blocker lets through per tile of thickness. 0 = fully opaque.
float WallTransmission;
// Temporal accumulation. The probe lattice lives in screen space, so panning slides it across
// the world and each probe's rays start hitting different tiles - occlusion flips on and off and
// the field shimmers. Blending against the previous frame's field, reprojected by the camera's
// motion, averages that aliasing out over time.
float2 HistoryUvOffset;
float HistoryBlend;
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

float2 GetProbePosition(float2 probeOffset)
{
    return (probeOffset + 0.5) * ProbeSpacing;
}

float2 SourcePixelToScreenPixel(float2 sourcePixel)
{
    return ((sourcePixel + 0.5) / SourceResolution) * ViewportResolution;
}

float2 SourcePixelToWorld(float2 sourcePixel)
{
    float2 screenPixel = SourcePixelToScreenPixel(sourcePixel);
    return CameraOrigin + ((screenPixel - CameraViewCenter - CameraShakeOffset) / CameraScale);
}

float2 WorldToSourcePixel(float2 worldPixel)
{
    float2 screenPixel = CameraViewCenter + CameraShakeOffset + ((worldPixel - CameraOrigin) * CameraScale);
    return ((screenPixel / ViewportResolution) * SourceResolution) - 0.5;
}

float IsInsideSource(float2 sourcePixel)
{
    return sourcePixel.x >= 0.0 && sourcePixel.y >= 0.0 &&
           sourcePixel.x < SourceResolution.x && sourcePixel.y < SourceResolution.y
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

    return tex2D(TileGridSampler, (localCoordinate + 0.5) / TileGridResolution);
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

    return tex2D(TileEmissionColorSampler, (localCoordinate + 0.5) / TileGridResolution).rgb;
}

float2 WorldToTileCoordinate(float2 worldPixel)
{
    return floor((worldPixel + (TileSize * 0.5)) / TileSize);
}

// World units spanned by one light-buffer (source) pixel. Everything distance-related is
// expressed in world units / tiles so that zooming does not change how far light reaches or
// how quickly it falls off - previously the falloff term used raw light-buffer pixels, so the
// lit radius silently rescaled with every zoom step.
float WorldUnitsPerSourcePixel()
{
    return (ViewportResolution.x / max(1.0, SourceResolution.x)) / max(0.000001, CameraScale);
}

// Sub-tile occluder test. Creatures are drawn smaller than one tile and sit near tile centres,
// so testing a single point per tile cell (the old behaviour) usually missed them entirely and
// they cast no shadow at all. Sample across the portion of the cell the ray actually crosses.
//
// Casters live in two masks by height (see LightingOccluderHeight). Rays march outward FROM the
// receiving probe toward the light, so the hit distance IS how far the receiver sits behind the
// caster - which is exactly the shadow length. Full-height casters (walls, radars, solid rock)
// block at any such distance, giving a long shadow. Short casters (creatures) fade out past
// ShortShadowTiles, so a receiver further away effectively sees over them - a short shadow, still
// cast directly away from whichever light the ray is pointing at.
float SampleSpriteOcclusion(
    float2 worldStart,
    float2 worldDirection,
    float fromT,
    float toT,
    float receiverDistanceTiles)
{
    float shortFade = saturate(1.0 - (receiverDistanceTiles / max(0.001, ShortShadowTiles)));
    float occlusion = 0.0;
    for (int step = 0; step < 3; step++)
    {
        float t = lerp(fromT, toT, (step + 0.5) / 3.0);
        float2 sampleSource = WorldToSourcePixel(worldStart + (worldDirection * t));
        if (IsInsideSource(sampleSource) < 0.5)
        {
            continue;
        }

        float2 uv = (sampleSource + 0.5) / SourceResolution;
        float tallCoverage = tex2D(TallOccluderSampler, uv).a;
        float shortCoverage = tex2D(EntityOccluderSampler, uv).a;

        // Full-height blockers are not perfectly opaque: they pass WallTransmission of the light,
        // so rock glows faintly from a deposit behind it instead of reading as a hard cutout.
        occlusion = max(occlusion, tallCoverage * (1.0 - WallTransmission));
        occlusion = max(occlusion, shortCoverage * shortFade);
    }

    return occlusion;
}

// Marches one probe's interval and returns rgb = radiance gathered, a = 1 when the interval
// terminated on an occluder (so the merge knows no far-field light can pass through it).
float4 SampleRaySegment(float2 startPixel, float2 direction, float rayLength, float originPixels)
{
    float2 endPixel = startPixel + (direction * rayLength);
    float2 worldStart = SourcePixelToWorld(startPixel);
    float2 worldEnd = SourcePixelToWorld(endPixel);
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

    float worldPerPixel = WorldUnitsPerSourcePixel();
    float intervalWorldLength = rayLength * worldPerPixel;
    float originWorldDistance = originPixels * worldPerPixel;

    // Traverse tile cells instead of taking sparse screen samples so barriers cannot be skipped.
    for (int cellStepIndex = 0; cellStepIndex < 64; cellStepIndex++)
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
        float rangeTiles = max(0.001, LightRangeTiles);
        float fadeBand = rangeTiles * 0.35;
        float rangeFade = saturate((rangeTiles - distanceTiles) / fadeBand);
        // Partially offset that inherent 1/d concentration so the lit pool is broad and even
        // rather than a hot core with nothing around it: hold the near field back, leave the far
        // field untouched. This is what lowers peak brightness while extending useful reach.
        float nearFieldSoftening = saturate(0.72 + (distanceTiles / 10.0));
        float attenuation = rangeFade * nearFieldSoftening;
        // Emission is gathered through whatever transmittance survives up to this cell. A cell's
        // own occluder does not dim its own emission, so this is applied before the hit test.
        radiance += SampleTileEmissionColor(cell) * tileCell.b * attenuation * transmittance;

        // Sprite-accurate, height-aware occlusion: creatures AND solid terrain/structures are
        // rasterised into the occluder mask, so light stops on the drawn silhouette rather than on
        // the whole tile square, and short casters only shade what is close behind them.
        float spriteOcclusion = SampleSpriteOcclusion(
            worldStart, worldDirection, currentT, segmentEnd, distanceTiles);
        transmittance *= saturate(1.0 - spriteOcclusion);

        // The mask only exists for the rendered screen. Outside it, fall back to the tile grid so
        // light cannot leak in from off-view geometry. The grid only holds full-height blockers.
        float2 cellCentreSource = WorldToSourcePixel(cell * TileSize);
        if (tileCell.r > 0.5 && IsInsideSource(cellCentreSource) < 0.5)
        {
            // Same partial transmission as the on-screen path, so light does not behave
            // differently just because the blocker happens to be off the edge of the view.
            transmittance *= WallTransmission;
        }

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
    float2 probePosition = GetProbePosition(probeOffset);
    float2 direction = GetRayDirection(rayOffset, RayDimension);
    float2 startPixel = probePosition + (direction * IntervalOrigin);
    return SampleRaySegment(startPixel, direction, IntervalLength, IntervalOrigin);
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
        float4 sampleValue = tex2D(PreviousCascadeSampler, (packedPixel + 0.5) / HigherCascadeResolution);
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
    float2 probePosition = GetProbePosition(probeOffset);
    float2 mergePosition = probePosition + (direction * (IntervalOrigin + IntervalLength));
    float2 higherProbeCoordinate = (mergePosition / HigherProbeSpacing) - 0.5;
    float2 higherProbeBase = floor(higherProbeCoordinate);
    float2 interpolation = saturate(higherProbeCoordinate - higherProbeBase);

    float4 topLeft = SampleHigherProbe(higherProbeBase, rayOffset);
    float4 topRight = SampleHigherProbe(higherProbeBase + float2(1.0, 0.0), rayOffset);
    float4 bottomLeft = SampleHigherProbe(higherProbeBase + float2(0.0, 1.0), rayOffset);
    float4 bottomRight = SampleHigherProbe(higherProbeBase + float2(1.0, 1.0), rayOffset);
    float4 upper = lerp(
        lerp(topLeft, topRight, interpolation.x),
        lerp(bottomLeft, bottomRight, interpolation.x),
        interpolation.y);

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
            radiance += tex2D(TextureSampler, (packedPixel + 0.5) / CascadeResolution).rgb;
        }
    }

    return float4(radiance / 64.0, 1.0);
}

// TextureSampler = this frame's freshly reduced field, PreviousCascadeSampler = last frame's
// accumulated field. HistoryBlend is the weight given to the new frame; 1.0 discards history
// entirely (first frame, or after a zoom where reprojection would be invalid).
float4 LightingAccumulatePixel(VertexShaderOutput input) : COLOR0
{
    float4 current = tex2D(TextureSampler, input.TextureCoordinates);
    float2 historyUv = input.TextureCoordinates + HistoryUvOffset;
    float blend = HistoryBlend;

    // Anything reprojecting from outside the previous field has no valid history to reuse.
    if (historyUv.x < 0.0 || historyUv.y < 0.0 || historyUv.x > 1.0 || historyUv.y > 1.0)
    {
        blend = 1.0;
    }

    float4 history = tex2D(PreviousCascadeSampler, saturate(historyUv));
    return lerp(history, current, saturate(blend));
}

float4 CompositePixel(VertexShaderOutput input) : COLOR0
{
    float4 scene = tex2D(TextureSampler, input.TextureCoordinates);
    float3 cascadeLight = tex2D(LightingSampler, input.TextureCoordinates * LightingFieldUvScale).rgb;
    float3 directEmission = tex2D(EmissiveSampler, input.TextureCoordinates).rgb;
    // Ray radiance is no longer scaled by RayDimension^2 (that made cascade 4 carry 256x the
    // energy of cascade 0 and broke the merge), so apply one uniform gain here instead, then
    // tonemap with a Reinhard-style curve. That curve never fully saturates, which keeps the
    // glow a soft gradient rather than a hard-edged filled disc.
    // Gain kept low relative to the (now much longer) light range: rays reach considerably
    // further, so per-sample brightness has to come down or everything within range saturates.
    float3 litRadiance = cascadeLight * LightingEnabled * 11.0;
    float3 cascadeResponse = litRadiance / (litRadiance + 1.0);
    float3 lightFactor = Ambient + (cascadeResponse * 0.65);
    float3 color = (scene.rgb * saturate(lightFactor)) + (directEmission * 0.17);
    return float4(color, scene.a);
}

float4 LightingAddPixel(VertexShaderOutput input) : COLOR0
{
    float4 scene = tex2D(TextureSampler, input.TextureCoordinates);
    float3 cascadeLight = tex2D(LightingSampler, input.TextureCoordinates * LightingFieldUvScale).rgb;
    float3 directEmission = tex2D(EmissiveSampler, input.TextureCoordinates).rgb;
    // Ray radiance is no longer scaled by RayDimension^2 (that made cascade 4 carry 256x the
    // energy of cascade 0 and broke the merge), so apply one uniform gain here instead, then
    // tonemap with a Reinhard-style curve. That curve never fully saturates, which keeps the
    // glow a soft gradient rather than a hard-edged filled disc.
    // Gain kept low relative to the (now much longer) light range: rays reach considerably
    // further, so per-sample brightness has to come down or everything within range saturates.
    float3 litRadiance = cascadeLight * LightingEnabled * 11.0;
    float3 cascadeResponse = litRadiance / (litRadiance + 1.0);
    // Water is a smooth surface, so it returns light more sharply than rough rock does. Raising
    // the response to a power narrows it into a highlight, which reads as a wet sheen rather than
    // a uniformly brighter tile. Purely a reflectance change - no extra light is introduced.
    float water = tex2D(WaterMaskSampler, input.TextureCoordinates).a;
    float3 sheen = cascadeResponse * cascadeResponse * WaterSheenStrength * water;

    // The contribution is drawn with an additive blend state; keep alpha non-zero on
    // backends that fold source alpha into the color contribution.
    return float4((scene.rgb * cascadeResponse * 0.65) + sheen + (directEmission * 0.17), 1.0);
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

technique LightingAccumulate
{
    pass Pass1
    {
        VertexShader = compile VS_SHADERMODEL SpriteVertexShader();
        PixelShader = compile PS_SHADERMODEL LightingAccumulatePixel();
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
