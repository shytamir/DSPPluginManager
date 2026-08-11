using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Mono.Cecil;

namespace DSPPluginManager.Discovery
{
    internal sealed class PluginMetadataReader
    {
        private readonly PluginInspectionReferences references;
        private readonly AssemblyNameDefinition contractIdentity;

        internal PluginMetadataReader(PluginInspectionReferences references)
        {
            this.references = references ??
                throw new ArgumentNullException("references");
            using (AssemblyDefinition contract = AssemblyDefinition.ReadAssembly(
                references.ContractAssemblyPath,
                new ReaderParameters { InMemory = true, ReadSymbols = false }
            ))
            {
                contractIdentity = CloneIdentity(contract.Name);
            }
        }

        internal PluginInspectionResult Inspect(string candidatePath)
        {
            if (string.IsNullOrWhiteSpace(candidatePath) ||
                !Path.IsPathRooted(candidatePath) ||
                IsDriveRelative(candidatePath))
            {
                throw new ArgumentException(
                    "Candidate assembly path must be absolute.",
                    "candidatePath"
                );
            }
            string path = Path.GetFullPath(candidatePath);

            ManagedImageKind imageKind;
            try
            {
                imageKind = ManagedImageProbe.Inspect(path);
            }
            catch (Exception exception)
                when (exception is IOException ||
                      exception is UnauthorizedAccessException)
            {
                return Rejected(
                    PluginInspectionDiagnosticCode.MalformedAssembly,
                    path,
                    exception.GetType().Name
                );
            }
            if (imageKind == ManagedImageKind.NonManaged)
            {
                return Rejected(
                    PluginInspectionDiagnosticCode.NonManagedFile,
                    path,
                    "No CLR metadata directory was found."
                );
            }
            if (imageKind == ManagedImageKind.Malformed)
            {
                return Rejected(
                    PluginInspectionDiagnosticCode.MalformedAssembly,
                    path,
                    "The portable executable structure is malformed."
                );
            }

            string candidateDirectory = Path.GetDirectoryName(path);
            using (BoundedAssemblyResolver resolver = new BoundedAssemblyResolver(
                references,
                candidateDirectory
            ))
            {
                try
                {
                    using (AssemblyDefinition assembly =
                        AssemblyDefinition.ReadAssembly(
                            path,
                            new ReaderParameters
                            {
                                AssemblyResolver = resolver,
                                InMemory = true,
                                ReadSymbols = false,
                                ReadingMode = ReadingMode.Deferred
                            }
                        ))
                    {
                        return InspectAssembly(assembly, path);
                    }
                }
                catch (AssemblyResolutionException exception)
                {
                    return Rejected(
                        PluginInspectionDiagnosticCode.MissingReference,
                        path,
                        exception.AssemblyReference == null ?
                            exception.Message :
                            exception.AssemblyReference.FullName
                    );
                }
                catch (BadImageFormatException exception)
                {
                    return Rejected(
                        PluginInspectionDiagnosticCode.MalformedAssembly,
                        path,
                        exception.GetType().Name
                    );
                }
                catch (InvalidDataException exception)
                {
                    return Rejected(
                        PluginInspectionDiagnosticCode.InvalidMetadata,
                        path,
                        exception.Message
                    );
                }
                catch (ResolutionException exception)
                {
                    return Rejected(
                        PluginInspectionDiagnosticCode.MissingReference,
                        path,
                        exception.Message
                    );
                }
                catch (IOException exception)
                {
                    return Rejected(
                        PluginInspectionDiagnosticCode.MalformedAssembly,
                        path,
                        exception.GetType().Name
                    );
                }
                catch (Exception exception)
                    when (exception is ArgumentException ||
                          exception is InvalidOperationException ||
                          exception is IndexOutOfRangeException ||
                          exception is NotSupportedException ||
                          exception is OverflowException)
                {
                    return Rejected(
                        PluginInspectionDiagnosticCode.MalformedAssembly,
                        path,
                        exception.GetType().Name
                    );
                }
            }
        }

