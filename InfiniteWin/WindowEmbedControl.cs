using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;

namespace InfiniteWin
{
    /// <summary>
    /// Control that embeds a real Windows window as a child window using SetParent
    /// This allows real interaction with the window, unlike DWM thumbnails which are read-only
    /// </summary>
    public class WindowEmbedControl : Border, IDisposable
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

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private const int GWL_STYLE = -16;
        private const int WS_CAPTION = 0x00C00000;
        private const int WS_THICKFRAME = 0x00040000;
        private const int WS_MINIMIZE = 0x20000000;
        private const int WS_MAXIMIZE = 0x01000000;
        private const int WS_SYSMENU = 0x00080000;
        private const int WS_CHILD = 0x40000000;
        private const int WS_VISIBLE = 0x10000000;

        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const uint SWP_FRAMECHANGED = 0x0020;

        #endregion

        private IntPtr _sourceWindow;
        private IntPtr _originalParent = IntPtr.Zero;
        private int _originalStyle = 0;
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
        private ResizeDirection _resizeDirection;

        private const double MinimumWindowSize = 100;

        private enum ResizeDirection
        {
            None,
            BottomRight,
            BottomLeft,
            TopRight,
            TopLeft
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

        // Selection state
        private bool _isSelected = false;

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
        private const double MaxInitialWidth = 800;
        private const double MaxInitialHeight = 600;

        public WindowEmbedControl(IntPtr sourceWindow)
        {
            _sourceWindow = sourceWindow;
            WindowTitle = GetWindowTitle(_sourceWindow);
            
            // Set up border style
            BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x64, 0x96, 0xFF)); // #6496FF
            BorderThickness = new Thickness(2);
            CornerRadius = new CornerRadius(8);
            Background = new SolidColorBrush(Color.FromArgb(0xFF, 0x1E, 0x1E, 0x1E));
            
            // Create container for embedded window
            var grid = new Grid();
            
            // Host border for the embedded window
            _hostBorder = new Border
            {
                Background = Brushes.Black,
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(5, 25, 5, 5),
                ClipToBounds = true
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
                Text = WindowTitle + " [EMBEDDED]",
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            titleGrid.Children.Add(_titleText);

            // Mode toggle button (switch back to thumbnail)
            _modeToggleButton = new Button
            {
                Content = "📸",
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
                ToolTip = "Toggle thumbnail mode (read-only view)"
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
                    double left = Canvas.GetLeft(this);
                    double top = Canvas.GetTop(this);

                    if (double.IsNaN(left)) left = 0;
                    if (double.IsNaN(top)) top = 0;

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
                            Canvas.SetLeft(this, left + delta.X);
                            break;
                        case ResizeDirection.TopRight:
                            newWidth = _resizeStartWidth + delta.X;
                            newHeight = _resizeStartHeight - delta.Y;
                            Canvas.SetTop(this, top + delta.Y);
                            break;
                        case ResizeDirection.TopLeft:
                            newWidth = _resizeStartWidth - delta.X;
                            newHeight = _resizeStartHeight - delta.Y;
                            Canvas.SetLeft(this, left + delta.X);
                            Canvas.SetTop(this, top + delta.Y);
                            break;
                    }

                    // Enforce minimum size
                    newWidth = Math.Max(MinimumWindowSize, newWidth);
                    newHeight = Math.Max(MinimumWindowSize, newHeight);

                    Width = newWidth;
                    Height = newHeight;

                    UpdateEmbeddedWindowSize();
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
                
                if (sourceWidth > 0 && sourceHeight > 0)
                {
                    CalculateAndSetSize(sourceWidth, sourceHeight);
                    return;
                }
            }
            
