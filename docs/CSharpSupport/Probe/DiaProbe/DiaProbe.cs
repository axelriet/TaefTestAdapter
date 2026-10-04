using System;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Navigation;

class Program
{
    static void Main(string[] args)
    {
        var queries = new[]
        {
            ("Contoso.Managed.Tests.BasicTests", "Passing"),
            ("Contoso.Managed.Tests.BasicTests", "AsyncMethod"),
            ("Contoso.Managed.Tests.BasicTests", "LightweightData"),
            ("Contoso.Managed.Tests.DataDrivenClass", "InDataClass"),
            ("GlobalNamespaceTests", "InGlobal"),
            ("Contoso.Managed.Tests.Outer+Nested", "InNested"),
        };
        foreach (string dll in args)
        {
            Console.WriteLine("== " + dll);
            try
            {
                using (var session = new DiaSession(dll))
                {
                    foreach (var (type, method) in queries)
                    {
                        DiaNavigationData data = session.GetNavigationData(type, method);
                        Console.WriteLine($"   {type}.{method}: " + (data == null ? "null" : $"{System.IO.Path.GetFileName(data.FileName)}:{data.MinLineNumber}-{data.MaxLineNumber}"));
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("   EXCEPTION " + e.GetType().Name + ": " + e.Message);
            }
        }
    }
}
