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
        /// <summary> 
        /// ジョブログファイルの取得 - ObserverType検証強化版
        /// </summary> 
        public void SetJobLogDetailViewModel()
        {
            try
            {
                _if.GetJobLinkFile(_vm.Scenario, _vm.Eda, _vm.FileName, _vm.FilePath).ContinueWith(x =>
                {
                    try
                    {
                        if (x.Result != null)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                _vm.Scenario = x.Result.SCENARIO;
                                _vm.Eda = x.Result.EDA;
                                _vm.JobId = x.Result.JOBID;
                                _vm.FileName = x.Result.FILENAME;
                                _vm.OldFileName = x.Result.FILENAME;
                                _vm.FilePath = x.Result.FILEPATH;
                                _vm.OldFilePath = x.Result.FILEPATH;
                                _vm.SelectedFileType = (emFileType)x.Result.FILETYPE;
                                
                                // ObserverTypeの検証と設定
                                var observerType = x.Result.OBSERVERTYPE;
                                if (observerType == 0 || observerType == 1)
                                {
                                    _vm.ObserverType = observerType;
                                    LogFile.WriteLog($"ObserverType設定完了: {observerType}");
                                }
                                else
                                {
                                    ErrLogFile.WriteLog($"データベースから無効なOBSERVERTYPE値: {observerType}。デフォルト値(0)を使用します。");
                                    _vm.ObserverType = 0;
                                }
                            });
                        }
                        else
                        {
                            ErrLogFile.WriteLog("SetJobLogDetailViewModel: データベースから結果が取得できませんでした");
                        }
                    }
                    catch (Exception ex)
                    {
                        ErrLogFile.WriteLog($"SetJobLogDetailViewModel データ設定エラー: {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"SetJobLogDetailViewModel エラー: {ex.Message}");
            }
        }
    }
}