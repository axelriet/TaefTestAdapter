// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// This file has been modified for TAEF support.

using System;
using System.Runtime.Serialization;
using System.ServiceModel;

namespace TaefTestAdapter.Common
{
    /// <summary>
    /// The debugger engines the debugger attacher service can attach to a process.
    /// </summary>
    public enum DebuggerEngine
    {
        Native, ManagedAndNative
    }


    /// <summary>
    /// Interface of DebuggerAttacherService.
    /// </summary>
    [ServiceContract]
    public interface IDebuggerAttacherService
    {
        /// <summary>
        /// Attaches the debugger to the specified process.
        /// </summary>
        /// <param name="processId">ID of a process to attach to</param>
        /// <param name="debuggerEngine">Engine kind to be attached</param>
        [OperationContract]
        [FaultContract(typeof(DebuggerAttacherServiceFault))]
        void AttachDebugger(int processId, DebuggerEngine debuggerEngine);
    }

    /// <summary>
    /// Fault reported from DebuggerAttacherService.
    /// </summary>
    [DataContract]
    public class DebuggerAttacherServiceFault
    {
        /// <summary>
        /// Creates a fault with message <paramref name="message"/>.
        /// </summary>
        public DebuggerAttacherServiceFault(string message)
        {
            Message = message;
        }

        /// <summary>
        /// Why the debugger could not be attached.
        /// </summary>
        [DataMember]
        public string Message { get; private set; }
    }

    /// <summary>
    /// Abstract wrapper around IDebuggerAttacherService.
    /// </summary>
    public interface IDebuggerAttacherServiceWrapper : IDisposable
    {
        /// <summary>
        /// The wrapped object.
        /// </summary>
        IDebuggerAttacherService Service { get; }
    }

    /// <summary>
    /// Wrapper around IDebuggerAttacherService channel proxy.
    /// </summary>
    public class DebuggerAttacherServiceProxyWrapper : IDebuggerAttacherServiceWrapper
    {
        /// <summary>
        /// Wraps channel proxy <paramref name="proxy"/>.
        /// </summary>
        public DebuggerAttacherServiceProxyWrapper(IDebuggerAttacherService proxy)
        {
            Service = proxy;
        }

        /// <summary>
        /// The wrapped channel proxy.
        /// </summary>
        public IDebuggerAttacherService Service { get; private set; }

        private bool _disposedValue = false;

        /// <summary>
        /// Closes (or aborts) the wrapped channel proxy.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    try
                    {
                        ((IClientChannel)Service).Close();
                    }
                    catch (CommunicationException)
                    {
                        ((IClientChannel)Service).Abort();
                        // Not rethrowing CommunicationException
                    }
                    catch (Exception)
                    {
                        ((IClientChannel)Service).Abort();
                        throw;
                    }
                }
                _disposedValue = true;
            }
        }

        /// <summary>
        /// Closes (or aborts) the wrapped channel proxy.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
