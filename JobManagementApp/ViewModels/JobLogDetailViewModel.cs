using System;
using System.Windows.Input;
using JobManagementApp.Commands;
using JobManagementApp.Models;
using System.Windows;
using System.ComponentModel;
using System.Linq;
using JobManagementApp.Manager;

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
        public int[] cmbFileCount => Enumerable.Range(1, 20).ToArray(); // MAX20ファイルまでとする。
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
        // 監視タイプ（自動(0)・手動(1)）- 検証機能付き
        private int _observerType = 0; // デフォルト値を明示的に設定
        public int ObserverType
        {
            get { return _observerType; }
            set
            {
                // 入力値の検証
                if (value != 0 && value != 1)
                {
                    ErrLogFile.WriteLog($"ObserverType に無効な値が設定されました: {value}。デフォルト値(0)を使用します。");
                    _observerType = 0; // デフォルトは自動
                }
                else
                {
                    _observerType = value;
                }
                OnPropertyChanged(nameof(ObserverType));
                OnPropertyChanged(nameof(IsAutoObserver));
                OnPropertyChanged(nameof(IsManualObserver));
            }
        }

        // ラジオボタン用のboolプロパティ（より確実な方法）
        public bool IsAutoObserver
        {
            get { return _observerType == 0; }
            set 
            { 
                if (value) 
                {
                    ObserverType = 0;
                }
            }
        }

        public bool IsManualObserver
        {
            get { return _observerType == 1; }
            set 
            { 
                if (value) 
                {
                    ObserverType = 1;
                }
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

        // 新規作成の場合 - 初期化を強化
        public JobLogDetailViewModel(IJobLogDetailModel IF, string scenario, string eda)
        {
            try
            {
                // 初期値セット
                this.IsButtonEnabled = true;
                this.Scenario = scenario;
                this.Eda = eda;
                this.ObserverType = 0; // 明示的に自動を設定
                this.SelectedFileConut = cmbFileCount[0];

                // コマンド 初期化
                _command = new JobLogDetailCommand(this, IF);
                UpdateCommand = new RelayCommand(_command.UpdateButton_Click);
                CloseCommand = new RelayCommand(_command.CloseButton_Click);
                DeleteCommand = new RelayCommand(_command.DeleteButton_Click);

                // ジョブID 読み込み
                _command.GetJobId();
                
                LogFile.WriteLog($"JobLogDetailViewModel初期化完了: ObserverType={ObserverType}");
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"JobLogDetailViewModel初期化エラー: {ex.Message}");
                throw;
            }
        }

        // 編集の場合 - 初期化を強化
        public JobLogDetailViewModel(IJobLogDetailModel IF, string scenario, string eda, string fileName, string filePath, int fileCount)
        {
            try
            {
                // 初期値セット
                this.IsButtonEnabled = true;
                this.Scenario = scenario;
                this.Eda = eda;
                this.FileName = fileName;
                this.FilePath = filePath;
                this.SelectedFileConut = fileCount;
                this.ObserverType = 0; // データ取得前のデフォルト値

                // コマンド 初期化
                _command = new JobLogDetailCommand(this, IF);
                UpdateCommand = new RelayCommand(_command.UpdateButton_Click);
                CloseCommand = new RelayCommand(_command.CloseButton_Click);
                DeleteCommand = new RelayCommand(_command.DeleteButton_Click);

                // データ取得して画面に表示
                _command.SetJobLogDetailViewModel();
                
                LogFile.WriteLog($"JobLogDetailViewModel編集初期化完了: ObserverType={ObserverType}");
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"JobLogDetailViewModel編集初期化エラー: {ex.Message}");
                throw;
            }
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