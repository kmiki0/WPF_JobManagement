using JobManagementApp.Helpers;
using JobManagementApp.Models;
using JobManagementApp.Services;
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
    public class JobLogDetailCommand
    {
        private readonly JobLogDetailViewModel _vm;
        private readonly IJobLogDetailModel _if;

        public JobLogDetailCommand(JobLogDetailViewModel VM, IJobLogDetailModel IF)
        {
            _vm = VM;
            _if = IF;
        }

        /// <summary> 
        /// ジョブログファイルの取得
        /// </summary> 
        public void SetJobLogDetailViewModel()
        {
            _if.GetJobLinkFile(_vm.Scenario, _vm.Eda, _vm.FileName, _vm.FilePath).ContinueWith(x =>
            {
                if (x.Result != null)
                {
                    _vm.Scenario = x.Result.SCENARIO;
                    _vm.Eda = x.Result.EDA;
                    _vm.JobId = x.Result.JOBID;
                    _vm.FileName = x.Result.FILENAME;
                    _vm.OldFileName = x.Result.FILENAME;
                    _vm.FilePath = x.Result.FILEPATH;
                    _vm.OldFilePath = x.Result.FILEPATH;
                    _vm.SelectedFileType = (emFileType)x.Result.FILETYPE;
                    _vm.ObserverType = x.Result.OBSERVERTYPE != 0;
                }
            });
        }


        /// <summary> 
        /// JobID 取得
        /// </summary> 
        public void GetJobId()
        {
            _if.GetJobId(_vm.Scenario, _vm.Eda).ContinueWith(x =>
            {
                if (x.Result != "")
                {
                    _vm.JobId =  x.Result;
                }
                else
                {
                    // 取得出来ない場合
                    _vm.JobId = "不明なPGID";
                }
            });
        }

        /// <summary> 
        /// 更新ボタン　押下イベント
        /// </summary> 
        public void UpdateButton_Click(object _)
        {
            // ボタン処理可能か
            if (!_vm.IsButtonEnabled) return;
            _vm.IsButtonEnabled = false;

            // 関連ファイル型に画面項目をセット
            JobLinkFile job = SetJobLinkFileFromVm();

            // 削除
            _if.DeleteJobLinkFile(job).ContinueWith(x =>
            {
                // 削除完了したい場合のみ、登録処理
                if (x.Result)
                {
                    // 登録
                    _if.RegistJobLinkFile(job).ContinueWith(y =>
                    {
                        if (y.Result)
                        {
                            MessageBox.Show("ジョブ関連ファイルの更新が完了しました。", "メッセージ", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
                            _vm.RequestClose_event();
                            _vm.window?.Dispatcher.Invoke(() => _vm.window.Close());
                        }
                        else
                        {
                            MessageBox.Show("ジョブ関連ファイルの更新に失敗しました。", "メッセージ", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
                        }
                    });
                }
                else
                {
                    MessageBox.Show("ジョブ関連ファイルの削除に失敗しました。", "メッセージ", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
                }

                _vm.IsButtonEnabled = true;
            });
        }

        /// <summary> 
        /// 閉じるボタン　押下イベント
        /// </summary> 
        public void CloseButton_Click(object _)
        {
            // ボタン処理可能か
            if (!_vm.IsButtonEnabled) return;

            _vm.window?.Close();
        }

        /// <summary> 
        /// 削除ボタン　押下イベント
        /// </summary> 
        public void DeleteButton_Click(object _)
        {
            // ボタン処理可能か
            if (!_vm.IsButtonEnabled) return;
            _vm.IsButtonEnabled = false;

            // 関連ファイル型に画面項目をセット
            JobLinkFile job = SetJobLinkFileFromVm();

            // 削除
            _if.DeleteJobLinkFile(job).ContinueWith(x =>
            {
                if (x.Result)
                {
                    MessageBox.Show("ジョブ関連ファイルの削除しました。", "メッセージ", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
                    _vm.RequestClose_event();
                    _vm.window?.Dispatcher.Invoke(() => _vm.window.Close());
                }
                else
                {
                    MessageBox.Show("ジョブ関連ファイルの削除に失敗しました。", "メッセージ", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
                }

                _vm.IsButtonEnabled = true;
            });
        }

        // JobLinkFile に VM の値をセット
        private JobLinkFile SetJobLinkFileFromVm()
        {
            return new JobLinkFile
            {
                SCENARIO = _vm.Scenario,
                EDA = _vm.Eda,
                FILENAME = _vm.FileName,
                OLDFILENAME = _vm.OldFileName,
                FILEPATH = _vm.FilePath,
                OLDFILEPATH = _vm.OldFilePath,
                FILETYPE = (int)_vm.SelectedFileType,
                OBSERVERTYPE = _vm.ObserverType ? 1 : 0,
            };
        }
    }
}
