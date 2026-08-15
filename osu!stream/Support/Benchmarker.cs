using System;
using System.Diagnostics;

namespace osum.Support
{
    internal class Benchmarker : IDisposable
    {
        private readonly Stopwatch sw = new Stopwatch();
        private readonly string OperationName;

        public Benchmarker(string operation = null)
        {
            OperationName = operation;
            Logging.Write($"Started {operation}");
            sw.Start();
        }

        #region IDisposable Members

        public void Dispose()
        {
            if (OperationName != null)
                Logging.Write(OperationName + " took " + sw.ElapsedMilliseconds + "ms");
            else
                Logging.Write("operation took " + sw.ElapsedMilliseconds + "ms");
        }

        #endregion
    }
}