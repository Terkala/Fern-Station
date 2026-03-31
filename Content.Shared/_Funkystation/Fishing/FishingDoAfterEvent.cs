using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Funkystation.Fishing;

[Serializable, NetSerializable]
public sealed partial class FishingDoAfterEvent : SimpleDoAfterEvent;
