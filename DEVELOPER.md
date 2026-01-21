# InfiniteWin Developer Documentation

## Architecture Overview

### Project Structure

```
InfiniteWin/
├── App.xaml[.cs]                    # Application entry point
├── MainWindow.xaml[.cs]             # Main canvas window with zoom/pan
├── WindowThumbnailControl.cs        # DWM thumbnail control (P/Invoke)
├── WindowEmbedControl.cs            # Embedded window control (SetParent)
└── WindowSelectorDialog.xaml[.cs]   # Window selection dialog
```

### Component Responsibilities

#### 1. App (Application Entry)
- Defines application resources and theme
- Sets MainWindow as startup window

#### 2. MainWindow (Main Canvas)
**UI Elements:**
- `MainCanvas`: Canvas with ScaleTransform and TranslateTransform
- Toolbar: Add Window, Reset View, Zoom Level display
- Hint text at bottom

**Functionality:**
- Mouse wheel zoom (10% - 500%)
- Right/Middle-click pan
- Manage WindowThumbnailControl instances
- Resource cleanup on close

**Key Methods:**
- `Canvas_MouseWheel()`: Zoom centered on mouse position
- `Canvas_MouseMove()`: Pan canvas when dragging
- `AddWindowThumbnail()`: Create and add thumbnail control
- `ResetViewButton_Click()`: Reset to 100% zoom and origin

#### 3. WindowThumbnailControl (DWM Integration)
**Windows API Used:**
- `DwmRegisterThumbnail`: Register window for thumbnail
- `DwmUpdateThumbnailProperties`: Update thumbnail display
- `DwmUnregisterThumbnail`: Cleanup thumbnail
- `SetForegroundWindow`: Activate source window

