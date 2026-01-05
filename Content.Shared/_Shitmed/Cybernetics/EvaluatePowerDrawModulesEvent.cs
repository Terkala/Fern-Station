// SPDX-FileCopyrightText: 2025 terkala <appleorange64@gmail.com>
//
// SPDX-License-Identifier: MIT

namespace Content.Shared._Shitmed.Cybernetics;

/// <summary>
/// Event raised when power-drawing modules should be evaluated.
/// Server-side systems should subscribe to this to evaluate power-drawing modules.
/// </summary>
[ByRefEvent]
public struct EvaluatePowerDrawModulesEvent
{
    public EntityUid Body;

    public EvaluatePowerDrawModulesEvent(EntityUid body)
    {
        Body = body;
    }
}
