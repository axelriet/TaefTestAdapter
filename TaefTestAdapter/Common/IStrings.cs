// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// This file has been modified for TAEF support.

namespace TaefTestAdapter.Common
{
    /// <summary>
    /// Texts of the adapter which contain its name.
    /// </summary>
    public interface IStrings
    {
        /// <summary>The product name.</summary>
        string ExtensionName { get; }
        /// <summary>A hint to the troubleshooting section of the documentation.</summary>
        string TroubleShootingLink { get; }
        /// <summary>The message logged when test discovery starts.</summary>
        string TestDiscoveryStarting { get; }
        /// <summary>The message logged when test execution starts.</summary>
        string TestExecutionStarting { get; }
    }
}
