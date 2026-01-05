// SPDX-FileCopyrightText: 2025 taydeo <td12233a@gmail.com>
//
// SPDX-License-Identifier: MIT

using Content.Shared.Implants;

namespace Content.Client.Implants;

/// <summary>
/// Client-side implementation of SharedSubdermalImplantSystem.
/// Minimal implementation for dependency injection compatibility.
/// </summary>
public sealed class SubdermalImplantSystem : SharedSubdermalImplantSystem
{
    // Client doesn't need to override any methods - the base class handles everything needed on client
}
