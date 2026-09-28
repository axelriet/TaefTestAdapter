// This file has been modified by Microsoft on 6/2017.
// This file has been modified for TAEF support.

using TaefTestAdapter.Common;
using Microsoft.Dia;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace TaefTestAdapter.DiaResolver
{

    internal sealed class DiaResolver : IDiaResolver
    {
        private const string ScopeSeparator = "::";

        private readonly string _binary;
        private readonly string _pdb;
        private readonly ILogger _logger;
        private readonly Stream _fileStream;

        private IDiaDataSource _diaDataSource;
        private IDiaSession _diaSession;
        private bool _isFastLinkPdb;

        internal DiaResolver(string binary, string pdb, ILogger logger)
        {
            _binary = binary;
            _pdb = pdb;
            _logger = logger;

            if (!File.Exists(pdb))
            {
                _logger.LogError($"PDB file '{pdb}' does not exist");
                return;
            }
            if (!TryCreateDiaInstance())
            {
                _logger.LogError("Couldn't find msdia140.dll to parse *.pdb files. You will not get any source locations for your tests.");
                return;
            }

            _logger.DebugInfo($"Parsing pdb file \"{pdb}\"");

            try
            {
                _fileStream = File.Open(pdb, FileMode.Open, FileAccess.Read, FileShare.Read);
                _diaDataSource.loadDataFromIStream(new DiaMemoryStream(_fileStream));
                _diaDataSource.openSession(out _diaSession);
            }
            catch (Exception e)
            {
                _logger.LogWarning($"Could not load PDB file '{pdb}' of binary '{binary}': {e.Message}");
                _logger.DebugWarning($"Exception:{Environment.NewLine}{e}");
                _diaSession = null;
                _fileStream?.Dispose();
                _fileStream = null;
            }
        }

        private bool TryCreateDiaInstance()
        {
            try
            {
                _diaDataSource = DiaFactory.CreateInstance();
                return true;
            }
            catch (Exception e)
            {
                _logger.DebugWarning($"Could not create DIA data source: {e.Message}");
                return false;
            }
        }

        public void Dispose()
        {
            ReleaseComObject(_diaSession);
            _diaSession = null;
            ReleaseComObject(_diaDataSource);
            _diaDataSource = null;
            _fileStream?.Dispose();
        }

        private static void ReleaseComObject(object comObject)
        {
            try
            {
                if (comObject != null && Marshal.IsComObject(comObject))
                    Marshal.FinalReleaseComObject(comObject);
            }
            catch (Exception)
            {
                // nothing we can do
            }
        }

        public IList<SourceFileLocation> GetFunctions(string symbolFilterString)
        {
            return FindChildren(GlobalScope, SymTagEnum.SymTagFunction, symbolFilterString, NameSearchOptions.NsfRegularExpression)
                .Select(ToSourceFileLocation)
                .Where(location => location != null)
                .ToList();
        }

        public IList<SourceFileLocation> FindFunctions(string functionName)
        {
            if (functionName == null)
                throw new ArgumentNullException(nameof(functionName));

            List<IDiaSymbol> functions = FindChildren(GlobalScope, SymTagEnum.SymTagFunction, functionName, NameSearchOptions.NsfCaseSensitive)
                .Where(HasCode)
                .ToList();
            if (functions.Count > 1)
                functions = functions.OrderBy(f => IsParameterless(f) ? 0 : 1).ToList();

            return functions
                .Select(ToSourceFileLocation)
                .Where(location => location != null)
                .ToList();
        }

        public IDictionary<string, SourceFileLocation> FindMemberFunctions(string typeName, ICollection<string> memberNames)
        {
            if (typeName == null)
                throw new ArgumentNullException(nameof(typeName));
            if (memberNames == null)
                throw new ArgumentNullException(nameof(memberNames));

            var result = new Dictionary<string, SourceFileLocation>();
            if (memberNames.Count == 0)
                return result;

            var requestedMembers = memberNames as ISet<string> ?? new HashSet<string>(memberNames);
            var candidates = new Dictionary<string, List<IDiaSymbol>>();
            foreach (IDiaSymbol type in FindChildren(GlobalScope, SymTagEnum.SymTagUDT, typeName, NameSearchOptions.NsfCaseSensitive))
            {
                foreach (IDiaSymbol function in FindChildren(type, SymTagEnum.SymTagFunction, null, NameSearchOptions.NsNone))
                {
                    // defined members have qualified names, e.g. "Ns::Class::Method"
                    string name = function.name;
                    if (name == null)
                        continue;
                    int index = name.LastIndexOf(ScopeSeparator, StringComparison.Ordinal);
                    string memberName = index < 0 ? name : name.Substring(index + ScopeSeparator.Length);
                    if (!requestedMembers.Contains(memberName) || !HasCode(function))
                        continue;

                    if (!candidates.TryGetValue(memberName, out List<IDiaSymbol> functions))
                        candidates.Add(memberName, functions = new List<IDiaSymbol>());
                    if (functions.All(f => f.relativeVirtualAddress != function.relativeVirtualAddress))
                        functions.Add(function);
                }
            }

            foreach (KeyValuePair<string, List<IDiaSymbol>> memberAndFunctions in candidates)
            {
                IDiaSymbol function = memberAndFunctions.Value.Count == 1
                    ? memberAndFunctions.Value[0]
                    : memberAndFunctions.Value.OrderBy(f => IsParameterless(f) ? 0 : 1).First();
                SourceFileLocation location = ToSourceFileLocation(function);
                if (location != null)
                    result.Add(memberAndFunctions.Key, location);
            }

            return result;
        }

        private IDiaSymbol GlobalScope => _diaSession?.globalScope;

        private static bool HasCode(IDiaSymbol function)
        {
            return function.relativeVirtualAddress != 0 && function.length > 0;
        }

        /// <returns>
        /// True if <paramref name="function"/> has no parameters. The parameters are the argument type children of the
        /// function type: its <c>count</c> can not be used, since it includes the implicit <c>this</c> parameter of
        /// non-static member functions (e.g. 1 for a TAEF test method <c>void Test()</c>).
        /// </returns>
        private bool IsParameterless(IDiaSymbol function)
        {
            try
            {
                IDiaSymbol functionType = function.type;
                return functionType != null
                    && !FindChildren(functionType, SymTagEnum.SymTagFunctionArgType, null, NameSearchOptions.NsNone).Any();
            }
            catch (Exception)
            {
                return false;
            }
        }

        private SourceFileLocation ToSourceFileLocation(IDiaSymbol function)
        {
            try
            {
                string name = function.name;
                uint rva = function.relativeVirtualAddress;
                ulong length = function.length;
                if (rva == 0 || length == 0)
                {
                    _logger.DebugWarning($"Function '{name}' has no code in PDB '{_pdb}'");
                    return null;
                }

                _diaSession.findLinesByRVA(rva, (uint)Math.Min(length, uint.MaxValue), out IDiaEnumLineNumbers lineNumbers);
                lineNumbers.Next(1, out IDiaLineNumber lineNumber, out uint fetched);
                if (fetched != 1 || lineNumber == null)
                {
                    _logger.DebugWarning($"Failed to locate line number for function '{name}' in PDB '{_pdb}'");
                    return null;
                }

                return new SourceFileLocation(name, lineNumber.sourceFile.fileName, lineNumber.lineNumber);
            }
            catch (Exception e)
            {
                _logger.DebugWarning($"Exception while locating a function of binary '{_binary}' in PDB '{_pdb}': {e.Message}");
                return null;
            }
        }

        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        private enum NameSearchOptions : uint
        {
            NsNone = 0x0u,
            NsfCaseSensitive = 0x1u,
            NsfCaseInsensitive = 0x2u,
            NsfFNameExt = 0x4u,
            NsfRegularExpression = 0x8u,
            NsfUndecoratedName = 0x10u
        }

        private IDiaEnumSymbols FindChildrenEnumeration(IDiaSymbol parent, SymTagEnum symTag, string name, NameSearchOptions options)
        {
            try
            {
                parent.findChildren(symTag, name, (uint)options, out IDiaEnumSymbols symbols);
                return symbols;
            }
            catch (NotImplementedException)
            {
                // https://developercommunity.visualstudio.com/content/problem/4631/dia-sdk-still-doesnt-support-debugfastlink.html
                _isFastLinkPdb = true;
                _logger.LogWarning($"PDB '{_pdb}' seems to be a partial PDB. In order to get source locations for your tests, please make sure to generate *full* PDBs for your test DLLs (linker option /DEBUG or /DEBUG:FULL - do not use /DEBUG:FASTLINK).");
            }
            catch (COMException e)
            {
                _logger.DebugWarning($"Exception while searching for '{name}' in PDB '{_pdb}': {e.Message}");
            }
            return null;
        }

        /// <returns>The children of <paramref name="parent"/> (empty if DIA failed to load or the PDB is not supported)</returns>
        private IEnumerable<IDiaSymbol> FindChildren(IDiaSymbol parent, SymTagEnum symTag, string name, NameSearchOptions options)
        {
            if (parent == null || _isFastLinkPdb) // Silently return when DIA failed to load
                yield break;

            IDiaEnumSymbols symbols = FindChildrenEnumeration(parent, symTag, name, options);
            if (symbols == null)
                yield break;

            // IDiaEnumSymbols.Next() is considerably faster than enumerating via IEnumerable
            while (true)
            {
                symbols.Next(1, out IDiaSymbol symbol, out uint fetched);
                if (fetched != 1 || symbol == null)
                    yield break;
                yield return symbol;
            }
        }

    }

}
