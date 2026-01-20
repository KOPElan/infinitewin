using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace InfiniteWin
{
    /// <summary>
    /// Main window for the virtual canvas desktop application
    /// </summary>
    public partial class MainWindow : Window
    {
        private const double MinZoom = 0.1;
        private const double MaxZoom = 5.0;
        private const double ZoomSpeed = 0.1;

        private Point _lastMousePosition;
        private bool _isPanning = false;

        public MainWindow()
        {
            InitializeComponent();
            
            // Attach event handlers for canvas interaction
            MainCanvas.MouseWheel += Canvas_MouseWheel;
            MainCanvas.MouseDown += Canvas_MouseDown;
            MainCanvas.MouseMove += Canvas_MouseMove;
            MainCanvas.MouseUp += Canvas_MouseUp;
            MainCanvas.MouseLeave += Canvas_MouseLeave;
        }

        /// <summary>
        /// Handle mouse wheel for zooming
        /// Zoom is centered on mouse position
        /// </summary>
        private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var canvas = sender as Canvas;
            if (canvas == null) return;

            // Get mouse position relative to canvas
            Point mousePos = e.GetPosition(canvas);

            // Calculate zoom factor
            double zoomFactor = e.Delta > 0 ? (1 + ZoomSpeed) : (1 - ZoomSpeed);
            double newScale = CanvasScaleTransform.ScaleX * zoomFactor;

            // Clamp zoom level
            newScale = Math.Max(MinZoom, Math.Min(MaxZoom, newScale));

            // Calculate the adjustment needed to keep zoom centered on mouse
            double scaleChange = newScale / CanvasScaleTransform.ScaleX;
            
            // Adjust translation to zoom towards mouse position
            CanvasTranslateTransform.X = mousePos.X - (mousePos.X - CanvasTranslateTransform.X) * scaleChange;
            CanvasTranslateTransform.Y = mousePos.Y - (mousePos.Y - CanvasTranslateTransform.Y) * scaleChange;

            // Apply the new scale
            CanvasScaleTransform.ScaleX = newScale;
            CanvasScaleTransform.ScaleY = newScale;

            // Update zoom display
            UpdateZoomDisplay();

            e.Handled = true;
        }

        /// <summary>
        /// Handle mouse down for starting pan operation
        /// </summary>
        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Right or middle button starts panning
            if (e.RightButton == MouseButtonState.Pressed || e.MiddleButton == MouseButtonState.Pressed)
            {
                _isPanning = true;
                _lastMousePosition = e.GetPosition(this);
                MainCanvas.CaptureMouse();
                MainCanvas.Cursor = Cursors.SizeAll;
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handle mouse move for panning the canvas
        /// </summary>
        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                Point currentPosition = e.GetPosition(this);
                Vector delta = currentPosition - _lastMousePosition;

                CanvasTranslateTransform.X += delta.X;
                CanvasTranslateTransform.Y += delta.Y;

                _lastMousePosition = currentPosition;
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handle mouse up to stop panning
        /// </summary>
        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                MainCanvas.ReleaseMouseCapture();
                MainCanvas.Cursor = Cursors.Arrow;
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handle mouse leave to stop panning
        /// </summary>
        private void Canvas_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                MainCanvas.ReleaseMouseCapture();
                MainCanvas.Cursor = Cursors.Arrow;
            }
        }

        /// <summary>
        /// Update the zoom level display
        /// </summary>
        private void UpdateZoomDisplay()
        {
            int zoomPercent = (int)(CanvasScaleTransform.ScaleX * 100);
            ZoomLevelText.Text = $"{zoomPercent}%";
        }

        /// <summary>
        /// Add Window button click handler
        /// Opens window selector dialog
        /// </summary>
        private void AddWindowButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WindowSelectorDialog();
            dialog.Owner = this;
            
            if (dialog.ShowDialog() == true && dialog.SelectedWindow != IntPtr.Zero)
            {
                AddWindowThumbnail(dialog.SelectedWindow);
            }
        }

        /// <summary>
        /// Reset View button click handler
        /// Resets zoom to 100% and pan to origin
        /// </summary>
        private void ResetViewButton_Click(object sender, RoutedEventArgs e)
        {
            CanvasScaleTransform.ScaleX = 1.0;
            CanvasScaleTransform.ScaleY = 1.0;
            CanvasTranslateTransform.X = 0;
            CanvasTranslateTransform.Y = 0;
            UpdateZoomDisplay();
        }

        /// <summary>
        /// Add a window thumbnail to the canvas
        /// </summary>
        private void AddWindowThumbnail(IntPtr hwnd)
        {
            try
            {
                var thumbnail = new WindowThumbnailControl(hwnd);
                
                // Random position on canvas (visible area)
                Random rand = new Random();
                double x = rand.Next(50, 500);
                double y = rand.Next(50, 400);
                
                Canvas.SetLeft(thumbnail, x);
                Canvas.SetTop(thumbnail, y);
                
                // Handle close event
                thumbnail.CloseRequested += (s, args) =>
                {
                    MainCanvas.Children.Remove(thumbnail);
                    thumbnail.Dispose();
                };
                
                MainCanvas.Children.Add(thumbnail);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to add window thumbnail: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Clean up all thumbnails when window is closing
        /// </summary>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Dispose all thumbnail controls
            foreach (UIElement child in MainCanvas.Children)
            {
                if (child is WindowThumbnailControl thumbnail)
                {
                    thumbnail.Dispose();
                }
            }
            MainCanvas.Children.Clear();
            
            base.OnClosing(e);
        }
    }
}
