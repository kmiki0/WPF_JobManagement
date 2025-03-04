using JobManagementApp.BaseClass;
using JobManagementApp.Helpers;
using JobManagementApp.Manager;
using JobManagementApp.Models;
using JobManagementApp.ViewModels;
using JobManagementApp.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace JobManagementApp.Commands
{
    class JobLogCommand : JobCommandArgument
    {
        //ボタン　クリックイベント
        // 一時保存フォルダボタン
        public void TempFolderButtonCommand(object parameter, Action<string> updatevm)
        {
            string tempFolderPath = parameter.ToString().Trim();

            // 文字列の最後の文字が「¥」の場合、切り取る
            if (tempFolderPath.EndsWith("\\"))
            {
                tempFolderPath = tempFolderPath.Substring(0, tempFolderPath.Length - 1);
            }

            // 入力されたパスが正しいかチェック 
            if (Directory.Exists(tempFolderPath))
            {
                // 正しい場合、キャッシュに保存
                UserFileManager manager = new UserFileManager();
                manager.SaveUserFilePath(manager.CacheKey_FilePath, tempFolderPath);

                // vmの値を更新
                updatevm(tempFolderPath);

                MessageBox.Show("キャッシュに保存しました。");
            }
            else
            {
                MessageBox.Show("入力されているフォルダ名が正しくありません。");
            }
        }

        /// <summary> 
        /// ログ追加　押下イベント
        /// </summary> 
        public void AddLogButtonCommand(object parameter)
        {
            var jobPrm = parameter as JobParamModel;

            // ViewModel 生成
            JobLogDetailViewModel vm = new JobLogDetailViewModel(jobPrm.Scenario, jobPrm.Eda);
            // 返却用のCloseイベント 上書き
            //vm.RequestClose += LogDetailWindow_RequestClose;
            JobLogDetailWindow logDetailWindow = new JobLogDetailWindow(vm);
            var window = logDetailWindow as System.Windows.Window;
            // ウィンドウの表示位置　調整
            WindowHelper.SetWindowLocation(ref window);
            vm.window = logDetailWindow;
            logDetailWindow.DataContext = vm;
            logDetailWindow.ShowDialog();
        }

        // フォルダを開くボタン
        public void FolderButtonCommand(object parameter)
        {
            string ToCopyFolderPath = parameter.ToString().Trim();
            Process.Start("explorer.exe", ToCopyFolderPath);
        }

        // 閉じるボタン
        public  void CloseButtonCommand(object parameter)
        {
            Window window = parameter as Window;

            if (window != null)
            {
                window.Close();
            }
        }

    }
}
