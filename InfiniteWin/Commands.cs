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
        private readonly WindowThumbnailControl _thumbnail;
        private readonly double _left;
        private readonly double _top;
        private bool _executed = false;

        public RemoveWindowCommand(Canvas canvas, WindowThumbnailControl thumbnail)
        {
            _canvas = canvas;
            _thumbnail = thumbnail;
            _left = Canvas.GetLeft(thumbnail);
            _top = Canvas.GetTop(thumbnail);
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
                Canvas.SetLeft(_thumbnail, _left);
                Canvas.SetTop(_thumbnail, _top);
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
}
