// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using TaefTestAdapter.Common;

namespace TaefTestAdapter.DiaResolver
{
    /// <summary>Resolves managed methods without loading test assemblies into the adapter process.</summary>
    public sealed class ManagedSymbolResolver : IDisposable
    {
        private readonly string _binary;
        private readonly ILogger _logger;
        private readonly string _pathExtension;
        private readonly IReadOnlyList<string> _additionalPdbs;
        private readonly IDiaResolverFactory _diaResolverFactory;
        private readonly Dictionary<string, ManagedSymbolResolver> _references = new Dictionary<string, ManagedSymbolResolver>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<TypeDefinitionHandle>> _types = new Dictionary<string, List<TypeDefinitionHandle>>(StringComparer.Ordinal);
        private readonly Dictionary<MethodDefinitionHandle, MethodDefinitionHandle> _stateMachines = new Dictionary<MethodDefinitionHandle, MethodDefinitionHandle>();
        private PEReader _pe;
        private MetadataReader _metadata;
        private MetadataReaderProvider _pdbProvider;
        private MetadataReader _portablePdb;
        private IDiaResolver _windowsPdb;

        public string PdbFile { get; private set; }
        public bool HasSymbols => _portablePdb != null || _windowsPdb != null;
        public string SymbolsProblem { get; private set; }

        public ManagedSymbolResolver(string binary, ILogger logger = null, string pathExtension = null,
            IEnumerable<string> additionalPdbs = null, IDiaResolverFactory diaResolverFactory = null)
            : this(binary, logger, pathExtension, additionalPdbs, diaResolverFactory, null)
        {
        }

        private ManagedSymbolResolver(string binary, ILogger logger, string pathExtension,
            IEnumerable<string> additionalPdbs, IDiaResolverFactory diaResolverFactory, Func<MetadataReader, bool> matchesReference)
        {
            _binary = binary;
            _logger = logger ?? new SilentLogger();
            _pathExtension = pathExtension;
            _additionalPdbs = (additionalPdbs ?? Enumerable.Empty<string>()).ToList().AsReadOnly();
            _diaResolverFactory = diaResolverFactory ?? DefaultDiaResolverFactory.Instance;
            try
            {
                _pe = new PEReader(ManagedAssemblyInfo.OpenRead(binary));
                if (!_pe.HasMetadata)
                {
                    SymbolsProblem = "The test DLL has no managed metadata.";
                    return;
                }
                _metadata = _pe.GetMetadataReader();
                if (matchesReference != null && !matchesReference(_metadata))
                {
                    Dispose();
                    return;
                }
                foreach (TypeDefinitionHandle handle in _metadata.TypeDefinitions)
                {
                    TypeDefinition type = _metadata.GetTypeDefinition(handle);
                    AddType(ManagedMetadata.GetTypeName(_metadata, handle), handle);
                    if (!type.GetDeclaringType().IsNil)
                        AddType(_metadata.GetString(type.Name), handle); // TAEF lists nested classes by simple name
                }

                OpenSymbols(_pathExtension, _additionalPdbs, _diaResolverFactory);
                if (_portablePdb != null)
                {
                    foreach (MethodDebugInformationHandle handle in _portablePdb.MethodDebugInformation)
                    {
                        MethodDefinitionHandle kickoff = _portablePdb.GetMethodDebugInformation(handle).GetStateMachineKickoffMethod();
                        if (!kickoff.IsNil)
                            _stateMachines[kickoff] = MetadataTokens.MethodDefinitionHandle(MetadataTokens.GetRowNumber(handle));
                    }
                }
            }
            catch (Exception e)
            {
                SymbolsProblem = $"Managed source information could not be read: {e.Message}";
                _logger.DebugInfo($"Could not resolve managed symbols of '{binary}': {e.Message}");
                Dispose();
            }
        }

        public SourceFileLocation FindLocation(string typeName, string methodName)
        {
            if (_metadata == null || string.IsNullOrEmpty(typeName))
                return null;
            try
            {
                string name = methodName == null ? typeName : typeName + "." + methodName;
                return FindLocation(typeName, methodName, name, new HashSet<string>(StringComparer.OrdinalIgnoreCase), 0);
            }
            catch (Exception e)
            {
                _logger.DebugInfo($"Could not locate managed method '{typeName}.{methodName}' of '{_binary}': {e.Message}");
                return null;
            }
        }

