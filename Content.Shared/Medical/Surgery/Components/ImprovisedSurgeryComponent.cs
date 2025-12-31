// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Medical.Surgery.Components;

/// <summary>
/// Base component for tracking improvised surgery steps performed on a body part.
/// Each improvised operation type should have its own component that inherits from this.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImprovisedSurgeryComponent : Component
{
    /// <summary>
    /// The integrity cost that was applied when this improvised step was performed.
    /// This will be removed when the repair operation is completed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 IntegrityCost { get; set; } = FixedPoint2.Zero;

    /// <summary>
    /// The operation ID that was performed improvised.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<Content.Shared.Medical.Surgery.Operations.SurgeryOperationPrototype> OperationId { get; set; } = default!;
}

/// <summary>
/// Tracks that bones were removed via improvised method (bashing).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImprovisedBoneRemovalComponent : ImprovisedSurgeryComponent
{
}

/// <summary>
/// Tracks that tissue was cut via improvised method (slashing).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImprovisedTissueCutComponent : ImprovisedSurgeryComponent
{
}

/// <summary>
/// Tracks that bleeders were clamped via improvised method (wirecutters/heat).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImprovisedBleederClampingComponent : ImprovisedSurgeryComponent
{
}

/// <summary>
/// Tracks that wounds were cauterized via improvised method (heat damage).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImprovisedCauterizationComponent : ImprovisedSurgeryComponent
{
}

/// <summary>
/// Tracks that blood vessels were severed via improvised method (slashing).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImprovisedSeverBloodVesselsComponent : ImprovisedSurgeryComponent
{
}

/// <summary>
/// Tracks that tissue was retracted via improvised method (prying).
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImprovisedRetractTissueComponent : ImprovisedSurgeryComponent
{
}
