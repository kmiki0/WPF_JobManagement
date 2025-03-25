using System.Windows;
using JobManagementApp.Views;

namespace JobManagementApp.Helpers
{
    public static class WindowHelper
    {
        public static Window GetMainWindow()
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window is MainWindow mainWindow)
                {
                    return mainWindow;
                }
            }

            return null;
        }

        public static Window GetJobLogWindow()
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window is JobLogWindow logWindow)
                {
                    return logWindow;
                }
            }

            return null;
        }
        public static Window GetJobLogDetailWindow()
        {
            foreach (Window window in Application.Current.Windows)
            {
                if (window is JobLogDetailWindow logWindow)
                {
                    return logWindow;
                }
            }

            return null;
        }

        /// <summary> 
        /// MainWindowから中心位置に引数のウィンドウ セット
        /// </summary> 
        public static Window SetWindowLocation(ref Window newWindow)
        {
            // メインウィンドウ 取得
            Window mainWindow = GetMainWindow();
            if (mainWindow != null)
            {
                // 新しいウィンドウの位置を計算
                newWindow.Left = mainWindow.Left + (mainWindow.Width - newWindow.Width) / 2;
                newWindow.Top = mainWindow.Top + (mainWindow.Height - newWindow.Height) / 2;
            }

            return newWindow;
        }
    }
}
