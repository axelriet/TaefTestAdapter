// This file has been added for TAEF support.

using System;
using System.Globalization;
using System.ServiceModel;
using TaefTestAdapter.Common;

namespace TaefTestAdapter.DebuggerBridge
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                if (args.Length != 4)
                    throw new ArgumentException("Expected process id, debugger engine, service id and timeout.");
                int processId = int.Parse(args[0], CultureInfo.InvariantCulture);
                var engine = (DebuggerEngine) int.Parse(args[1], CultureInfo.InvariantCulture);
                if (processId <= 0 || !Enum.IsDefined(typeof(DebuggerEngine), engine) ||
                    !Guid.TryParse(args[2], out _))
                    throw new ArgumentException("Invalid debugger attach request.");
                var timeout = TimeSpan.FromMilliseconds(int.Parse(args[3], CultureInfo.InvariantCulture));
                using (var proxy = new DebuggerAttacherServiceProxyWrapper(DebuggerAttacherServiceConfiguration.CreateProxy(args[2], timeout)))
                    proxy.Service.AttachDebugger(processId, engine);
                return 0;
            }
            catch (FaultException<DebuggerAttacherServiceFault> e)
            {
                Console.Error.WriteLine(e.Detail?.Message ?? e.Message);
                return 1;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.Message);
                return 1;
            }
        }
    }
}
