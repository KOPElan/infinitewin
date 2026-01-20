using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace InfiniteWin
{
    /// <summary>
    /// Control that displays a Windows DWM thumbnail of another window
    /// </summary>
    public class WindowThumbnailControl : Border, IDisposable
    {
        #region Win32 API Declarations

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;

            public RECT(int left, int top, int right, int bottom)
            {
                this.left = left;
                this.top = top;
                this.right = right;
                this.bottom = bottom;
            }

            public int Width => right - left;
            public int Height => bottom - top;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SIZE
        {
            public int cx;
            public int cy;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DWM_THUMBNAIL_PROPERTIES
        {
            public int dwFlags;
            public RECT rcDestination;
            public RECT rcSource;
            public byte opacity;
            public bool fVisible;
            public bool fSourceClientAreaOnly;
        }

        private const int DWM_TNP_RECTDESTINATION = 0x00000001;
        private const int DWM_TNP_RECTSOURCE = 0x00000002;
        private const int DWM_TNP_OPACITY = 0x00000004;
        private const int DWM_TNP_VISIBLE = 0x00000008;
        private const int DWM_TNP_SOURCECLIENTAREAONLY = 0x00000010;

        [DllImport("dwmapi.dll")]
        private static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumb);

        [DllImport("dwmapi.dll")]
        private static extern int DwmUnregisterThumbnail(IntPtr thumb);

        [DllImport("dwmapi.dll")]
        private static extern int DwmUpdateThumbnailProperties(IntPtr thumb, ref DWM_THUMBNAIL_PROPERTIES props);

        [DllImport("dwmapi.dll")]
        private static extern int DwmQueryThumbnailSourceSize(IntPtr thumb, out SIZE size);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

        #endregion

        private IntPtr _sourceWindow;
        private IntPtr _thumbnail = IntPtr.Zero;
        private IntPtr _hostHandle = IntPtr.Zero;
        private bool _disposed = false;
        
        private Point _dragStartPosition;
        private bool _isDragging = false;
        
        private DateTime _lastClickTime = DateTime.MinValue;
        private const int DoubleClickMilliseconds = 500;

        private Button _closeButton;
        private TextBlock _titleText;
        private Border _hostBorder;

        public event EventHandler? CloseRequested;

        private const double DefaultWidth = 400;
        private const double DefaultHeight = 300;
        private const double MaxInitialWidth = 600;
        private const double MaxInitialHeight = 400;

        public WindowThumbnailControl(IntPtr sourceWindow)
        {
            _sourceWindow = sourceWindow;
            
            // Set up border style
            BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x64, 0x96, 0xFF)); // #6496FF
            BorderThickness = new Thickness(2);
            CornerRadius = new CornerRadius(8);
            Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x1E, 0x1E, 0x1E));
            
            // Create container for thumbnail
            var grid = new Grid();
            
            // Host border for the DWM thumbnail
            _hostBorder = new Border
            {
                Background = Brushes.Black,
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(5, 25, 5, 5)
            };
            grid.Children.Add(_hostBorder);

            // Title bar
            var titleBar = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x00, 0x00, 0x00)),
                Height = 25,
                VerticalAlignment = VerticalAlignment.Top,
                CornerRadius = new CornerRadius(6, 6, 0, 0)
            };
            
            var titleGrid = new Grid();
            titleBar.Child = titleGrid;
            
            // Window title
            _titleText = new TextBlock
            {
                Text = GetWindowTitle(_sourceWindow),
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            titleGrid.Children.Add(_titleText);

            // Close button
            _closeButton = new Button
            {
                Content = "×",
                Width = 20,
                Height = 20,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 3, 0),
                Background = new SolidColorBrush(Color.FromArgb(0xC8, 0xFF, 0x00, 0x00)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            _closeButton.Click += CloseButton_Click;
            titleGrid.Children.Add(_closeButton);

            grid.Children.Add(titleBar);
            Child = grid;

            // Set initial size based on source window
            SetInitialSize();

            // Event handlers
            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseLeftButtonUp += OnMouseLeftButtonUp;
            MouseMove += OnMouseMove;

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private string GetWindowTitle(IntPtr hwnd)
        {
            var sb = new System.Text.StringBuilder(256);
            GetWindowText(hwnd, sb, 256);
            string title = sb.ToString();
            return string.IsNullOrEmpty(title) ? "Untitled Window" : title;
        }

        private void SetInitialSize()
        {
            Width = DefaultWidth;
            Height = DefaultHeight;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Get the host window handle
            var window = Window.GetWindow(this);
            if (window != null)
            {
                _hostHandle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                RegisterThumbnail();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            UnregisterThumbnail();
        }

        private void RegisterThumbnail()
        {
            if (_hostHandle == IntPtr.Zero || _sourceWindow == IntPtr.Zero)
                return;

            if (_thumbnail != IntPtr.Zero)
                UnregisterThumbnail();

            try
            {
                int result = DwmRegisterThumbnail(_hostHandle, _sourceWindow, out _thumbnail);
                if (result == 0 && _thumbnail != IntPtr.Zero)
                {
                    UpdateThumbnail();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to register thumbnail: {ex.Message}");
            }
        }

        private void UnregisterThumbnail()
        {
            if (_thumbnail != IntPtr.Zero)
            {
                try
                {
                    DwmUnregisterThumbnail(_thumbnail);
                }
                catch { }
                finally
                {
                    _thumbnail = IntPtr.Zero;
                }
            }
        }

        /// <summary>
        /// Update the DWM thumbnail position and size
        /// Call this when the control position changes or canvas transforms change
        /// </summary>
        public void UpdateThumbnail()
        {
            if (_thumbnail == IntPtr.Zero || _hostBorder == null)
                return;

            try
            {
                var window = Window.GetWindow(this);
                if (window == null)
                    return;

                // Get the four corners of the host border in window coordinates to account for transforms
                var topLeft = _hostBorder.TransformToAncestor(window).Transform(new Point(0, 0));
                var bottomRight = _hostBorder.TransformToAncestor(window).Transform(
                    new Point(_hostBorder.ActualWidth, _hostBorder.ActualHeight));

                int left = (int)topLeft.X;
                int top = (int)topLeft.Y;
                int right = (int)bottomRight.X;
                int bottom = (int)bottomRight.Y;

                var props = new DWM_THUMBNAIL_PROPERTIES
                {
                    dwFlags = DWM_TNP_RECTDESTINATION | DWM_TNP_VISIBLE | DWM_TNP_OPACITY | DWM_TNP_SOURCECLIENTAREAONLY,
                    rcDestination = new RECT(left, top, right, bottom),
                    opacity = 255,
                    fVisible = true,
                    fSourceClientAreaOnly = false
                };

                DwmUpdateThumbnailProperties(_thumbnail, ref props);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update thumbnail: {ex.Message}");
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            UpdateThumbnail();
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Don't start drag if clicking close button
            if (e.OriginalSource is Button)
                return;

            // Check for double-click
            DateTime now = DateTime.Now;
            if ((now - _lastClickTime).TotalMilliseconds < DoubleClickMilliseconds)
            {
                // Double-click detected
                ActivateSourceWindow();
                _lastClickTime = DateTime.MinValue; // Reset to prevent triple-click
                e.Handled = true;
                return;
            }
            
            _lastClickTime = now;
            _isDragging = true;
            _dragStartPosition = e.GetPosition(Parent as UIElement);
            CaptureMouse();
            e.Handled = true;
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();
                e.Handled = true;
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && Parent is Canvas canvas)
            {
                Point currentPosition = e.GetPosition(canvas);
                
                double left = Canvas.GetLeft(this);
                double top = Canvas.GetTop(this);
                
                // Handle NaN values
                if (double.IsNaN(left)) left = 0;
                if (double.IsNaN(top)) top = 0;

                Vector delta = currentPosition - _dragStartPosition;
                
                Canvas.SetLeft(this, left + delta.X);
                Canvas.SetTop(this, top + delta.Y);

                _dragStartPosition = currentPosition;
                
                // Update DWM thumbnail position during drag
                UpdateThumbnail();
                
                e.Handled = true;
            }
        }

        private void ActivateSourceWindow()
        {
            // Activate the source window
            if (IsWindow(_sourceWindow))
            {
                SetForegroundWindow(_sourceWindow);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                UnregisterThumbnail();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        ~WindowThumbnailControl()
        {
            Dispose();
        }
    }
}
