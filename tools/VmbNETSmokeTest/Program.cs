using System;
using System.Linq;
using System.Threading;
using VmbNET;

// VmbNET smoke test — exercises the exact API the Bonsai.VimbaX port uses.
//
// Run this on the machine with the Alvium 1800 U attached (Vimba X SDK installed):
//   dotnet run --project tools/VmbNETSmokeTest
//
// Expected with a camera: lists it, opens it, acquires ~30 frames, prints
// dimensions + pixel format. With no camera: prints "Found 0 cameras" and exits 0.

internal static class Program
{
    private static int Main(string[] args)
    {
        Console.WriteLine($"VmbC version: {IVmbSystem.VmbCVersion}");
        Console.WriteLine($"VmbNET version: {typeof(IVmbSystem).Assembly.GetName().Version}");

        try
        {
            using var vmb = IVmbSystem.Startup();
            var cameras = vmb.GetCameras();
            Console.WriteLine($"Found {cameras.Count} camera(s).");
            foreach (var c in cameras)
                Console.WriteLine($"  - Id={c.Id}  Serial={c.Serial}  Model={c.ModelName}  Name={c.Name}");

            if (cameras.Count == 0)
            {
                Console.WriteLine("No camera attached — API startup/enumeration OK. Exiting.");
                return 0;
            }

            var camera = cameras[0];
            using var openCamera = camera.Open(ICameraInfo.AccessModeValue.Full);
            Console.WriteLine($"Opened {camera.ModelName} (serial {camera.Serial}).");

            int received = 0;
            const int target = 30;
            using var done = new ManualResetEventSlim(false);

            openCamera.FrameReceived += (s, e) =>
            {
                using var frame = e.Frame; // auto-requeued on dispose
                if (frame.FrameStatus == IFrame.FrameStatusValue.Completed)
                {
                    if (received == 0)
                        Console.WriteLine($"First frame: {frame.Width}x{frame.Height}  " +
                                          $"PixelFormat={frame.PixelFormat}  BufferSize={frame.BufferSize}  " +
                                          $"ImageData=0x{frame.ImageData.ToInt64():X}");
                    received++;
                    if (received >= target) done.Set();
                }
            };

            Console.WriteLine($"Starting acquisition, waiting for {target} frames...");
            using (var acquisition = openCamera.StartFrameAcquisition())
            {
                if (!done.Wait(TimeSpan.FromSeconds(10)))
                    Console.WriteLine($"WARNING: only received {received}/{target} frames in 10s.");
            }

            Console.WriteLine($"Done. Received {received} frame(s). Acquisition stopped, camera closed.");
            return received > 0 ? 0 : 2;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ERROR: {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }
}
