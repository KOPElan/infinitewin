using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

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
            
            // Update all thumbnails after zoom (deferred to allow layout update)
            Dispatcher.BeginInvoke(new Action(() => UpdateAllThumbnails()), System.Windows.Threading.DispatcherPriority.Render);

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
                
                // Update all thumbnails during pan (deferred to allow layout update)
                Dispatcher.BeginInvoke(new Action(() => UpdateAllThumbnails()), System.Windows.Threading.DispatcherPriority.Render);
                
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
        /// Save Layout button click handler
        /// Saves the current canvas layout to a JSON file
        /// </summary>
        private void SaveLayoutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "InfiniteWin Layout (*.iwl)|*.iwl|JSON files (*.json)|*.json|All files (*.*)|*.*",
                    DefaultExt = "iwl",
                    FileName = "layout.iwl"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    SaveLayout(saveDialog.FileName);
                    MessageBox.Show("Layout saved successfully!", "Success", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save layout: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Load Layout button click handler
        /// Loads a canvas layout from a JSON file
        /// </summary>
        private void LoadLayoutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var openDialog = new OpenFileDialog
                {
                    Filter = "InfiniteWin Layout (*.iwl)|*.iwl|JSON files (*.json)|*.json|All files (*.*)|*.*",
                    DefaultExt = "iwl"
                };

                if (openDialog.ShowDialog() == true)
                {
                    LoadLayout(openDialog.FileName);
                    MessageBox.Show("Layout loaded successfully!", "Success", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load layout: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Save the current canvas layout to a file
        /// </summary>
        private void SaveLayout(string filename)
        {
            var layout = new LayoutData
            {
                CanvasScaleX = CanvasScaleTransform.ScaleX,
                CanvasScaleY = CanvasScaleTransform.ScaleY,
                CanvasTranslateX = CanvasTranslateTransform.X,
                CanvasTranslateY = CanvasTranslateTransform.Y
            };

            foreach (UIElement child in MainCanvas.Children)
            {
                if (child is WindowThumbnailControl thumbnail)
                {
                    layout.Windows.Add(new WindowThumbnailData
                    {
                        WindowHandle = thumbnail.SourceWindow,
                        Left = Canvas.GetLeft(thumbnail),
                        Top = Canvas.GetTop(thumbnail),
                        Width = thumbnail.Width,
                        Height = thumbnail.Height,
                        WindowTitle = thumbnail.WindowTitle
                    });
                }
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(layout, options);
            File.WriteAllText(filename, json);
        }

        /// <summary>
        /// Load a canvas layout from a file
        /// </summary>
        private void LoadLayout(string filename)
        {
            string json = File.ReadAllText(filename);
            var layout = JsonSerializer.Deserialize<LayoutData>(json);

            if (layout == null)
            {
                throw new Exception("Invalid layout file");
            }

            // Clear existing thumbnails
            foreach (UIElement child in MainCanvas.Children)
            {
                if (child is WindowThumbnailControl thumbnail)
                {
                    thumbnail.Dispose();
                }
            }
            MainCanvas.Children.Clear();

            // Restore canvas transform
            CanvasScaleTransform.ScaleX = layout.CanvasScaleX;
            CanvasScaleTransform.ScaleY = layout.CanvasScaleY;
            CanvasTranslateTransform.X = layout.CanvasTranslateX;
            CanvasTranslateTransform.Y = layout.CanvasTranslateY;
            UpdateZoomDisplay();

            // Restore windows
            foreach (var windowData in layout.Windows)
            {
                try
                {
                    // Check if window still exists
                    if (!WindowThumbnailControl.IsWindowValid(windowData.WindowHandle))
                    {
                        continue; // Skip windows that no longer exist
                    }

                    var thumbnail = new WindowThumbnailControl(windowData.WindowHandle);
                    
                    // Restore position and size
                    Canvas.SetLeft(thumbnail, windowData.Left);
                    Canvas.SetTop(thumbnail, windowData.Top);
                    thumbnail.Width = windowData.Width;
                    thumbnail.Height = windowData.Height;
                    
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
                    System.Diagnostics.Debug.WriteLine($"Failed to restore window {windowData.WindowTitle}: {ex.Message}");
                }
            }

            // Update all thumbnails
            Dispatcher.BeginInvoke(new Action(() => UpdateAllThumbnails()), System.Windows.Threading.DispatcherPriority.Render);
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
            
            // Update all thumbnails after reset (deferred to allow layout update)
            Dispatcher.BeginInvoke(new Action(() => UpdateAllThumbnails()), System.Windows.Threading.DispatcherPriority.Render);
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
        /// Update all window thumbnails on the canvas
        /// Called after zoom/pan operations to update DWM thumbnail positions
        /// </summary>
        private void UpdateAllThumbnails()
        {
            // Force layout update to ensure transforms are applied
            MainCanvas.UpdateLayout();
            
            foreach (UIElement child in MainCanvas.Children)
            {
                if (child is WindowThumbnailControl thumbnail)
                {
                    thumbnail.UpdateThumbnail();
                }
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
