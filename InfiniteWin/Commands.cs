using System;
using System.Windows;
using System.Windows.Controls;

namespace InfiniteWin
{
    /// <summary>
    /// Command for adding a window thumbnail
    /// </summary>
    public class AddWindowCommand : ICommand
    {
        private readonly Canvas _canvas;
        private readonly WindowThumbnailControl _thumbnail;
        private bool _executed = false;

        public AddWindowCommand(Canvas canvas, WindowThumbnailControl thumbnail)
        {
            _canvas = canvas;
            _thumbnail = thumbnail;
        }

        public void Execute()
        {
            if (!_executed)
            {
                _canvas.Children.Add(_thumbnail);
                _executed = true;
            }
        }

        public void Undo()
        {
            if (_executed)
            {
                _canvas.Children.Remove(_thumbnail);
                _thumbnail.Dispose();
                _executed = false;
            }
        }
    }

    /// <summary>
    /// Command for removing a window thumbnail
    /// </summary>
    public class RemoveWindowCommand : ICommand
    {
        private readonly Canvas _canvas;
        private WindowThumbnailControl _thumbnail;
        private readonly IntPtr _sourceWindow;
        private readonly double _left;
        private readonly double _top;
        private readonly double _width;
        private readonly double _height;
        private readonly Action<WindowThumbnailControl> _setupEventHandlers;
        private bool _executed = false;

        public RemoveWindowCommand(Canvas canvas, WindowThumbnailControl thumbnail, Action<WindowThumbnailControl> setupEventHandlers)
        {
            _canvas = canvas;
            _thumbnail = thumbnail;
            _sourceWindow = thumbnail.SourceWindow;
            _left = Canvas.GetLeft(thumbnail);
            _top = Canvas.GetTop(thumbnail);
            _width = thumbnail.Width;
            _height = thumbnail.Height;
            _setupEventHandlers = setupEventHandlers;
        }

        public void Execute()
        {
            if (!_executed)
            {
                _canvas.Children.Remove(_thumbnail);
                _thumbnail.Dispose();
                _executed = true;
            }
        }

        public void Undo()
        {
            if (_executed)
            {
                // Create a new thumbnail control instead of reusing the disposed one
                _thumbnail = new WindowThumbnailControl(_sourceWindow);
                _thumbnail.Width = _width;
                _thumbnail.Height = _height;
                Canvas.SetLeft(_thumbnail, _left);
                Canvas.SetTop(_thumbnail, _top);
                
                // Reconnect event handlers
                _setupEventHandlers(_thumbnail);
                
                _canvas.Children.Add(_thumbnail);
                _executed = false;
            }
        }
    }

    /// <summary>
    /// Command for moving a window thumbnail
    /// </summary>
    public class MoveWindowCommand : ICommand
    {
        private readonly WindowThumbnailControl _thumbnail;
        private readonly double _oldLeft;
        private readonly double _oldTop;
        private readonly double _newLeft;
        private readonly double _newTop;

        public MoveWindowCommand(WindowThumbnailControl thumbnail, double oldLeft, double oldTop, double newLeft, double newTop)
        {
            _thumbnail = thumbnail;
            _oldLeft = oldLeft;
            _oldTop = oldTop;
            _newLeft = newLeft;
            _newTop = newTop;
        }

        public void Execute()
        {
            Canvas.SetLeft(_thumbnail, _newLeft);
            Canvas.SetTop(_thumbnail, _newTop);
            _thumbnail.UpdateThumbnail();
        }

        public void Undo()
        {
            Canvas.SetLeft(_thumbnail, _oldLeft);
            Canvas.SetTop(_thumbnail, _oldTop);
            _thumbnail.UpdateThumbnail();
        }
    }

    /// <summary>
    /// Command for resizing a window thumbnail
    /// </summary>
    public class ResizeWindowCommand : ICommand
    {
        private readonly WindowThumbnailControl _thumbnail;
        private readonly double _oldWidth;
        private readonly double _oldHeight;
        private readonly double _oldLeft;
        private readonly double _oldTop;
        private readonly double _newWidth;
        private readonly double _newHeight;
        private readonly double _newLeft;
        private readonly double _newTop;

        public ResizeWindowCommand(WindowThumbnailControl thumbnail, 
            double oldWidth, double oldHeight, double oldLeft, double oldTop,
            double newWidth, double newHeight, double newLeft, double newTop)
        {
            _thumbnail = thumbnail;
            _oldWidth = oldWidth;
            _oldHeight = oldHeight;
            _oldLeft = oldLeft;
            _oldTop = oldTop;
            _newWidth = newWidth;
            _newHeight = newHeight;
            _newLeft = newLeft;
            _newTop = newTop;
        }

        public void Execute()
        {
            _thumbnail.Width = _newWidth;
            _thumbnail.Height = _newHeight;
            Canvas.SetLeft(_thumbnail, _newLeft);
            Canvas.SetTop(_thumbnail, _newTop);
            _thumbnail.UpdateThumbnail();
        }

        public void Undo()
        {
            _thumbnail.Width = _oldWidth;
            _thumbnail.Height = _oldHeight;
            Canvas.SetLeft(_thumbnail, _oldLeft);
            Canvas.SetTop(_thumbnail, _oldTop);
            _thumbnail.UpdateThumbnail();
        }
    }
}
