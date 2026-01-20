# InfiniteWin Application UI Preview

## Main Window Layout

```
┌────────────────────────────────────────────────────────────────┐
│ InfiniteWin - Virtual Canvas Desktop              ☐ □ ✕       │
├────────────────────────────────────────────────────────────────┤
│                                                                 │
│                                    ┌─────────────────────────┐ │
│                                    │ Add Window │ Reset View │ │
│                                    │          │ 100%          │ │
│                                    └─────────────────────────┘ │
│    ┌────────────────────────┐                                 │
│    │ Window Title       [×] │                                 │
│    ├────────────────────────┤                                 │
│    │                        │                                 │
│    │  [Live Window          │       ┌────────────────────┐   │
│    │   Thumbnail]           │       │ Another Window [×] │   │
│    │                        │       ├────────────────────┤   │
│    │                        │       │                    │   │
│    └────────────────────────┘       │  [Live Window      │   │
│                                     │   Thumbnail]       │   │
│                                     │                    │   │
│                                     └────────────────────┘   │
│                                                                 │
│                                                                 │
│         Right-click or Middle-click to pan • Mouse wheel      │
│         to zoom • Double-click window to activate              │
└────────────────────────────────────────────────────────────────┘
```

## Window Selector Dialog

```
┌──────────────────────────────────┐
│ Select Window            ☐ □ ✕  │
├──────────────────────────────────┤
│ Select a window to add to the    │
│ canvas:                           │
│                                   │
│ ┌────────────────────────────┐   │
│ │ Calculator                 │   │
│ │ File Explorer              │   │
│ │ Google Chrome              │   │
│ │ Microsoft Edge             │   │
│ │ Notepad                    │   │
│ │ Task Manager               │   │
│ │ Visual Studio Code         │   │
│ │ ...                        │   │
│ └────────────────────────────┘   │
│                                   │
│              ┌────┐  ┌────────┐  │
│              │ OK │  │ Cancel │  │
│              └────┘  └────────┘  │
└──────────────────────────────────┘
```

## Window Thumbnail Control

```
┌────────────────────────┐
│ Window Title       [×] │  ← Title bar (semi-transparent black)
├────────────────────────┤
│ ┌──────────────────┐   │
│ │                  │   │
│ │  Live DWM        │   │  ← DWM Thumbnail area
│ │  Thumbnail       │   │     (shows live window content)
│ │  of Source       │   │
│ │  Window          │   │
│ │                  │   │
│ └──────────────────┘   │
└────────────────────────┘
  ↑
  Blue border (#6496FF)
  with rounded corners
```

## Color Scheme

### Main Window
- **Background**: `#FF2B2B2B` (Dark Gray)
- **Canvas**: Transparent with dark background showing through

### Toolbar
- **Background**: `#CC000000` (Semi-transparent Black)
- **Buttons**: Default WPF button style
- **Text**: White

### Window Thumbnails
- **Border**: `#FF6496FF` (Blue) - 2px thickness
- **Background**: `#FF1E1E1E` (Very Dark Gray)
- **Title Bar**: `#CC000000` (Semi-transparent Black)
- **Title Text**: White, 12px
- **Close Button**: `#C8FF0000` (Semi-transparent Red)
- **Close Button Text**: White "×", 16px bold
- **Corner Radius**: 8px (outer), 6px (inner)

### Window Selector Dialog
- **Background**: `#FF2B2B2B` (Dark Gray)
- **List Background**: `#FF1E1E1E` (Very Dark Gray)
- **Border**: `#FF6496FF` (Blue)
- **Text**: White
- **Selected Item**: `#FF6496FF` (Blue)
- **Hover Item**: `#FF4A76DD` (Lighter Blue)

### Helper Text
- **Color**: `#88FFFFFF` (Semi-transparent White)
- **Font Size**: 14px

## Interaction States

### Panning
```
Cursor: SizeAll (⊕)
Action: Canvas translates with mouse movement
Visual: No specific indicator
```

### Zooming
```
Trigger: Mouse wheel scroll
Visual: Canvas scales, zoom percentage updates in toolbar
Range: 10% - 500%
```

### Dragging Thumbnail
```
Cursor: Arrow
Action: Thumbnail moves with mouse
Visual: No specific indicator
```

### Hovering Close Button
```
Cursor: Hand
Visual: Button can be visually highlighted (default WPF behavior)
```

## Layout Measurements

### Main Window
- **Default Size**: 1200 × 800 pixels
- **Minimum Size**: Not set (can be resized to any size)
- **Startup Position**: Center screen

### Window Thumbnails
- **Default Size**: 400 × 300 pixels
- **Title Bar Height**: 25 pixels
- **Border Thickness**: 2 pixels
- **Inner Margin**: 5 pixels
- **Close Button**: 20 × 20 pixels

### Window Selector Dialog
- **Default Size**: 600 × 500 pixels
- **Startup Position**: Centered over main window
- **Resize Mode**: Resizable

## Toolbar Layout

```
┌─────────────────────────────────────────┐
│ [Add Window] [Reset View] 100%          │
│  ↑            ↑            ↑            │
│  Button       Button       TextBlock   │
│  Padding:     Padding:     Margin:     │
│  10,5         10,5         10,5        │
│  Margin:      Margin:                  │
│  5            5                        │
└─────────────────────────────────────────┘
```

## Canvas Transform Visualization

### Zoom In (200%)
```
Original Canvas:
┌────────────────┐
│    ┌────┐      │
│    │ A  │      │
│    └────┘      │
└────────────────┘

Zoomed Canvas (ScaleTransform 2.0):
┌────────────────┐
│ ┌──────────┐   │
│ │          │   │
│ │    A     │   │  ← Everything 2x larger
│ │          │   │
│ └──────────┘   │
└────────────────┘
```

### Pan (TranslateTransform)
```
Original Position:
┌────────────────┐
│    ┌────┐      │
│    │ A  │      │
│    └────┘      │
└────────────────┘

Panned (X+50, Y+30):
┌────────────────┐
│                │
│         ┌────┐ │
│         │ A  │ │  ← Moved right and down
│         └────┘ │
└────────────────┘
```

## User Workflow

### Adding a Window

```
1. User clicks "Add Window" button
   ↓
2. Window Selector Dialog appears
   ↓
3. User selects window from list
   ↓
4. User clicks OK or double-clicks item
   ↓
5. Thumbnail appears at random position on canvas
   ↓
6. User can drag, zoom, or interact with thumbnail
```

### Navigating the Canvas

```
1. User scrolls mouse wheel
   ↓
2. Canvas zooms in/out centered on mouse position
   ↓
3. Zoom level updates in toolbar (e.g., "150%")
   
OR

1. User right-clicks and drags
   ↓
2. Cursor changes to SizeAll
   ↓
3. Canvas pans with mouse movement
   ↓
4. User releases mouse button
   ↓
5. Cursor returns to normal
```

### Managing Thumbnails

```
Single-click on thumbnail:
   ↓
Drag to move position

Double-click on thumbnail:
   ↓
Source window activates and comes to foreground

Click × button:
   ↓
Thumbnail removed from canvas
DWM resources cleaned up
```

## Technical Notes

- All measurements in pixels (device-independent units)
- XAML uses default WPF rendering
- DWM thumbnails update automatically when source window changes
- Transforms applied in order: Scale → Translate
- Canvas children positioned using Canvas.Left and Canvas.Top attached properties
- Z-order: Last added child appears on top
