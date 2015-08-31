using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xtr3D.Net;

namespace CSharpVisualSkeletonSample
{
    class ColorImageDrawer
    {
        ImageInfo m_imageInfo;
        private WriteableBitmap m_bmp;
        private Int32Rect m_rect;
        

        public ImageSource ImageSource
        {
            get
            {
                return m_bmp;
            }
        }

        internal ColorImageDrawer(ImageInfo imageInfo)
        {
            m_imageInfo = imageInfo;
            m_bmp = new WriteableBitmap(m_imageInfo.Width, m_imageInfo.Height, 96, 96, PixelFormats.Rgb24, null);
            m_rect = new Int32Rect(0, 0, m_imageInfo.Width, m_imageInfo.Height);
        }

        internal void DrawColorImage(byte[] image)
        {
            m_bmp.WritePixels(m_rect,
                        image,
                        m_imageInfo.Width *
                        (m_imageInfo.BitsPerPixel / 8),
                        0);
        }
    }
}
