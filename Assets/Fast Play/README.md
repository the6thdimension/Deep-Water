# Fast Play

Deep Water enters play mode **without a domain reload**: Project Settings > Editor > Enter Play Mode Options has "Reload Domain" off; the scene still reloads. That turns a ~10 s play-mode entry (a domain reload plus the scene) into roughly the scene load alone. Decided by SIX on 2026-10-25; measured in `BUILD-SPEED-LEDGER.md`, round 6.

## The price, and how it is paid

Without a reload, static fields keep their values from the last play session. Two tests guard against that:

- **`Tests/Editor/StaticStateTests`** reflects over every first-party runtime assembly (Sea State, General Quarters, Air Wing, Guided Fury, Carrier Ops, RH Radar Suite, RH Shared, DeepWater.Input, the Reckoning spike).
  - Every mutable static must either be reset at play start, by a `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` method in its type, or be listed in `Allowed` with the reason it may outlive a session.
  - Immutable statics pass by themselves: consts, readonly values, strings, delegates, and stateless strategy singletons such as `ProportionalNavigation.Instance`.
  - **A new static fails the test until it is reset or justified.**
- **`Tests/Editor/SameFixAcrossPlaysTests`** plays `GQ_Range` twice in a row without a reload.
  - Each play starts the saturation raid and captures the combat core's Fix at the same tick.
  - The two Fixes must match: static state leaking from the first play into the second would change it.

## Adding a static

```csharp
static readonly List<Foo> s_live = new List<Foo>();

// Play mode starts without a domain reload: clear the last session's state before the first scene loads.
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
static void ResetStatics() => s_live.Clear();
```

Static events need the same: set them to `null` in the reset.

## Not covered

Vendor code (Obi, NWH, RCC, SensorToolkit, FlightSimLite, ML-Agents) was not audited. Those packs serve the legacy scenes (CVN-78 FORD, DDG Engagement, SENTINAL, Helo). If one of those scenes misbehaves on its second play, use **Deep Water > Fast Play > Reload Domain On Play** to turn the reload back on (the menu shows a tick while it reloads).
