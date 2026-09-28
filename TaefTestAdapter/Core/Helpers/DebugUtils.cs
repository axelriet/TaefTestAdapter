// This file has been modified for TAEF support.

using System;

namespace TaefTestAdapter.Helpers
{
    /// <summary>
    /// Argument checks.
    /// </summary>
    public static class DebugUtils
    {
        public static void AssertIsNotNull(object parameter, string parameterName)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(parameterName);
            }
        }

        public static void AssertIsNull(object parameter, string parameterName)
        {
            if (parameter != null)
            {
                throw new ArgumentException(parameterName + " must be null");
            }
        }

    }

}