            // Fallback to default size
            Width = DefaultWidth;
            Height = DefaultHeight;
        }

        /// <summary>
        /// Calculate window size maintaining aspect ratio with size constraints
        /// </summary>
        private void CalculateAndSetSize(int sourceWidth, int sourceHeight)
        {
            // Calculate aspect ratio
            double aspectRatio = (double)sourceWidth / sourceHeight;
            
            // Clamp aspect ratio to reasonable bounds
            const double MinAspectRatio = 0.2;
            const double MaxAspectRatio = 5.0;
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
            
            // Ensure minimum size
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
            EmbedWindow();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            RestoreWindow();
        }

        /// <summary>
        /// Embed the window by setting this application as its parent
        /// </summary>
        private void EmbedWindow()
        {
            if (_sourceWindow == IntPtr.Zero || !IsWindow(_sourceWindow))
                return;

            try
            {
                // Save original parent (usually null for top-level windows)
                _originalParent = SetParent(_sourceWindow, IntPtr.Zero);
                
                // Save original window style
                _originalStyle = GetWindowLong(_sourceWindow, GWL_STYLE);

                // Get the host window handle
                var window = Window.GetWindow(this);
                if (window == null)
                    return;

                var hostHandle = new WindowInteropHelper(window).Handle;
                
                // Set our application window as the parent
                SetParent(_sourceWindow, hostHandle);

                // Modify window style to make it a child window
                // Remove window decorations (caption, thick frame, system menu)
                int style = _originalStyle;
                style &= ~WS_CAPTION;
                style &= ~WS_THICKFRAME;
                style &= ~WS_MINIMIZE;
                style &= ~WS_MAXIMIZE;
                style &= ~WS_SYSMENU;
                style |= WS_CHILD;
                style |= WS_VISIBLE;

                SetWindowLong(_sourceWindow, GWL_STYLE, style);

                // Position and size the embedded window
                UpdateEmbeddedWindowSize();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to embed window: {ex.Message}");
            }
        }

        /// <summary>
        /// Restore the window to its original state
        /// </summary>
        private void RestoreWindow()
        {
            if (_sourceWindow == IntPtr.Zero || !IsWindow(_sourceWindow))
                return;

            try
            {
                // Restore original window style
                if (_originalStyle != 0)
                {
                    SetWindowLong(_sourceWindow, GWL_STYLE, _originalStyle);
                }

                // Restore original parent
                SetParent(_sourceWindow, _originalParent);

                // Force window to update its frame
                SetWindowPos(_sourceWindow, IntPtr.Zero, 0, 0, 0, 0,
                    SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to restore window: {ex.Message}");
            }
        }

        /// <summary>
        /// Update the embedded window's position and size
        /// </summary>
        private void UpdateEmbeddedWindowSize()
        {
            if (_sourceWindow == IntPtr.Zero || !IsWindow(_sourceWindow) || _hostBorder == null)
                return;

            try
            {
                var window = Window.GetWindow(this);
                if (window == null)
                    return;

                // Get the position of the host border in window coordinates
                var topLeft = _hostBorder.TransformToAncestor(window).Transform(new Point(0, 0));

                // Get DPI scale factors
                var source = PresentationSource.FromVisual(window);
                if (source?.CompositionTarget == null)
                    return;

                double dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
                double dpiScaleY = source.CompositionTarget.TransformToDevice.M22;

                // Convert from WPF DIPs to physical pixels
                int left = (int)(topLeft.X * dpiScaleX);
                int top = (int)(topLeft.Y * dpiScaleY);
                int width = (int)(_hostBorder.ActualWidth * dpiScaleX);
                int height = (int)(_hostBorder.ActualHeight * dpiScaleY);

                // Ensure minimum size
                width = Math.Max(50, width);
                height = Math.Max(50, height);

                // Position and size the embedded window
                SetWindowPos(_sourceWindow, IntPtr.Zero, left, top, width, height,
                    SWP_NOZORDER | SWP_NOACTIVATE | SWP_SHOWWINDOW);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update embedded window: {ex.Message}");
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            UpdateEmbeddedWindowSize();
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Don't start drag if clicking close button or already resizing
            if (e.OriginalSource is Button || _isResizing)
                return;

            // Select this window on click
            IsSelected = true;

            // Check for double-click
            DateTime now = DateTime.Now;
            if ((now - _lastClickTime).TotalMilliseconds < DoubleClickMilliseconds)
            {
                // Double-click detected - bring embedded window to focus
                if (IsWindow(_sourceWindow))
                {
                    SetForegroundWindow(_sourceWindow);
                }
                _lastClickTime = DateTime.MinValue;
                e.Handled = true;
                return;
            }
            
            _lastClickTime = now;
            _isDragging = true;
            _dragStartPosition = e.GetPosition(Parent as UIElement);
            CaptureMouse();
            
            DragStarted?.Invoke(this, EventArgs.Empty);
            
            e.Handled = true;
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();
                
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
                
                if (double.IsNaN(left)) left = 0;
                if (double.IsNaN(top)) top = 0;

                Vector delta = currentPosition - _dragStartPosition;
                
                Canvas.SetLeft(this, left + delta.X);
                Canvas.SetTop(this, top + delta.Y);

                _dragStartPosition = currentPosition;
                
                // Update embedded window position during drag
                UpdateEmbeddedWindowSize();
                
                e.Handled = true;
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
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0xFF, 0xD7, 0x00)); // Gold
                BorderThickness = new Thickness(3);
            }
            else
            {
                BorderBrush = new SolidColorBrush(Color.FromArgb(0xFF, 0x64, 0x96, 0xFF)); // #6496FF
                BorderThickness = new Thickness(2);
            }
            
            InvalidateVisual();
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
                if (_isDragging)
                {
                    _isDragging = false;
                    if (IsMouseCaptured)
                    {
                        ReleaseMouseCapture();
                    }
                }
                
                if (_isResizing)
                {
                    _isResizing = false;
                }
                
                RestoreWindow();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        ~WindowEmbedControl()
        {
            Dispose();
        }
    }
}
