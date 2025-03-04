using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JobManagementApp.BaseClass;
using JobManagementApp.Helpers;
using JobManagementApp.Models;
using JobManagementApp.ViewModels;
using JobManagementApp.Views;

namespace JobManagementApp.Commands
{
    class JobLogItemCommand : JobCommandArgument
    {
        /// <summary> 
        /// 編集ボタン　押下イベント
        /// </summary> 
        public void EditButtonCommand(object parameter)
        {
            var jobPrm = parameter as JobParamModel;

            // ViewModel 生成
            JobLogDetailViewModel vm = new JobLogDetailViewModel(jobPrm.Scenario, jobPrm.Eda, jobPrm.FileName, jobPrm.FilePath);
            // 返却用のCloseイベント 上書き
            vm.RequestClose += LogDetailWindow_RequestClose;
            JobLogDetailWindow logDetailWindow = new JobLogDetailWindow(vm);
            var window = logDetailWindow as System.Windows.Window;
            // ウィンドウの表示位置　調整
            WindowHelper.SetWindowLocation(ref window);
            vm.window = logDetailWindow;
            logDetailWindow.DataContext = vm;
            logDetailWindow.ShowDialog();
        }

        /// <summary> 
        /// ログ編集ウィンドウ Closeイベント
        /// </summary> 
        private void LogDetailWindow_RequestClose(object sender, JobParamModel e)
        {
            // TODO : Logwindow 再度表示
            //JobLogViewModel vm = JobLogViewModel.GetInstance(e.Scenario, e.Eda);

            // JobLogWindow アクティブ
            System.Windows.Window jobLogWindow = WindowHelper.GetJobLogWindow();
            if (jobLogWindow != null) jobLogWindow.Activate();
        }

    }
}
