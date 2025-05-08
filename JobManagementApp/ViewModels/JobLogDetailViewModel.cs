using System;
using System.Windows.Input;
using JobManagementApp.Commands;
using JobManagementApp.Models;
using System.Windows;
using System.ComponentModel;
using System.Linq;

namespace JobManagementApp.ViewModels
{
    public class JobLogDetailViewModel : INotifyPropertyChanged
    {
        // イベント処理
        private readonly JobLogDetailCommand _command;

        public event EventHandler<JobParamModel> RequestClose;

        public Window window;

        // シナリオ
        public string Scenario { get; set; }
        // 枝番
        public string Eda { get; set; }
        // ジョブID
        private string _jobId { get; set; }
        public string JobId {
            get => _jobId;
            set
            {
                _jobId = value;
                OnPropertyChanged(nameof(JobId));
            }
        }
        // ファイル名
        public string FileName { get; set; }
        // ファイル名（以前）
        public string OldFileName { get; set; }
        // ファイルパス
        public string FilePath { get; set; }
        // ファイルパス（以前）
        public string OldFilePath { get; set; }
        // ファイルタイプ
        public Array cmbFileType => Enum.GetValues(typeof(emFileType));
        private emFileType _selectedFileType;
        public emFileType SelectedFileType
        {
            get { return _selectedFileType; }
            set
            {
                _selectedFileType = value;
                OnPropertyChanged(nameof(SelectedFileType));
            }
        }
        // 同名ファイル個数
        public int[] cmbFileCount => Enumerable.Range(1, 10).ToArray(); // MAX10ファイルまでとする。
        private int _selectedFileCount;
        public int SelectedFileConut
        {
            get { return _selectedFileCount; }
            set
            {
                _selectedFileCount = value;
                OnPropertyChanged(nameof(SelectedFileConut));
            }
        }


        // 監視タイプ（自動(0)・手動(1)）
        private int _observerType { get; set; }
        public int ObserverType
        {
            get { return _observerType; }
            set
            {
                _observerType = value;
                OnPropertyChanged(nameof(ObserverType));
            }
        }

        // 登録ボタン
        public ICommand UpdateCommand { get; set; }
        // 削除ボタン
        public ICommand DeleteCommand { get; set; }
        // 閉じるボタン
        public ICommand CloseCommand { get; set; }

        // ボタン処理可能
        private bool _isButtonEnabled;
        public bool IsButtonEnabled
        {
            get { return _isButtonEnabled; }
            set
            {
                _isButtonEnabled = value;
                OnPropertyChanged(nameof(IsButtonEnabled));
            }
        }

        // 新規作成の場合
        public JobLogDetailViewModel(IJobLogDetailModel IF, string scenario, string eda)
        {
            // 初期値セット
            this.IsButtonEnabled = true;
            this.Scenario = scenario;
            this.Eda = eda;
            this.ObserverType = 0;
            this.SelectedFileConut = cmbFileCount[0];

            // コマンド 初期化
            _command = new JobLogDetailCommand(this, IF);
            UpdateCommand = new RelayCommand(_command.UpdateButton_Click);
            CloseCommand = new RelayCommand(_command.CloseButton_Click);
            DeleteCommand = new RelayCommand(_command.DeleteButton_Click);

            // ジョブID 読み込み
            _command.GetJobId();
        }

        // 編集の場合
        public JobLogDetailViewModel(IJobLogDetailModel IF, string scenario, string eda, string fileName, string filePath, int fileCount)
        {
            // 初期値セット
            this.IsButtonEnabled = true;
            this.Scenario = scenario;
            this.Eda = eda;
            this.FileName = fileName;
            this.FilePath = filePath;
            this.SelectedFileConut = fileCount;

            // コマンド 初期化
            _command = new JobLogDetailCommand(this, IF);
            UpdateCommand = new RelayCommand(_command.UpdateButton_Click);
            CloseCommand = new RelayCommand(_command.CloseButton_Click);
            DeleteCommand = new RelayCommand(_command.DeleteButton_Click);

            // データ取得して画面に表示
            _command.SetJobLogDetailViewModel();
        }

        /// <summary>
        /// 返却用 Closeイベント
        /// </summary>
        public void RequestClose_event()
        {
            // JobLogDetailの値をEventHandler<JobParamModel>型でセット
            this.RequestClose.Invoke(this, new JobParamModel
            {
                Scenario = this.Scenario,
                Eda = this.Eda,
            });
        }

        // Vm変更するとき
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
