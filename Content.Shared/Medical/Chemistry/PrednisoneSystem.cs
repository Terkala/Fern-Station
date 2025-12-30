// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Medical.Integrity;

namespace Content.Shared.Medical.Chemistry;

/// <summary>
/// System that manages immunosuppressant effects on integrity.
/// </summary>
public abstract class SharedPrednisoneSystem : EntitySystem
{
    [Dependency] protected readonly SharedIntegritySystem Integrity = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PrednisoneComponent, ComponentStartup>(OnPrednisoneStartup);
        SubscribeLocalEvent<PrednisoneComponent, ComponentShutdown>(OnPrednisoneShutdown);
    }

    protected void OnPrednisoneStartup(EntityUid uid, PrednisoneComponent component, ComponentStartup args)
    {
        // Integrity bonus is handled by server system's Update method
        // which recalculates total bonus from all active components
    }

    protected void OnPrednisoneShutdown(EntityUid uid, PrednisoneComponent component, ComponentShutdown args)
    {
        // Integrity bonus is handled by server system's Update method
        // which recalculates total bonus from all active components
    }
}

