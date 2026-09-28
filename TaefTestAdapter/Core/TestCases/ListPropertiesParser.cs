// This file has been added for TAEF support.

using System.Collections.Generic;

namespace TaefTestAdapter.TestCases
{

    /// <summary>
    /// Parses the complete output of <c>TE.exe &lt;test DLL&gt; /listProperties</c> (see <see cref="StreamingListPropertiesParser"/>).
    /// </summary>
    public class ListPropertiesParser
    {
        /// <summary>The <c>Error: ...</c> lines of the last parsed output.</summary>
        public IReadOnlyList<string> Errors { get; private set; } = new string[0];

        /// <summary>The <c>Warning: ...</c> lines of the last parsed output.</summary>
        public IReadOnlyList<string> Warnings { get; private set; } = new string[0];

        /// <summary>The lines of the last parsed output which could not be interpreted.</summary>
        public IReadOnlyList<string> UnexpectedLines { get; private set; } = new string[0];

        /// <returns>The tests listed in <paramref name="consoleOutput"/>, in order of appearance.</returns>
        public IList<TestCaseDescriptor> ParseListPropertiesOutput(IEnumerable<string> consoleOutput)
        {
            var testCaseDescriptors = new List<TestCaseDescriptor>();

            var actualParser = new StreamingListPropertiesParser();
            actualParser.TestCaseDescriptorCreated += (sender, args) => testCaseDescriptors.Add(args.TestCaseDescriptor);

            foreach (string line in consoleOutput)
            {
                actualParser.ReportLine(line);
            }
            actualParser.Flush();

            Errors = actualParser.Errors;
            Warnings = actualParser.Warnings;
            UnexpectedLines = actualParser.UnexpectedLines;
            return testCaseDescriptors;
        }

    }

}
