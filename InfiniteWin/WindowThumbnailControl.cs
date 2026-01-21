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

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

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
        private Button _modeToggleButton;
        private TextBlock _titleText;
        private Border _hostBorder;

        // Resize handling
        private bool _isResizing = false;
        private Point _resizeStartPosition;
        private double _resizeStartWidth;
        private double _resizeStartHeight;
        private double _resizeStartLeft;
        private double _resizeStartTop;
        private ResizeDirection _resizeDirection;

        private const double MinimumThumbnailSize = 100;

        private enum ResizeDirection
        {
            None,
            BottomRight,
            BottomLeft,
            TopRight,
            TopLeft,
            Right,
            Left,
            Bottom,
            Top
        }

        public event EventHandler? CloseRequested;
        public event EventHandler? DragStarted;
        public event EventHandler? DragCompleted;
        public event EventHandler? ResizeStarted;
        public event EventHandler? ResizeCompleted;
        public event EventHandler? ModeToggleRequested;

        // Public properties for layout save/load
        public IntPtr SourceWindow => _sourceWindow;
        public string WindowTitle { get; private set; } = string.Empty;

        // Selection and maximize state
        private bool _isSelected = false;
        private bool _isMaximized = false;
        private double _savedLeft;
        private double _savedTop;
        private double _savedWidth;
        private double _savedHeight;

        // Selection changed event
        public event EventHandler? SelectionChanged;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    UpdateSelectionVisual();
                    SelectionChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private const double DefaultWidth = 400;
        private const double DefaultHeight = 300;
        private const double MaxInitialWidth = 800;  // Increased for better resolution
        private const double MaxInitialHeight = 600; // Increased for better resolution

        public WindowThumbnailControl(IntPtr sourceWindow)
        {
            _sourceWindow = sourceWindow;
            WindowTitle = GetWindowTitle(_sourceWindow);
            
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
                Text = WindowTitle,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            titleGrid.Children.Add(_titleText);

            // Mode toggle button (embed/thumbnail)
            _modeToggleButton = new Button
            {
                Content = "⚡",
                Width = 20,
                Height = 20,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 26, 0),
                Background = new SolidColorBrush(Color.FromArgb(0xC8, 0x00, 0x96, 0xFF)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                ToolTip = "Toggle embed mode (interact with window)"
            };
            _modeToggleButton.Click += ModeToggleButton_Click;
            titleGrid.Children.Add(_modeToggleButton);

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

            // Add resize handles (corners)
            AddResizeHandle(grid, HorizontalAlignment.Right, VerticalAlignment.Bottom, Cursors.SizeNWSE, ResizeDirection.BottomRight);
            AddResizeHandle(grid, HorizontalAlignment.Left, VerticalAlignment.Bottom, Cursors.SizeNESW, ResizeDirection.BottomLeft);
            AddResizeHandle(grid, HorizontalAlignment.Right, VerticalAlignment.Top, Cursors.SizeNESW, ResizeDirection.TopRight);
            AddResizeHandle(grid, HorizontalAlignment.Left, VerticalAlignment.Top, Cursors.SizeNWSE, ResizeDirection.TopLeft);

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

        /// <summary>
        /// Add a resize handle to the control
        /// </summary>
        private void AddResizeHandle(Grid grid, HorizontalAlignment hAlign, VerticalAlignment vAlign, 
            Cursor cursor, ResizeDirection direction)
        {
            var handle = new Border
            {
                Width = 12,
                Height = 12,
                Background = new SolidColorBrush(Color.FromArgb(0x80, 0x64, 0x96, 0xFF)),
                HorizontalAlignment = hAlign,
                VerticalAlignment = vAlign,
                Cursor = cursor,
                Margin = new Thickness(2),
                CornerRadius = new CornerRadius(2)
            };

            handle.MouseLeftButtonDown += (s, e) =>
            {
                _isResizing = true;
                _resizeDirection = direction;
                _resizeStartPosition = e.GetPosition(Parent as UIElement);
                _resizeStartWidth = Width;
                _resizeStartHeight = Height;
                
                // Save original position
                _resizeStartLeft = Canvas.GetLeft(this);
                _resizeStartTop = Canvas.GetTop(this);
                if (double.IsNaN(_resizeStartLeft)) _resizeStartLeft = 0;
                if (double.IsNaN(_resizeStartTop)) _resizeStartTop = 0;
                
                handle.CaptureMouse();
                ResizeStarted?.Invoke(this, EventArgs.Empty);
                e.Handled = true;
            };

            handle.MouseLeftButtonUp += (s, e) =>
            {
                if (_isResizing)
                {
                    _isResizing = false;
                    handle.ReleaseMouseCapture();
                    ResizeCompleted?.Invoke(this, EventArgs.Empty);
                    e.Handled = true;
                }
            };

            handle.MouseMove += (s, e) =>
            {
                if (_isResizing && Parent is Canvas canvas)
                {
                    Point currentPosition = e.GetPosition(canvas);
                    Vector delta = currentPosition - _resizeStartPosition;

                    double newWidth = _resizeStartWidth;
                    double newHeight = _resizeStartHeight;

                    // Calculate new size based on resize direction
                    switch (_resizeDirection)
                    {
                        case ResizeDirection.BottomRight:
                            newWidth = _resizeStartWidth + delta.X;
                            newHeight = _resizeStartHeight + delta.Y;
                            break;
                        case ResizeDirection.BottomLeft:
                            newWidth = _resizeStartWidth - delta.X;
                            newHeight = _resizeStartHeight + delta.Y;
                            Canvas.SetLeft(this, _resizeStartLeft + delta.X);
                            break;
                        case ResizeDirection.TopRight:
                            newWidth = _resizeStartWidth + delta.X;
                            newHeight = _resizeStartHeight - delta.Y;
                            Canvas.SetTop(this, _resizeStartTop + delta.Y);
                            break;
                        case ResizeDirection.TopLeft:
                            newWidth = _resizeStartWidth - delta.X;
                            newHeight = _resizeStartHeight - delta.Y;
                            Canvas.SetLeft(this, _resizeStartLeft + delta.X);
                            Canvas.SetTop(this, _resizeStartTop + delta.Y);
                            break;
                    }

                    // Enforce minimum size
                    newWidth = Math.Max(MinimumThumbnailSize, newWidth);
                    newHeight = Math.Max(MinimumThumbnailSize, newHeight);

                    Width = newWidth;
                    Height = newHeight;

                    UpdateThumbnail();
                    e.Handled = true;
                }
            };

            grid.Children.Add(handle);
        }

        private void SetInitialSize()
        {
            // Try to get the source window dimensions
            if (GetWindowRect(_sourceWindow, out RECT rect))
            {
                int sourceWidth = rect.Width;
                int sourceHeight = rect.Height;
                
                // Check if window appears to be minimized (very small dimensions)
                // Minimized windows typically have very small sizes on taskbar
                bool likelyMinimized = sourceWidth < 50 || sourceHeight < 50;
                
                if (sourceWidth > 0 && sourceHeight > 0 && !likelyMinimized)
                {
                    CalculateAndSetSize(sourceWidth, sourceHeight);
                    return;
                }
            }
            
            // Fallback to default size if we can't get window dimensions or window is minimized
            Width = DefaultWidth;
            Height = DefaultHeight;
        }

        /// <summary>
        /// Update size based on DWM thumbnail source size (called after thumbnail registration)
        /// This provides accurate dimensions even for minimized windows
        /// </summary>
        private void UpdateSizeFromThumbnail()
        {
            if (_thumbnail == IntPtr.Zero)
                return;

            try
            {
                int result = DwmQueryThumbnailSourceSize(_thumbnail, out SIZE size);
                if (result == 0 && size.cx > 0 && size.cy > 0)
                {
                    CalculateAndSetSize(size.cx, size.cy);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to query thumbnail size: {ex.Message}");
            }
        }

        /// <summary>
        /// Calculate thumbnail size maintaining aspect ratio with size constraints
        /// </summary>
        private void CalculateAndSetSize(int sourceWidth, int sourceHeight)
        {
            // Calculate aspect ratio
            double aspectRatio = (double)sourceWidth / sourceHeight;
            
            // Clamp aspect ratio to reasonable bounds to avoid extremely distorted thumbnails
            // This handles edge cases like extremely wide or tall windows
            const double MinAspectRatio = 0.2;  // 1:5 (very tall)
            const double MaxAspectRatio = 5.0;  // 5:1 (very wide)
            aspectRatio = Math.Max(MinAspectRatio, Math.Min(MaxAspectRatio, aspectRatio));
            
            // Start with a target width, constrain to max
            double targetWidth = Math.Min(DefaultWidth, MaxInitialWidth);
            double targetHeight = targetWidth / aspectRatio;
            
            // If height exceeds max, scale down based on height instead
            if (targetHeight > MaxInitialHeight)
            {
                targetHeight = MaxInitialHeight;
                targetWidth = targetHeight * aspectRatio;
            }
            
            // Ensure minimum size (at least 200px on the smaller dimension)
            const double MinDimension = 200;
            if (targetWidth < MinDimension && targetHeight < MinDimension)
            {
                if (aspectRatio >= 1.0)
                {
                    targetWidth = MinDimension;
                    targetHeight = MinDimension / aspectRatio;
                }
                else
                {
                    targetHeight = MinDimension;
                    targetWidth = MinDimension * aspectRatio;
                }
            }
            
            Width = targetWidth;
            Height = targetHeight;
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
                    // Now that thumbnail is registered, update size based on actual content
                    UpdateSizeFromThumbnail();
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

                // Get DPI scale factors
                var source = PresentationSource.FromVisual(window);
                if (source?.CompositionTarget == null)
                    return;

                double dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                double dpiScaleY = source.CompositionTarget.TransformToDevice.M22;

                // Get the four corners of the host border in window coordinates to account for transforms
                var topLeft = _hostBorder.TransformToAncestor(window).Transform(new Point(0, 0));
                var bottomRight = _hostBorder.TransformToAncestor(window).Transform(
                    new Point(_hostBorder.ActualWidth, _hostBorder.ActualHeight));

                // Convert from WPF DIPs to physical pixels for DWM
                int left = (int)(topLeft.X * dpiScaleX);
                int top = (int)(topLeft.Y * dpiScaleY);
                int right = (int)(bottomRight.X * dpiScaleX);
                int bottom = (int)(bottomRight.Y * dpiScaleY);

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
            // Don't start drag if clicking close button or already resizing
            if (e.OriginalSource is Button || _isResizing)
                return;

            // Bring to front by reordering in parent Canvas
            // This ensures both WPF elements and DWM thumbnails appear in correct order
            BringToFront();

            // Select this thumbnail on click
            IsSelected = true;

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
            
            // Notify drag started
            DragStarted?.Invoke(this, EventArgs.Empty);
            
            e.Handled = true;
        }

        /// <summary>
        /// Bring this window to the front by moving it to the end of parent's children collection
        /// This ensures both WPF rendering and DWM thumbnail overlay order are correct
        /// </summary>
        private void BringToFront()
        {
            if (Parent is Panel panel)
            {
                var index = panel.Children.IndexOf(this);
                if (index >= 0 && index < panel.Children.Count - 1)
                {
                    // Remove and re-add to move to end (rendered last = on top)
                    panel.Children.RemoveAt(index);
                    panel.Children.Add(this);
                    
                    // Defer thumbnail update to allow visual tree to update first
                    Dispatcher.BeginInvoke(new Action(() => UpdateThumbnail()), System.Windows.Threading.DispatcherPriority.Render);
                }
            }
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();
                
                // Notify drag completed
                DragCompleted?.Invoke(this, EventArgs.Empty);
                
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

        private void ModeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            ModeToggleRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Update visual appearance based on selection state
        /// </summary>
        private void UpdateSelectionVisual()
        {
            if (_isSelected)
            {
                // Show selection with a brighter border
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xD7, 0x00)); // Gold
                BorderThickness = new Thickness(3);
            }
            else
            {
                // Normal border
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x64, 0x96, 0xFF)); // #6496FF
                BorderThickness = new Thickness(2);
            }
            
            // Force visual update
            InvalidateVisual();
        }

        /// <summary>
        /// Toggle between maximized (filling parent window) and normal size
        /// </summary>
        public void ToggleMaximize(double parentWidth, double parentHeight)
        {
            // Validate parent dimensions
            if (parentWidth <= 0 || parentHeight <= 0)
            {
                System.Diagnostics.Debug.WriteLine("Cannot maximize: invalid parent dimensions");
                return;
            }

            if (Parent is Canvas canvas)
            {
                if (!_isMaximized)
                {
                    // Save current state
                    double currentLeft = Canvas.GetLeft(this);
                    double currentTop = Canvas.GetTop(this);
                    
                    // Handle NaN values (not yet positioned)
                    _savedLeft = double.IsNaN(currentLeft) ? 0 : currentLeft;
                    _savedTop = double.IsNaN(currentTop) ? 0 : currentTop;
                    _savedWidth = Width;
                    _savedHeight = Height;

                    // Validate saved dimensions
                    if (_savedWidth <= 0 || _savedHeight <= 0)
                    {
                        System.Diagnostics.Debug.WriteLine("Cannot maximize: invalid saved dimensions");
                        return;
                    }

                    // Maximize to fill parent while preserving aspect ratio
                    const double margin = 20;
                    double availableWidth = parentWidth - (margin * 2);
                    double availableHeight = parentHeight - (margin * 2);
                    
                    // Calculate current aspect ratio
                    double aspectRatio = _savedWidth / _savedHeight;
                    
                    // Calculate dimensions that fit within available space while preserving aspect ratio
                    double newWidth, newHeight;
                    
                    // Determine which dimension to fill (the longer edge)
                    if (availableWidth / availableHeight > aspectRatio)
                    {
                        // Height is the limiting factor - fill height
                        newHeight = availableHeight;
                        newWidth = newHeight * aspectRatio;
                    }
                    else
                    {
                        // Width is the limiting factor - fill width
                        newWidth = availableWidth;
                        newHeight = newWidth / aspectRatio;
                    }
                    
                    // Center the thumbnail in the available space
                    double left = margin + (availableWidth - newWidth) / 2;
                    double top = margin + (availableHeight - newHeight) / 2;
                    
                    Canvas.SetLeft(this, left);
                    Canvas.SetTop(this, top);
                    Width = newWidth;
                    Height = newHeight;

                    _isMaximized = true;
                }
                else
                {
                    // Restore saved state
                    Canvas.SetLeft(this, _savedLeft);
                    Canvas.SetTop(this, _savedTop);
                    Width = _savedWidth;
                    Height = _savedHeight;

                    _isMaximized = false;
                }

                UpdateThumbnail();
            }
        }

        /// <summary>
        /// Check if a window handle is still valid
        /// </summary>
        public static bool IsWindowValid(IntPtr hwnd)
        {
            return IsWindow(hwnd);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // Stop dragging if currently dragging
                if (_isDragging)
                {
                    _isDragging = false;
                    if (IsMouseCaptured)
                    {
                        ReleaseMouseCapture();
                    }
                }
                
                // Stop resizing if currently resizing
                if (_isResizing)
                {
                    _isResizing = false;
                }
                
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
