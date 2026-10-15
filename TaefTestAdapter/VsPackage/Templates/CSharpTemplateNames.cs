using System;
using System.Collections.Generic;
using System.Linq;

namespace TaefTestAdapter.VsPackage.Templates
{
    /// <summary>C# template names retain namespace segments and avoid the members of the generated test class.</summary>
    public static class CSharpTemplateNames
    {
        private static readonly HashSet<string> ReservedClassNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "Log", "Verify", "TestContext", "ClassSetup", "ClassCleanup", "MethodSetup", "MethodCleanup",
            "Addition", "WithProperties", "DataDriven", "SampleTests"
        };

        public static string GetNamespace(string name)
        {
            return string.Join(".", (name ?? "").Split('.')
                .Select(segment => TemplateNames.ToIdentifier(segment, TemplateNames.DefaultNamespace)));
        }

        public static string GetClassName(string name)
        {
            string identifier = TemplateNames.ToIdentifier(name, TemplateNames.DefaultClassName);
            while (ReservedClassNames.Contains(identifier))
                identifier += "Tests";
            return identifier;
        }
    }
}
