// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Server.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared._Shitmed.Body.Events;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Slime;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Medical.Slime;

/// <summary>
/// System that handles slime limb and head regeneration.
/// Slime limbs and heads automatically regenerate 1 minute after being severed,
/// then slowly heal to full health over 4 minutes.
/// </summary>
public sealed class SlimeLimbRegenerationSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _body = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private const float RegenerationDelaySeconds = 60f; // 1 minute
    private const float RegenerationDurationSeconds = 240f; // 4 minutes
    private const float InitialHealthPercent = 0.05f; // 5% health when regenerated

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeLimbRegenerationComponent, ComponentStartup>(OnRegenerationStartup);
        SubscribeLocalEvent<BodyComponent, BodyPartDroppedEvent>(OnBodyPartDropped);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Process regenerations tracked on bodies
        var query = EntityQueryEnumerator<SlimeLimbRegenerationComponent, BodyComponent>();
        while (query.MoveNext(out var uid, out var regenComp, out var body))
        {
            // Process each regeneration in the list
            for (var i = regenComp.Regenerations.Count - 1; i >= 0; i--)
            {
                var regen = regenComp.Regenerations[i];
                UpdateRegeneration(uid, regen, regenComp, body, frameTime);
            }
        }
    }

    private void OnRegenerationStartup(EntityUid uid, SlimeLimbRegenerationComponent component, ComponentStartup args)
    {
        // Component initialization - no action needed, regenerations are added via MarkLimbSevered
    }

    private void OnBodyPartDropped(EntityUid body, BodyComponent bodyComp, ref BodyPartDroppedEvent args)
    {
        // Check if this is a slime body
        if (bodyComp.Prototype == null || bodyComp.Prototype != "Slime")
            return;

        var part = args.Part;
        
        // Only regenerate limbs and head (not torso or core organs)
        if (!TryComp<BodyPartComponent>(part, out var partComp))
            return;

        // Skip torso - only limbs and head can regenerate
        if (partComp.PartType == BodyPartType.Torso)
            return;

        // Get slot information from the part's ParentSlot (set before dropping)
        // Also use SlotId field as fallback
        var slotId = partComp.ParentSlot?.Id ?? partComp.SlotId;
        
        // Find parent part from body using slot ID
        EntityUid? parentPart = null;
        if (slotId != null)
        {
            // Find the parent part that has this slot
            if (bodyComp.RootContainer.ContainedEntity != null)
            {
                var allParts = _body.GetBodyPartChildren(bodyComp.RootContainer.ContainedEntity.Value);
                foreach (var (partUid, partComponent) in allParts)
            {
                    if (partComponent.Children.ContainsKey(slotId))
                    {
                        parentPart = partUid;
                        break;
                    }
                }
            }
            
            // If no parent part found, it might be attached directly to body
            // In that case, parentPart stays null and we'll attach directly to body
        }

        // Get the prototype ID of the severed part before it's destroyed
        var meta = MetaData(part);
        var partPrototypeId = meta.EntityPrototype?.ID;

        if (partPrototypeId == null)
            return;

        // Mark limb or head for regeneration on the body
        MarkLimbSevered(body, partPrototypeId, slotId, parentPart);
    }

    private void UpdateRegeneration(
        EntityUid body,
        SlimeRegenerationData regen,
        SlimeLimbRegenerationComponent regenComp,
        BodyComponent bodyComp,
        float frameTime)
    {
        var elapsed = (_timing.CurTime - regen.SeveredTime).TotalSeconds;

        // Wait 1 minute before regenerating
        if (elapsed < RegenerationDelaySeconds)
            return;

        // If not yet regenerated, spawn a new part and attach it at low health
        if (!regen.HasRegenerated)
        {
            var newPart = RegenerateLimb(body, regen, bodyComp);
            if (newPart != null)
            {
                regen.HasRegenerated = true;
                regen.RegeneratedPart = GetNetEntity(newPart.Value);
                Dirty(body, regenComp);
            }
            else
            {
                // Failed to regenerate, remove this regeneration
                regenComp.Regenerations.Remove(regen);
                if (regenComp.Regenerations.Count == 0)
                {
                    RemComp<SlimeLimbRegenerationComponent>(body);
                }
                else
                {
                    Dirty(body, regenComp);
                }
            }
            return;
        }

        // After regeneration, slowly heal the regenerated part to full health over 4 minutes
        if (regen.RegeneratedPart != null)
        {
            var regeneratedPartUid = GetEntity(regen.RegeneratedPart.Value);
            if (regeneratedPartUid.IsValid() && TryComp<DamageableComponent>(regeneratedPartUid, out var damageable))
            {
                var fullyHealed = HealRegeneratingLimb(regeneratedPartUid, regen, damageable, frameTime);
                if (fullyHealed)
                {
                    // Fully healed, remove this regeneration
                    regenComp.Regenerations.Remove(regen);
                    if (regenComp.Regenerations.Count == 0)
                    {
                        RemComp<SlimeLimbRegenerationComponent>(body);
                    }
                    else
                    {
                        Dirty(body, regenComp);
                    }
                }
            }
            else
            {
                // Part no longer has damageable component, regeneration complete
                regenComp.Regenerations.Remove(regen);
                if (regenComp.Regenerations.Count == 0)
                {
                    RemComp<SlimeLimbRegenerationComponent>(body);
                }
                else
                {
                    Dirty(body, regenComp);
                }
            }
        }
        else
        {
            // Regenerated part was destroyed, remove regeneration
            regenComp.Regenerations.Remove(regen);
            if (regenComp.Regenerations.Count == 0)
            {
                RemComp<SlimeLimbRegenerationComponent>(body);
            }
            else
            {
                Dirty(body, regenComp);
            }
        }
    }

    private bool HealRegeneratingLimb(
        EntityUid uid,
        SlimeRegenerationData regen,
        DamageableComponent damageable,
        float frameTime)
    {
        var elapsed = (_timing.CurTime - regen.SeveredTime).TotalSeconds;
        var regenerationElapsed = elapsed - RegenerationDelaySeconds;
        
        if (regenerationElapsed >= RegenerationDurationSeconds)
        {
            // Fully healed
            return true;
        }

        // Calculate target health (5% to 100% over 4 minutes)
        var healthProgress = (float)(regenerationElapsed / RegenerationDurationSeconds);
        var maxHealth = damageable.Damage.GetTotal();
        if (maxHealth <= 0)
            maxHealth = 100f; // Default if no damage types defined

        // Target health goes from 5% (InitialHealthPercent) to 100% (1.0)
        var targetHealthPercent = InitialHealthPercent + (1f - InitialHealthPercent) * healthProgress;
        var targetHealth = maxHealth * (1f - targetHealthPercent);

        // Heal toward target health
        var currentHealth = damageable.TotalDamage;
        if (currentHealth > targetHealth)
        {
            var healAmount = currentHealth - targetHealth;
            // Heal gradually (about 1% per second)
            var healThisTick = FixedPoint2.Min(healAmount, FixedPoint2.New((float)maxHealth * 0.01f * frameTime));
            
            if (healThisTick > 0 && currentHealth > 0)
            {
                var healSpec = new DamageSpecifier();
                foreach (var (damageType, amount) in damageable.Damage.DamageDict)
                {
                    if (amount > 0)
                    {
                        // Distribute healing proportionally across damage types
                        var proportion = (float)amount / currentHealth;
                        healSpec.DamageDict[damageType] = -healThisTick * proportion;
                    }
                }

                _damageable.TryChangeDamage(uid, healSpec, ignoreResistances: true);
            }
        }

        return false; // Not fully healed yet
    }

    /// <summary>
    /// Regenerates a severed slime limb/head by spawning a new part directly into the container and attaching it to the body at low health.
    /// </summary>
    private EntityUid? RegenerateLimb(
        EntityUid body,
        SlimeRegenerationData regen,
        BodyComponent bodyComp)
    {
        if (!_prototypes.TryIndex<EntityPrototype>(regen.PartPrototypeId, out var partProto))
            return null;

        EntityUid? newPart = null;
        bool attached = false;

        // Spawn the part directly into the container to avoid it appearing on the ground
        if (regen.ParentPart != null && regen.SlotId != null)
        {
            var parentPartUid = GetEntity(regen.ParentPart.Value);
            if (parentPartUid.IsValid())
            {
                // Attach to parent part - spawn directly into the parent part's container
                if (TryComp<BodyPartComponent>(parentPartUid, out var parentPartComp))
            {
                    var containerId = SharedBodySystem.GetPartSlotContainerId(regen.SlotId);
                    if (TrySpawnInContainer(regen.PartPrototypeId, parentPartUid, containerId, out newPart))
                {
                    if (TryComp<BodyPartComponent>(newPart.Value, out var partComp))
                    {
                        // Set up the part component properly
                        if (parentPartComp.Children.TryGetValue(regen.SlotId, out var slot))
                        {
                            partComp.ParentSlot = slot;
                        }
                        partComp.Body = body;
                        Dirty(newPart.Value, partComp);
                        attached = true;
                    }
                }
            }
            }
        }
        else if (regen.SlotId != null)
        {
            // Attach directly to body (for head or root-level parts)
            // Spawn directly into the body's root container
            var containerId = SharedBodySystem.BodyRootContainerId;
            if (TrySpawnInContainer(regen.PartPrototypeId, body, containerId, out newPart))
            {
                if (TryComp<BodyPartComponent>(newPart.Value, out var partComp))
                {
                    partComp.Body = body;
                    Dirty(newPart.Value, partComp);
                    attached = true;
                }
            }
        }

        if (!attached || newPart == null)
        {
            if (newPart != null)
                Del(newPart.Value);
            return null;
        }

        if (!TryComp<BodyPartComponent>(newPart.Value, out var finalPartComp))
        {
            Del(newPart.Value);
            return null;
        }

        // Set the new part to very low health (5% of max)
        if (TryComp<DamageableComponent>(newPart, out var damageable))
        {
            var maxHealth = damageable.Damage.GetTotal();
            if (maxHealth <= 0)
                maxHealth = 100f; // Default if no damage types defined

            // Apply damage to bring it to 5% health
            var targetDamage = maxHealth * (1f - InitialHealthPercent);
            var damageNeeded = targetDamage;

            if (damageNeeded > 0)
            {
                var damageSpec = new DamageSpecifier();
                
                // Distribute damage across all damage types proportionally
                var damageTypeCount = damageable.Damage.DamageDict.Count;
                if (damageTypeCount > 0)
                {
                    foreach (var (damageType, _) in damageable.Damage.DamageDict)
                    {
                        damageSpec.DamageDict[damageType] = damageNeeded / damageTypeCount;
                    }
                }
                else
                {
                    // If no damage types, use a default
                    damageSpec.DamageDict["Blunt"] = damageNeeded;
                }

                _damageable.TryChangeDamage(newPart, damageSpec, ignoreResistances: true);
            }
        }

        return newPart;
    }

    /// <summary>
    /// Marks a slime limb/head as severed and starts the regeneration process on the body.
    /// </summary>
    public void MarkLimbSevered(
        EntityUid body,
        string partPrototypeId,
        string? slotId = null,
        EntityUid? parentPart = null)
    {
        var regenComp = EnsureComp<SlimeLimbRegenerationComponent>(body);
        
        var regen = new SlimeRegenerationData
        {
            SeveredTime = _timing.CurTime,
            PartPrototypeId = partPrototypeId,
            SlotId = slotId,
            ParentPart = parentPart != null ? GetNetEntity(parentPart.Value) : null,
            HasRegenerated = false,
            RegeneratedPart = null
        };
        
        regenComp.Regenerations.Add(regen);
        Dirty(body, regenComp);
    }
}

