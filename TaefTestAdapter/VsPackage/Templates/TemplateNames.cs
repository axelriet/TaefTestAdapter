// This file has been added for TAEF support.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace TaefTestAdapter.VsPackage.Templates
{
    /// <summary>
    /// The C++ names of the code created by the TAEF Test Project and TAEF Test templates (see
    /// <see cref="TaefTemplateWizard"/>): TitleCase identifiers without underscores, as usual in Windows code, e.g. the
    /// namespace <c>ContosoUnitTests</c> for the project "Contoso.Unit Tests" and the class <c>MyNewTests</c> for the file
    /// "My New-Tests.cpp". Visual Studio's own parameters replace the characters which are not valid in an identifier by
    /// <c>_</c> (<c>$safeprojectname$</c> <c>Contoso_Unit_Tests</c>, <c>$safeitemname$</c> <c>My_New_Tests</c>), and
    /// <c>$rootnamespace$</c> is the RootNamespace of the project file, which need not be an identifier at all.
    /// </summary>
    public static class TemplateNames
    {
        public const string DefaultNamespace = "TaefTests";
        public const string DefaultClassName = "TaefTest";

        // prefix of a name which would start with a digit
        private const string DigitPrefix = "Taef";

        // appended to a name which would clash with the templates' code
        private const string ClashSuffix = "Tests";

        // The names which break the templates' code as name of the namespace or of the test class. They were determined
        // by compiling the item template (with and without INLINE_TEST_METHOD_MARKUP) and the project template with each
        // TitleCase identifier and macro of their preprocessed code as namespace and as class name (MSVC 14.51, TAEF and
        // Windows SDK 10.0.28000 (10.0.26100 declares the same names), x64 and x86, compiled as set up by the project
        // template: /std:c++17 /permissive- /sdl /utf-8, UNICODE). Some names compile, but break the test DLL otherwise
        // (see TaefMacroNames and ProjectTemplateClassName) or change silently (see Macros).

        // the fixtures of the item template's class: a member named like its class would be a constructor, and without
        // INLINE_TEST_METHOD_MARKUP, TEST_CLASS_SETUP(ClassSetup) etc. declare the method only __if_not_exists(ClassSetup),
        // so neither may the namespace be named like them
        private const string FixtureNames = "ClassSetup ClassCleanup MethodSetup MethodCleanup";

        // the test methods of the item template's class
        private const string TestMethodNames = "Addition Strings WithProperties DataDriven";

        // used unqualified by the templates' code (after using namespace WEX::Common, WEX::Logging, WEX::TestExecution)
        private const string UnqualifiedNames = "String Log TestData";

        // TAEF's namespace: the VERIFY_* macros use WEX::TestExecution unqualified, which a class WEX hides; a namespace WEX
        // would be TAEF's own, where test classes clash with TAEF's declarations (e.g. Common, Logging, TestClass)
        private const string TaefNamespace = "WEX";

        // used unqualified by TAEF's macros: HRESULT (return type of the invokers of TEST_METHOD and the fixtures), and
        // TaefClassNameTester, a local struct of TEST_CLASS: a test class of that name compiles, but TE.exe then creates an
        // instance of the struct instead of the test class
        private const string TaefMacroNames = "HRESULT TaefClassNameTester";

        // the class of the project template, which is in the same namespace: a second class SampleTests does not link
        private const string ProjectTemplateClassName = "SampleTests";

        // object-like macros, which the preprocessor replaces, so that the code does not compile or silently gets another
        // name (e.g. FAR is empty, StringCchCopy becomes StringCchCopyW): those of the project template (WIN32, UNICODE,
        // NDEBUG) and of the CRT and Windows SDK headers included by WexTestClass.h, such as errno.h, math.h, winerror.h,
        // excpt.h and strsafe.h (DUMMYNEONSTRUCT: ARM64 only)
        private const string Macros =
            "AbnormalTermination BUFSIZ DOMAIN DUMMYNEONSTRUCT E2BIG EACCES EADDRINUSE EADDRNOTAVAIL EAFNOSUPPORT " +
            "EAGAIN EALREADY EBADF EBADMSG EBUSY ECANCELED ECHILD ECONNABORTED ECONNREFUSED ECONNRESET EDEADLK " +
            "EDEADLOCK EDESTADDRREQ EDOM EEXIST EFAULT EFBIG EHOSTUNREACH EIDRM EILSEQ EINPROGRESS EINTR EINVAL EIO " +
            "EISCONN EISDIR ELOOP EMFILE EMLINK EMSGSIZE ENAMETOOLONG ENETDOWN ENETRESET ENETUNREACH ENFILE ENOBUFS " +
            "ENODATA ENODEV ENOENT ENOEXEC ENOLCK ENOLINK ENOMEM ENOMSG ENOPROTOOPT ENOSPC ENOSR ENOSTR ENOSYS " +
            "ENOTCONN ENOTDIR ENOTEMPTY ENOTRECOVERABLE ENOTSOCK ENOTSUP ENOTTY ENXIO EOF EOPNOTSUPP EOTHER " +
            "EOVERFLOW EOWNERDEAD EPERM EPIPE EPROTO EPROTONOSUPPORT EPROTOTYPE ERANGE EROFS ESPIPE ESRCH ETIME " +
            "ETIMEDOUT ETXTBSY EWOULDBLOCK EXDEV FAR FORCEINLINE GetExceptionCode INFINITY NAN NDEBUG NOERROR NULL " +
            "OVERFLOW PLOSS PURE REFCLSID REFFMTID REFGUID REFIID SING STDAPI STDAPICALLTYPE STDAPIV STDAPIVCALLTYPE " +
            "STDMETHODCALLTYPE STDMETHODIMP STDMETHODIMPV STDMETHODVCALLTYPE StringCbCat StringCbCatEx StringCbCatN " +
            "StringCbCatNEx StringCbCopy StringCbCopyEx StringCbCopyN StringCbCopyNEx StringCbGets StringCbGetsEx " +
            "StringCbLength StringCbPrintf StringCbPrintfEx StringCbVPrintf StringCbVPrintfEx StringCchCat " +
            "StringCchCatEx StringCchCatN StringCchCatNEx StringCchCopy StringCchCopyEx StringCchCopyN " +
            "StringCchCopyNEx StringCchGets StringCchGetsEx StringCchLength StringCchPrintf StringCchPrintfEx " +
            "StringCchVPrintf StringCchVPrintfEx STRSAFEAPI STRSAFEWORKERAPI STRUNCATE THIS TLOSS UNALIGNED " +
            "UNALIGNED64 UnalignedStringCbLength UnalignedStringCbLengthW UnalignedStringCchLength " +
            "UnalignedStringCchLengthW UNDERFLOW UNICODE WEOF WIN32 WSABASEERR WSAEACCES WSAEADDRINUSE " +
            "WSAEADDRNOTAVAIL WSAEAFNOSUPPORT WSAEALREADY WSAEBADF WSAECANCELLED WSAECONNABORTED WSAECONNREFUSED " +
            "WSAECONNRESET WSAEDESTADDRREQ WSAEDISCON WSAEDQUOT WSAEFAULT WSAEHOSTDOWN WSAEHOSTUNREACH " +
            "WSAEINPROGRESS WSAEINTR WSAEINVAL WSAEINVALIDPROCTABLE WSAEINVALIDPROVIDER WSAEISCONN WSAELOOP " +
            "WSAEMFILE WSAEMSGSIZE WSAENAMETOOLONG WSAENETDOWN WSAENETRESET WSAENETUNREACH WSAENOBUFS WSAENOMORE " +
            "WSAENOPROTOOPT WSAENOTCONN WSAENOTEMPTY WSAENOTSOCK WSAEOPNOTSUPP WSAEPFNOSUPPORT WSAEPROCLIM " +
            "WSAEPROTONOSUPPORT WSAEPROTOTYPE WSAEPROVIDERFAILEDINIT WSAEREFUSED WSAEREMOTE WSAESHUTDOWN " +
            "WSAESOCKTNOSUPPORT WSAESTALE WSAETIMEDOUT WSAETOOMANYREFS WSAEUSERS WSAEWOULDBLOCK WSANOTINITIALISED " +
            "WSASYSCALLFAILURE WSASYSNOTREADY WSAVERNOTSUPPORTED";

        // the types and functions which the headers included by WexTestClass.h declare in the global namespace, where no
        // namespace may have their names
        private const string GlobalDeclarations =
            "BOOL CLSID DWORD ExceptionCollidedUnwind ExceptionContinueExecution ExceptionContinueSearch " +
            "ExceptionNestedException FILE FMTID GUID HANDLE HRESULT HUGE IID InlineIsEqualGUID IsEqualGUID IStream " +
            "LONG LPCGUID LPCLSID LPFMTID LPGUID LPIID StringCbCatA StringCbCatExA StringCbCatExW StringCbCatNA " +
            "StringCbCatNExA StringCbCatNExW StringCbCatNW StringCbCatW StringCbCopyA StringCbCopyExA " +
            "StringCbCopyExW StringCbCopyNA StringCbCopyNExA StringCbCopyNExW StringCbCopyNW StringCbCopyW " +
            "StringCbGetsA StringCbGetsExA StringCbGetsExW StringCbGetsW StringCbLengthA StringCbLengthW " +
            "StringCbPrintfA StringCbPrintfExA StringCbPrintfExW StringCbPrintfW StringCbVPrintfA StringCbVPrintfExA " +
            "StringCbVPrintfExW StringCbVPrintfW StringCchCatA StringCchCatExA StringCchCatExW StringCchCatNA " +
            "StringCchCatNExA StringCchCatNExW StringCchCatNW StringCchCatW StringCchCopyA StringCchCopyExA " +
            "StringCchCopyExW StringCchCopyNA StringCchCopyNExA StringCchCopyNExW StringCchCopyNW StringCchCopyW " +
            "StringCchGetsA StringCchGetsExA StringCchGetsExW StringCchGetsW StringCchLengthA StringCchLengthW " +
            "StringCchPrintfA StringCchPrintfExA StringCchPrintfExW StringCchPrintfW StringCchVPrintfA " +
            "StringCchVPrintfExA StringCchVPrintfExW StringCchVPrintfW ULONG";

        // functions of strsafe.h marked with #pragma deprecated: using their names is an error (C4995, an error with /sdl)
        private const string DeprecatedNames =
            "StringCopyWorkerA StringCopyWorkerW StringExHandleFillBehindNullA StringExHandleFillBehindNullW " +
            "StringExHandleOtherFlagsA StringExHandleOtherFlagsW StringExValidateDestA " +
            "StringExValidateDestAndLengthA StringExValidateDestAndLengthW StringExValidateDestW " +
            "StringExValidateSrcA StringExValidateSrcW StringGetsWorkerA StringGetsWorkerW StringLengthWorkerA " +
            "StringLengthWorkerW StringValidateDestA StringValidateDestAndLengthA StringValidateDestAndLengthW " +
            "StringValidateDestW StringVPrintfWorkerA StringVPrintfWorkerW UnalignedStringLengthWorkerW";

        private static readonly HashSet<string> ReservedNamespaceSet = Names(
            FixtureNames, UnqualifiedNames, TaefNamespace, Macros, GlobalDeclarations, DeprecatedNames);

        private static readonly HashSet<string> ReservedClassNameSet = Names(
            FixtureNames, TestMethodNames, UnqualifiedNames, TaefNamespace, TaefMacroNames, ProjectTemplateClassName, Macros,
            DeprecatedNames);

        /// <summary>The names which the namespace of the test classes must not have.</summary>
        public static IReadOnlyCollection<string> ReservedNamespaces => ReservedNamespaceSet;

        /// <summary>The names which the test class of the item template must not have.</summary>
        public static IReadOnlyCollection<string> ReservedClassNames => ReservedClassNameSet;

        /// <returns>
        /// The namespace of the test classes for a project named <paramref name="name"/> (or with this RootNamespace): the
        /// name as TitleCase identifier (see <see cref="ToIdentifier"/>), followed by <c>Tests</c> if it would clash with
        /// the templates' code; <see cref="DefaultNamespace"/> if the name contains no letter or digit.
        /// </returns>
        public static string GetNamespace(string name)
        {
            return AvoidClash(ToIdentifier(name, DefaultNamespace), ReservedNamespaceSet);
        }

        /// <returns>
        /// The name of the test class of the file <c>&lt;fileInputName&gt;.cpp</c>: the file name as TitleCase identifier
        /// (see <see cref="ToIdentifier"/>), followed by <c>Tests</c> if it would clash with the templates' code;
        /// <see cref="DefaultClassName"/> if the file name contains no letter or digit.
        /// </returns>
        public static string GetClassName(string fileInputName)
        {
            return AvoidClash(ToIdentifier(fileInputName, DefaultClassName), ReservedClassNameSet);
        }

        /// <returns>
        /// <paramref name="name"/> as TitleCase C++ identifier without underscores: every character which is not a letter or
        /// digit (including <c>_</c>) separates words, the first character of each word is made upper case, the others are
        /// kept as typed ("my tests-2" and "My_Tests-2" become <c>MyTests2</c>, "TAEF" stays <c>TAEF</c>). Letters of all
        /// scripts are kept (the templates' projects are compiled with /utf-8), as are combining marks which follow a letter
        /// or digit. <c>Taef</c> is prepended to a name which would start with a digit; <paramref name="fallback"/> is
        /// returned if <paramref name="name"/> contains no letter or digit.
        /// </returns>
        public static string ToIdentifier(string name, string fallback)
        {
            name = name ?? "";
            var identifier = new StringBuilder();
            bool isStartOfWord = true;
            int index = 0;
            while (index < name.Length)
            {
                // a character outside of the Basic Multilingual Plane is a surrogate pair
                string character = name.Substring(index, char.IsSurrogatePair(name, index) ? 2 : 1);
                index += character.Length;

                if (char.IsLetterOrDigit(character, 0))
                {
                    identifier.Append(isStartOfWord ? character.ToUpperInvariant() : character);
                    isStartOfWord = false;
                }
                else if (!isStartOfWord && IsCombiningMark(character))
                {
                    identifier.Append(character);
                }
                else
                {
                    isStartOfWord = true;
                }
            }

            if (identifier.Length == 0)
                return fallback;
            if (char.IsDigit(identifier.ToString(), 0))
                identifier.Insert(0, DigitPrefix);
            return identifier.ToString();
        }

        private static bool IsCombiningMark(string character)
        {
            UnicodeCategory category = char.GetUnicodeCategory(character, 0);
            return category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.SpacingCombiningMark;
        }

        private static string AvoidClash(string identifier, ISet<string> reservedNames)
        {
            while (reservedNames.Contains(identifier))
                identifier += ClashSuffix;
            return identifier;
        }

        private static HashSet<string> Names(params string[] lists)
        {
            return new HashSet<string>(
                lists.SelectMany(l => l.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)), StringComparer.Ordinal);
        }
    }
}