        private SourceFileLocation FindLocation(string typeName, string methodName, string name, ISet<string> visited, int depth)
        {
            if (_metadata == null || depth >= 64)
                return null;
            TypeDefinitionHandle type = FindType(typeName);
            while (!type.IsNil && depth++ < 64 && visited.Add(Path.GetFullPath(_binary) + "|" + MetadataTokens.GetToken(type)))
            {
                List<MethodDefinitionHandle> methods = _metadata.GetTypeDefinition(type).GetMethods()
                    .Where(handle => methodName == null || _metadata.GetString(_metadata.GetMethodDefinition(handle).Name) == methodName).ToList();
                if (methodName != null && methods.Count > 0)
                    return Locate(methods.OrderBy(GetParameterCount).First(), name);
                foreach (MethodDefinitionHandle method in methods)
                {
                    SourceFileLocation location = Locate(method, name);
                    if (location != null)
                        return location;
                }
                EntityHandle baseType = GetBaseTypeHandle(_metadata.GetTypeDefinition(type).BaseType);
                if (baseType.Kind == HandleKind.TypeDefinition)
                {
                    type = (TypeDefinitionHandle)baseType;
                    continue;
                }
                if (baseType.Kind == HandleKind.TypeReference)
                {
                    EntityHandle scope = baseType;
                    int nesting = 0;
                    while (scope.Kind == HandleKind.TypeReference && nesting++ < 64)
                        scope = _metadata.GetTypeReference((TypeReferenceHandle)scope).ResolutionScope;
                    if (scope.Kind == HandleKind.AssemblyReference)
                    {
                        ManagedSymbolResolver dependency = OpenReferencedAssembly((AssemblyReferenceHandle)scope);
                        return dependency?.FindLocation(ManagedMetadata.GetTypeName(_metadata, baseType), methodName, name, visited, depth);
                    }
                }
                return null;
            }
            return null;
        }