        private PluginInspectionResult InspectAssembly(
            AssemblyDefinition assembly,
            string path
        )
        {
            TypeDefinition[] markedTypes = AllTypes(assembly.MainModule.Types)
                .Where(type => MarkerAttributes(type).Any())
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToArray();
            if (markedTypes.Length == 0)
            {
                return Rejected(
                    PluginInspectionDiagnosticCode.NoPluginType,
                    path,
                    "No type carries the supported plugin marker."
                );
            }

            List<RecognizedPluginCandidate> candidates =
                new List<RecognizedPluginCandidate>();
            string contentHash = ComputeContentHash(path);
            foreach (TypeDefinition type in markedTypes)
            {
                CustomAttribute[] markers = MarkerAttributes(type).ToArray();
                if (markers.Length != 1)
                {
                    return Rejected(
                        PluginInspectionDiagnosticCode.InvalidMetadata,
                        path,
                        "Type '" + type.FullName +
                        "' has more than one plugin marker."
                    );
                }
                if (!type.IsClass || type.IsInterface || type.BaseType == null)
                {
                    return Rejected(
                        PluginInspectionDiagnosticCode.UnsupportedPluginType,
                        path,
                        "Marked type '" + type.FullName +
                        "' is not a supported class."
                    );
                }
                if (!InheritsSupportedBase(type))
                {
                    return Rejected(
                        PluginInspectionDiagnosticCode.UnsupportedPluginType,
                        path,
                        "Marked type '" + type.FullName +
                        "' does not inherit the supported plugin base."
                    );
                }
                if (type.IsAbstract)
                {
                    return Rejected(
                        PluginInspectionDiagnosticCode.AbstractPluginType,
                        path,
                        "Marked type '" + type.FullName + "' is abstract."
                    );
                }

                string identifier;
                string displayName;
                Version version;
                string metadataError;
                if (!TryReadMetadata(
                        markers[0],
                        out identifier,
                        out displayName,
                        out version,
                        out metadataError
                    ))
                {
                    return Rejected(
                        PluginInspectionDiagnosticCode.InvalidMetadata,
                        path,
                        "Type '" + type.FullName + "': " + metadataError
                    );
                }

                candidates.Add(new RecognizedPluginCandidate(
                    identifier,
                    PluginContractRules.GetIdentifierComparisonKey(identifier),
                    displayName,
                    version,
                    assembly.Name.FullName,
                    path,
                    type.FullName,
                    contentHash
                ));
            }

            if (candidates.Count != 1)
            {
                return Rejected(
                    PluginInspectionDiagnosticCode.MultiplePluginTypes,
                    path,
                    "The assembly contains " + candidates.Count +
                    " eligible plugin types."
                );
            }
            return PluginInspectionResult.Recognized(candidates[0]);
        }

        private bool InheritsSupportedBase(TypeDefinition type)
        {
            HashSet<string> visited = new HashSet<string>(StringComparer.Ordinal);
            TypeReference current = type.BaseType;
            while (current != null)
            {
                if (IsContractType(current, PluginContractRules.BaseTypeName))
                {
                    return true;
                }
                if (string.Equals(
                        current.FullName,
                        "System.Object",
                        StringComparison.Ordinal
                    ))
                {
                    return false;
                }

                string key = current.Scope + "|" + current.FullName;
                if (!visited.Add(key))
                {
                    throw new InvalidDataException(
                        "The plugin base-type chain contains a cycle."
                    );
                }
                TypeDefinition resolved = current.Resolve();
                if (resolved == null)
                {
                    return false;
                }
                current = resolved.BaseType;
            }
            return false;
        }

