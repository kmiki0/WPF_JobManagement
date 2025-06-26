using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using JobManagementApp.Views;
using JobManagementApp.ViewModels;
using JobManagementApp.Models;
using JobManagementApp.Manager;
using JobManagementApp.Helpers;
using JobManagementApp.BaseClass;

namespace JobManagementApp.Commands
{
    public class JobLogCommand : JobCommandArgument
    {
        private FileCopyProgress _fileCopyProgress = new FileCopyProgress();

        private readonly JobLogViewModel _vm;
        private readonly IJobLogModel _if;
        private readonly DateTime _fromDateTime;
        private readonly DateTime _toDateTime;

        public event Action<string, string, int, int> ProgressChanged;

        public JobLogCommand(JobLogViewModel VM, IJobLogModel IF)
        {
            _vm = VM;
            _if = IF;

            // MainWindowから検索範囲を取得
            try
            {
                _fromDateTime = DateTime.Parse(MainViewModel.Instance.SearchFromDate);
                _toDateTime = DateTime.Parse(MainViewModel.Instance.SearchToDate);
            }
            catch (Exception ex)
            {
                LogFile.WriteLog($"JobLogCommand: 日付解析エラー - {ex.Message}");
                _fromDateTime = DateTime.Now.Date;
                _toDateTime = DateTime.Now.AddHours(1);
            }

            Init();
        }

