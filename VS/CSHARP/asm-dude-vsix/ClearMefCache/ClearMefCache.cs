﻿// The MIT License (MIT)
//
// Copyright (c) 2018 Henk-Jan Lebbink
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.IO;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace AsmDude.ClearMefCache
{
    internal sealed class ClearMefCache
    {
        private ClearMefCache(AsyncPackage package)
        {
            this.ServiceProvider = package;
        }

        private static ClearMefCache Instance;

        private AsyncPackage ServiceProvider { get; }

        public static void Initialize(AsyncPackage package)
        {
            Instance = new ClearMefCache(package);
        }

        //Clear the MEF Cache
        public static async void Clear()
        {
            var componentModelHost = await Instance.ServiceProvider.GetServiceAsync(typeof(SVsComponentModelHost)) as IVsComponentModelHost;
            string folder = componentModelHost.GetFolderPath();

            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }

        //Restart Visual Studio
        public static async void Restart()
        {
            var shell = await Instance.ServiceProvider.GetServiceAsync(typeof(SVsShell)) as IVsShell4;
            shell.Restart((uint)__VSRESTARTTYPE.RESTART_Normal);
        }
    }
}
