using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Bonsai.VimbaX
{
    // Ensures the native Vimba X core (VmbC.dll) and its dependencies can be
    // located when the package is installed into Bonsai.
    //
    // The VmbNET.win-x64 NuGet package ships VmbC.dll + the *_AVT.dll
    // dependencies as contentFiles, which the build copies next to
    // Bonsai.VimbaX.dll. We also pack them into lib/net472 (see the .csproj)
    // so they are deployed alongside our managed assembly in Bonsai's Packages
    // folder. However, the managed VmbNET wrapper resolves "VmbC" via a plain
    // DllImport, and the Windows loader searches next to the host process
    // (Bonsai.exe) and PATH -- NOT the package's lib folder. Without help this
    // throws DllNotFoundException unless the Vimba X SDK happens to be on PATH.
    //
    // Adding our own assembly directory to the native search path via
    // SetDllDirectory makes VmbC.dll (and its bundled dependencies) load
    // cleanly, with no dependency on the SDK being on PATH. This mirrors the
    // approach used by other native-backed Bonsai packages.
    static class VmbNativeLoader
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetDllDirectory(string lpPathName);

        static readonly object gate = new object();
        static bool initialized;

        public static void EnsureNativeSearchPath()
        {
            if (initialized) return;
            lock (gate)
            {
                if (initialized) return;
                try
                {
                    var assemblyDirectory = Path.GetDirectoryName(typeof(VmbNativeLoader).Assembly.Location);
                    // Only redirect the search path if the native core is actually
                    // sitting next to us; otherwise leave the default resolution
                    // (e.g. Vimba X SDK on PATH) untouched.
                    if (!string.IsNullOrEmpty(assemblyDirectory) &&
                        File.Exists(Path.Combine(assemblyDirectory, "VmbC.dll")))
                    {
                        SetDllDirectory(assemblyDirectory);
                    }
                }
                catch
                {
                    // Best-effort: fall back to the default loader search order.
                }
                initialized = true;
            }
        }
    }
}
