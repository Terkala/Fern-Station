// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Shared.Body.Components;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Content.Shared.Medical.Surgery;
using Robust.Shared.Utility;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Content.Server.Medical.CyberLimb;

namespace Content.Server.Implants;

/// <summary>
/// System that handles storage implant organ functionality:
/// - Right-click access when tissue layer is opened on torso
/// - Ensuring it functions as inventory when removed from body
/// </summary>
public sealed class StorageImplantOrganSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly CyberneticsUpkeepSystem _cyberneticsUpkeep = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Centralized subscription to avoid duplicates with CyberneticsUpkeepSystem
        SubscribeLocalEvent<BodyComponent, GetVerbsEvent<Verb>>(OnGetBodyVerbs);
    }

    /// <summary>
    /// Adds verbs to the body entity to access storage implant when tissue layer is opened on torso.
    /// </summary>
    private void OnGetBodyVerbs(EntityUid uid, BodyComponent component, GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        // Find torso
        var torso = _body.GetBodyChildrenOfType(uid, BodyPartType.Torso, component).FirstOrDefault();
        if (torso == default)
            return;

        // Check if tissue layer is retracted on torso
        if (!TryComp<SurgeryLayerComponent>(torso.Id, out var layer) || !layer.TissueRetracted)
            return;

        // Find storage implant organ in torso
        if (!TryComp<BodyPartComponent>(torso.Id, out var partComp))
            return;

        if (!partComp.Organs.ContainsKey("storage_implant"))
            return;

        // Get the storage implant organ
        var organContainerId = SharedBodySystem.GetOrganContainerId("storage_implant");
        if (!_container.TryGetContainer(torso.Id, organContainerId, out var organContainer))
            return;

        EntityUid? storageImplant = null;
        foreach (var organ in organContainer.ContainedEntities)
        {
            if (HasComp<StorageComponent>(organ) && MetaData(organ).EntityPrototype?.ID == "OrganStorageImplant")
            {
                storageImplant = organ;
                break;
            }
        }

        if (storageImplant == null || !TryComp<StorageComponent>(storageImplant, out var storage))
            return;

        // Add verb to access storage
        args.Verbs.Add(new Verb
        {
            Act = () =>
            {
                // Open storage UI
                _storage.OpenStorageUI(storageImplant.Value, args.User, storage);
            },
            Text = "Access Storage Implant",
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/inventory.svg.192dpi.png")),
            Priority = 1
        });

        // Dispatch to CyberneticsUpkeepSystem for cybernetics storage verbs
        _cyberneticsUpkeep.OnGetBodyVerbs(uid, component, args);
    }
}

