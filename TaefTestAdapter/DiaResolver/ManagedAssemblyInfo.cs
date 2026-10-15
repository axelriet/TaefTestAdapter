// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using TaefTestAdapter.Common;

namespace TaefTestAdapter.DiaResolver
{
    /// <summary>Reads managed assembly metadata without loading the assembly or its dependencies.</summary>
    public sealed class ManagedAssemblyInfo
    {
        public bool IsTaefTestAssembly { get; private set; }
        public bool IsILOnly { get; private set; }
        public bool Is32BitRequired { get; private set; }
        public ushort MachineType { get; private set; }
        public string TargetFramework { get; private set; }
        public bool HasCoreClrProfile { get; private set; }
        public IReadOnlyList<string> ReferencedAssemblies { get; private set; }

        /// <returns>Metadata of a managed PE assembly, or null for unreadable, malformed, or native files.</returns>
        public static ManagedAssemblyInfo Read(string path, ILogger logger = null)
        {
            try
            {
                using (var stream = OpenRead(path))
                using (var pe = new PEReader(stream))
                {
                    if (!pe.HasMetadata || pe.PEHeaders.CorHeader == null)
                        return null;
                    MetadataReader metadata = pe.GetMetadataReader();
                    if (!metadata.IsAssembly)
                        return null;

                    var references = metadata.AssemblyReferences
                        .Select(handle => metadata.GetString(metadata.GetAssemblyReference(handle).Name))
                        .ToList().AsReadOnly();
                    CorFlags flags = pe.PEHeaders.CorHeader.Flags;
                    return new ManagedAssemblyInfo
                    {
                        MachineType = (ushort)pe.PEHeaders.CoffHeader.Machine,
                        IsILOnly = (flags & CorFlags.ILOnly) != 0,
                        Is32BitRequired = (flags & CorFlags.Requires32Bit) != 0,
                        IsTaefTestAssembly = references.Any(name => string.Equals(name, "TE.Managed", StringComparison.OrdinalIgnoreCase)) && HasTestMarkup(metadata),
                        TargetFramework = ReadTargetFramework(metadata),
                        HasCoreClrProfile = ReadCoreClrProfile(metadata),
                        ReferencedAssemblies = references
                    };
                }
            }
            catch (Exception e)
            {
                logger?.DebugInfo($"Could not read managed metadata of '{path}': {e.Message}");
                return null;
            }
        }

        private static string ReadTargetFramework(MetadataReader metadata)
        {
            foreach (CustomAttributeHandle handle in metadata.GetAssemblyDefinition().GetCustomAttributes())
            {
                CustomAttribute attribute = metadata.GetCustomAttribute(handle);
                if (ManagedMetadata.GetAttributeTypeName(metadata, attribute) != "System.Runtime.Versioning.TargetFrameworkAttribute")
                    continue;
                BlobReader value = metadata.GetBlobReader(attribute.Value);
                return value.ReadUInt16() == 1 ? value.ReadSerializedString() : null;
            }
            return null;
        }

        private static bool HasTestMarkup(MetadataReader metadata)
        {
            foreach (CustomAttributeHandle handle in metadata.CustomAttributes)
            {
                CustomAttribute attribute = metadata.GetCustomAttribute(handle);
                string type = ManagedMetadata.GetAttributeTypeName(metadata, attribute);
                if (attribute.Parent.Kind == HandleKind.TypeDefinition &&
                    (type == "WEX.TestExecution.Markup.TestClassAttribute" ||
                     type == "Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute"))
                    return true;
            }
            return false;
        }

        private static bool ReadCoreClrProfile(MetadataReader metadata)
        {
            foreach (CustomAttributeHandle handle in metadata.CustomAttributes)
            {
                try
                {
                    CustomAttribute attribute = metadata.GetCustomAttribute(handle);
                    if (attribute.Parent.Kind != HandleKind.AssemblyDefinition &&
                        attribute.Parent.Kind != HandleKind.TypeDefinition &&
                        attribute.Parent.Kind != HandleKind.MethodDefinition)
                        continue;
                    string type = ManagedMetadata.GetAttributeTypeName(metadata, attribute);
                    if (type != "WEX.TestExecution.Markup.TestPropertyAttribute" &&
                        type != "Microsoft.VisualStudio.TestTools.UnitTesting.TestPropertyAttribute")
                        continue;
                    BlobReader value = metadata.GetBlobReader(attribute.Value);
                    if (value.ReadUInt16() == 1 &&
                        string.Equals(value.ReadSerializedString(), "CoreClrProfile", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrEmpty(value.ReadSerializedString()))
                        return true;
                }
                catch (BadImageFormatException) { }
                catch (ArgumentOutOfRangeException) { }
                catch (InvalidOperationException) { }
            }
            return false;
        }

        internal static FileStream OpenRead(string path)
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }
    }

    internal static class ManagedMetadata
    {
        internal static string GetAttributeTypeName(MetadataReader metadata, CustomAttribute attribute)
        {
            EntityHandle type;
            if (attribute.Constructor.Kind == HandleKind.MemberReference)
                type = metadata.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent;
            else if (attribute.Constructor.Kind == HandleKind.MethodDefinition)
                type = metadata.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType();
            else
                return null;
            return GetTypeName(metadata, type);
        }

        internal static string GetTypeName(MetadataReader metadata, EntityHandle handle, int depth = 0)
        {
            if (depth > 64)
                return null; // bound nesting even for cyclic metadata in a malformed PE
            if (handle.Kind == HandleKind.TypeDefinition)
            {
                TypeDefinition type = metadata.GetTypeDefinition((TypeDefinitionHandle)handle);
                string name = metadata.GetString(type.Name);
                TypeDefinitionHandle parent = type.GetDeclaringType();
                return parent.IsNil ? Qualify(metadata.GetString(type.Namespace), name) : GetTypeName(metadata, parent, depth + 1) + "+" + name;
            }
            if (handle.Kind == HandleKind.TypeReference)
            {
                TypeReference type = metadata.GetTypeReference((TypeReferenceHandle)handle);
                string name = metadata.GetString(type.Name);
                return type.ResolutionScope.Kind == HandleKind.TypeReference
                    ? GetTypeName(metadata, (TypeReferenceHandle)type.ResolutionScope, depth + 1) + "+" + name
                    : Qualify(metadata.GetString(type.Namespace), name);
            }
            return null;
        }

        private static string Qualify(string ns, string name) => string.IsNullOrEmpty(ns) ? name : ns + "." + name;
    }
}
