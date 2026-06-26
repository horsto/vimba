using OpenCV.Net;

namespace Bonsai.VimbaX
{
    public class VimbaDataFrame
    {
        public VimbaDataFrame(IplImage image, ulong frameID, ulong timestamp)
        {
            Image = image;
            FrameID = frameID;
            Timestamp = timestamp;
        }

        public IplImage Image { get; private set; }

        public ulong FrameID { get; private set; }

        public ulong Timestamp { get; private set; }
    }
}
