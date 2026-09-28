// This file has been modified by Microsoft on 6/2017.
// This file has been modified for TAEF support.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TaefTestAdapter.Common;

namespace TaefTestAdapter.Helpers
{

    /// <summary>
    /// File system, timing and validation helpers.
    /// </summary>
    public static class Utils
    {

        public static string GetTempDirectory()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);
            return tempDirectory;
        }

        public static bool DeleteDirectory(string directory)
        {
            return DeleteDirectory(directory, out _);
        }

        public static bool DeleteDirectory(string directory, out string errorMessage)
        {
            try
            {
                Directory.Delete(directory, true);
                errorMessage = null;
                return true;
            }
            catch (Exception e)
            {
                errorMessage = e.Message;
                return false;
            }
        }

        public static string GetExtendedPath(string pathExtension)
        {
            string path = Environment.GetEnvironmentVariable("PATH");
            return string.IsNullOrEmpty(pathExtension) ? path : $"{pathExtension};{path}";
        }

        public static string GetTimestamp()
        {
            return DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
        }

        /// <exception cref="AggregateException">If at least one of the actions has thrown an exception</exception>
        public static bool SpawnAndWait(Action[] actions, int timeoutInMs = Timeout.Infinite)
        {
            var tasks = new Task[actions.Length];
            for (int i = 0; i < actions.Length; i++)
            {
                tasks[i] = Task.Run(actions[i]);
            }
      
            return Task.WaitAll(tasks, timeoutInMs);
        }

        public static void ValidateRegex(string pattern)
        {
            try
            {
                // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                Regex.Match(string.Empty, pattern);
            }
            catch (ArgumentException e)
            {
                throw new Exception($"Invalid regular expression \"{pattern}\", exception message: {e.Message}");
            }
        }

        public static void ValidateTraitRegexes(string value)
        {
            // The parser will throw if the value is not well formed.
            var parser = new RegexTraitParser(null);
            parser.ParseTraitsRegexesString(value, ignoreErrors: false);
        }

        public static void ValidateEnvironmentVariables(string value)
        {
            // The parser will throw if the value is not well formed.
            var parser = new EnvironmentVariablesParser(null);
            parser.ParseEnvironmentVariablesString(value, ignoreErrors: false);
        }

        /// <summary>
        /// Format of TE.exe's /testTimeout switch (and of option TestTimeout).
        /// </summary>
        public const string TestTimeoutFormat = "[Day.]Hour[:Minute[:Second[.FractionalSeconds]]]";

        private static readonly Regex TestTimeoutRegex = new Regex(
            @"^(?:(?<days>[0-9]{1,8})\.)?(?<hours>[0-9]{1,2})(?::(?<minutes>[0-9]{1,2})(?::(?<seconds>[0-9]{1,2})(?:\.(?<fraction>[0-9]{1,7}))?)?)?$",
            RegexOptions.CultureInvariant);

        /// <summary>
        /// Checks whether <paramref name="value"/> is a valid value for TE.exe's /testTimeout switch, i.e. has format
        /// [Day.]Hour[:Minute[:Second[.FractionalSeconds]]] with hours 0-23, minutes and seconds 0-59 and at most 7 digits
        /// of fractional seconds (TE.exe ignores other values with a warning). null, empty and whitespace-only values
        /// (option not set) are valid; leading and trailing whitespace is ignored.
        /// </summary>
        public static bool IsValidTestTimeout(string value)
        {
            return IsValidTestTimeout(value, out _);
        }

        /// <exception cref="ArgumentException">If <paramref name="value"/> is not a valid test timeout (see <see cref="IsValidTestTimeout(string)"/>)</exception>
        public static void ValidateTestTimeout(string value)
        {
            if (!IsValidTestTimeout(value, out string errorMessage))
                throw new ArgumentException(errorMessage, nameof(value));
        }

        private static bool IsValidTestTimeout(string value, out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrWhiteSpace(value))
                return true;

            Match match = TestTimeoutRegex.Match(value.Trim());
            if (!match.Success)
            {
                errorMessage = $"Invalid test timeout '{value}': expected format is {TestTimeoutFormat}, e.g. 0:05 (5 minutes) or 0:0:30 (30 seconds)";
                return false;
            }

            int hours = int.Parse(match.Groups["hours"].Value, CultureInfo.InvariantCulture);
            int minutes = match.Groups["minutes"].Success ? int.Parse(match.Groups["minutes"].Value, CultureInfo.InvariantCulture) : 0;
            int seconds = match.Groups["seconds"].Success ? int.Parse(match.Groups["seconds"].Value, CultureInfo.InvariantCulture) : 0;
            if (hours > 23 || minutes > 59 || seconds > 59)
            {
                errorMessage = $"Invalid test timeout '{value}': hours must be in the range 0-23, minutes and seconds in the range 0-59 (format: {TestTimeoutFormat})";
                return false;
            }

            return true;
        }

        public static string[] SplitAdditionalPdbs(string additionalPdbs)
        {
            return additionalPdbs.Split(new[] {';'}, StringSplitOptions.RemoveEmptyEntries);
        }

        public static bool ValidatePattern(string pattern, out string errorMessage)
        {
            return ValidatePattern(pattern, out string dummy1, out string dummy2, out errorMessage);
        }

        private static bool ValidatePattern(string pattern, out string directory, out string filePattern, out string errorMessage)
        {
            errorMessage = "";

            try
            {
                filePattern = Path.GetFileName(pattern);
                if (string.IsNullOrWhiteSpace(filePattern))
                {
                    filePattern = null;
                }
            }
            catch (Exception)
            {
                filePattern = null;
            }

            try
            {
                directory = Path.GetDirectoryName(pattern);
            }
            catch (Exception)
            {
                directory = null;
            }

            if (filePattern == null || directory == null)
            {
                errorMessage = $"Additional PDB pattern '{pattern}' is invalid: ";
                if (filePattern == null && directory == null)
                {
                    errorMessage += "path part and file pattern part can not be found";
                }
                else if (filePattern == null)
                {
                    errorMessage += "file pattern part can not be found";
                }
                else
                {
                    errorMessage += "path part can not be found";
                }

                return false;
            }

            return true;
        }

        public static string[] GetMatchingFiles(string pattern, ILogger logger)
        {
            if (!ValidatePattern(pattern, out string path, out string filePattern, out string errorMessage))
            {
                logger.LogError(errorMessage);
                return new string[]{};
            }

            try
            {
                // ReSharper disable AssignNullToNotNullAttribute
                return Directory.GetFiles(path, filePattern);
                // ReSharper restore AssignNullToNotNullAttribute
            }
            catch (Exception e)
            {
                logger.LogError($"Error while evaluating additional PDB pattern '{pattern}': {e.Message}");
                return new string[] { };
            }
        }
        
    }

}