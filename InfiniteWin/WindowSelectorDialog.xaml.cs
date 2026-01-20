using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace InfiniteWin
{
    /// <summary>
    /// Dialog for selecting a window to add to the canvas
    /// </summary>
    public partial class WindowSelectorDialog : Window
    {
        #region Win32 API Declarations

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetShellWindow();

        #endregion

        public IntPtr SelectedWindow { get; private set; }

        private class WindowInfo
        {
            public IntPtr Handle { get; set; }
            public string Title { get; set; } = string.Empty;

            public override string ToString() => Title;
        }

        public WindowSelectorDialog()
        {
            InitializeComponent();
            LoadWindows();
        }

        /// <summary>
        /// Enumerate all visible windows and populate the list
        /// </summary>
        private void LoadWindows()
        {
            var windows = new List<WindowInfo>();
            IntPtr shellWindow = GetShellWindow();

            EnumWindows((hWnd, lParam) =>
            {
                // Skip invisible windows
                if (!IsWindowVisible(hWnd))
                    return true;

                // Skip windows without title
                int length = GetWindowTextLength(hWnd);
                if (length == 0)
                    return true;

                // Get window title
                var builder = new StringBuilder(length + 1);
                GetWindowText(hWnd, builder, builder.Capacity);
                string title = builder.ToString();

                // Filter out empty titles and shell window
                if (string.IsNullOrWhiteSpace(title) || hWnd == shellWindow)
                    return true;

                windows.Add(new WindowInfo
                {
                    Handle = hWnd,
                    Title = title
                });

                return true;
            }, IntPtr.Zero);

            // Sort by title
            windows.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.OrdinalIgnoreCase));

            // Populate list box
            WindowListBox.ItemsSource = windows;

            // Select first item if available
            if (windows.Count > 0)
            {
                WindowListBox.SelectedIndex = 0;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (WindowListBox.SelectedItem is WindowInfo windowInfo)
            {
                SelectedWindow = windowInfo.Handle;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Please select a window first.", "No Selection", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void WindowListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (WindowListBox.SelectedItem is WindowInfo windowInfo)
            {
                SelectedWindow = windowInfo.Handle;
                DialogResult = true;
                Close();
            }
        }
    }
}