        private ManagedSymbolResolver OpenReferencedAssembly(AssemblyReferenceHandle handle)
        {
            AssemblyReference reference = _metadata.GetAssemblyReference(handle);
            string name = _metadata.GetString(reference.Name);
            if (string.IsNullOrEmpty(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || Path.GetFileName(name) != name)
                return null;
            string directory = Path.GetDirectoryName(Path.GetFullPath(_binary));
            foreach (string extension in new[] { ".dll", ".exe" })
            {
                string path = Path.Combine(directory, name + extension);
                if (_references.TryGetValue(path, out ManagedSymbolResolver existing))
                {
                    if (MatchesReference(existing._metadata, reference))
                        return existing;
                    continue;
                }
                if (!File.Exists(path))
                    continue;
                var resolver = new ManagedSymbolResolver(path, _logger, _pathExtension, _additionalPdbs,
                    _diaResolverFactory, metadata => MatchesReference(metadata, reference));
                if (resolver._metadata != null)
                {
                    _references.Add(path, resolver);
                    return resolver;
                }
                resolver.Dispose();
            }
            return null;
        }

        private bool MatchesReference(MetadataReader metadata, AssemblyReference reference)
        {
            if (metadata == null || !metadata.IsAssembly)
                return false;
            AssemblyDefinition definition = metadata.GetAssemblyDefinition();
            if (!string.Equals(metadata.GetString(definition.Name), _metadata.GetString(reference.Name), StringComparison.OrdinalIgnoreCase)
                || definition.Version != reference.Version
                || !string.Equals(metadata.GetString(definition.Culture), _metadata.GetString(reference.Culture), StringComparison.OrdinalIgnoreCase))
                return false;
            byte[] key = metadata.GetBlobBytes(definition.PublicKey);
            if ((reference.Flags & AssemblyFlags.PublicKey) == 0 && key.Length > 0)
            {
                using (SHA1 hash = SHA1.Create())
                    key = hash.ComputeHash(key).Reverse().Take(8).ToArray();
            }
            return key.SequenceEqual(_metadata.GetBlobBytes(reference.PublicKeyOrToken));
        }

        private void AddType(string name, TypeDefinitionHandle handle)
        {
            if (!_types.TryGetValue(name, out List<TypeDefinitionHandle> matches))
                _types.Add(name, matches = new List<TypeDefinitionHandle>());
            if (!matches.Contains(handle))
                matches.Add(handle);
        }

        private TypeDefinitionHandle FindType(string name)
        {
            return _types.TryGetValue(name, out List<TypeDefinitionHandle> matches) && matches.Count == 1 ? matches[0] : default(TypeDefinitionHandle);
        }

        private MethodDefinitionHandle FindMethod(TypeDefinitionHandle type, string name)
        {
            var visited = new HashSet<TypeDefinitionHandle>();
            while (!type.IsNil && visited.Add(type))
            {
                List<MethodDefinitionHandle> methods = _metadata.GetTypeDefinition(type).GetMethods()
                    .Where(handle => _metadata.GetString(_metadata.GetMethodDefinition(handle).Name) == name).ToList();
                if (methods.Count > 0)
                    return methods.OrderBy(handle => GetParameterCount(handle)).First();
                EntityHandle baseType = GetBaseTypeHandle(_metadata.GetTypeDefinition(type).BaseType);
                type = baseType.Kind == HandleKind.TypeDefinition ? (TypeDefinitionHandle)baseType : default(TypeDefinitionHandle);
            }
            return default(MethodDefinitionHandle);
        }

        private int GetParameterCount(MethodDefinitionHandle method)
        {
            BlobReader signature = _metadata.GetBlobReader(_metadata.GetMethodDefinition(method).Signature);
            SignatureHeader header = signature.ReadSignatureHeader();
            if (header.IsGeneric)
                signature.ReadCompressedInteger();
            return signature.ReadCompressedInteger();
        }

        private EntityHandle GetBaseTypeHandle(EntityHandle handle)
        {
            if (handle.Kind == HandleKind.TypeSpecification)
            {
                BlobReader signature = _metadata.GetBlobReader(_metadata.GetTypeSpecification((TypeSpecificationHandle)handle).Signature);
                // GENERICINST CLASS/VALUETYPE TypeDefOrRefEncoded (arguments are irrelevant to source locations)
                if (signature.ReadByte() != 0x15)
                    return default(EntityHandle);
                byte kind = signature.ReadByte();
                if (kind != 0x11 && kind != 0x12)
                    return default(EntityHandle);
                handle = signature.ReadTypeHandle();
            }
            return handle.Kind == HandleKind.TypeDefinition || handle.Kind == HandleKind.TypeReference ? handle : default(EntityHandle);
        }

        private SourceFileLocation Locate(MethodDefinitionHandle method, string name)
        {
            if (_stateMachines.TryGetValue(method, out MethodDefinitionHandle moveNext))
                method = moveNext;
            else
            {
                MethodDefinitionHandle stateMachine = FindStateMachineMethod(method);
                if (!stateMachine.IsNil)
                    method = stateMachine;
            }

            if (_portablePdb != null)
            {
                int row = MetadataTokens.GetRowNumber(method);
                if (row > _portablePdb.GetTableRowCount(TableIndex.MethodDebugInformation))
                    return null;
                MethodDebugInformation information = _portablePdb.GetMethodDebugInformation(MetadataTokens.MethodDebugInformationHandle(row));
                foreach (SequencePoint point in information.GetSequencePoints())
                {
                    if (point.IsHidden || point.StartLine <= 0)
                        continue;
                    DocumentHandle document = point.Document.IsNil ? information.Document : point.Document;
                    if (document.IsNil)
                        continue;
                    string file = _portablePdb.GetString(_portablePdb.GetDocument(document).Name);
                    return new SourceFileLocation(name, file, (uint)point.StartLine);
                }
                return null;
            }
            SourceFileLocation result = _windowsPdb?.FindFunctionByToken(unchecked((uint)MetadataTokens.GetToken(method)));
            return result == null ? null : new SourceFileLocation(name, result.Sourcefile, result.Line);
        }

        private MethodDefinitionHandle FindStateMachineMethod(MethodDefinitionHandle method)
        {
            foreach (CustomAttributeHandle handle in _metadata.GetMethodDefinition(method).GetCustomAttributes())
            {
                CustomAttribute attribute = _metadata.GetCustomAttribute(handle);
                string name = ManagedMetadata.GetAttributeTypeName(_metadata, attribute);
                if (name != "System.Runtime.CompilerServices.AsyncStateMachineAttribute"
                    && name != "System.Runtime.CompilerServices.IteratorStateMachineAttribute"
                    && name != "System.Runtime.CompilerServices.AsyncIteratorStateMachineAttribute")
                    continue;
                BlobReader value = _metadata.GetBlobReader(attribute.Value);
                if (value.ReadUInt16() != 1)
                    continue;
                string stateType = value.ReadSerializedString();
                if (stateType == null)
                    continue;
                int separator = stateType.IndexOfAny(new[] { ',', '[' });
                if (separator >= 0)
                    stateType = stateType.Substring(0, separator);
                TypeDefinitionHandle type = FindType(stateType);
                if (!type.IsNil)
                    return FindMethod(type, "MoveNext");
            }
            return default(MethodDefinitionHandle);
        }

        private void OpenSymbols(string pathExtension, IEnumerable<string> additionalPdbs, IDiaResolverFactory factory)
        {
            var entries = _pe.ReadDebugDirectory();
            DebugDirectoryEntry codeViewEntry = entries.FirstOrDefault(entry => entry.Type == DebugDirectoryEntryType.CodeView);
            bool hasCodeView = codeViewEntry.Type == DebugDirectoryEntryType.CodeView;
            CodeViewDebugDirectoryData codeView = hasCodeView ? _pe.ReadCodeViewDebugDirectoryData(codeViewEntry) : default(CodeViewDebugDirectoryData);
            foreach (DebugDirectoryEntry entry in entries.Where(e => e.Type == DebugDirectoryEntryType.EmbeddedPortablePdb))
            {
                try
                {
                    MetadataReaderProvider provider = _pe.ReadEmbeddedPortablePdbDebugDirectoryData(entry);
                    if (AcceptPortablePdb(provider, hasCodeView, codeView, codeViewEntry.Stamp))
                        return;
                }
                catch (Exception e)
                {
                    _logger.DebugInfo($"Could not read embedded managed PDB of '{_binary}': {e.Message}");
                }
            }

            foreach (string path in GetPdbCandidates(hasCodeView ? codeView.Path : null, pathExtension, additionalPdbs))
            {
                if (!File.Exists(path))
                    continue;
                try
                {
                    using (FileStream stream = ManagedAssemblyInfo.OpenRead(path))
                    {
                        var signature = new byte[4];
                        bool portable = stream.Read(signature, 0, signature.Length) == 4 && BitConverter.ToUInt32(signature, 0) == 0x424A5342;
                        stream.Position = 0;
                        if (portable && hasCodeView)
                        {
                            var provider = MetadataReaderProvider.FromPortablePdbStream(stream, MetadataStreamOptions.PrefetchMetadata);
                            if (AcceptPortablePdb(provider, hasCodeView, codeView, codeViewEntry.Stamp))
                            {
                                PdbFile = path;
                                return;
                            }
                        }
                        else if (hasCodeView)
                        {
                            IDiaResolver dia = factory.Create(_binary, path, _logger);
                            if (dia.MatchesPdb(codeView.Guid, unchecked((uint)codeView.Age)))
                            {
                                _windowsPdb = dia;
                                PdbFile = path;
                                return;
                            }
                            dia.Dispose();
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.DebugInfo($"Could not read managed PDB '{path}': {e.Message}");
                }
            }
            SymbolsProblem = "No matching managed PDB was found (portable, embedded, or Windows PDB).";
        }

        private bool AcceptPortablePdb(MetadataReaderProvider provider, bool hasCodeView, CodeViewDebugDirectoryData codeView, uint stamp)
        {
            try
            {
                MetadataReader pdb = provider.GetMetadataReader();
                var id = new BlobContentId(pdb.DebugMetadataHeader.Id);
                if (!hasCodeView || (id.Guid == codeView.Guid && id.Stamp == stamp && codeView.Age == 1))
                {
                    _pdbProvider = provider;
                    _portablePdb = pdb;
                    return true;
                }
            }
            catch
            {
                provider.Dispose();
                throw;
            }
            provider.Dispose();
            return false;
        }

        private IEnumerable<string> GetPdbCandidates(string embeddedPath, string pathExtension, IEnumerable<string> additionalPdbs)
        {
            var candidates = new List<string>();
            if (!string.IsNullOrEmpty(embeddedPath))
                candidates.Add(embeddedPath);
            candidates.Add(Path.ChangeExtension(_binary, ".pdb"));
            string fileName = string.IsNullOrEmpty(embeddedPath) ? Path.GetFileName(candidates.Last()) : Path.GetFileName(embeddedPath);
            candidates.Add(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(_binary)), fileName));
            candidates.Add(fileName);
            string searchPath = pathExtension + ";" + Environment.GetEnvironmentVariable("PATH");
            foreach (string directory in searchPath.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                try { candidates.Add(Path.Combine(directory.Trim().Trim('"'), fileName)); }
                catch (ArgumentException) { }
            }
            candidates.AddRange(additionalPdbs ?? Enumerable.Empty<string>());
            return candidates.Distinct(StringComparer.OrdinalIgnoreCase);
        }

        public void Dispose()
        {
            foreach (ManagedSymbolResolver reference in _references.Values)
                reference.Dispose();
            _references.Clear();
            _windowsPdb?.Dispose();
            _windowsPdb = null;
            _pdbProvider?.Dispose();
            _pdbProvider = null;
            _portablePdb = null;
            _pe?.Dispose();
            _pe = null;
            _metadata = null;
        }

        private sealed class SilentLogger : LoggerBase
        {
            public SilentLogger() : base(() => OutputMode.None) { }
            public override void Log(Severity severity, string message) { }
        }
    }
}
