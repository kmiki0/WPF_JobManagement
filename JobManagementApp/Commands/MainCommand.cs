using JobManagementApp.BaseClass;
using JobManagementApp.Helpers;
using JobManagementApp.Manager;
using JobManagementApp.ViewModels;
using JobManagementApp.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace JobManagementApp.Commands
{
    class MainCommand : JobCommandArgument
    {
        /// <summary> 
        /// ユーザー保存　押下イベント
        /// </summary> 
        public void CacheUserUpdateCommand(object parameter, Action<string> updatevm)
        {
            if (!string.IsNullOrEmpty(parameter?.ToString()))
            {
                var userId = parameter?.ToString();
                // 正しい場合、キャッシュに保存
                UserFileManager manager = new UserFileManager();
                manager.SaveUserFilePath(manager.CacheKey_UserId, userId);

                // vmの値を更新
                updatevm(userId);

                MessageBox.Show("キャッシュに保存しました。");
            }
        }

        /// <summary> 
        /// 画面更新　押下イベント
        /// </summary> 
        public void RefreshButtonCommand(object parameter)
        {
        }

        /// <summary> 
        /// ジョブ追加　押下イベント
        /// </summary> 
        public void NewJobButtonCommand(object parameter)
        {
            JobDetailViewModel vm = new JobDetailViewModel();
            //vm.RequestClose += DetailWindow_RequestClose;
            JobDetailWindow detailWindow = new JobDetailWindow(vm);
            var window = detailWindow as System.Windows.Window;
            // ウィンドウの表示位置　調整
            WindowHelper.SetWindowLocation(ref window);
            vm.window = detailWindow;
            detailWindow.DataContext = vm;
            detailWindow.ShowDialog();
        }

    }
}
