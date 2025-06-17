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
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using Application = System.Windows.Application;
using MessageBox = System.Windows.Forms.MessageBox;

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
            try
            {
                var arg = ConvertParameter(parameter);
                
                // 既存の詳細ウィンドウが開いていないかチェック
                if (IsDetailWindowAlreadyOpen(arg.scenario, arg.eda))
                {
                    return;
                }

                JobDetailViewModel vm = new JobDetailViewModel(new JobDetailModel(), arg.scenario, arg.eda);
                vm.RequestClose += DetailWindow_RequestClose;
                
                JobDetailWindow detailWindow = new JobDetailWindow(vm);
                var window = detailWindow as System.Windows.Window;
                
                // ウィンドウの表示位置　調整
                WindowHelper.SetWindowLocation(ref window);
                vm.window = detailWindow;
                detailWindow.DataContext = vm;
                
                // ウィンドウクローズイベントの処理
                detailWindow.Closed += (sender, e) => HandleWindowClosed(vm);
                detailWindow.Show();
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"DetailButton_Click エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 指定されたジョブの詳細ウィンドウが既に開いているかチェック
        /// </summary>
        private bool IsDetailWindowAlreadyOpen(string scenario, string eda)
        {
            try
            {
                foreach (Window window in Application.Current.Windows)
                {
                    if (window is JobDetailWindow detailWindow && 
                        detailWindow.DataContext is JobDetailViewModel vm)
                    {
                        if (vm.Scenario == scenario && vm.Eda == eda)
                        {
                            // 既に開いている場合はアクティブにする
                            window.Activate();
                            return true;
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"IsDetailWindowAlreadyOpen エラー: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// ウィンドウクローズ時の処理
        /// </summary>
        private void HandleWindowClosed(JobDetailViewModel vm)
        {
            try
            {
                // ViewModelのクリーンアップ
                vm?.Dispose();
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"HandleWindowClosed エラー: {ex.Message}");
            }
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
            JobLogWindow jobLogWindow = sender as JobLogWindow;
            if (jobLogWindow != null)
            {
                JobLogViewModel vm = (JobLogViewModel)jobLogWindow.DataContext;
                //vm._command._multiFileWatcher.Dispose();
                 IFileWatcherManager _fw = App.ServiceProvider.GetRequiredService<IFileWatcherManager>();
                // ジョブIDに紐づく、ログファイルを監視対象から外す
                _fw.RemoveAllWhereJobId(vm.Id);
            }

            // MainWindow アクティブ
            System.Windows.Window mainWindow = WindowHelper.GetMainWindow();
            if (mainWindow != null) mainWindow.Activate();
        }
    }
}
