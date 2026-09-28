// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// This file has been modified for TAEF support.

namespace TaefTestAdapter.Common
{
   /// <summary>
   /// The texts of Test Adapter for TAEF.
   /// </summary>
   public class Strings : IStrings
   {
      private static readonly IStrings _strings = new Strings();

      public static IStrings Instance => _strings;

      public string ExtensionName => "Test Adapter for TAEF";
      public string TroubleShootingLink => "See the Troubleshooting section of the Test Adapter for TAEF documentation (README.md).";
      public string TestDiscoveryStarting => "Test Adapter for TAEF: Test discovery starting...";
      public string TestExecutionStarting => "Test Adapter for TAEF: Test execution starting...";

   }
}
