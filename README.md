# DSP Plugin Manager

DSP Plugin Manager is an early-stage plugin host and lifecycle manager for
[Dyson Sphere Program](https://store.steampowered.com/app/1366540/Dyson_Sphere_Program/).
It is being built to remove the mandatory BepInEx dependency from a focused set
of DSP mods while preserving the hosting behavior those mods rely on.

The project is currently defining its contracts and architecture. It does not
yet provide an installable loader, a public plugin API, or a migration-ready
package.

The durable product and engineering contract is maintained in
[docs/PROJECT.md](docs/PROJECT.md).

## Intended first capability

The first useful release is expected to provide a small DSP-specific runtime
that can:

- start inside the supported DSP Unity Mono process;
- discover managed plugins from a bounded, documented location;
- read stable plugin identity, version, dependency, and incompatibility
  metadata;
- reject invalid or duplicate plugins with actionable diagnostics;
- compute a deterministic load order;
- activate plugins and supervise their lifecycle without hiding failures;
- provide the per-plugin paths, logging, and configuration services required by
  the initial consumer mods;
- offer a deliberate migration path for the repository owner's established DSP
  mod pattern.

The exact migration mechanism is not yet selected. Source compatibility and
drop-in binary compatibility are different commitments; neither is implied by
this bootstrap documentation.

## Initial target

- Dyson Sphere Program on Windows.
- The game's Unity Mono runtime.
- Existing mod-facing baseline of C# 7.3 and .NET Framework 4.7.2 (`net472`).
- The lifecycle and service subset demonstrated by the initial consumer mods.

Harmony-based game patches may continue to be used by plugins, but Harmony is a
separate library and is not part of the lifecycle manager's reimplementation
scope by default.

## Deliberate limits

DSP Plugin Manager is not currently intended to be:

- a general-purpose replacement for BepInEx across games and runtimes;
- an IL2CPP, macOS, or Linux loader;
- a graphical mod installer, updater, or package catalog;
- a Thunderstore client;
- a security sandbox for untrusted plugins;
- a promise of compatibility with every existing BepInEx plugin or API;
- a rewrite of Harmony or other patching libraries.

These limits keep the project centered on replacing the actual dependency used
by the owner's DSP projects rather than reproducing an upstream framework in
full.

## Repository status

The project contract, repository guidance, semantic versioning, and a temporary
Thunderstore package pipeline are established. The pipeline packages an
intentionally empty DLL and placeholder store assets; it validates automation
only and does not produce installable software.

Run the local package pipeline with:

```text
build.cmd
```

`VERSION` supplies major and minor values. The build sequence supplies the
patch value, producing package version `M.m.N`, declared assembly/file version
`M.m.N.0`, and diagnostic label `M.m.N.<commit>`. Pushes to `main` run the same
pipeline in GitHub Actions with the workflow run number as `N`.

The temporary contract is documented in
[docs/THUNDERSTORE-PACKAGE.md](docs/THUNDERSTORE-PACKAGE.md). Installation and
usage instructions will be added only when their corresponding behavior exists
and has been validated.

## License

DSP Plugin Manager is licensed under the [Apache License 2.0](LICENSE).

BepInEx is a behavioral and scope reference, not a code source for this
repository. Third-party components retain their own licenses and must be
reviewed explicitly before redistribution.
