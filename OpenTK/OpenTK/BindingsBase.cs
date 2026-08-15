#region License
//
// The Open Toolkit Library License
//
// Copyright (c) 2006 - 2009 the Open Toolkit library.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights to 
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do
// so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
//
#endregion

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Threading;

[System.AttributeUsage(System.AttributeTargets.Delegate)]
public sealed class MonoNativeFunctionWrapperAttribute : Attribute{}

namespace OpenTK
{
    /// <summary>
    /// Provides a common foundation for all flat API bindings and implements the extension loading interface.
    /// </summary>
    public abstract class BindingsBase
    {
        #region Fields

        public record DelegateHandler(Func<IntPtr, Delegate> Factory, Action<Delegate> Setter, Action SetCore = null);
        protected Dictionary<string, DelegateHandler> DelegateSetters;

        bool rebuildExtensionList = true;

        #endregion

        #region Constructors

        // Reflection support for AOT:
        // OpenTK used reflection to load the Delegates and Core nested classes. And dynamically allocates delegates for Types via reflection
        // This will not work, instead we insert these two functions that get auto generated (once manually for now) and populate the dictionaries
        // Most importantly, now the delegates are called with a compile-time known type which works for mono aot
        protected abstract void LoadExtensionSetters();

        /// <summary>
        /// Constructs a new BindingsBase instance.
        /// </summary>
        public BindingsBase()
        {
            LoadExtensionSetters();
        }

        #endregion

        #region Protected Members

        /// <summary>
        /// Gets or sets a <see cref="System.Boolean"/> that indicates whether the list of supported extensions may have changed.
        /// </summary>
        protected bool RebuildExtensionList
        {
            get { return rebuildExtensionList; }
            set { rebuildExtensionList = value; }
        }

        /// <summary>
        /// Retrieves an unmanaged function pointer to the specified function.
        /// </summary>
        /// <param name="funcname">
        /// A <see cref="System.String"/> that defines the name of the function.
        /// </param>
        /// <returns>
        /// A <see cref="IntPtr"/> that contains the address of funcname or IntPtr.Zero,
        /// if the function is not supported by the drivers.
        /// </returns>
        /// <remarks>
        /// Note: some drivers are known to return non-zero values for unsupported functions.
        /// Typical values include 1 and 2 - inheritors are advised to check for and ignore these
        /// values.
        /// </remarks>
        protected abstract IntPtr GetAddress(string funcname);

        /// <summary>
        /// Gets an object that can be used to synchronize access to the bindings implementation.
        /// </summary>
        /// <remarks>This object should be unique across bindings but consistent between bindings
        /// of the same type. For example, ES10.GL, OpenGL.GL and CL10.CL should all return 
        /// unique objects, but all instances of ES10.GL should return the same object.</remarks>
        protected abstract object SyncRoot { get; }

        #endregion

        #region Internal Members

        #region LoadEntryPoints

        internal void LoadEntryPoints()
        {
            int supported = 0;
            Debug.Write("Loading extensions for " + this.GetType().FullName + "... ");

            Stopwatch time = new Stopwatch();
            time.Reset();
            time.Start();

            foreach (var (k, v) in DelegateSetters)
            {
                Delegate d = GetExtensionDelegate(k);
                if (d != null || v.SetCore != null)
                    ++supported;

                try 
                {
                    lock (SyncRoot) 
                    {
                        if (d == null && v.SetCore != null) 
                            v.SetCore();
                        else
                            v.Setter(d);
                    }
                }
                catch (Exception ex)
                {
                    Debug.Print("Failed to set delegate for {0}: {1}", k, ex);
                    throw;
                }
            }

            rebuildExtensionList = true;

            time.Stop();
            Debug.Print("{0} extensions loaded in {1} ms.", supported, time.Elapsed.TotalMilliseconds);
            time.Reset();
        }

        #endregion

        #endregion

        #region Private Members

        #region GetExtensionDelegate

        static int Allocated = 0;
        Delegate GetExtensionDelegate(string name)
        {
            IntPtr address = GetAddress(name);
            
            if (address == IntPtr.Zero ||
                address == new IntPtr(1) ||     // Workaround for buggy nvidia drivers which return
                address == new IntPtr(2))       // 1 or 2 instead of IntPtr.Zero for some extensions.
            {
                return null;
            }
            else
            {
                //Debug.Print("Loading extension {0} at address {1} (total {2}).", name, address, Interlocked.Increment(ref Allocated));
                return DelegateSetters[name].Factory(address);
            }
        }

        protected static Delegate Make<T>(IntPtr address) where T : Delegate
        {
            return Marshal.GetDelegateForFunctionPointer<T>(address);
        }

        #endregion

        #endregion
    }
}