        /// <summary> 
        /// 初期化
        /// </summary> 
        private void Init()
        {
            // イベント設定
            _fileCopyProgress.ProgressChanged += (fileName, destPath, totalSize, progress) =>
            {
                ProgressChanged?.Invoke(fileName, destPath, totalSize, progress);
            };

            // キャッシュ読み込み
            UserFileManager manager = new UserFileManager();
            _vm.TempSavePath = manager.GetCache(manager.CacheKey_FilePath);

            // ジョブID 読み込み
            LoadJobId();

            // MainWindowから検索範囲を取得
            _vm.UpdateSearchDateDisplay();

            // ログ一覧 読み込み + ダウンロード実行
            LoadLogListAndDownload();
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
        /// ログ一覧読み込み + 一回だけのダウンロード実行
        /// </summary>
        private void LoadLogListAndDownload()
        {
            var logList = new List<JobLogItemViewModel>();

            _if.GetJobLinkFile(_vm.Scenario, _vm.Eda).ContinueWith(async x =>
            {
                try
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
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _vm.Logs = new ObservableCollection<JobLogItemViewModel>(logList);
                    });

                    // 一回だけダウンロード実行
                    await StartInitialDownload();

                    // ダウンロード完了後にサマリー更新
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _vm.UpdateDownloadSummary();
                    });
                }
                catch (Exception ex)
                {
                    ErrLogFile.WriteLog($"LoadLogListAndDownload エラー: {ex.Message}");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _vm.DownloadSummary = "❌ ログリストの読み込みに失敗しました";
                    });
                }
            });
        }

        /// <summary>
        /// 一回だけのダウンロード実行
        /// </summary>
        public async Task StartInitialDownload()
        {
            try
            {
                if (_vm.Logs == null || _vm.Logs.Count == 0)
                {
                    LogFile.WriteLog("StartInitialDownload: ダウンロード対象がありません");
                    return;
                }

                var downloadTasks = new List<Task>();

                // 非同期処理でファイルダウンロード
                foreach (JobLogItemViewModel log in _vm.Logs)
                {
                    var logInfo = new LogInfo
                    {
                        JobId = log.Id,
                        LogFromPath = Path.Combine(log.FilePath, log.FileName),
                        FileCount = log.FileCount,
                        IsMultiFile = log.FileType != emFileType.LOG
                    };

                    if (log.FileType == emFileType.LOG)
                    {
                        // 通常ファイル（単一）
                        downloadTasks.Add(DownloadSingleFile(logInfo));
                    }
                    else
                    {
                        // 複数ファイル
                        downloadTasks.Add(DownloadMultipleFiles(logInfo));
                    }
                }

                // 全てのダウンロードタスクを並列実行
                await Task.WhenAll(downloadTasks);
                
                LogFile.WriteLog($"StartInitialDownload: {downloadTasks.Count}個のダウンロードタスクが完了しました");
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"StartInitialDownload エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 単一ファイルのダウンロード
        /// </summary>
        private async Task DownloadSingleFile(LogInfo logInfo)
        {
            try
            {
                // ファイル存在チェック
                if (!File.Exists(logInfo.LogFromPath))
                {
                    await UpdateLogItemStatus(logInfo, emObserverStatus.ERROR, "ファイルが見つかりません");
                    return;
                }

                await HandleFileCopy(logInfo);
                await UpdateLogItemStatus(logInfo, emObserverStatus.SUCCESS, "ダウンロード完了");
            }
            catch (UnauthorizedAccessException ex)
            {
                await UpdateLogItemStatus(logInfo, emObserverStatus.ERROR, "アクセス権限がありません");
                ErrLogFile.WriteLog($"DownloadSingleFile アクセス権限エラー: {ex.Message}");
            }
            catch (DirectoryNotFoundException ex)
            {
                await UpdateLogItemStatus(logInfo, emObserverStatus.ERROR, "フォルダが見つかりません");
                ErrLogFile.WriteLog($"DownloadSingleFile フォルダエラー: {ex.Message}");
            }
            catch (IOException ex)
            {
                await UpdateLogItemStatus(logInfo, emObserverStatus.ERROR, "ファイルアクセスエラー");
                ErrLogFile.WriteLog($"DownloadSingleFile I/Oエラー: {ex.Message}");
            }
            catch (Exception ex)
            {
                await UpdateLogItemStatus(logInfo, emObserverStatus.ERROR, "予期しないエラー");
                ErrLogFile.WriteLog($"DownloadSingleFile エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 複数ファイルのダウンロード
        /// </summary>
        private async Task DownloadMultipleFiles(LogInfo logInfo)
        {
            try
            {
                // ディレクトリ存在チェック
                var directory = Path.GetDirectoryName(logInfo.LogFromPath);
                if (!Directory.Exists(directory))
                {
                    await UpdateLogItemStatus(logInfo, emObserverStatus.ERROR, "対象フォルダが見つかりません");
                    return;
                }

                List<FileInfo> existingFiles = GetLatestFiles(logInfo.LogFromPath, logInfo.FileCount);
                
                if (existingFiles.Count == 0)
                {
                    await UpdateLogItemStatus(logInfo, emObserverStatus.ERROR, "対象ファイルが見つかりません");
                    return;
                }

                // 並列処理用のタスクリスト
                var copyTasks = new List<Task>();
                var successCount = 0;
                var errorCount = 0;
                
                foreach (FileInfo file in existingFiles)
                {
                    var fileLogInfo = new LogInfo
                    {
                        JobId = logInfo.JobId,
                        LogFromPath = file.FullName,
                        FileCount = logInfo.FileCount,
                        IsMultiFile = true
                    };
                    
                    copyTasks.Add(HandleFileCopyWithResult(fileLogInfo).ContinueWith(task =>
                    {
                        if (task.Result)
                            Interlocked.Increment(ref successCount);
                        else
                            Interlocked.Increment(ref errorCount);
                    }));
                }
                
                await Task.WhenAll(copyTasks);
                
                // 結果に応じてステータス更新
                if (errorCount == 0)
                {
                    await UpdateLogItemStatus(logInfo, emObserverStatus.SUCCESS, $"{successCount}ファイル ダウンロード完了");
                }
                else if (successCount > 0)
                {
                    await UpdateLogItemStatus(logInfo, emObserverStatus.ERROR, $"{successCount}成功, {errorCount}エラー");
                }
                else
                {
                    await UpdateLogItemStatus(logInfo, emObserverStatus.ERROR, "全ファイルのダウンロードに失敗");
                }
            }
            catch (Exception ex)
            {
                await UpdateLogItemStatus(logInfo, emObserverStatus.ERROR, "予期しないエラー");
                ErrLogFile.WriteLog($"DownloadMultipleFiles エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// ファイルコピーとエラーハンドリング
        /// </summary>
        private async Task<bool> HandleFileCopyWithResult(LogInfo info)
        {
            try
            {
                await HandleFileCopy(info);
                return true;
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"HandleFileCopyWithResult エラー: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 指定した件数分、最新のファイル情報を取得
        /// </summary>
        public List<FileInfo> GetLatestFiles(string fullPath, int fileCount)
        {
            try
            {
                var directory = new DirectoryInfo(Path.GetDirectoryName(fullPath));
                var files = directory.GetFiles()
                    .Where(t => t.LastWriteTime >= _fromDateTime && 
                            t.LastWriteTime <= _toDateTime)
                    .Where(f => f.Name.Contains(Path.GetFileName(fullPath)))
                    .OrderByDescending(f => f.LastWriteTime)
                    .Take(fileCount)
                    .ToList();
                
                return files;
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"GetLatestFiles エラー: {ex.Message}");
                return new List<FileInfo>();
            }
        }

        /// <summary>
        /// ファイルコピー処理
        /// </summary>
        private async Task HandleFileCopy(LogInfo info)
        {
            try
            {
                // コピー先フォルダパスがセットされていない場合、カレントディレクトリをセット
                if (string.IsNullOrEmpty(_vm.TempSavePath))
                {
                    _vm.TempSavePath = AppDomain.CurrentDomain.BaseDirectory;
                }

                // 日付のコピーフォルダパス 作成
                string todayCopyPath = Path.Combine(_vm.TempSavePath, DateTime.Now.ToString("yyyyMMdd"));
                string copyPath = Path.Combine(todayCopyPath, info.JobId);

                // コピー先フォルダが存在しない場合、フォルダ 作成
                if (!Directory.Exists(copyPath)) Directory.CreateDirectory(copyPath);

                // コピー元ファイル
                string fromFilePath = info.LogFromPath;
                string toFilePath = Path.Combine(copyPath, Path.GetFileName(info.LogFromPath));

                // コピー実施
                await _fileCopyProgress.CopyFile(fromFilePath, toFilePath);

                // コピー先フォルダパスを設定
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _vm.ToCopyFolderPath = Path.GetDirectoryName(toFilePath);
                });
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"HandleFileCopy エラー: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// ログアイテムのステータス更新
        /// </summary>
        private async Task UpdateLogItemStatus(LogInfo logInfo, emObserverStatus status, string message)
        {
            await Task.Run(() =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var logItem = _vm.Logs.FirstOrDefault(x => 
                        x.FileName == Path.GetFileName(logInfo.LogFromPath) &&
                        x.Id == logInfo.JobId);
                    
                    if (logItem != null)
                    {
                        logItem.ObserverStatus = status;
                        logItem.ErrorMessage = message;
                        
                        // ステータスに応じた表示更新
                        switch (status)
                        {
                            case emObserverStatus.SUCCESS:
                                logItem.CopyPercent = "100 %";
                                if (File.Exists(logInfo.LogFromPath))
                                {
                                    var fileInfo = new FileInfo(logInfo.LogFromPath);
                                    logItem.Size = (fileInfo.Length / 1024).ToString("N0") + " KB";
                                    logItem.UpdateDate = fileInfo.LastWriteTime.ToString("yyyy/MM/dd HH:mm:ss");
                                }
                                break;
                            case emObserverStatus.ERROR:
                                logItem.CopyPercent = "エラー";
                                logItem.Size = "-";
                                logItem.LineCount = message;
                                break;
                        }
                    }
                });
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
                System.Diagnostics.Process.Start("explorer.exe", path);
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
    }
}