**UI Components:**
- Blue border (#6496FF) with rounded corners
- Title bar with window title
- Mode toggle button (⚡, blue, switches to embed mode)
- Close button (red, top-right)
- Host border for DWM thumbnail

**Interaction:**
- Left-click drag: Move thumbnail
- Double-click: Activate source window
- Mode toggle button: Switch to embed mode
- Close button: Remove from canvas

**Key Methods:**
- `RegisterThumbnail()`: Initialize DWM thumbnail
- `UpdateThumbnail()`: Update DWM properties
- `OnMouseLeftButtonDown()`: Handle drag start and double-click detection
- `ActivateSourceWindow()`: Bring source window to foreground
- `Dispose()`: Clean up DWM resources

#### 4. WindowEmbedControl (Window Embedding)
**Windows API Used:**
- `SetParent`: Embed window as child window
- `SetWindowLong`: Modify window style for embedding
- `SetWindowPos`: Position and size embedded window
- `GetWindowLong`: Get original window style
- `SetForegroundWindow`: Focus embedded window

**UI Components:**
- Blue border (#6496FF) with rounded corners
- Title bar with "[EMBEDDED]" indicator
- Mode toggle button (📸, blue, switches back to thumbnail)
- Close button (red, top-right)
- Host border containing the embedded window

**Interaction:**
- Left-click drag: Move embedded window
- Double-click: Focus embedded window
- Mode toggle button: Switch back to thumbnail mode
- Close button: Restore original window and remove from canvas
- Direct interaction: All mouse/keyboard events work with embedded window

**Key Methods:**
- `EmbedWindow()`: Use SetParent to embed the window
- `RestoreWindow()`: Restore window to original parent and style
- `UpdateEmbeddedWindowSize()`: Update embedded window position/size
- `Dispose()`: Restore window and clean up resources

#### 5. WindowSelectorDialog (Window Enumeration)
**Windows API Used:**
- `EnumWindows`: Enumerate all top-level windows
- `IsWindowVisible`: Filter visible windows
- `GetWindowText`: Get window titles
- `GetShellWindow`: Exclude shell window

**Functionality:**
- List all visible windows with titles
- Filter empty/invisible windows
- Double-click or OK to select
- Cancel to close without selection

**Key Methods:**
- `LoadWindows()`: Enumerate and populate window list
- `OkButton_Click()`: Return selected window handle
- `WindowListBox_MouseDoubleClick()`: Quick selection

## Win32 API Reference

### DWM (Desktop Window Manager) API

```csharp
// Register a thumbnail
int DwmRegisterThumbnail(
    IntPtr dest,    // Destination window handle (host)
    IntPtr src,     // Source window handle (to thumbnail)
    out IntPtr thumb // Thumbnail handle (output)
);

// Update thumbnail properties
int DwmUpdateThumbnailProperties(
    IntPtr thumb,                        // Thumbnail handle
    ref DWM_THUMBNAIL_PROPERTIES props   // Properties to update
);

// Unregister thumbnail
int DwmUnregisterThumbnail(IntPtr thumb);

// Query source size
int DwmQueryThumbnailSourceSize(
    IntPtr thumb,
    out SIZE size
);
```

### DWM_THUMBNAIL_PROPERTIES Structure

```csharp
struct DWM_THUMBNAIL_PROPERTIES
{
    public int dwFlags;                  // Flags indicating which fields are set
    public RECT rcDestination;           // Destination rectangle
    public RECT rcSource;                // Source rectangle (crop)
    public byte opacity;                 // Opacity (0-255)
    public bool fVisible;                // Visibility flag
    public bool fSourceClientAreaOnly;   // Show only client area
}
```

### Flags (dwFlags)
- `DWM_TNP_RECTDESTINATION = 0x00000001`: Set destination rect
- `DWM_TNP_RECTSOURCE = 0x00000002`: Set source rect
- `DWM_TNP_OPACITY = 0x00000004`: Set opacity
- `DWM_TNP_VISIBLE = 0x00000008`: Set visibility
- `DWM_TNP_SOURCECLIENTAREAONLY = 0x00000010`: Client area only

### Window Embedding API

```csharp
// Set window parent (reparent window)
IntPtr SetParent(
    IntPtr hWndChild,      // Child window handle
    IntPtr hWndNewParent   // New parent window handle
);

// Set window position and size
bool SetWindowPos(
    IntPtr hWnd,           // Window handle
    IntPtr hWndInsertAfter, // Z-order handle
    int X,                 // X position
    int Y,                 // Y position
    int cx,                // Width
    int cy,                // Height
    uint uFlags            // Position flags
);

// Get/Set window style
int GetWindowLong(IntPtr hWnd, int nIndex);
int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
```

### Window Styles (for embedding)
- `GWL_STYLE = -16`: Window style index
- `WS_CHILD = 0x40000000`: Child window style
- `WS_VISIBLE = 0x10000000`: Visible window
- `WS_CAPTION = 0x00C00000`: Title bar (removed when embedding)
- `WS_THICKFRAME = 0x00040000`: Resizable border (removed when embedding)

### User32 API

```csharp
// Enumerate all top-level windows
bool EnumWindows(
    EnumWindowsProc enumProc,  // Callback function
    IntPtr lParam              // Application-defined value
);

// Check if window is visible
bool IsWindowVisible(IntPtr hWnd);

// Get window text/title
int GetWindowText(
    IntPtr hWnd,
    StringBuilder text,
    int count
);

// Activate window
bool SetForegroundWindow(IntPtr hWnd);

// Check if window handle is valid
bool IsWindow(IntPtr hWnd);
```

## WPF Transform System

### Canvas Transforms

The canvas uses a `TransformGroup` with two transforms:

1. **ScaleTransform**: Handles zoom level
   - `ScaleX` and `ScaleY`: 0.1 to 5.0 (10% to 500%)
   - Applied first

2. **TranslateTransform**: Handles pan offset
   - `X` and `Y`: Canvas offset in pixels
   - Applied after scale

### Zoom Algorithm (Mouse-Centered)

```csharp
// 1. Get mouse position on canvas
Point mousePos = e.GetPosition(canvas);

// 2. Calculate new scale
double newScale = currentScale * zoomFactor;
newScale = Clamp(newScale, 0.1, 5.0);

// 3. Calculate scale change ratio
double scaleChange = newScale / currentScale;

// 4. Adjust translation to keep mouse position fixed
translateX = mousePos.X - (mousePos.X - translateX) * scaleChange;
translateY = mousePos.Y - (mousePos.Y - translateY) * scaleChange;

// 5. Apply new scale
CanvasScaleTransform.ScaleX = newScale;
CanvasScaleTransform.ScaleY = newScale;
```

## Color Scheme

| Element | Color | Hex Code |
|---------|-------|----------|
| Background | Dark Gray | `#FF2B2B2B` |
| Window Border | Blue | `#FF6496FF` |
| Toolbar Background | Semi-transparent Black | `#CC000000` |
| Hint Text | Semi-transparent White | `#88FFFFFF` |
| Close Button | Semi-transparent Red | `#C8FF0000` |
| Title Bar | Semi-transparent Black | `#CC000000` |
| Thumbnail Background | Dark Gray | `#FF1E1E1E` |

## Event Flow

### Adding a Window
1. User clicks "Add Window" button
2. `AddWindowButton_Click()` opens `WindowSelectorDialog`
3. Dialog enumerates windows using `EnumWindows`
4. User selects window, dialog returns handle
5. `AddWindowThumbnail()` creates `WindowThumbnailControl`
6. Control is positioned randomly and added to canvas
7. Control registers DWM thumbnail in `OnLoaded()`

### Zooming
1. User scrolls mouse wheel
2. `Canvas_MouseWheel()` calculates new scale
3. Translation is adjusted to keep mouse position fixed
4. Transforms are updated
5. Zoom level display is updated

### Panning
1. User right/middle-clicks and drags
2. `Canvas_MouseDown()` starts pan mode
3. `Canvas_MouseMove()` updates translation
4. `Canvas_MouseUp()` ends pan mode

### Moving Thumbnail
1. User left-clicks thumbnail
2. `OnMouseLeftButtonDown()` detects click (not on close button)
3. If double-click: Activate source window
4. If single-click: Start drag mode
5. `OnMouseMove()` updates Canvas.Left/Top
6. `OnMouseLeftButtonUp()` ends drag mode

### Closing Thumbnail
1. User clicks close button
2. `CloseButton_Click()` fires `CloseRequested` event
3. MainWindow removes control from canvas
4. `Dispose()` unregisters DWM thumbnail

### Toggling Embed Mode
1. User clicks ⚡ button on thumbnail
2. `ModeToggleButton_Click()` fires `ModeToggleRequested` event
3. MainWindow calls `ToggleWindowMode()`
4. Thumbnail is disposed and removed
5. `WindowEmbedControl` is created with same position/size
6. Window is embedded using `SetParent()`
7. Window style is modified to remove decorations
8. Window is positioned inside host border

### Toggling Thumbnail Mode
1. User clicks 📸 button on embed control
2. `ModeToggleButton_Click()` fires `ModeToggleRequested` event
3. MainWindow calls `ToggleEmbedMode()`
4. Embed control restores original window state
5. Embed control is disposed and removed
6. `WindowThumbnailControl` is created with same position/size
7. DWM thumbnail is registered

## Resource Management

### IDisposable Pattern

All `WindowThumbnailControl` and `WindowEmbedControl` instances implement `IDisposable`:

```csharp
// WindowThumbnailControl
public void Dispose()
{
    if (!_disposed)
    {
        UnregisterThumbnail();  // DWM cleanup
        _disposed = true;
    }
    GC.SuppressFinalize(this);
}

// WindowEmbedControl
public void Dispose()
{
    if (!_disposed)
    {
        RestoreWindow();  // Restore original parent and style
        _disposed = true;
    }
    GC.SuppressFinalize(this);
}
}

~WindowThumbnailControl()
{
    Dispose();
}
```

### Cleanup on Exit

`MainWindow.OnClosing()` ensures all thumbnails are disposed:

```csharp
protected override void OnClosing(CancelEventArgs e)
{
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
```

## Building and Testing

### Build Commands

```bash
# Build in Debug mode
dotnet build

# Build in Release mode
dotnet build -c Release

# Run the application
dotnet run

# Publish standalone executable
dotnet publish -c Release -r win-x64 --self-contained true
```

### Testing Checklist

- [ ] Add multiple windows to canvas
- [ ] Zoom in to 500% (max)
- [ ] Zoom out to 10% (min)
- [ ] Pan canvas with right-click drag
- [ ] Pan canvas with middle-click drag
- [ ] Move thumbnail by dragging
- [ ] Double-click thumbnail to activate window
- [ ] Close thumbnail with × button
- [ ] Reset view to 100% and origin
- [ ] Close main window (verify no memory leaks)
- [ ] Add window that gets closed (handle gracefully)

## Known Limitations

1. **DWM Thumbnails**: Only works on Windows Vista and later
2. **Cross-Monitor**: Thumbnails update when window moves monitors
3. **Minimized Windows**: May not display correctly when source is minimized
4. **Performance**: Many thumbnails (>20) may impact performance
5. **Linux/Mac**: Won't build/run (Windows-only P/Invoke)

## Future Enhancements

- Window resize handles on thumbnails
- Save/load canvas layout to JSON
- Search/filter windows by title
- Keyboard shortcuts (Ctrl+Z for undo, etc.)
- Window grouping with visual containers
- Export canvas as image (PNG)
- Multi-monitor aware positioning
- Window auto-refresh when content changes
- Snap-to-grid positioning
- Thumbnail size presets

## Troubleshooting

### Build Issues
- **NETSDK1100**: Enable Windows targeting with `<EnableWindowsTargeting>true</EnableWindowsTargeting>`
- **Missing WPF**: Ensure `<UseWPF>true</UseWPF>` in .csproj
- **Target Framework**: Use `net6.0-windows` or higher

### Runtime Issues
- **Thumbnail not showing**: Ensure source window is visible and not minimized
- **DWM error**: Check Windows DWM service is running
- **Access denied**: Some system windows cannot be thumbnailed
- **Performance**: Reduce number of thumbnails or increase update interval

### API Return Codes
- `0` (S_OK): Success
- `0x87260001`: Invalid argument
- `0x80004005`: Unspecified error
- `0x8007000E`: Out of memory
