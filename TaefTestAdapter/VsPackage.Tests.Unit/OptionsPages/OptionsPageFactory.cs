// This file has been added for TAEF support.

using System;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.Shell;

namespace TaefTestAdapter.VsPackage.OptionsPages
{
    /// <summary>
    /// Creates options pages outside of Visual Studio.
    /// </summary>
    /// <remarks>
    /// <see cref="DialogPage"/>'s constructor needs a running Visual Studio (settings manager, JoinableTaskContext) and
    /// throws otherwise. However, the field initializers of the page class (which set the options' default values) run
    /// before the base class constructor is called, so the page's own properties are fully usable if the constructor is
    /// invoked on an uninitialized object and its exception is ignored. The finalizer is suppressed since the
    /// <see cref="DialogPage"/> part of the object is not initialized.
    /// </remarks>
    internal static class OptionsPageFactory
    {
        public static T Create<T>() where T : DialogPage
        {
            var page = (T)FormatterServices.GetUninitializedObject(typeof(T));
            GC.SuppressFinalize(page);

            ConstructorInfo constructor = typeof(T).GetConstructor(Type.EmptyTypes);
            if (constructor == null)
                throw new InvalidOperationException($"{typeof(T).Name} has no default constructor");

            try
            {
                constructor.Invoke(page, null);
            }
            catch (TargetInvocationException)
            {
                // expected outside of Visual Studio (thrown by DialogPage's constructor after the page's field initializers have run)
            }

            return page;
        }
    }
}
