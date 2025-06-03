using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using JobManagementApp.Views;
using JobManagementApp.ViewModels;
using JobManagementApp.Models;
using JobManagementApp.Manager;
using JobManagementApp.Helpers;
using JobManagementApp.BaseClass;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace JobManagementApp.Commands
{
    public class JobLogCommand : JobCommandArgument
    {
        public MultiFileWatcher _multiFileWatcher;

        private readonly IFileWatcherManager _fw = App.ServiceProvider.GetRequiredService<IFileWatcherManager>();

        private readonly JobLogViewModel _vm;
        private readonly IJobLogModel _if;
        
        public JobLogCommand(JobLogViewModel VM, IJobLogModel IF)
        {
            _vm = VM;
            _if = IF;

            Init();
        }

        private static ConcurrentDictionary<string, SemaphoreSlim> fileSemaphores;
        private Dictionary<string, FileSystemWatcher> watchers;

        /// <summary> 
        /// 初期化
        /// </summary> 
        private void Init()
        {
            fileSemaphores = new ConcurrentDictionary<string, SemaphoreSlim>();
            watchers = new Dictionary<string, FileSystemWatcher>();

            // キャッシュ読み込み
            UserFileManager manager = new UserFileManager();
            _vm.TempSavePath = manager.GetCache(manager.CacheKey_FilePath);

            // ジョブID 読み込み
            LoadJobId();

            // ログ一覧 読み込み
            LoadLogList();
        }

        // ログ一覧 読み込み
        public void LoadLogList()
        {
            var logList = new List<JobLogItemViewModel>();

            _if.GetJobLinkFile(_vm.Scenario, _vm.Eda).ContinueWith(x =>
            {
                // ジョブごとにUIを生成する
                foreach (JobLinkFile job in x.Result)
                {
                    // enum変換
                    emFileType fileType = Enum.TryParse(job.FILETYPE.ToString(), out fileType) ? fileType : emFileType.LOG;

                    var item = new JobLogItemViewModel {
                        Scenario = job.SCENARIO,
                        Eda = job.EDA,
                        Id = job.JOBID,
                        FilePath = job.FILEPATH,
                        FileName = job.FILENAME,
                        DisplayFileName = job.FILENAME,
                        FileType = fileType,
                        FileCount = job.FILECOUNT,
                        ObserverType = job.OBSERVERTYPE,
                        ObserverStatus = emObserverStatus.OBSERVER
                    };
                    logList.Add(item);
                }
                // 画面項目にセット
                _vm.Logs = new ObservableCollection<JobLogItemViewModel>(logList);

                // 関連ファイルの監視を開始
                StartMonitoring();
            });
        }


        /// <summary> 
        /// vmのジョブIDに値セット
        /// </summary> 
        public void LoadJobId()
        {
            // シナリオと枝番からジョブIDを検索
            _if.GetJobManegment(_vm.Scenario, _vm.Eda).ContinueWith(x =>
            {
                _vm.Id = x.Result.ID;
            });
        }

        /// <summary> 
        /// パス保存ボタン クリック処理
        /// </summary> 
        public void TempFolderButton_Click(object prm)
        {
            string path = prm.ToString().Trim();

            // 文字列の最後の文字が「¥」の場合、切り取る
            if (path.EndsWith("\\"))
            {
                path = path.Substring(0, path.Length - 1);
            }

            // 入力されたパスが正しいかチェック 
            if (Directory.Exists(path))
            {
                // 正しい場合、キャッシュに保存
                if (_if.SaveCachePath(path))
                {
                    // vmの値を更新
                    _vm.TempSavePath = path;
                    MessageBox.Show("キャッシュに保存しました。", "メッセージ", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
                }
            }
            else
            {
                MessageBox.Show("入力されているフォルダ名が正しくありません。", "メッセージ", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
            }
        }

        /// <summary> 
        /// ログ追加ボタン クリック処理
        /// </summary> 
        public void AddLogButton_Click(object prm)
        {
            var jobPrm = prm as JobParamModel;

            // ViewModel 生成
            JobLogDetailViewModel vm = new JobLogDetailViewModel(new JobLogDetailModel(), jobPrm.Scenario, jobPrm.Eda);
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
            // MainViewModelに通知するための処理を追加
            JobLogViewModel.RecreateViewModel(e);
        }


        /// <summary> 
        /// ファイルを開くボタン クリック処理
        /// </summary> 
        public void FolderButton_Click(object prm)
        {
            string path = prm?.ToString();
            //　空の場合、セーブしているフォルダ名 + yyyyMMdd + 機能IDでパス作成
            if (string.IsNullOrEmpty(path))
            {
                var yyyyMMdd = DateTime.Now.ToString("yyyyMMdd");
                path = Path.Combine(_vm.TempSavePath, yyyyMMdd);
                path = Path.Combine(path, _vm.Id); 
            }

            // フォルダが存在しない場合は作成
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            if (!string.IsNullOrEmpty(path))
            {
                Process.Start("explorer.exe", path);
            }
            else
            {
                MessageBox.Show("フォルダが生成されていません。", "メッセージ", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
            }
        }

        /// <summary> 
        /// 閉じるボタン クリック処理
        /// </summary> 
        public void CloseButton_Click(object prm)
        {
            if (!(prm is null))
            {
                Window window = prm as Window;

                if (window != null)
                {
                    window.Close();
                }
            }
        }


        // ==================================
        // 　ログ監視
        // ==================================
        public async Task StartMonitoring()
        {
            var _multiFileWatcher = new MultiFileWatcher(_vm.Logs.ToList(), _vm.TempSavePath, DateTime.Parse(MainViewModel.Instance.SearchFromDate));

            // イベント ファイルコピー時
            _multiFileWatcher.ProgressChanged += (filePath, destPath, totalSize, percent) => 
            {
                var logInfo = _fw.GetAllLogInfos().Values.Where(x => x.LogFromPath == filePath).FirstOrDefault();
                // Watcher対象ということを前提
                if (logInfo != null)
                {
                    // Normalログのパターン
                    if (logInfo.IsMultiFile == false)
                    {
                        var log = _vm.Logs.ToList().Where(x => Path.Combine(x.FilePath, x.DisplayFileName) == filePath).FirstOrDefault();
                        if (log != null)
                        {
                            // 画面更新
                            UpdateUIToWatcher(filePath, destPath, totalSize, percent, log);
                        }
                    }
                    // 複数あるファイル
                    else
                    {
                        // 画面にあることを確認
                        var log = _vm.Logs.ToList().Where(x => Path.Combine(x.FilePath, x.DisplayFileName) == filePath).FirstOrDefault();

                        // 画面にある場合、値を更新
                        if (log != null)
                        {
                            // ダウンロード完了したら、行数カウント
                            if (UpdateUIToWatcher(filePath, destPath, totalSize, percent, log) == emObserverStatus.SUCCESS)
                            {
                                log.LineCount = GetLineCount(destPath, log).ToString() + " 件";
                            }
                        }
                        // 画面にない場合
                        else
                        {
                            // 正規表現で日付部分とその後のアンダーバーを取り除く
                            var fileName = Regex.Replace(Path.GetFileName(filePath), @"^\d{14}_?", "");
                            var multiLog = _vm.Logs.ToList().Where(x => x.FileName == fileName && x.DisplayFileName == x.FileName).FirstOrDefault();

                            // 元となるファイル名がある場合
                            if (multiLog != null)
                            {
                                // その項目を更新する
                                if (UpdateUIToWatcher(filePath, destPath, totalSize, percent, multiLog) == emObserverStatus.SUCCESS)
                                {
                                    multiLog.LineCount = GetLineCount(destPath, log).ToString() + " 件";
                                }
                            }
                            else
                            {

                                var newLogInfo = _vm.Logs.ToList().Where(x => x.FileName == fileName).FirstOrDefault();

                                if (newLogInfo != null)
                                {
                                    // 新規で項目を追加する
                                    var newLog = new JobLogItemViewModel
                                    {
                                        Scenario = newLogInfo.Scenario,
                                        Eda = newLogInfo.Eda,
                                        FilePath = filePath,
                                        FileName = Path.GetFileName(filePath),
                                        DisplayFileName = Path.GetFileName(filePath),
                                        FileType = newLogInfo.FileType,
                                        FileCount = newLogInfo.FileCount,
                                        ObserverType = newLogInfo.ObserverType,
                                        Size = totalSize.ToString("N0") + " KB",
                                        UpdateDate = File.GetLastWriteTime(filePath).ToString("yyyy/MM/dd HH:mm:ss"),
                                        CopyPercent = percent.ToString() + " %",
                                        ObserverStatus = percent >= 100 ? emObserverStatus.SUCCESS : emObserverStatus.OBSERVER,
                                    };
                                    // ダウンロード完了してたら、行数カウント
                                    if (newLog.ObserverStatus == emObserverStatus.SUCCESS)
                                    {
                                        newLog.LineCount = GetLineCount(destPath, newLog).ToString() + " 件";
                                    }

                                    // Logsに追加
                                    var updateLogList = _vm.Logs.ToList();
                                    updateLogList.Add(newLog);
                                    _vm.Logs = new ObservableCollection<JobLogItemViewModel>(updateLogList);

                                    // 画面にあって、multiWatcherにないものを削除する
                                    RemoveUINotInWatcher();
                                }
                            }
                        }
                    }
                }

                //bool isMultiLog = false;

                //_vm.ToCopyFolderPath = Path.GetDirectoryName(destPath);

                //// 進行状況を画面に表示するコード
                //var log = _vm.Logs.ToList().Where(x => Path.Combine(x.FilePath, x.DisplayFileName) == filePath).FirstOrDefault();

                //// csv, tsvの場合は、先頭の日付を抜いて検索
                //if (log is null)
                //{
                //    // 正規表現で日付部分とその後のアンダーバーを取り除く
                //    var fileName = Regex.Replace(Path.GetFileName(filePath), @"^\d{14}_?", "");
                //    // 初回のみ、ここであたる
                //    log = _vm.Logs.ToList().Where(x => x.FileName == fileName && x.DisplayFileName == x.FileName).FirstOrDefault();
                //    // マルチログ対応
                //    isMultiLog = true;
                //}

                //// マルチログ + logがNullの場合、新規でlogに追加
                //if (isMultiLog && log is null)
                //{
                //    // ファイル名が類似のものを参照して、新しくLogに追加
                //    var fileName = Regex.Replace(Path.GetFileName(filePath), @"^\d{14}_?", "");
                //    log = _vm.Logs.ToList().Where(x => x.FileName == fileName).FirstOrDefault();

                //    var newLog = new JobLogItemViewModel
                //    {
                //        Scenario = log.Scenario,
                //        Eda = log.Eda,
                //        FilePath = log.FilePath,
                //        FileName = log.FileName,
                //        DisplayFileName = Path.GetFileName(filePath),
                //        FileType = log.FileType,
                //        FileCount = log.FileCount,
                //        ObserverType = log.ObserverType,
                //        Size = totalSize.ToString("N0") + " KB",
                //        UpdateDate = File.GetLastWriteTime(filePath).ToString("yyyy/MM/dd HH:mm:ss"),
                //        CopyPercent = percent.ToString() + " %",
                //        ObserverStatus = percent >= 100 ? emObserverStatus.SUCCESS : emObserverStatus.OBSERVER,
                //    };

                //    // 既にダウンロードが完了している場合、件数取得
                //    if (newLog.ObserverStatus == emObserverStatus.SUCCESS)
                //    {
                //        newLog.LineCount = GetLineCount(destPath, log).ToString() + " 件";
                //    }

                //    var logList = _vm.Logs.ToList();
                //    logList.Add(newLog);

                //    _vm.Logs = new ObservableCollection<JobLogItemViewModel>(logList);
                //}
                //// logがある場合、画面値 更新
                //else if (log != null)
                //{
                //    log.DisplayFileName = Path.GetFileName(filePath);
                //    log.Size = totalSize.ToString("N0") + " KB";
                //    log.CopyPercent = percent.ToString() + " %";
                //    log.UpdateDate = File.GetLastWriteTime(filePath).ToString("yyyy/MM/dd HH:mm:ss");

                //    if (percent >= 100)
                //    {
                //        log.ObserverStatus = emObserverStatus.SUCCESS;

                //        // 100% 取り込めたら Recv, Sendファイルの場合、行数カウント
                //        if (log.FileType == emFileType.RECEIVE || log.FileType == emFileType.SEND)
                //        {
                //            log.LineCount = GetLineCount(destPath, log).ToString() + " 件";
                //        }
                //    }
                //    else
                //    {
                //        log.ObserverStatus = emObserverStatus.OBSERVER;
                //    }
                //}
            };

            // 監視開始
            await _multiFileWatcher.StartMonitoring();
        }
        // Watcherを元に画面要素を更新
        private emObserverStatus UpdateUIToWatcher(string filePath, string destPath, int totalSize, int percent, JobLogItemViewModel log)
        {

            // ダウンロード率によって、状態を変化させる
            if (percent < 100)
            {
                log.ObserverStatus = emObserverStatus.OBSERVER;
            }
            else
            {
                log.ObserverStatus = emObserverStatus.SUCCESS;

            }

            log.DisplayFileName = Path.GetFileName(filePath);
            log.Size = totalSize.ToString("N0") + " KB";
            log.CopyPercent = percent.ToString() + " %";
            log.UpdateDate = File.GetLastWriteTime(filePath).ToString("yyyy/MM/dd HH:mm:ss");

            return log.ObserverStatus;
        }
        // Watcherになくて、画面にあるものを削除
        private void RemoveUINotInWatcher()
        {
            var watchers = _fw.GetAllMultiWatchers();
            var watcherKeys = watchers.Keys.ToHashSet();

            // 削除対象のログを抽出
            var logsToRemove = _vm.Logs
                .Where(x => (x.FileType == emFileType.RECEIVE || x.FileType == emFileType.SEND))
                .Where(x =>
                {
                    var fullPath = Path.Combine(x.FilePath, x.DisplayFileName);
                    return watcherKeys.Contains(fullPath);
                })
                .ToList();

            // ログの削除
            foreach (var delLog in logsToRemove)
            {
                var deleteLogList = _vm.Logs.ToList();
                deleteLogList.Remove(delLog);
                _vm.Logs = new ObservableCollection<JobLogItemViewModel>(deleteLogList);
            }
        }

        private int GetLineCount(string filePath, JobLogItemViewModel log)
        {
            int lineCount = 0;

            // 一時ファイルにコピーして行数をカウント
            string tempFilePath = Path.GetTempFileName();
            File.Copy(filePath, tempFilePath, true);

            // 行数をカウント
            lineCount = File.ReadLines(tempFilePath).Count();

            // 一時ファイルを削除
            File.Delete(tempFilePath);

            return lineCount;
        }
    }
}
