// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Medical.Limb.Targeting;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Medical.Limb.Targeting;

/// <summary>
/// Server-side targeting system that handles target selection and updates.
/// </summary>
public sealed class TargetingSystem : SharedTargetingSystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<TargetingRequestMessage>(OnTargetingRequest);
    }

    private void OnTargetingRequest(TargetingRequestMessage msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } uid)
            return;

        SetTarget(uid, msg.Target);
    }

    /// <summary>
    /// Changes the selected target for an entity.
    /// </summary>
    public void SetTarget(EntityUid uid, TargetBodyPart target, LimbTargetingComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!IsValidTarget(target))
            return;

        if (component.SelectedTarget == target)
            return;

        component.SelectedTarget = target;
        Dirty(uid, component);

        // Play sound if configured
        if (component.TargetChangeSound != null)
        {
            _audio.PlayEntity(component.TargetChangeSound, uid, uid);
        }
    }

    /// <summary>
    /// Updates the integrity state of a target body part.
    /// </summary>
    public void UpdateLimbStatus(EntityUid uid, TargetBodyPart target, LimbIntegrityState state, LimbTargetingComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!IsValidTarget(target))
            return;

        component.LimbStatus[target] = state;
        Dirty(uid, component);
    }
}
