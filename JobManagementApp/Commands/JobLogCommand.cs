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

namespace JobManagementApp.Commands
{
    public class JobLogCommand : JobCommandArgument
    {
        public MultiFileWatcher _multiFileWatcher;

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

            UserFileManager manager = new UserFileManager();
            var getSearchTime = manager.GetCache(manager.CacheKey_SearchTime);
            var whereTime = getSearchTime == "" ? DateTime.Now.ToString("yyyy/MM/dd ") + "00:00" : DateTime.Now.ToString("yyyy/MM/dd ") + getSearchTime;

            using (_multiFileWatcher = new MultiFileWatcher(_vm.Logs.ToList(), _vm.TempSavePath, DateTime.Parse(whereTime)))
            {
                _multiFileWatcher.ProgressChanged += (filePath, destPath, totalSize, percent) => 
                {
                    // 複数ファイル対応
                    bool isMultiLog = false;

                    _vm.ToCopyFolderPath = Path.GetDirectoryName(destPath);

                    // 進行状況を画面に表示するコード
                    var log = _vm.Logs.ToList().Where(x => Path.Combine(x.FilePath, x.DisplayFileName) == filePath).FirstOrDefault();

                    // csv, tsvの場合は、先頭の日付を抜いて検索
                    if (log is null)
                    {
                        // 正規表現で日付部分とその後のアンダーバーを取り除く
                        var fileName = Regex.Replace(Path.GetFileName(filePath), @"^\d{14}_?", "");
                        // 初回のみ、ここであたる
                        log = _vm.Logs.ToList().Where(x => x.FileName == fileName && x.DisplayFileName == x.FileName).FirstOrDefault();
                        // マルチログ対応
                        isMultiLog = true;
                    }

                    // マルチログ + logがNullの場合、新規でlogに追加
                    if (isMultiLog && log is null)
                    {
                        // ファイル名が類似のものを参照して、新しくLogに追加
                        var fileName = Regex.Replace(Path.GetFileName(filePath), @"^\d{14}_?", "");
                        log = _vm.Logs.ToList().Where(x => x.FileName == fileName).FirstOrDefault();

                        var newLog = new JobLogItemViewModel
                        {
                            Scenario = log.Scenario,
                            Eda = log.Eda,
                            FilePath = log.FilePath,
                            FileName = log.FileName,
                            DisplayFileName = Path.GetFileName(filePath),
                            FileType = log.FileType,
                            FileCount = log.FileCount,
                            ObserverType = log.ObserverType,
                            Size = totalSize.ToString("N0") + " KB",
                            UpdateDate = File.GetLastWriteTime(filePath).ToString("yyyy/MM/dd HH:mm:ss"),
                            CopyPercent = percent.ToString() + " %",
                            ObserverStatus = percent >= 100 ? emObserverStatus.SUCCESS : emObserverStatus.OBSERVER,
                        };

                        // 既にダウンロードが完了している場合、件数取得
                        if (newLog.ObserverStatus == emObserverStatus.SUCCESS)
                        {
                            newLog.LineCount = GetLineCount(destPath, log).ToString() + " 件";
                        }

                        var logList = _vm.Logs.ToList();
                        logList.Add(newLog);

                        _vm.Logs = new ObservableCollection<JobLogItemViewModel>(logList);
                    }
                    // logがある場合、画面値 更新
                    else if (log != null)
                    {
                        log.DisplayFileName = Path.GetFileName(filePath);
                        log.Size = totalSize.ToString("N0") + " KB";
                        log.CopyPercent = percent.ToString() + " %";
                        log.UpdateDate = File.GetLastWriteTime(filePath).ToString("yyyy/MM/dd HH:mm:ss");

                        if (percent >= 100)
                        {
                            log.ObserverStatus = emObserverStatus.SUCCESS;

                            // 100% 取り込めたら Recv, Sendファイルの場合、行数カウント
                            if (log.FileType == emFileType.RECEIVE || log.FileType == emFileType.SEND)
                            {
                                log.LineCount = GetLineCount(destPath, log).ToString() + " 件";
                            }
                        }
                    }
                };

                await _multiFileWatcher.StartMonitoring();
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
