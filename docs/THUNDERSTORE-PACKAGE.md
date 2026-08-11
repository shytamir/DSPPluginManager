# Thunderstore Package Contract

## Current status

The package pipeline is operational, but its product payload is a generated,
zero-byte `DSPPluginManager.dll`. The ZIP exists only to validate versioning,
Thunderstore structure, package integrity, and GitHub Actions automation before
the runtime project exists. It is not installable software and must not be
published as a release.

## ZIP layout

```text
manifest.json
README.md
icon.png
LICENSE
BUILD-INFO.txt
DSPPluginManager.dll
```

The first three files are Thunderstore's
[case-sensitive required root files](https://wiki.thunderstore.io/mods/creating-a-package).
The icon is a generated 256 by 256 placeholder PNG. `README.md` and the
manifest contain temporary copy. The remaining files are package-specific
extras used to exercise artifact and provenance checks.

No final game installation path is established by this temporary root layout.

## Version mapping

`VERSION` supplies manually selected major (`M`) and minor (`m`) components.
The GitHub Actions run number supplies patch/sequence (`N`):

```text
Package and semantic version: M.m.N
Assembly and file version:    M.m.N.0
Diagnostic release label:     M.m.N.<7-character-commit>
```

The package version is written to `manifest.json`. All three classes, the full
source commit, and workflow sequence are written to `BUILD-INFO.txt` and
validated before packaging.

Because the current DLL is empty, `M.m.N.0` is declared build metadata rather
than real assembly/file metadata. Once a compiled project exists, the build
must stamp and inspect those properties on the DLL before this placeholder
exception is removed.

## Inputs

- `VERSION`: major and minor version source.
- `packaging/manifest.template.json`: manifest with one
  `{{VERSION_NUMBER}}` placeholder.
- `packaging/README.md`: temporary UTF-8 store README.
- `LICENSE`: repository license.
- CI run number and triggering commit: automatic patch and diagnostic identity.

The placeholder icon and empty DLL are generated during the build. Generated
files and packages remain under `artifacts/` and are not committed.

The build also acquires the manager-owned Harmony stack described by
`dependencies/managed-dependencies.lock.json`. It validates exact NuGet package
and DLL hashes, the selected `net35` assets, assembly identities, direct managed
references, dependency closure, and four retained MIT notices. Generated files
are staged under `artifacts/managed-dependencies/`; the plugin compile-reference
directory exposes only `0Harmony.dll`, while MonoMod and Mono.Cecil remain
implementation dependencies.

The dependency stack is not added to the temporary Thunderstore ZIP. Its final
installation layout remains a product packaging decision, and the current ZIP
must not imply one.

## Build and validation

Run `build.cmd` for a local sequence-1 package, or pass another sequence and
commit to the PowerShell entry point:

```powershell
./scripts/Invoke-PackageBuild.ps1 -Sequence 42 -Commit <full-commit>
```

The pipeline validates generic Thunderstore requirements and build integrity:

- exact acquisition and staging of the locked manager-owned Harmony closure;
- required root names and case;
- standard ZIP layout without duplicate or backslash entry names;
- package size below Thunderstore's documented limit;
- required manifest v1 fields;
- allowed package-name characters and length;
- three-number semantic version matching the generated version;
- description length, dependencies shape, and website URL shape;
- non-empty UTF-8 Markdown README;
- 256 by 256 PNG icon;
- exact placeholder DLL and `BUILD-INFO.txt` hashes/content;
- the three-class version mapping.

It deliberately does not validate product behavior, installation paths,
dependencies, user instructions, or final branding while those contracts remain
undecided.

## Hosted workflow

Every push to `main`, and each manual workflow dispatch, runs
`.github/workflows/package.yml`. A passing run uploads the package ZIP,
`BUILD-INFO.txt`, `PACKAGE-REPORT.md`, and `DEPENDENCY-REPORT.md` as one GitHub
Actions artifact retained for 30 days.

The uploaded GitHub artifact is build evidence only. It is not a Thunderstore
publication.
