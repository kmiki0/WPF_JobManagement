using System;
using System.Windows.Forms;
using JobManagementApp.Views;
using JobManagementApp.ViewModels;
using JobManagementApp.Models;
using JobManagementApp.Services;
using JobManagementApp.Helpers;
using JobManagementApp.BaseClass;
using System.Linq;
using JobManagementApp.Manager;

namespace JobManagementApp.Commands
{
    // JobListItemViewModel イベント 処理
    public class JobListItemCommand : JobCommandArgument
    {
        private readonly JobListItemViewModel _vm;

        public JobListItemCommand(JobListItemViewModel VM)
        {
            _vm = VM;
        }

        /// <summary> 
        /// 実行ボタン 押下処理
        /// </summary> 
        public void RunButton_Click(object parameter)
        {
            var arg = ConvertParameter(parameter);
            // メッセージボックスを表示
            DialogResult result = MessageBox.Show(
                "運用処理管理Rを更新します。\n本当によろしいですか？", 
                "確認", 
                MessageBoxButtons.YesNo, 
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2
            );

            // ユーザーが「はい」を選択した場合の処理
            if (result == DialogResult.Yes)
            {
                JobService.UpdateUnyoCtrl(arg.scenario, arg.eda);
                _vm.Status = emStatus.RUN;
            }
        }

        /// <summary> 
        /// 詳細ボタン 押下処理
        /// </summary> 
        public void DetailButton_Click(object parameter)
        {
            var arg = ConvertParameter(parameter);
            JobDetailViewModel vm = new JobDetailViewModel(new JobDetailModel(), arg.scenario, arg.eda);
            vm.RequestClose += DetailWindow_RequestClose;
            JobDetailWindow detailWindow = new JobDetailWindow(vm);
            var window = detailWindow as System.Windows.Window;
            // ウィンドウの表示位置　調整
            WindowHelper.SetWindowLocation(ref window);
            vm.window = detailWindow;
            detailWindow.DataContext = vm;
            detailWindow.Show();
        }

        /// <summary> 
        /// 詳細ウィンドウ Closeイベント
        /// </summary> 
        private void DetailWindow_RequestClose(object sender, JobListItemViewModel e)
        {
            // MainViewModelに通知するための処理を追加
            MainViewModel.Instance.UpdateJobs(e);
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
            JobLogViewModel vm = new JobLogViewModel(new JobLogModel(), arg.scenario, arg.eda);
            logWindow.Closed += LogWindow_Closed;
            vm.window = logWindow;
            logWindow.DataContext = vm;
            logWindow.Show();
        }

        /// <summary> 
        /// ログウィンドウ Closeイベント
        /// </summary> 
        public void LogWindow_Closed(object sender, EventArgs e)
        {
            // 
            JobLogWindow jobLogWindow = sender as JobLogWindow;
            if (jobLogWindow != null)
            {
                JobLogViewModel vm = (JobLogViewModel)jobLogWindow.DataContext;
                vm._command._multiFileWatcher.Dispose();
            }

            // MainWindow アクティブ
            System.Windows.Window mainWindow = WindowHelper.GetMainWindow();
            if (mainWindow != null) mainWindow.Activate();
        }
    }
}
