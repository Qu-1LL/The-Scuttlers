using Microsoft.Xna.Framework;

namespace TriloGame.Game.Rendering.Lighting;

public static class OreLightSettings
{
    public const int BaseProbeSpacing = 8;
    public const int BaseRayDimension = 8;
    // How far the merged cascade hierarchy can see, in TILES. This is the one place the reach is
    // defined, and defining it in world units is the point.
    //
    // It used to be implied: the cascade intervals were laid out in light-buffer PIXELS, so the reach
    // in tiles was whatever those pixels happened to cover at the current zoom - which shrank as you
    // zoomed in. GetZoomFactor existed solely to cancel that, by scaling probe spacing and interval
    // lengths together, which welded the two quantities to each other. Everything downstream then had
    // to divide the factor back out (GetLightingFieldUvScale), and the lattice grew to several times
    // the screen because probe spacing was being stretched to fix a problem that belonged to reach.
    //
    // Reach and probe density are independent choices and are now written down as such: reach is this
    // fixed world distance, probe density stays screen-relative (a fixed number of probes per screen,
    // which is what bounds the cost).
    //
    // Tightened from 25.6 tiles - the value the old pixel-based layout happened to produce at the
    // default zoom - so a deposit lights its own chamber rather than most of the way to the next one.
    //
    // THERE IS A HARD FLOOR AT 17 TILES and it is worth knowing where it comes from before tuning
    // this further. GetBaseIntervalWorld takes max(authoredFloor, probeWorldSpacing), and at the
    // default zoom the spacing is 102.4 world units; with four cascades the intervals multiply that
    // by (4^4 - 1)/3 = 85, which is 17.0 tiles. Authoring anything below that is silently ignored -
    // the spacing wins and the reach stays at 17 - so the constant would stop describing what the
    // renderer does. Going lower means fewer cascades or a finer probe lattice, not a smaller number
    // here. 18 keeps the authored value the binding one with a little headroom.
    public const float LightReachTiles = 18f;
    // The cascade hierarchy's total reach is fixed in LIGHT-BUFFER PIXELS (see
    // LightingCascadeLayout.GetTotalReachPixels), so its size in WORLD TILES shrinks as the camera
    // zooms in - a fixed pixel budget covers less world the closer the camera gets. At the default
    // zoom this interval gave a reach of only ~17 tiles, which at 4x zoom-in shrank to ~4 tiles: any
    // point more than a few tiles from a deposit read as flat, unlit Ambient, which is what showed up
    // as the view going almost fully dark at max zoom. Raised from 2 to 3 - the largest value that
    // does not push this viewport's cascade count from 5 to 6 (a real extra full-screen render pass,
    // not just a tuning change) - for a flat ~1.5x reach increase at every zoom level, at zero extra
    // rendering cost.
    public const float CascadeIntervalTexels = 3f;
    public const int CascadeRaySteps = 12;
    public const int MinCascadeCount = 4;
    // Four levels, not five or six - so the hierarchy is now a fixed depth.
    //
    // Probe count and ray count trade off inside a fixed packed texture: cascade k stores
    // probeCount_k x rayDimension_k per axis, and rayDimension doubles per level. Five levels put
    // 9x6 probes in the top cascade with 16384 rays each; four put 16x10 probes there with 4096.
    //
    // That matters because the top cascades have to COVER their own look-ahead. A merge from cascade
    // k samples cascade k+1 at the far end of k's interval - 6.4 tiles out here - and if the lattice
    // does not extend that far the far field is either wrong or faded away. Covering 6.4 tiles with
    // 9x6 probes needs them ~5 tiles apart, which makes the bounce visibly blocky; with 16x10 it is
    // ~2 tiles, which is fine for a term this smooth. See GetCascadeProbeWorldSpacing.
    //
    // The angular resolution given up is not missed: at cascade 0's interval the rays were already
    // ~3.8 world units apart against a 24 unit probe spacing, i.e. heavily oversampled. And this is
    // one fewer full-screen pass and a smaller packed target (1024x640 rather than 1152x768).
    public const int MaxCascadeCount = 4;
    // A bright ambient floor leaves no dynamic range for dim, long-range light: an added 4%
    // reads as nothing against a 58%-lit floor, so extending the light range has no visible
    // effect. Dropping the floor is what lets far-reaching light actually be seen, and matches
    // the cave reading as innately dark with ore as the real light source.
    //
    // This is exactly the brightness of a pixel no light reaches, so it is the one knob that sets
    // how dark the darkest areas are. Everything else only adds on top of it.
    //
    // It was raised to 0.24 back when the cascade hierarchy's reach in tiles shrank with zoom, which
    // made "outside light range" a common case that read as a blackout rather than an unlit floor.
    // That is no longer true - the reach is zoom-invariant now, and GetZoomFactor's clamp stopped
    // the probe lattice falling short of the screen when zoomed out - so the floor can come back
    // down to darken the cave without the unlit case swallowing the screen.
    //
    // Lowered together with a matching rise in LitContribution below. Dropping this alone would dim
    // the WHOLE image, lit surfaces included, which lowers contrast instead of raising it; moving
    // the two in opposite directions holds a fully lit surface at the same peak and widens the gap
    // beneath it, which is what "darker" actually means here.
    public const float Ambient = 0.15f;
    // Uniform gain applied to gathered radiance before the tonemap. The tonemap is Reinhard-style,
    // so the gain decides at what radiance level light stops being visible - i.e. how far from a
    // deposit its light still reads. Raising it widens the lit pool rather than brightening its
    // core, because the core is already on the saturating part of the curve.
    //
    // Lowered from 34, then again from 20, to tighten the pool around each deposit. This is the right
    // knob for radius precisely because of that saturation: it moves where light falls below
    // visibility without touching how bright the deposit itself reads. Note it interacts with
    // Ambient - a darker floor gives dim far-field light more contrast to show up against, so some of
    // a gain reduction is spent cancelling that out rather than shrinking the pool.
    //
    // This is one of the two knobs that actually govern how far light CARRIES, and the reason is the
    // shape of the falloff below: attenuation is flat at 1.0 from under a tile out to where the range
    // taper begins, so across that whole span the only thing dimming light is the inherent 1/d of a
    // shrinking solid angle. LightReachTiles sets where the taper starts; this sets how quickly the
    // 1/d term falls under the visibility floor. Cutting the reach without cutting this just moves a
    // taper that light had already faded out before reaching.
    public const float LightGain = 11f;
    // Weight of the tonemapped light on top of Ambient. Ambient + LitContribution is the brightest
    // a fully lit surface gets; kept a little under 1 so a fully lit surface still has somewhere to
    // go and does not clip into flat white the moment it is fully lit. Raised in step with the drop
    // in Ambient so that peak stays where it was and only the floor moves.
    public const float LitContribution = 0.83f;
    // Fraction of the usable range over which light tapers to nothing at the far edge. The taper has
    // to exist: the cascade hierarchy stops dead at its own reach, and an abrupt stop shows up as a
    // ring around every deposit whose radius changes with the zoom.
    //
    // Raised from 0.3, which is the other half of shortening how far light carries. Because
    // attenuation is flat at 1.0 everywhere inside the taper, this fraction is really "where does
    // light START dimming" - at 0.3 of an 18 tile range that was 12.6 tiles out, so a deposit lit at
    // full strength most of the way to its own limit. At 0.55 it begins at 8.1 tiles and falls off
    // over the remaining 9.9, which is a long enough ramp that the edge stays soft rather than
    // becoming the hard ring the taper exists to prevent.
    public const float LightFadeFraction = 0.55f;
    // Brightness a sample right at the emitter keeps, and the fraction of the range over which it
    // ramps back to full. Gathering over all directions gives an inherent 1/d concentration; taking
    // a little off the very near field keeps the deposit from reading as a blown-out dot. Kept mild:
    // pulling it down hard darkens the pool rather than widening it, because widening comes from
    // LightGain lifting the far field, not from holding the near field back.
    public const float NearFieldFloor = 0.7f;
    public const float NearFieldRampFraction = 0.15f;
    // Keep deposits visibly brighter than the subdued ambient world.
    public const float OreIntensity = 0.90f;
    // Radius of the glow drawn directly on the deposit. This sprite ignores geometry entirely, so
    // it must stay barely wider than the deposit itself: at 5.5 tiles it was acting as room
    // lighting and visibly bleeding across walls (measured: floor tiles behind a wall received 71%
    // of unblocked light, dropping to 14% once this halo was removed). All actual room lighting
    // comes from the ray-marched cascade, which respects walls.
    //
    // Tightened again alongside the LightGain reduction. This halo and the cascade pool have to come
    // down together: it ignores geometry entirely, so if it stays wide while the ray-marched pool
    // shrinks around it, it stops being a glow ON the deposit and becomes the widest light in the
    // scene - the exact wall-bleed failure the measurement above describes, just reached from the
    // other direction.
    public const float OreRadiusTiles = 1.7f;
    // Hard ceiling on how far ore light carries, in tiles. This is a CAP, not the working range: the
    // real range is whatever the cascade hierarchy can see at the current zoom (which is roughly one
    // screen), and this only bites when zoomed far out, where an unbounded range would let a single
    // deposit tint half the map.
    //
    // Cut alongside LightReachTiles and in proportion to it. The two describe the same distance at
    // opposite ends of the zoom ladder, so moving only one of them makes zooming out reach much
    // further than zooming in - light would visibly spread as the camera pulled back, which reads as
    // the zoom changing the world rather than the view of it.
    public const float OreLightRangeTiles = 45f;
    // How far past the viewport the world tile grid is built, in tiles. This has to cover the ray
    // march's actual reach (~26 tiles at the current cascade layout), NOT just the visible area.
    //
    // Outside the grid SampleTileCell returns "unknown blocker": no emission, and solid. So with the
    // old 1-tile padding, every emitter more than a tile off-screen contributed nothing and every
    // off-screen cell behaved like rock. Panning slid that boundary across the world, so a deposit
    // crossing it switched between contributing its full light and contributing none - light popping
    // in and out purely because of where the camera happened to be pointing. That is the single
    // largest source of lighting changing under camera movement, and no amount of probe-lattice
    // snapping or temporal blending can hide it, because the input data genuinely changes.
    //
    // The grid is a CPU array of one colour per tile, so covering the real reach costs about 12k
    // cells at the most zoomed-out rung - cheap next to what it fixes.
    public const int TileGridPaddingTiles = 28;
    // How far a short caster's shadow (a creature's) reaches behind it, in tiles. Full-height
    // casters - walls, radars, solid rock - are not limited this way and shadow the whole range.
    public const float ShortShadowTiles = 2.5f;
    // Shadow bias for short casters: how close to the receiver a caster stops counting, in tiles.
    // Only covers sampling slop between a probe and its own first sample now. It used to be 0.5,
    // which was sized to a creature footprint because it was also standing in for "the probe is
    // inside this creature" - a job it could not do without taking the whole near field with it.
    // Cascades 0 and 1 only reach ~0.375 tiles, so a half-tile cutoff silently disabled short-caster
    // occlusion in exactly the cascades whose probes (0.2 tiles apart) are fine enough to resolve a
    // creature, leaving the shadow to cascades whose probes are spaced wider than a trilobite. The
    // self-occlusion test in RadianceCascade.fx handles the inside case directly, which is what lets
    // this shrink to its actual purpose.
    public const float ShortShadowMinTiles = 0.12f;
    // Creature shadows are a screen-space pass in the composite now (GetCreatureShadow in
    // RadianceCascade.fx), not a sprite drawn into the scene layer.
    //
    // What it replaced: a full copy of the creature's silhouette, translated 0.22 tiles away from
    // whichever ore was nearest. A rigid translation slides the entire shape off the body instead of
    // staying welded where it meets the ground, so it read as a sticker rather than a shadow - and no
    // offset value fixes that, since a larger one is simply more detached. Its direction came from a
    // 14-tile search of the visible emitter list, which in a cave this sparse usually found nothing
    // and fell back to a hardcoded "straight down", which is why it appeared stuck at one angle.
    //
    // How dark the shadow gets at the caster, before proximity and directionality scale it.
    public const float CreatureShadowStrength = 0.85f;
    // The distance response lives in the shadow's DARKNESS, not its length.
    //
    // Length is the wrong channel for it on a caster this short. A trilobite stands 0.22 tiles high,
    // so the similar-triangles model only spans CreatureShadowMin..MaxLengthTiles - a third of a tile
    // of travel end to end, which is not a difference the eye reads as "nearer the light". Darkness
    // has the whole 0..1 range to work in and is what actually reads: a creature standing on a
    // deposit sits in a hard dark shadow, one across the room barely marks the floor.
    //
    // Within this distance the shadow is at full strength; past ShadowFadeDistanceTiles it is gone.
    // The fade finishes INSIDE CreatureShadowLightRadiusTiles on purpose, so a shadow tapers away
    // rather than switching off on the frame its last emitter leaves the search radius.
    public const float ShadowFullStrengthDistanceTiles = 1.5f;
    public const float ShadowFadeDistanceTiles = 8f;
    // Shadow length comes from a height model rather than a constant.
    //
    // A caster of height H standing d away from a light at height h casts a shadow of length
    // H*d/(h-H). That single expression gives the behaviour a constant cannot: directly under a light
    // the shadow shrinks to nothing, and it lengthens as the caster moves away. A fixed length is
    // wrong in both directions at once - too long beside a deposit, too short across the room.
    //
    // Heights are in tiles, measured against the same world units everything else uses. A trilobite
    // is low to the ground; a building is not, which is why its shadow is the longer of the two.
    public const float ShadowLightHeightTiles = 1.15f;
    public const float CreatureShadowHeightTiles = 0.22f;
    public const float BuildingShadowHeightTiles = 0.6f;
    // Hard cap, because the model diverges as a caster approaches the light's own height and because
    // a very long shadow reads as wrong in a top-down view however correct the arithmetic is.
    //
    // Capped PER CASTER rather than globally, because the two casters fail at different lengths. A
    // building is tall and its base is wide, so a long shadow still reads as attached to it. A
    // trilobite is barely off the ground and about a tile across, so once its shadow passes roughly
    // its own body length it stops reading as cast by the creature and starts reading as a separate
    // dark smear beside it - which is what "too long" means here.
    //
    // Creatures also get a FLOOR, which is a deliberate departure from the similar-triangles model.
    // That model goes to zero directly beneath a light, and a creature with no shadow at all reads as
    // unplaced - as though it were hovering rather than standing on the floor. The floor keeps a
    // short contact shadow under it at all times, and because the length is clamped rather than
    // scaled, the direction still comes from the light.
    //
    // Buildings have no floor (see BuildingShadowMinLengthTiles): they are large enough that the
    // model's own near-light shortening never takes them to nothing.
    public const float CreatureShadowMinLengthTiles = 0.2f;
    public const float CreatureShadowMaxLengthTiles = 0.5f;
    public const float BuildingShadowMinLengthTiles = 0f;
    public const float BuildingShadowMaxLengthTiles = 1.6f;
    // Penumbra: how much the silhouette widens per tile of shadow length. Long shadows are softer,
    // which is both what real ones do and what makes a long extrusion merge instead of banding.
    public const float ShadowSpreadPerTile = 0.55f;
    // World distance between extrusion steps. This is the number that decides whether the shadow
    // reads as one shape or as a row of copies: the step has to be no larger than the thinnest
    // feature being extruded, or that feature lands as separate dashes with gaps between them. A
    // trilobite's legs are roughly this wide. Step COUNT is derived from it and the length, so a
    // longer shadow spends more draws rather than stretching the gaps.
    public const float ShadowStepWorld = 26f;
    // How far to look for emitting cells when working out which way the light is coming from, in
    // tiles. Read off the world tile grid, which spans the whole lighting footprint - so unlike the
    // old screen-culled emitter search, a deposit off the edge of the view still steers the shadow.
    public const float CreatureShadowLightRadiusTiles = 10f;
    // How ANISOTROPIC the incoming light has to be before it counts as coming from a definite
    // direction, on a 0..1 scale: 1 means every ray at that probe sees the same source, 0 means light
    // arrives equally from all sides. Below this the shadow fades out, so flat ambient lighting
    // produces no shadow instead of an arbitrary one.
    //
    // This is a real measurement of the incoming radiance distribution (see LightingDirectionPixel),
    // not a spatial gradient of the reduced field. The gradient version could not be made
    // zoom-stable: it needed a baseline distance, and one field texel meant 0.05 tiles zoomed in
    // against 1.6 zoomed out, while a fixed world baseline ran off the lattice edge at high zoom and
    // came back clamped. Neither failure exists here, because there is no baseline and no neighbour
    // lookup.
    //
    // This is the FLOOR of a ramp, not a switch. Below it there is no shadow; from it up to fully
    // one-sided light the shadow darkens continuously, so a second deposit lighting the creature from
    // another angle progressively fills its shadow in rather than doing nothing until it has almost
    // exactly cancelled the first. Treating it as a switch (which a bare clamp does) meant every
    // creature above 0.25 anisotropy cast an identical full-strength shadow, so competing light was
    // invisible across almost the whole range where it matters.
    //
    // Lower = shadows appear more readily; higher = only strongly directional light casts.
    public const float CreatureShadowDirectionality = 0.25f;
    // Fraction of light that passes through a blocker per tile of thickness. Applied
    // multiplicatively as the ray marches, so a one-tile barrier passes this much and a three-tile
    // one passes this cubed - thicker material is naturally more opaque.
    //
    // Split by occluder class, because Tall and Impassable were previously interchangeable: both
    // reported IsFullHeight, both took the same single transmission, and the enum was describing a
    // distinction the renderer did not honour. They are genuinely different materials.
    //
    // Tall - built walls and radars. Structures, not geology: panels and frames with gaps, so light
    // gets through. Raised from the old shared 0.15 so that piercing is actually visible; a one-tile
    // wall now passes 35% and a two-tile wall 12%, where before two tiles killed it outright at 2%.
    public const float WallTransmission = 0.35f;
    // Impassable - solid terrain: intact ore deposits, cave crystals, bedrock. Metres of rock rather
    // than a built panel, so it is much closer to opaque than a wall is. A single tile passes 8% and
    // two tiles reach the ray march's own 2% cut-off, which is what keeps deep rock genuinely dark
    // now that walls no longer are.
    public const float RockTransmission = 0.08f;
    // Water is treated as a mostly specular surface: its own texture is dimmed to WaterAlbedo so it
    // stays only faintly visible, and what you mainly see is reflected light at WaterSheenStrength.
    // That means unlit water reads as near-black and lit water mirrors the ore colour around it.
    public const float WaterAlbedo = 0.45f;
    public const float WaterSheenStrength = 0.85f;
    // Ripple on the water's reflection. The highlight is distorted by a travelling wave rather
    // than the texture being warped, which is what a reflection on a moving surface actually does.
    // Strength is an offset in lighting-field UV space, so it scales with the screen, not the zoom.
    // Set to 0 to hold the reflection still.
    // Amplitude in TILES, converted to a UV offset through the camera so the distortion covers the
    // same world distance at every zoom level.
    public const float WaterRippleStrength = 0.4f;
    // Depth of the travelling bright/dark banding on the highlight. This is what actually makes the
    // ripple visible: the lighting field is low resolution and smooth, so warping its lookup alone
    // barely changes the reflection.
    public const float WaterRippleContrast = 0.5f;
    // Radians of wave phase per tile. Higher = tighter, choppier ripples.
    public const float WaterRippleWavesPerTile = 1.5f;
    // Radians per second. Higher = faster travel.
    public const float WaterRippleSpeed = 1.15f;
    public const float LumeniteMinimumPulse = 0.38f;

    public static readonly Color SharedOreLightColor = new(255, 209, 158, 255);
}
