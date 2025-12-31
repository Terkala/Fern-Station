// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Server.Body.Systems;
using Content.Server.Medical.Surgery;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Medical.Autodoc;
using Content.Shared.Medical.Surgery;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using System.Linq;

namespace Content.Server.Medical.Autodoc;

/// <summary>
/// Server-side autodoc system that handles the 3 operation modes.
/// </summary>
public sealed class AutodocSystem : SharedAutodocSystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SurgerySystem _surgery = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        Subs.BuiEvents<AutodocComponent>(AutodocUIKey.Key, subs =>
        {
            subs.Event<AutodocSetModeMessage>(OnSetMode);
            subs.Event<AutodocSelectOrganMessage>(OnSelectOrgan);
            subs.Event<AutodocActivateMessage>(OnActivate);
        });

        // Update UI when items are inserted/removed from organ slot
        SubscribeLocalEvent<AutodocComponent, EntInsertedIntoContainerMessage>(OnItemInserted);
        SubscribeLocalEvent<AutodocComponent, EntRemovedFromContainerMessage>(OnItemRemoved);
    }

    private void OnItemInserted(Entity<AutodocComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == ent.Comp.OrganSlot)
        {
            UpdateUI(ent);
        }
    }

    private void OnItemRemoved(Entity<AutodocComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == ent.Comp.OrganSlot)
        {
            UpdateUI(ent);
        }
    }

    private void OnSetMode(Entity<AutodocComponent> ent, ref AutodocSetModeMessage msg)
    {
        ent.Comp.Mode = msg.Mode;
        Dirty(ent, ent.Comp);
        UpdateUI(ent);
    }

    private void OnSelectOrgan(Entity<AutodocComponent> ent, ref AutodocSelectOrganMessage msg)
    {
        ent.Comp.SelectedOrgan = GetEntity(msg.Organ);
        Dirty(ent, ent.Comp);
        UpdateUI(ent);
    }

    private void OnActivate(Entity<AutodocComponent> ent, ref AutodocActivateMessage msg)
    {
        var patient = GetStrappedPatient(ent);
        if (patient == null)
        {
            Popup.PopupEntity("No patient strapped to operating table.", ent, ent);
            return;
        }

        switch (ent.Comp.Mode)
        {
            case AutodocMode.OrganImplant:
                PerformOrganImplant(ent, patient.Value);
                break;
            case AutodocMode.MedicalCare:
                PerformMedicalCare(ent, patient.Value);
                break;
            case AutodocMode.OrganRemoval:
                PerformOrganRemoval(ent, patient.Value);
                break;
        }
    }

    private void PerformOrganImplant(Entity<AutodocComponent> autodoc, EntityUid patient)
    {
        if (!_itemSlots.TryGetSlot(autodoc, autodoc.Comp.OrganSlot, out var slot) || slot.Item == null)
        {
            Popup.PopupEntity("No organ in autodoc slot.", autodoc, autodoc);
            return;
        }

        var organ = slot.Item.Value;

        // Find appropriate body part to implant organ into
        if (!TryComp<BodyComponent>(patient, out var body))
        {
            Popup.PopupEntity("Patient has no body.", autodoc, autodoc);
            return;
        }

        // Get torso (most organs go in torso)
        var torso = _body.GetBodyChildrenOfType(patient, BodyPartType.Torso, body).FirstOrDefault();
        if (torso == default)
        {
            Popup.PopupEntity("Cannot find torso to implant organ.", autodoc, autodoc);
            return;
        }

        // Perform implant using surgery system
        // Autodoc uses high-quality tools (no improvised tag), and operating table is linked
        if (_surgery.TryInstallImplant(organ, patient, torso.Id, autodoc, null, autodoc.Comp.OperatingTable))
        {
            Popup.PopupEntity("Organ successfully implanted.", autodoc, autodoc);
            _itemSlots.TryEject(autodoc, autodoc.Comp.OrganSlot, null, out _);
        }
        else
        {
            Popup.PopupEntity("Organ implant failed.", autodoc, autodoc);
        }
    }

    private void PerformMedicalCare(Entity<AutodocComponent> autodoc, EntityUid patient)
    {
        // Apply basic brute/burn healing
        var healing = new DamageSpecifier();
        healing.DamageDict.Add("Brute", -10);
        healing.DamageDict.Add("Burn", -10);

        _damageable.TryChangeDamage(patient, healing);
        Popup.PopupEntity("Medical care applied.", autodoc, autodoc);
    }

    private void PerformOrganRemoval(Entity<AutodocComponent> autodoc, EntityUid patient)
    {
        if (autodoc.Comp.SelectedOrgan == null)
        {
            Popup.PopupEntity("No organ selected for removal.", autodoc, autodoc);
            return;
        }

        var organ = autodoc.Comp.SelectedOrgan.Value;

        // Verify organ is in patient
        if (!TryComp<BodyComponent>(patient, out var body) || !_body.GetBodyOrgans(patient, body).Any(o => o.Id == organ))
        {
            Popup.PopupEntity("Selected organ not found in patient.", autodoc, autodoc);
            return;
        }

        // Perform removal using surgery system
        if (_surgery.TryRemoveImplant(organ, patient))
        {
            Popup.PopupEntity("Organ successfully removed.", autodoc, autodoc);
            autodoc.Comp.SelectedOrgan = null;
            Dirty(autodoc, autodoc.Comp);
        }
        else
        {
            Popup.PopupEntity("Organ removal failed.", autodoc, autodoc);
        }
    }

    public void UpdateUI(Entity<AutodocComponent> ent)
    {
        var patient = GetStrappedPatient(ent);
        var hasOrgan = _itemSlots.TryGetSlot(ent, ent.Comp.OrganSlot, out var slot) && slot.Item != null;

        // Get list of organs for removal mode
        var availableOrgans = new List<NetEntity>();
        if (patient != null && TryComp<BodyComponent>(patient, out var body))
        {
            var organs = _body.GetBodyOrgans(patient.Value, body);
            foreach (var (organUid, _) in organs)
            {
                availableOrgans.Add(GetNetEntity(organUid));
            }
        }

        var state = new AutodocBoundUserInterfaceState(
            ent.Comp.Mode,
            ent.Comp.IsActive,
            ent.Comp.SelectedOrgan != null ? GetNetEntity(ent.Comp.SelectedOrgan.Value) : null,
            patient != null,
            hasOrgan,
            availableOrgans
        );

        if (TryComp<UserInterfaceComponent>(ent, out var uiComp))
            _ui.SetUiState((ent, uiComp), AutodocUIKey.Key, state);
    }
}

