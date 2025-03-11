using JobManagementApp.BaseClass;
using JobManagementApp.Helpers;
using JobManagementApp.Manager;
using JobManagementApp.Models;
using JobManagementApp.ViewModels;
using JobManagementApp.Views;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace JobManagementApp.Commands
{
    class JobLogCommand : JobCommandArgument
    {
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
            _vm.TempSavePath = manager.GetUserFilePath(manager.CacheKey_FilePath);

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

                    var item = new JobLogItemViewModel{
                        Scenario = job.SCENARIO,
                        Eda = job.EDA,
                        FilePath = job.FILEPATH,
                        FileName = job.FILENAME,
                        DisplayFileName = job.FILENAME,
                        FileType = fileType,
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




        // ======================================
        // 以下、別ファイル予定
        // ======================================
        // 関連ファイルの監視を開始
        public void StartMonitoring()
        {
            foreach (var log in _vm.Logs)
            {
                // タイプによって、検索方法を変える
                if (log.FileType == emFileType.LOG)
                {
                    MonitorFixedFile(log);
                }
                else
                {
                    // ファイル名の前に日付項目など、複数件ある場合
                    MonitorDynamicFile(log);
                }
            }
        }

        // ファイル名が固定の場合
        private void MonitorFixedFile(JobLogItemViewModel log)
        {
            try
            {
                // 監視条件（変更日付、サイズ）
                var watcher = new FileSystemWatcher
                {
                    Path = log.FilePath,
                    Filter = log.FileName,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
                };

                watcher.Changed += (sender, e) => OnFixedFileChanged(log);
                watcher.EnableRaisingEvents = true;
                watchers[log.FileName] = watcher;

                // 初期状態でファイル情報を取得
                UpdateFileInfo(log, Path.Combine(log.FilePath, log.FileName));

            }
            catch (Exception)
            {
                MessageBox.Show($"{Path.Combine(log.FilePath, log.FileName)} にアクセス出来ませんでした。", "メッセージ", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
                //throw;
            }
        }

        private void OnFixedFileChanged(JobLogItemViewModel log)
        {
            UpdateFileInfo(log, Path.Combine(log.FilePath, log.FileName));
        }

        // ファイル名が動的の場合
        private void MonitorDynamicFile(JobLogItemViewModel log)
        {
            try
            {
                // 監視条件（ファイル名、変更日付、サイズ）
                var watcher = new FileSystemWatcher
                {
                    Path = log.FilePath,
                    Filter = $"*{log.FileName}",
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
                };

                watcher.Created += (sender, e) => OnDynamicFileChanged(log);
                watcher.Changed += (sender, e) => OnDynamicFileChanged(log);
                watcher.EnableRaisingEvents = true;
                watchers[log.FileName] = watcher;

                // 初期状態で最新のファイルを取得
                AddLatestFile(log);
            }
            catch (Exception)
            {
                MessageBox.Show($"{Path.Combine(log.FilePath, log.FileName)} にアクセス出来ませんでした。", "メッセージ", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
                //throw;
            }
        }

        private void OnDynamicFileChanged(JobLogItemViewModel log)
        {
            AddLatestFile(log);
        }

        private void AddLatestFile(JobLogItemViewModel log)
        {
            var latestFile = Directory.GetFiles(log.FilePath, $"*{log.FileName}")
                                      .Select(f => new FileInfo(f))
                                      .OrderByDescending(f => f.LastWriteTime)
                                      .FirstOrDefault();

            if (latestFile != null)
            {
                UpdateFileInfo(log, latestFile.FullName);
            }
        }

        private void UpdateFileInfo(JobLogItemViewModel log, string fullFileName)
        {
            var fileInfo = new FileInfo(fullFileName);
            log.DisplayFileName = fileInfo.Name;
            log.UpdateDate = fileInfo.LastWriteTime.ToString("yyyy/MM/dd HH:mm:ss");
            log.Size = $"{(int)Math.Round(fileInfo.Length / 1024.0)} KB";
            log.ObserverStatus = emObserverStatus.OBSERVER; // 監視中
            log.CopyPercent = "0 %";
            MonitorFile(log);
        }

        private async void MonitorFile(JobLogItemViewModel log)
        {
            try
            {
                while (true)
                {
                    var fileInfo = new FileInfo(Path.Combine(log.FilePath, log.DisplayFileName));
                    log.UpdateDate = fileInfo.LastWriteTime.ToString("yyyy/MM/dd HH:mm:ss");
                    log.Size = $"{(int)Math.Round(fileInfo.Length / 1024.0)} KB";

                    // 監視完了
                    if (log.ObserverStatus == emObserverStatus.SUCCESS) break;

                    // 3秒待機
                    await Task.Delay(3000);

                    var newFileInfo = new FileInfo(Path.Combine(log.FilePath, log.DisplayFileName));
                    if (fileInfo.LastWriteTime == newFileInfo.LastWriteTime && fileInfo.Length == newFileInfo.Length)
                    {

                        // コピー先フォルダパスがセットされていない場合、パス作成
                        if (string.IsNullOrEmpty(_vm.ToCopyFolderPath))
                        {
                            string ToCopyPath = Path.Combine(_vm.TempSavePath, DateTime.Now.ToString("yyyyMMdd"));
                            _vm.ToCopyFolderPath = Path.Combine(ToCopyPath, _vm.Id);
                        }

                        // コピー先フォルダが存在しない場合、フォルダを作成する
                        if (!Directory.Exists(_vm.ToCopyFolderPath))
                        {
                            Directory.CreateDirectory(_vm.ToCopyFolderPath);
                        }

                        // コピー元ファイル
                        string sourceFilePath = $@"{Path.Combine(log.FilePath, log.DisplayFileName)}";
                        string destinationFilePath = $@"{Path.Combine(_vm.ToCopyFolderPath, log.DisplayFileName)}";


                        if (ShouldCopyFile(sourceFilePath, destinationFilePath))
                        {
                            // コピー実施する場合、パーセント表示する
                            await CopyFileWithProgress(sourceFilePath, destinationFilePath, log).ContinueWith(async x =>  {

                                // ログファイル以外カウント
                                if (log.FileType != emFileType.LOG)
                                {
                                    log.LineCount = GetLineCount(destinationFilePath, log).ToString() + " 件";
                                }
                            });
                            File.SetLastWriteTime(destinationFilePath, File.GetLastWriteTime(sourceFilePath));
                        }
                        else
                        {
                            // 同じ場合、100% 固定
                            log.CopyPercent = "100 %";
                            log.ObserverStatus = emObserverStatus.SUCCESS;
                            // ログファイル以外カウント
                            if(log.FileType != emFileType.LOG)
                            {
                                log.LineCount = GetLineCount(destinationFilePath, log).ToString() + " 件";
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                throw;
            }
        }

        public bool ShouldCopyFile(string sourceFilePath, string destinationFilePath)
        {
            if (!File.Exists(destinationFilePath))
            {
                return true;
            }

            FileInfo sourceFileInfo = new FileInfo(sourceFilePath);
            FileInfo destinationFileInfo = new FileInfo(destinationFilePath);

            return sourceFileInfo.Length != destinationFileInfo.Length ||
                   sourceFileInfo.LastWriteTime != destinationFileInfo.LastWriteTime;
        }

        public async Task CopyFileWithProgress(string sourceFilePath, string destinationFilePath, JobLogItemViewModel log)
        {
            const int bufferSize = 1024 * 1024; // 1MBのバッファサイズ
            byte[] buffer = new byte[bufferSize];
            long totalBytes = new FileInfo(sourceFilePath).Length;
            long totalBytesCopied = 0;

            var semaphore = fileSemaphores.GetOrAdd(sourceFilePath, new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();

            try
            {
                using (FileStream sourceStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (FileStream destinationStream = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    int bytesRead;
                    while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, bufferSize)) > 0)
                    {
                        await destinationStream.WriteAsync(buffer, 0, bytesRead);
                        totalBytesCopied += bytesRead;
                        log.CopyPercent = $"{totalBytesCopied * 100 / totalBytes} %";

                        // 100% になれば、「取得済」にする
                        if ($"{totalBytesCopied * 100 / totalBytes}" == "100")
                        {


                            log.ObserverStatus = emObserverStatus.SUCCESS;
                        }
                    }
                }

            }
            finally
            {
                semaphore.Release();
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
