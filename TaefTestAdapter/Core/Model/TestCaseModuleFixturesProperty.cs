// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace TaefTestAdapter.Model
{
    /// <summary>Exact managed module fixture names, kept separately from display traits and class fixtures.</summary>
    public class TestCaseModuleFixturesProperty : TestProperty
    {
        public static readonly string Id = typeof(TestCaseModuleFixturesProperty).FullName;
        public const string Label = "TAEF module fixtures";

        public IReadOnlyList<string> Names { get; }

        public TestCaseModuleFixturesProperty(IEnumerable<string> names)
            : this(new XElement("Fixtures", names.Select(name => new XElement("Fixture", name))))
        {
        }

        private TestCaseModuleFixturesProperty(XElement fixtures) : base(fixtures.ToString(SaveOptions.DisableFormatting))
        {
            Names = fixtures.Elements("Fixture").Select(element => element.Value).ToList().AsReadOnly();
        }

        public static TestCaseModuleFixturesProperty TryParse(string serialization)
        {
            if (string.IsNullOrEmpty(serialization))
                return null;
            try
            {
                XElement fixtures = XElement.Parse(serialization);
                if (fixtures.Name != "Fixtures" || !fixtures.Elements("Fixture").Any()
                    || fixtures.Elements("Fixture").Any(element => string.IsNullOrEmpty(element.Value)))
                    return null;
                return new TestCaseModuleFixturesProperty(fixtures);
            }
            catch (XmlException) { return null; }
            catch (ArgumentException) { return null; }
        }
    }
}
