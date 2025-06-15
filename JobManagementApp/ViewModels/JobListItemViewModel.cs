using JobManagementApp.Commands;
using JobManagementApp.Models;
using System;
using System.ComponentModel;
using System.Windows.Input;

namespace JobManagementApp.ViewModels
{
    public class JobLogItemViewModel : INotifyPropertyChanged
    {
        // イベント処理
        private readonly JobLogItemCommand _command;

        // 静的プロパティ（初期設定後変更されない）
        public string Scenario { get; set; }
        public string Eda { get; set; }
        public string Id { get; set; }
        public emFileType FileType { get; set; }
        public int FileCount { get; set; }
        public string FilePath { get; set; }
        public string FileName { get; set; }
        public int ObserverType { get; set; }
        
        // 画面表示用ファイル名
        private string _displayFileName;
        public string DisplayFileName 
        {
            get => _displayFileName;
            set
            {
                if (_displayFileName != value)
                {
                    _displayFileName = value;
                    OnPropertyChanged(nameof(DisplayFileName));
                }
            }
        }

        // 更新日時
        private string _updateDate;
        public string UpdateDate 
        {
            get => _updateDate;
            set
            {
                if (_updateDate != value)
                {
                    _updateDate = value;
                    OnPropertyChanged(nameof(UpdateDate));
                }
            }
        }

        // 行数
        private string _lineCount;
        public string LineCount 
        {
            get => _lineCount;
            set
            {
                if (_lineCount != value)
                {
                    _lineCount = value;
                    OnPropertyChanged(nameof(LineCount));
                }
            }
        }

        // ファイルサイズ
        private string _size;
        public string Size 
        {
            get => _size;
            set
            {
                if (_size != value)
                {
                    _size = value;
                    OnPropertyChanged(nameof(Size));
                }
            }
        }

        // 監視ステータス
        private emObserverStatus _observerStatus;
        public emObserverStatus ObserverStatus 
        {
            get => _observerStatus;
            set
            {
                if (_observerStatus != value)
                {
                    _observerStatus = value;
                    OnPropertyChanged(nameof(ObserverStatus));
                }
            }
        }

        // ファイルコピー進行率
        private string _copyPercent;
        public string CopyPercent 
        {
            get => _copyPercent;
            set
            {
                if (_copyPercent != value)
                {
                    _copyPercent = value;
                    OnPropertyChanged(nameof(CopyPercent));
                }
            }
        }

        // コマンド
        public ICommand EditCommand { get; set; }

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public JobLogItemViewModel()
        {
            // コマンド初期化
            _command = new JobLogItemCommand(this);
            EditCommand = new RelayCommand(_command.EditButton_Click);
            
            // 初期値設定
            _displayFileName = "";
            _updateDate = "";
            _lineCount = "";
            _size = "";
            _copyPercent = "";
            _observerStatus = emObserverStatus.STOP;
        }

        // INotifyPropertyChanged実装
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}