        private bool TryReadMetadata(
            CustomAttribute marker,
            out string identifier,
            out string displayName,
            out Version version,
            out string error
        )
        {
            identifier = null;
            displayName = null;
            version = null;
            error = null;
            if (!IsContractType(
                    marker.Constructor.DeclaringType,
                    PluginContractRules.MetadataTypeName
                ) || marker.ConstructorArguments.Count != 3)
            {
                error = "The plugin marker constructor encoding is unsupported.";
                return false;
            }
            foreach (CustomAttributeArgument argument in
                marker.ConstructorArguments)
            {
                if (!string.Equals(
                        argument.Type.FullName,
                        "System.String",
                        StringComparison.Ordinal
                    ) || !(argument.Value is string))
                {
                    error = "All plugin marker values must be non-null strings.";
                    return false;
                }
            }

            identifier = (string)marker.ConstructorArguments[0].Value;
            displayName = (string)marker.ConstructorArguments[1].Value;
            string versionText = (string)marker.ConstructorArguments[2].Value;
            if (!PluginContractRules.IsValidIdentifier(identifier))
            {
                error = "The stable identifier is invalid.";
                return false;
            }
            if (!PluginContractRules.TryParseVersion(versionText, out version))
            {
                error = "The version is not canonical major.minor.patch.";
                return false;
            }
            return true;
        }

        private IEnumerable<CustomAttribute> MarkerAttributes(
            TypeDefinition type
        )
        {
            return type.CustomAttributes.Where(attribute =>
                IsContractType(
                    attribute.AttributeType,
                    PluginContractRules.MetadataTypeName
                )
            );
        }

        private bool IsContractType(TypeReference type, string fullName)
        {
            if (type == null ||
                !string.Equals(type.FullName, fullName, StringComparison.Ordinal))
            {
                return false;
            }
            AssemblyNameReference scope = type.Scope as AssemblyNameReference;
            return scope != null &&
                string.Equals(
                    scope.Name,
                    contractIdentity.Name,
                    StringComparison.Ordinal
                ) && Equals(scope.Version, contractIdentity.Version) &&
                string.Equals(
                    NormalizeCulture(scope.Culture),
                    NormalizeCulture(contractIdentity.Culture),
                    StringComparison.OrdinalIgnoreCase
                ) &&
                TokensEqual(scope.PublicKeyToken, contractIdentity.PublicKeyToken);
        }

        private static IEnumerable<TypeDefinition> AllTypes(
            IEnumerable<TypeDefinition> roots
        )
        {
            foreach (TypeDefinition type in roots)
            {
                yield return type;
                foreach (TypeDefinition nested in AllTypes(type.NestedTypes))
                {
                    yield return nested;
                }
            }
        }

        private static PluginInspectionResult Rejected(
            PluginInspectionDiagnosticCode code,
            string path,
            string detail
        )
        {
            return PluginInspectionResult.Rejected(
                new PluginInspectionDiagnostic(code, path, detail)
            );
        }

        private static string ComputeContentHash(string path)
        {
            using (FileStream stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete
            ))
            using (SHA256 algorithm = SHA256.Create())
            {
                return BitConverter.ToString(algorithm.ComputeHash(stream))
                    .Replace("-", string.Empty);
            }
        }

        private static bool TokensEqual(byte[] left, byte[] right)
        {
            left = left ?? new byte[0];
            right = right ?? new byte[0];
            return left.SequenceEqual(right);
        }

        private static string NormalizeCulture(string culture)
        {
            return string.IsNullOrEmpty(culture) ||
                string.Equals(culture, "neutral", StringComparison.OrdinalIgnoreCase) ?
                string.Empty : culture;
        }

        private static bool IsDriveRelative(string path)
        {
            return path.Length >= 2 &&
                char.IsLetter(path[0]) &&
                path[1] == ':' &&
                (path.Length == 2 ||
                 (path[2] != Path.DirectorySeparatorChar &&
                  path[2] != Path.AltDirectorySeparatorChar));
        }

        private static AssemblyNameDefinition CloneIdentity(
            AssemblyNameReference name
        )
        {
            AssemblyNameDefinition clone = new AssemblyNameDefinition(
                name.Name,
                name.Version
            )
            {
                Culture = name.Culture
            };
            if (name.PublicKeyToken != null)
            {
                clone.PublicKeyToken = (byte[])name.PublicKeyToken.Clone();
            }
            return clone;
        }
    }
}
