using Content.Shared.EntityTable;
using Content.Shared.FixedPoint;
using Content.Shared._Funkystation.Fishing;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Funkystation.Fishing;

/// <summary>
/// A fishing rod that can pull items from deep enough puddles.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SharedFishingSystem))]
public sealed partial class FishingRodComponent : Component
{
    /// <summary> Inclusive window around the target line: success if <c>abs(progress - target) &lt;= this</c> (bar is 0..1). </summary>
    public const float TimingHitHalfWidth = 0.1f;

    /// <summary> Sentinel: no timing minigame active. </summary>
    public const float NoTimingTarget = -1f;

    [DataField]
    public TimeSpan CastDuration = TimeSpan.FromSeconds(2);

    [DataField]
    public FixedPoint2 MinimumPuddleVolume = FixedPoint2.New(10);

    [DataField]
    public FixedPoint2 DrainPerCast = FixedPoint2.New(5);

    [DataField]
    public ProtoId<EntityTablePrototype> LootTable = "FishingLootTable";

    [DataField]
    public SoundSpecifier CastCompleteSound = new SoundPathSpecifier("/Audio/Items/hiss.ogg");

    [DataField]
    public SoundSpecifier CastStartSound = new SoundPathSpecifier("/Audio/Effects/Fluids/slosh.ogg");

    /// <summary> When set, a fishing cast is active and <see cref="FishingCastTargetNormalized"/> is the hit line. </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoNetworkedField]
    public TimeSpan? FishingCastStartTime;

    /// <summary> Goal position on the bar [0,1], or <see cref="NoTimingTarget"/> when idle. </summary>
    [AutoNetworkedField]
    public float FishingCastTargetNormalized = NoTimingTarget;

    [AutoNetworkedField]
    public bool FishingCastTimingSucceeded;
}
