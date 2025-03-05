using System;
using System.Windows.Forms;
using JobManagementApp.Views;
using JobManagementApp.ViewModels;
using JobManagementApp.Models;
using JobManagementApp.Services;
using JobManagementApp.Helpers;
using JobManagementApp.BaseClass;
using System.Linq;

namespace JobManagementApp.Commands
{
    // JobListItemViewModel イベント 処理
    public class JobListItemCommand : JobCommandArgument
    {
        /// <summary> 
        /// 実行ボタン 押下処理
        /// </summary> 
        public void RunButton_Click(object parameter)
        {
            var arg = ConvertParameter(parameter);
            // メッセージボックスを表示
            DialogResult result = MessageBox.Show(
                "運用処理管理Rを更新しますか？", "確認", MessageBoxButtons.YesNo, MessageBoxIcon.Question
            );

            // ユーザーが「はい」を選択した場合の処理
            if (result == DialogResult.Yes)
            {
                JobService.UpdateUnyoCtrl(arg.scenario, arg.eda);
            }

        }

        /// <summary> 
        /// 詳細ボタン 押下処理
        /// </summary> 
        public void DetailButton_Click(object parameter)
        {
            var arg = ConvertParameter(parameter);
            JobDetailViewModel vm = new JobDetailViewModel(arg.scenario, arg.eda);
            vm.RequestClose += DetailWindow_RequestClose;
            JobDetailWindow detailWindow = new JobDetailWindow(vm);
            var window = detailWindow as System.Windows.Window;
            // ウィンドウの表示位置　調整
            WindowHelper.SetWindowLocation(ref window);

            vm.window = detailWindow;
            detailWindow.DataContext = vm;
            detailWindow.ShowDialog();
        }

        /// <summary> 
        /// 詳細ウィンドウ Closeイベント
        /// </summary> 
        private void DetailWindow_RequestClose(object sender, JobListItemViewModel e)
        {
            // MainViewModelに通知するための処理を追加
            MainViewModel.Instance.UpdateJobs(e);

            // MainWindow アクティブ
            System.Windows.Window mainWindow = WindowHelper.GetMainWindow();
            if (mainWindow != null) mainWindow.Activate();
        }

        /// <summary> 
        /// ログボタン 押下処理
        /// </summary> 
        public void LogButton_Click(object parameter)
        {
            var arg = ConvertParameter(parameter);
            JobLogWindow logWindow = new JobLogWindow();
            var window = logWindow as System.Windows.Window;
            // ウィンドウの表示位置　調整
            WindowHelper.SetWindowLocation(ref window);
            JobLogViewModel vm = new JobLogViewModel(arg.scenario, arg.eda);
            logWindow.Closed += LogWindow_Closed;
            vm.window = logWindow;
            logWindow.DataContext = vm;
            logWindow.ShowDialog();
        }

        /// <summary> 
        /// ログウィンドウ Closeイベント
        /// </summary> 
        public void LogWindow_Closed(object sender, EventArgs e)
        {
            // MainWindow アクティブ
            System.Windows.Window mainWindow = WindowHelper.GetMainWindow();
            if (mainWindow != null) mainWindow.Activate();
        }
    }
}
