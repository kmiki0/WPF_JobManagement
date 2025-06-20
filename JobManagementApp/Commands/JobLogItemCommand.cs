﻿using System;
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
    public class JobLogItemCommand : JobCommandArgument
    {
        private readonly JobLogItemViewModel _vm;
        public JobLogItemCommand(JobLogItemViewModel VM)
        {
            _vm = VM;
        }

        /// <summary> 
        /// 編集ボタン　押下イベント
        /// </summary> 
        public void EditButton_Click(object prm)
        {
            var jobPrm = prm as JobParamModel;

            // ViewModel 生成
            JobLogDetailViewModel vm = new JobLogDetailViewModel(new JobLogDetailModel(), jobPrm.Scenario, jobPrm.Eda, jobPrm.FileName, jobPrm.FilePath, jobPrm.FileCount);
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
        /// 手動ログ取得 押下イベント
        /// </summary> 
        public void ManualLogButton_Click(object _)
        {
            

        }

        /// <summary> 
        /// ログ編集ウィンドウ Closeイベント
        /// </summary> 
        private void LogDetailWindow_RequestClose(object sender, JobParamModel e)
        {
            // MainViewModelに通知するための処理を追加
            JobLogViewModel.RecreateViewModel(e);
        }
    }
}
