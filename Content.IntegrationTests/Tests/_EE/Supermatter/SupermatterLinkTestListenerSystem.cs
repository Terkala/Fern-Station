// SPDX-FileCopyrightText: 2026 Funkystation Contributors
//
// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Reflection;

namespace Content.IntegrationTests.Tests._EE.Supermatter;

/// <summary>
/// Records <see cref="SignalReceivedEvent"/> raised on sinks for device-link integration tests.
/// </summary>
[Reflect(false)]
public sealed class SupermatterLinkTestListenerSystem : EntitySystem
{
    public readonly List<(EntityUid Sink, string Port)> Received = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DeviceLinkSinkComponent, SignalReceivedEvent>(OnSignal);
    }

    private void OnSignal(EntityUid uid, DeviceLinkSinkComponent comp, ref SignalReceivedEvent args)
    {
        Received.Add((uid, args.Port));
    }

    public void Clear()
    {
        Received.Clear();
    }
}
