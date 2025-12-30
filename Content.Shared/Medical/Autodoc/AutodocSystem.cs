// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Buckle.Components;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Medical.Autodoc;
using Content.Shared.Popups;

namespace Content.Shared.Medical.Autodoc;

/// <summary>
/// Base system for autodoc operations.
/// </summary>
public abstract partial class SharedAutodocSystem : EntitySystem
{
    [Dependency] protected readonly SharedPopupSystem Popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AutodocComponent, NewLinkEvent>(OnNewLink);
        SubscribeLocalEvent<AutodocComponent, PortDisconnectedEvent>(OnPortDisconnected);
    }

    private void OnNewLink(Entity<AutodocComponent> ent, ref NewLinkEvent args)
    {
        if (args.Sink == ent.Comp.OperatingTablePort)
        {
            ent.Comp.OperatingTable = args.Source;
            Dirty(ent, ent.Comp);
        }
    }

    private void OnPortDisconnected(Entity<AutodocComponent> ent, ref PortDisconnectedEvent args)
    {
        if (args.Sink == ent.Comp.OperatingTablePort && ent.Comp.OperatingTable == args.Source)
        {
            ent.Comp.OperatingTable = null;
            Dirty(ent, ent.Comp);
        }
    }

    /// <summary>
    /// Gets the patient strapped to the linked operating table.
    /// </summary>
    protected EntityUid? GetStrappedPatient(Entity<AutodocComponent> autodoc)
    {
        if (autodoc.Comp.OperatingTable == null)
            return null;

        if (!TryComp<BuckleComponent>(autodoc.Comp.OperatingTable, out var buckle) || buckle.BuckledEntities.Count == 0)
            return null;

        return buckle.BuckledEntities[0];
    }
}

