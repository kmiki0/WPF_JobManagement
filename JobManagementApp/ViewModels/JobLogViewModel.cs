using JobManagementApp.Commands;
using JobManagementApp.Manager;
using JobManagementApp.Models;
using JobManagementApp.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace JobManagementApp.ViewModels
{
    public class JobLogViewModel : INotifyPropertyChanged
    {
        public Window window { get; set; }

        // イベント処理
        private readonly JobLogCommand _jobLogCommand;

        // =======================================
        // 画面項目
        // =======================================
        // シナリオ
        public string Scenario { get; set; }
        // 枝番
        public string Eda { get; set; }
        // ジョブID
        public string Id { get; set; }
        // コピー先フォルダパス
        private string _toCopyFolderPath { get; set; }
        public string ToCopyFolderPath {
            get => _toCopyFolderPath;
            set
            {
                _toCopyFolderPath = value;
                OnPropertyChanged(nameof(ToCopyFolderPath));
            }
        }
        // 一時保存フォルダパス
        private string _tempSavePath { get; set; }
        public string TempSavePath {
            get => _tempSavePath;
            set
            {
                _tempSavePath = value;
                OnPropertyChanged(nameof(TempSavePath));
            }
        }

        private ObservableCollection<JobLogItemViewModel> _logs { get; set; }
        public ObservableCollection<JobLogItemViewModel> Logs {
            get => _logs;
            set
            {
                _logs = value;
                OnPropertyChanged(nameof(Logs));
            }
        }

        // 一時保存フォルダ　更新ボタン
        public ICommand TempFolderUpdateCommand { get; set; }
        // フォルダボタン
        public ICommand FolderCommand { get; set; }
        // 閉じるボタン
        public ICommand CloseCommand { get; set; }
        // ジョブ追加ボタン
        public ICommand AddLogCommand { get; set; }

        private static ConcurrentDictionary<string, SemaphoreSlim> fileSemaphores;

        private Dictionary<string, FileSystemWatcher> watchers;


        // シナリオと枝番に値がある場合、データ取得して画面に表示
        public JobLogViewModel(string scenario, string eda)
        {
            fileSemaphores = new ConcurrentDictionary<string, SemaphoreSlim>();
            watchers = new Dictionary<string, FileSystemWatcher>();


            this.Scenario = scenario;
            this.Eda = eda;

            this.Id = GetJobId();


            // キャッシュ読み込み
            UserFileManager manager = new UserFileManager();
            TempSavePath = manager.GetUserFilePath(manager.CacheKey_FilePath);

            // ログファイル一覧 セット
            SetLogs();


            // ボタンイベント 初期化
            _jobLogCommand = new JobLogCommand();
            TempFolderUpdateCommand = new RelayCommand(TempFolderButtonCommand);
            AddLogCommand = new RelayCommand(AddLogButtonCommand);
            FolderCommand = new RelayCommand(FolderButtonCommand);
            CloseCommand = new RelayCommand(CloseButtonCommand);

        }

        // ボタン押下　イベント
        private void AddLogButtonCommand(object parameter)
        {
            if (!string.IsNullOrEmpty(parameter?.ToString()))
            {
                _jobLogCommand.AddLogButtonCommand(parameter);
            }
        }
        private void TempFolderButtonCommand(object parameter)
        {
            if (!string.IsNullOrEmpty(parameter?.ToString()))
            {
                _jobLogCommand.TempFolderButtonCommand(parameter, filePath => TempSavePath = filePath);
            }
        }
        private void FolderButtonCommand(object parameter)
        {
            if (!string.IsNullOrEmpty(parameter?.ToString()))
            {
                _jobLogCommand.FolderButtonCommand(parameter);
            }
            else
            {
                MessageBox.Show("フォルダが生成されていません。");
            }
        }
        private void CloseButtonCommand(object parameter)
        {
            if (!(parameter is null))
            {
                _jobLogCommand.CloseButtonCommand(parameter);
            }
        }

        // シナリオと枝番から、ジョブIDを取得
        private string GetJobId()
        {
            string result = "";
            using (DataTable dt = JobService.GetJobManegment(Scenario, Eda))
            {
                if (dt.Rows.Count > 0)
                {
                    result = dt.Rows[0]["ID"].ToString();
                }
            }
            return result;
        }

        private async void SetLogs()
        {
            await Task.Run(() =>
            {
                SetLogs1(); 
                StartMonitoring();
            }
            );
        }

        // シナリオと枝番に紐づく、データをセット
        public async void SetLogs1()
        {
            var logList = new List<JobLogItemViewModel>();


            var dataTable = JobService.GetJobLinkFile(Scenario, Eda);

            // ジョブごとにUIを生成する
            foreach (DataRow row in dataTable.Rows)
            {
                // enum変換
                emFileType fileType = Enum.TryParse(row["FILETYPE"].ToString(), out fileType) ? fileType : emFileType.LOG;

                var item = new JobLogItemViewModel{
                    Scenario = row["SCENARIO"].ToString(),
                    Eda = row["EDA"].ToString(),
                    FilePath = row["FILEPATH"].ToString(),
                    FileName = row["FILENAME"].ToString(),
                    DisplayFileName = row["FILENAME"].ToString(),
                    FileType = fileType,
                    ObserverStatus = emObserverStatus.OBSERVER
                };

                logList.Add(item);
            }

            this.Logs = new ObservableCollection<JobLogItemViewModel>(logList);
        }


        // 関連ファイルの監視を開始
        public void StartMonitoring()
        {
            foreach (var log in this.Logs)
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
                MessageBox.Show($"{Path.Combine(log.FilePath, log.FileName)} にアクセス出来ませんでした。");
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
                MessageBox.Show($"{Path.Combine(log.FilePath, log.FileName)} にアクセス出来ませんでした。");
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
            while (true)
            {
                var fileInfo = new FileInfo(Path.Combine(log.FilePath, log.DisplayFileName));
                log.UpdateDate = fileInfo.LastWriteTime.ToString("yyyy/MM/dd HH:mm:ss");
                log.Size = $"{(int)Math.Round(fileInfo.Length / 1024.0)} KB";

                // 監視完了
                if (log.ObserverStatus == emObserverStatus.SUCCESS) break;

                // 10秒待機
                await Task.Delay(10000);

                var newFileInfo = new FileInfo(Path.Combine(log.FilePath, log.DisplayFileName));
                if (fileInfo.LastWriteTime == newFileInfo.LastWriteTime && fileInfo.Length == newFileInfo.Length)
                {

                    // コピー先フォルダパスがセットされていない場合、パス作成
                    if (string.IsNullOrEmpty(ToCopyFolderPath))
                    {
                        string ToCopyPath = Path.Combine(TempSavePath, DateTime.Now.ToString("yyyyMMdd"));
                        ToCopyFolderPath = Path.Combine(ToCopyPath, this.Id);
                    }

                    // コピー先フォルダが存在しない場合、フォルダを作成する
                    if (!Directory.Exists(ToCopyFolderPath))
                    {
                        Directory.CreateDirectory(ToCopyFolderPath);
                    }

                    // コピー元ファイル
                    string sourceFilePath = $@"{Path.Combine(log.FilePath, log.DisplayFileName)}";
                    string destinationFilePath = $@"{Path.Combine(ToCopyFolderPath, log.DisplayFileName)}";


                    if (ShouldCopyFile(sourceFilePath, destinationFilePath))
                    {
                        // コピー実施する場合、パーセント表示する
                        await CopyFileWithProgress(sourceFilePath, destinationFilePath, log);
                        File.SetLastWriteTime(destinationFilePath, File.GetLastWriteTime(sourceFilePath));
                    }
                    else
                    {
                        // 同じ場合、100% 固定
                        log.CopyPercent = "100 %";
                        log.ObserverStatus = emObserverStatus.SUCCESS;
                    }
                }
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























        // vm変更するとき
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
