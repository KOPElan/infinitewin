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
        // Minimum position change (in pixels) to trigger undo - prevents excessive undo entries 
        // during minor drag adjustments or float precision differences
        private const double PositionChangeThreshold = 0.1;

        private Point _lastMousePosition;
        private bool _isPanning = false;

        // Undo/Redo system
        private Stack<ICommand> _undoStack = new Stack<ICommand>();
        private Stack<ICommand> _redoStack = new Stack<ICommand>();

        // Currently selected thumbnail for spacebar toggle
        private WindowThumbnailControl? _selectedThumbnail = null;

        public MainWindow()
        {
            InitializeComponent();
            
            // Attach event handlers for canvas interaction
            // MouseWheel is attached to the Window so it works everywhere, not just on the canvas
            this.MouseWheel += Canvas_MouseWheel;
            MainCanvas.MouseDown += Canvas_MouseDown;
            MainCanvas.MouseMove += Canvas_MouseMove;
            MainCanvas.MouseUp += Canvas_MouseUp;
            MainCanvas.MouseLeave += Canvas_MouseLeave;

            // Handle spacebar at the preview level to prevent button activation
            PreviewKeyDown += MainWindow_PreviewKeyDown;

            // Set up keyboard shortcuts
            SetupKeyboardShortcuts();
        }

        /// <summary>
        /// Handle preview key down to intercept spacebar before buttons get it
        /// </summary>
        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Handle spacebar for maximize toggle
            if (e.Key == Key.Space && _selectedThumbnail != null)
            {
                ToggleMaximizeThumbnail();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Set up keyboard shortcuts
        /// </summary>
        private void SetupKeyboardShortcuts()
        {
            // Ctrl+Z - Undo
            var undoCommand = new RoutedCommand();
            undoCommand.InputGestures.Add(new KeyGesture(Key.Z, ModifierKeys.Control));
            CommandBindings.Add(new CommandBinding(undoCommand, (s, e) => Undo()));

            // Ctrl+Y - Redo
            var redoCommand = new RoutedCommand();
            redoCommand.InputGestures.Add(new KeyGesture(Key.Y, ModifierKeys.Control));
            CommandBindings.Add(new CommandBinding(redoCommand, (s, e) => Redo()));

            // Ctrl+S - Save Layout
            var saveCommand = new RoutedCommand();
            saveCommand.InputGestures.Add(new KeyGesture(Key.S, ModifierKeys.Control));
            CommandBindings.Add(new CommandBinding(saveCommand, (s, e) => SaveLayoutButton_Click(s, e)));

            // Ctrl+O - Open Layout
            var openCommand = new RoutedCommand();
            openCommand.InputGestures.Add(new KeyGesture(Key.O, ModifierKeys.Control));
            CommandBindings.Add(new CommandBinding(openCommand, (s, e) => LoadLayoutButton_Click(s, e)));
        }

        /// <summary>
        /// Toggle maximize state of the selected thumbnail
        /// </summary>
        private void ToggleMaximizeThumbnail()
        {
            if (_selectedThumbnail != null)
            {
                // Use RenderSize which is more reliable than ActualWidth/ActualHeight
                double width = RenderSize.Width;
                double height = RenderSize.Height;
                
                // Validate dimensions
                if (width > 0 && height > 0)
                {
                    _selectedThumbnail.ToggleMaximize(width, height);
                }
            }
        }

        /// <summary>
        /// Undo the last command
        /// </summary>
        private void Undo()
        {
            if (_undoStack.Count > 0)
            {
                var command = _undoStack.Pop();
                command.Undo();
                _redoStack.Push(command);
            }
        }

        /// <summary>
        /// Redo the last undone command
        /// </summary>
        private void Redo()
        {
            if (_redoStack.Count > 0)
            {
                var command = _redoStack.Pop();
                command.Execute();
                _undoStack.Push(command);
            }
        }

        /// <summary>
        /// Execute a command and add it to the undo stack
        /// </summary>
        private void ExecuteCommand(ICommand command)
        {
            command.Execute();
            _undoStack.Push(command);
            _redoStack.Clear(); // Clear redo stack when new command is executed
        }

        /// <summary>
        /// Handle mouse wheel for zooming
        /// Zoom is centered on mouse position
        /// </summary>
        private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            // Get mouse position relative to canvas
            Point mousePos = e.GetPosition(MainCanvas);

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
            int skippedCount = 0;
            foreach (var windowData in layout.Windows)
            {
                try
                {
                    // Check if window still exists
                    if (!WindowThumbnailControl.IsWindowValid(windowData.WindowHandle))
                    {
                        skippedCount++;
                        System.Diagnostics.Debug.WriteLine($"Skipping window '{windowData.WindowTitle}' - window no longer exists");
                        continue; // Skip windows that no longer exist
                    }

                    var thumbnail = new WindowThumbnailControl(windowData.WindowHandle);
                    
                    // Restore position and size
                    Canvas.SetLeft(thumbnail, windowData.Left);
                    Canvas.SetTop(thumbnail, windowData.Top);
                    thumbnail.Width = windowData.Width;
                    thumbnail.Height = windowData.Height;
                    
                    // Setup event handlers
                    SetupThumbnailEventHandlers(thumbnail);
                    
                    MainCanvas.Children.Add(thumbnail);
                }
                catch (Exception ex)
                {
                    skippedCount++;
                    System.Diagnostics.Debug.WriteLine($"Failed to restore window {windowData.WindowTitle}: {ex.Message}");
                }
            }

            if (skippedCount > 0)
            {
                System.Diagnostics.Debug.WriteLine($"Skipped {skippedCount} window(s) that no longer exist");
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
                
                // Setup event handlers for the thumbnail
                SetupThumbnailEventHandlers(thumbnail);
                
                // Execute add command
                var addCommand = new AddWindowCommand(MainCanvas, thumbnail);
                ExecuteCommand(addCommand);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to add window thumbnail: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Setup event handlers for a window thumbnail
        /// Used when creating thumbnails and when undoing remove operations
        /// </summary>
        private void SetupThumbnailEventHandlers(WindowThumbnailControl thumbnail)
        {
            // Store original position for move tracking
            double originalLeft = Canvas.GetLeft(thumbnail);
            double originalTop = Canvas.GetTop(thumbnail);
            double originalWidth = thumbnail.Width;
            double originalHeight = thumbnail.Height;
            
            // Handle selection changes
            thumbnail.SelectionChanged += (s, args) =>
            {
                // When this thumbnail is selected, deselect all others
                if (thumbnail.IsSelected)
                {
                    foreach (UIElement child in MainCanvas.Children)
                    {
                        if (child is WindowThumbnailControl otherThumbnail && otherThumbnail != thumbnail)
                        {
                            otherThumbnail.IsSelected = false;
                        }
                    }
                    _selectedThumbnail = thumbnail;
                }
                else if (_selectedThumbnail == thumbnail)
                {
                    _selectedThumbnail = null;
                }
            };
            
            // Handle close event
            thumbnail.CloseRequested += (s, args) =>
            {
                // Clear selection if this thumbnail is selected
                if (_selectedThumbnail == thumbnail)
                {
                    _selectedThumbnail = null;
                }
                
                var removeCommand = new RemoveWindowCommand(MainCanvas, thumbnail, SetupThumbnailEventHandlers);
                ExecuteCommand(removeCommand);
            };

            // Track drag start position for undo
            thumbnail.DragStarted += (s, args) =>
            {
                originalLeft = Canvas.GetLeft(thumbnail);
                originalTop = Canvas.GetTop(thumbnail);
            };

            // Track drag end for undo
            thumbnail.DragCompleted += (s, args) =>
            {
                double newLeft = Canvas.GetLeft(thumbnail);
                double newTop = Canvas.GetTop(thumbnail);
                
                // Only add to undo stack if position actually changed
                if (Math.Abs(newLeft - originalLeft) > PositionChangeThreshold || 
                    Math.Abs(newTop - originalTop) > PositionChangeThreshold)
                {
                    var moveCommand = new MoveWindowCommand(thumbnail, originalLeft, originalTop, newLeft, newTop);
                    _undoStack.Push(moveCommand);
                    _redoStack.Clear();
                }
            };

            // Track resize start for undo
            thumbnail.ResizeStarted += (s, args) =>
            {
                originalWidth = thumbnail.Width;
                originalHeight = thumbnail.Height;
                originalLeft = Canvas.GetLeft(thumbnail);
                originalTop = Canvas.GetTop(thumbnail);
            };

            // Track resize end for undo
            thumbnail.ResizeCompleted += (s, args) =>
            {
                double newWidth = thumbnail.Width;
                double newHeight = thumbnail.Height;
                double newLeft = Canvas.GetLeft(thumbnail);
                double newTop = Canvas.GetTop(thumbnail);
                
                // Only add to undo stack if size or position actually changed
                if (Math.Abs(newWidth - originalWidth) > PositionChangeThreshold || 
                    Math.Abs(newHeight - originalHeight) > PositionChangeThreshold ||
                    Math.Abs(newLeft - originalLeft) > PositionChangeThreshold || 
                    Math.Abs(newTop - originalTop) > PositionChangeThreshold)
                {
                    var resizeCommand = new ResizeWindowCommand(thumbnail, 
                        originalWidth, originalHeight, originalLeft, originalTop,
                        newWidth, newHeight, newLeft, newTop);
                    _undoStack.Push(resizeCommand);
                    _redoStack.Clear();
                }
            };
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
