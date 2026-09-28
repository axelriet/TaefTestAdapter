// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace TaefTestAdapter.TestCases
{

    /// <summary>
    /// Maps C++ names as spelled by TAEF (e.g. the base name of a test, see <see cref="Helpers.TaefNames.GetBaseName"/>) to
    /// the spellings used in PDBs. They differ as follows:
    /// <list type="bullet">
    /// <item>TAEF spells template arguments like <c>typeid</c> does (<c>Templ&lt;class std::vector&lt;int,class std::allocator&lt;int&gt; &gt; &gt;</c>,
    /// <c>Templ&lt;struct S&gt;</c>, <c>Templ&lt;enum E&gt;</c>), while the PDB omits the keywords <c>class</c>, <c>struct</c> and
    /// <c>union</c> (but not <c>enum</c>): <c>Templ&lt;std::vector&lt;int,std::allocator&lt;int&gt; &gt; &gt;</c>.</item>
    /// <item>TAEF spells anonymous namespaces <c>`anonymous-namespace'</c>; so do type names in the PDB, while function
    /// names in the PDB use <c>`anonymous namespace'</c>, and nested anonymous namespaces may be named by a hash
    /// (<c>A0x94c2b18a::`anonymous namespace'::Class::Method</c> for TAEF's
    /// <c>`anonymous-namespace'::`anonymous-namespace'::Class::Method</c>).</item>
    /// </list>
    /// </summary>
    public static class SymbolNames
    {
        /// <summary>Anonymous namespace as spelled by TAEF and in type names of PDBs.</summary>
        public const string TaefAnonymousNamespace = "`anonymous-namespace'";

        /// <summary>Anonymous namespace as spelled in function names of PDBs (and by the undecorator).</summary>
        public const string PdbAnonymousNamespace = "`anonymous namespace'";

        private static readonly Regex ClassKeyRegex = new Regex(@"\b(?:class|struct|union) ", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex EnumKeyRegex = new Regex(@"\benum ", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex HashedAnonymousNamespaceRegex = new Regex(@"(?<=^|::|[<,])A0x[0-9a-fA-F]{8}(?=::)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <returns>
        /// The spellings of the TAEF class name <paramref name="taefTypeName"/> (without data row suffix) to be looked up in
        /// the types of a PDB, most likely first.
        /// </returns>
        public static IList<string> GetTypeNameCandidates(string taefTypeName)
        {
            if (taefTypeName == null)
                throw new ArgumentNullException(nameof(taefTypeName));

            string withoutClassKeys = RemoveClassKeys(taefTypeName);
            return Distinct(
                withoutClassKeys,
                withoutClassKeys.Replace(TaefAnonymousNamespace, PdbAnonymousNamespace),
                RemoveEnumKeys(withoutClassKeys),
                taefTypeName);
        }

        /// <returns>
        /// The spellings of the TAEF function name <paramref name="taefFunctionName"/> (e.g. <c>Ns::Class::Method</c>) to be
        /// looked up in the functions of a PDB, most likely first. Names within nested anonymous namespaces can not be
        /// predicted (see <see cref="Normalize"/>).
        /// </returns>
        public static IList<string> GetFunctionNameCandidates(string taefFunctionName)
        {
            if (taefFunctionName == null)
                throw new ArgumentNullException(nameof(taefFunctionName));

            string withoutClassKeys = RemoveClassKeys(taefFunctionName);
            string withPdbAnonymousNamespaces = withoutClassKeys.Replace(TaefAnonymousNamespace, PdbAnonymousNamespace);
            return Distinct(
                withPdbAnonymousNamespaces,
                RemoveEnumKeys(withPdbAnonymousNamespaces),
                withoutClassKeys,
                taefFunctionName);
        }

        /// <returns>
        /// A normalized form of a C++ name for comparing names spelled by TAEF and PDBs: the keywords <c>class</c>,
        /// <c>struct</c>, <c>union</c> and <c>enum</c> are removed, all spellings of anonymous namespaces (including hash
        /// names like <c>A0x94c2b18a</c>) are replaced by <see cref="PdbAnonymousNamespace"/>, and whitespace is removed.
        /// </returns>
        public static string Normalize(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            string result = RemoveEnumKeys(RemoveClassKeys(name))
                .Replace(TaefAnonymousNamespace, PdbAnonymousNamespace);
            result = HashedAnonymousNamespaceRegex.Replace(result, PdbAnonymousNamespace);

            var builder = new StringBuilder(result.Length);
            foreach (char c in result)
            {
                if (!char.IsWhiteSpace(c))
                    builder.Append(c);
            }
            return builder.ToString();
        }

        /// <returns>True if <paramref name="name"/> contains an anonymous namespace (in TAEF or PDB spelling).</returns>
        public static bool ContainsAnonymousNamespace(string name)
        {
            return name != null &&
                (name.Contains(TaefAnonymousNamespace) || name.Contains(PdbAnonymousNamespace) || HashedAnonymousNamespaceRegex.IsMatch(name));
        }

        /// <returns>
        /// The part of <paramref name="name"/> after its last anonymous namespace (e.g. <c>Class::Method</c> for
        /// <c>`anonymous-namespace'::Class::Method</c>), or <paramref name="name"/> itself if it contains no anonymous namespace.
        /// </returns>
        public static string GetPartAfterLastAnonymousNamespace(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            int index = Math.Max(
                LastIndexAfter(name, TaefAnonymousNamespace + TaefConstants.ScopeSeparator),
                LastIndexAfter(name, PdbAnonymousNamespace + TaefConstants.ScopeSeparator));
            return index < 0 ? name : name.Substring(index);
        }

        private static int LastIndexAfter(string name, string value)
        {
            int index = name.LastIndexOf(value, StringComparison.Ordinal);
            return index < 0 ? -1 : index + value.Length;
        }

        private static string RemoveClassKeys(string name)
        {
            return name.IndexOf(' ') < 0 ? name : ClassKeyRegex.Replace(name, "");
        }

        private static string RemoveEnumKeys(string name)
        {
            return name.IndexOf(' ') < 0 ? name : EnumKeyRegex.Replace(name, "");
        }

        private static IList<string> Distinct(params string[] names)
        {
            var result = new List<string>(names.Length);
            foreach (string name in names)
            {
                if (!result.Contains(name))
                    result.Add(name);
            }
            return result;
        }

    }

}
