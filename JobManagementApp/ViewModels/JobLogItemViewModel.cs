using JobManagementApp.Commands;
using JobManagementApp.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace JobManagementApp.ViewModels
{
    public class JobLogItemViewModel : INotifyPropertyChanged
    {
        // イベント処理
        private readonly JobLogItemCommand _command;

        // シナリオ
        public string Scenario { get; set; }
        // 枝番
        public string Eda { get; set; }
         // ファイルタイプ
        public emFileType FileType { get; set; }
        // ファイルパス
        public string FilePath { get; set; }
        // ファイル名
        public string FileName { get; set; }
        // 画面表示用　ファイル名
        private string _displayFileName { get; set; }
        public string DisplayFileName {
            get => _displayFileName;
            set
            {
                _displayFileName = value;
                OnPropertyChanged(nameof(DisplayFileName));
            }
        }
        // 更新日時
        private string _updateDate { get; set; }
        public string UpdateDate {
            get => _updateDate;
            set
            {
                _updateDate = value;
                OnPropertyChanged(nameof(UpdateDate));
            }
        }
        // サイズ
        private string _size { get; set; }
        public string Size {
            get => _size;
            set
            {
                _size = value;
                OnPropertyChanged(nameof(Size));
            }
        }
        
        // 監視ステータス
        private emObserverStatus _observerStatus { get; set; }
        public emObserverStatus ObserverStatus {
            get => _observerStatus;
            set
            {
                _observerStatus = value;
                OnPropertyChanged(nameof(ObserverStatus));
            }
        }

        // ファイルコピーパーセント
        private string _copyPercent { get; set; }
        public string CopyPercent {
            get => _copyPercent;
            set
            {
                _copyPercent = value;
                OnPropertyChanged(nameof(CopyPercent));
            }
        }

        // 編集ボタン
        public ICommand EditCommand { get; set; }

        public JobLogItemViewModel()
        {
            // コマンド 初期化
            _command = new JobLogItemCommand(this);
            EditCommand = new RelayCommand(_command.EditButton_Click);
        }

        // vm変更するとき
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
