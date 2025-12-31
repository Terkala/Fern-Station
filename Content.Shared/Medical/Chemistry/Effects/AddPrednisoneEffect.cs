// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.EntityEffects;
using Content.Shared.FixedPoint;
using Content.Shared.Medical.Chemistry;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Chemistry.Effects;

/// <summary>
/// Entity effect that adds PrednisoneComponent to provide temporary integrity bonus.
/// This effect is applied when immunosuppressant is metabolized.
/// The component is automatically removed when immunosuppressant is no longer in the bloodstream or duration expires.
/// </summary>
public sealed partial class AddPrednisoneEffect : EntityEffect
{
    /// <summary>
    /// Integrity bonus per unit of immunosuppressant.
    /// </summary>
    [DataField]
    public FixedPoint2 IntegrityBonusPerUnit = FixedPoint2.New(1.0);

    /// <summary>
    /// Duration in seconds per unit of immunosuppressant.
    /// </summary>
    [DataField]
    public float DurationPerUnit = 60.0f; // 1 minute per unit

    public override void Effect(EntityEffectBaseArgs args)
    {
        if (args is not EntityEffectReagentArgs reagentArgs)
            return;

        // Calculate integrity bonus and duration based on quantity
        var quantity = reagentArgs.Quantity;
        var integrityBonus = IntegrityBonusPerUnit * quantity;
        var durationSeconds = DurationPerUnit * (float)quantity;

        // Get or create component
        var component = args.EntityManager.EnsureComponent<PrednisoneComponent>(args.TargetEntity);
        
        // If component already exists, take the maximum bonus
        if (component.IntegrityBonus < integrityBonus)
        {
            component.IntegrityBonus = integrityBonus;
        }
        
        // Add to duration (stacking multiple doses extends the timer)
        // Duration is stored as seconds remaining, server will convert to expiration time
        component.Duration += durationSeconds;

        args.EntityManager.Dirty(args.TargetEntity, component);
    }

    protected override string? ReagentEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
    {
        return "Provides temporary integrity bonus to reduce bio-rejection damage.";
    }
}

