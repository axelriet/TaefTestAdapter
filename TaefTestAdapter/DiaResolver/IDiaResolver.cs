// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;

namespace TaefTestAdapter.DiaResolver
{
    /// <summary>
    /// Looks up functions and their source locations in the PDB of a binary. Names are the undecorated, fully qualified
    /// names as stored in the PDB (e.g. <c>Ns::Class::Method</c>, <c>Templ&lt;std::vector&lt;int,std::allocator&lt;int&gt; &gt; &gt;::Method</c>,
    /// <c>`anonymous namespace'::Class::Method</c>). Functions without source information are never returned.
    /// </summary>
    public interface IDiaResolver : IDisposable
    {
        /// <summary>
        /// Finds all functions whose name matches <paramref name="symbolFilterString"/>, which may contain the wildcards
        /// <c>*</c> and <c>?</c> (e.g. <c>*::Method</c>). Note that this scans all functions of the PDB.
        /// </summary>
        IList<SourceFileLocation> GetFunctions(string symbolFilterString);

        /// <summary>
        /// Finds the functions named exactly <paramref name="functionName"/> (case-sensitive; several functions are
        /// returned for overloads, parameterless functions first).
        /// </summary>
        IList<SourceFileLocation> FindFunctions(string functionName);

        /// <summary>
        /// Finds member functions of the classes, structs and unions named exactly <paramref name="typeName"/>
        /// (case-sensitive, e.g. <c>Ns::Class</c>). Only members with code and with an unqualified name contained in
        /// <paramref name="memberNames"/> are located; for overloaded members, the parameterless one is preferred. This is
        /// much faster than looking up the members one by one with <see cref="FindFunctions"/>.
        /// </summary>
        /// <returns>The locations of the members found, by unqualified member name (e.g. <c>Method</c>).</returns>
        IDictionary<string, SourceFileLocation> FindMemberFunctions(string typeName, ICollection<string> memberNames);
    }
}
