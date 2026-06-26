using OpenCV.Net;
using System;
using System.ComponentModel;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using VmbNET;

namespace Bonsai.VimbaX
{
    [Description("Acquires a sequence of images from an Allied Vision camera using the Vimba X SDK (VmbNET).")]
    public class VimbaCapture : Source<VimbaDataFrame>
    {
        static readonly object systemLock = new object();

        [Description("The optional index of the camera from which to acquire images.")]
        public int? Index { get; set; }

        [TypeConverter(typeof(SerialNumberConverter))]
        [Description("The optional serial number of the camera from which to acquire images.")]
        public string SerialNumber { get; set; }

        [Description("Specifies the optional number of frame buffers to allocate for continuous acquisition.")]
        public int? FrameCount { get; set; }

        [FileNameFilter("XML Files (*.xml)|*.xml|All Files (*.*)|*.*")]
        [Editor("Bonsai.Design.OpenFileNameEditor, Bonsai.Design", "System.Drawing.Design.UITypeEditor, System.Drawing, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a")]
        [Description("The name of the file containing the camera settings (GenICam feature XML).")]
        public string SettingsFile { get; set; }

        static Func<IFrame, IplImage> GetConverter(IFrame.PixelFormatValue pixelFormat)
        {
            int inputChannels;
            int outputChannels;
            IplDepth outputDepth;
            ColorConversion? colorConversion = default;
            switch (pixelFormat)
            {
                case IFrame.PixelFormatValue.Mono8:
                    outputChannels = inputChannels = 1;
                    outputDepth = IplDepth.U8;
                    break;
                case IFrame.PixelFormatValue.BGR8:
                case IFrame.PixelFormatValue.RGB8:
                    outputChannels = inputChannels = 3;
                    outputDepth = IplDepth.U8;
                    if (pixelFormat == IFrame.PixelFormatValue.RGB8)
                    {
                        colorConversion = ColorConversion.Rgb2Bgr;
                    }
                    break;
                case IFrame.PixelFormatValue.BayerRG8:
                    inputChannels = 1;
                    outputChannels = 3;
                    outputDepth = IplDepth.U8;
                    colorConversion = ColorConversion.BayerBG2Bgr;
                    break;
                default:
                    throw new InvalidOperationException(string.Format("Unable to convert pixel format {0}.", pixelFormat));
            }

            return frame =>
            {
                var imageSize = new Size((int)frame.Width, (int)frame.Height);
                // VmbNET exposes the (de-)packed image payload as an unmanaged pointer,
                // so we can wrap it in an IplImage header directly (no managed pinning).
                var bufferHeader = new IplImage(imageSize, outputDepth, inputChannels, frame.ImageData);
                var output = new IplImage(imageSize, outputDepth, outputChannels);
                if (colorConversion.HasValue) CV.CvtColor(bufferHeader, output, colorConversion.Value);
                else CV.Copy(bufferHeader, output);
                return output;
            };
        }

        public override IObservable<VimbaDataFrame> Generate()
        {
            return Generate(Observable.Return(Unit.Default));
        }

        public IObservable<VimbaDataFrame> Generate<TSource>(IObservable<TSource> start)
        {
            return Observable.Create<VimbaDataFrame>((observer, cancellationToken) =>
            {
                var settingsFile = SettingsFile;
                var serialNumber = SerialNumber;
                var index = Index.GetValueOrDefault(0);
                var frameCount = FrameCount.GetValueOrDefault(0);
                return Task.Factory.StartNew(async () =>
                {
                    try
                    {
                        // IVmbSystem is a reference-counted singleton; dispose on the way out
                        // to shut down Vimba X once all references are released.
                        using var vmbSystem = IVmbSystem.Startup();

                        ICamera camera;
                        lock (systemLock)
                        {
                            var cameraList = vmbSystem.GetCameras();
                            if (!string.IsNullOrEmpty(serialNumber))
                            {
                                camera = null;
                                for (int i = 0; i < cameraList.Count; i++)
                                {
                                    if (cameraList[i].Serial == serialNumber)
                                    {
                                        camera = cameraList[i];
                                    }
                                }

                                if (camera == null)
                                {
                                    var message = string.Format("No Vimba X camera was found with serial number {0}.", serialNumber);
                                    throw new InvalidOperationException(message);
                                }
                            }
                            else
                            {
                                if (index < 0 || index >= cameraList.Count)
                                {
                                    var message = string.Format("No Vimba X camera was found at index {0}.", index);
                                    throw new InvalidOperationException(message);
                                }

                                camera = cameraList[index];
                            }
                        }

                        var imageFormat = default(IFrame.PixelFormatValue);
                        var converter = default(Func<IFrame, IplImage>);
                        using var waitHandle = new ManualResetEvent(false);
                        using var notification = cancellationToken.Register(() => waitHandle.Set());

                        using var openCamera = camera.Open(ICameraInfo.AccessModeValue.Full);
                        if (!string.IsNullOrEmpty(settingsFile))
                        {
                            openCamera.LoadSettings(settingsFile);
                        }

                        void OnFrameReceived(object sender, FrameReceivedEventArgs e)
                        {
                            // IFrame is IDisposable and is automatically requeued on dispose.
                            using var frame = e.Frame;
                            try
                            {
                                if (frame.FrameStatus == IFrame.FrameStatusValue.Completed)
                                {
                                    if (converter == null || frame.PixelFormat != imageFormat)
                                    {
                                        converter = GetConverter(frame.PixelFormat);
                                        imageFormat = frame.PixelFormat;
                                    }

                                    var output = converter(frame);
                                    observer.OnNext(new VimbaDataFrame(output, frame.Id, frame.Timestamp));
                                }
                            }
                            catch (Exception ex)
                            {
                                observer.OnError(ex);
                                waitHandle.Set();
                            }
                        }

                        openCamera.FrameReceived += OnFrameReceived;
                        try
                        {
                            await start;
                            using (frameCount > 0
                                ? openCamera.StartFrameAcquisition(ICapturingModule.AllocationModeValue.AnnounceFrame, (uint)frameCount)
                                : openCamera.StartFrameAcquisition())
                            {
                                waitHandle.WaitOne();
                            } // disposing the acquisition stops it
                        }
                        finally
                        {
                            openCamera.FrameReceived -= OnFrameReceived;
                        }
                    }
                    catch (Exception ex) { observer.OnError(ex); throw; }
                },
                cancellationToken,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            });
        }
    }
}
