using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace InfiniteWin
{
    /// <summary>
    /// Data structure for saving and loading canvas layouts
    /// </summary>
    public class LayoutData
    {
        public List<WindowThumbnailData> Windows { get; set; } = new List<WindowThumbnailData>();
        public double CanvasScaleX { get; set; } = 1.0;
        public double CanvasScaleY { get; set; } = 1.0;
        public double CanvasTranslateX { get; set; } = 0.0;
        public double CanvasTranslateY { get; set; } = 0.0;
    }

    /// <summary>
    /// Data for a single window thumbnail
    /// </summary>
    public class WindowThumbnailData
    {
        [JsonConverter(typeof(IntPtrConverter))]
        public IntPtr WindowHandle { get; set; }
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string WindowTitle { get; set; } = string.Empty;
    }
}
