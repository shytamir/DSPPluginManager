# Thunderstore Package Contract

## Current status

The package pipeline is operational and its product payload is a compiled,
versioned `net472` `DSPPluginManager.dll`. The assembly is the RM-01 foundation:
it intentionally exposes no public plugin contract and implements no startup,
discovery, or lifecycle behavior. The ZIP remains build evidence, is not
installable software, and must not be published as a release.

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

The build stamps `M.m.N.0` as the DLL's assembly and file version and stamps the
diagnostic release label as its informational/product version. Validation reads
those values back from the compiled assembly before packaging.

## Inputs

- `VERSION`: major and minor version source.
- `packaging/manifest.template.json`: manifest with one
  `{{VERSION_NUMBER}}` placeholder.
- `packaging/README.md`: temporary UTF-8 store README.
- `LICENSE`: repository license.
- `global.json`: exact .NET SDK used locally and in CI.
- `src/DSPPluginManager`: compiled internal pre-activation foundation.
- `tests/DSPPluginManager.Tests`: focused executable foundation, internal path
  model, bootstrap-diagnostic, and reserved-dependency resolution checks.
- CI run number and triggering commit: automatic patch and diagnostic identity.

The placeholder icon is generated during packaging. Compiled outputs, restored
build dependencies, test executables, and packages remain under `artifacts/`
and are not committed. The pinned .NET Framework 4.7.2 reference-assembly
package is a private build input and is not copied into product artifacts.

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
- locked restore through the pinned .NET SDK and private `net472` reference
  assemblies;
- successful compilation with C# 7.3-compatible code and no warnings;
- focused inspection of target framework, public surface, and version metadata;
- focused path normalization, containment, immutability, filesystem-conflict,
  and working-directory-independence checks;
- focused bootstrap failure-record destination, content, multiline-formatting,
  and non-throwing write-failure checks;
- fixture-based reserved-dependency success, identity, integrity, conflict,
  duplicate-placement, and ordinary-resolution pass-through checks;
- required root names and case;
- standard ZIP layout without duplicate or backslash entry names;
- package size below Thunderstore's documented limit;
- required manifest v1 fields;
- allowed package-name characters and length;
- three-number semantic version matching the generated version;
- description length, dependencies shape, and website URL shape;
- non-empty UTF-8 Markdown README;
- 256 by 256 PNG icon;
- exact compiled DLL and `BUILD-INFO.txt` hashes/content;
- the absence of unexpected package entries, including tests, PDBs, restored
  reference assemblies, and third-party runtime DLLs;
- the three-class version mapping.

It deliberately does not validate plugin-host behavior, installation paths,
runtime dependency placement, user instructions, or final branding while those
contracts remain undecided.

## Hosted workflow

Every push to `main`, and each manual workflow dispatch, runs
`.github/workflows/package.yml`. A passing run uploads the package ZIP,
`BUILD-INFO.txt`, `PACKAGE-REPORT.md`, and `DEPENDENCY-REPORT.md` as one GitHub
Actions artifact retained for 30 days.

The uploaded GitHub artifact is build evidence only. It is not a Thunderstore
publication.
