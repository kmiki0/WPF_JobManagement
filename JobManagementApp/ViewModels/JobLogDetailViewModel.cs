using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Windows.Input;
using JobManagementApp.Commands;
using JobManagementApp.Models;
using JobManagementApp.Services;
using System.Windows;
using System.ComponentModel;
using System.Data;

namespace JobManagementApp.ViewModels
{
    public class JobLogDetailViewModel : INotifyPropertyChanged
    {
        // イベント処理
        private readonly JobLogDetailCommand _jobLogDetailCommand;

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
        // 監視タイプ（自動・手動）
        public bool ObserverType { get; set; }

        // 登録ボタン
        public ICommand UpdateCommand { get; set; }
        // 削除ボタン
        public ICommand DeleteCommand { get; set; }
        // 閉じるボタン
        public ICommand CloseCommand { get; set; }


        // 新規作成の場合
        public JobLogDetailViewModel(string scenario, string eda)
        {
            this.Scenario = scenario;
            this.Eda = eda;

            // ジョブID 読み込み
            this.JobId = GetJobId();

            // 初期値セット
            this.ObserverType = true;

            _jobLogDetailCommand = new JobLogDetailCommand();
            UpdateCommand = new RelayCommand(UpdateButtonCommand);
            CloseCommand = new RelayCommand(CloseButtonCommand);
            DeleteCommand = new RelayCommand(DeleteButtonCommand);
        }

        // 編集の場合
        public JobLogDetailViewModel(string scenario, string eda, string fileName, string filePath)
        {
            this.Scenario = scenario;
            this.Eda = eda;
            this.FileName = fileName;
            this.FilePath = filePath;

            // データ取得して画面に表示
            SetJobLogDetailViewModel();

            _jobLogDetailCommand = new JobLogDetailCommand();
            UpdateCommand = new RelayCommand(UpdateButtonCommand);
            CloseCommand = new RelayCommand(CloseButtonCommand);
            DeleteCommand = new RelayCommand(DeleteButtonCommand);
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


        //Key項目からデータ取得
        public void SetJobLogDetailViewModel()
        {
            //ジョブログファイルの取得
            DataTable dt = JobService.GetJobLinkFile(Scenario, Eda, FileName, FilePath);

            if (dt.Rows.Count > 0)
            {
                this.Scenario = dt.Rows[0]["SCENARIO"].ToString();
                this.Eda = dt.Rows[0]["EDA"].ToString();
                this.JobId = dt.Rows[0]["JOBID"].ToString();
                this.FileName = dt.Rows[0]["FILENAME"].ToString();
                this.OldFileName = dt.Rows[0]["FILENAME"].ToString();
                this.FilePath = dt.Rows[0]["FILEPATH"].ToString();
                this.OldFilePath = dt.Rows[0]["FILEPATH"].ToString();
                this.SelectedFileType = (emFileType)int.Parse(dt.Rows[0]["FILETYPE"].ToString());
                this.ObserverType = int.Parse(dt.Rows[0]["OBSERVERTYPE"].ToString()) != 0;
            }
        }

        /// <summary> 
        /// 登録ボタン　押下イベント
        /// </summary> 
        private void UpdateButtonCommand(object parameter)
        {
            _jobLogDetailCommand.UpdateButtonCommand(SetJobLinkFileFromVm());
            CloseButtonCommand(null);
        }

        /// <summary> 
        /// 閉じるボタン　押下イベント
        /// </summary> 
        private void CloseButtonCommand(object parameter)
        {
            RequestClose?.Invoke(this, new JobParamModel{ 
                Scenario = this.Scenario,
                Eda = this.Eda,
            });

            _jobLogDetailCommand.CloseButtonCommand(window);
        }

        // 削除ボタン
        private void DeleteButtonCommand(object parameter)
        {
            _jobLogDetailCommand.DeleteButtonCommand(SetJobLinkFileFromVm());
            CloseButtonCommand(null);
        }

        // JobLinkFile に VM の値をセット
        private JobLinkFile SetJobLinkFileFromVm()
        {
            return new JobLinkFile
            {
                SCENARIO = this.Scenario,
                EDA = this.Eda,
                FILENAME = this.FileName,
                OLDFILENAME = this.OldFileName,
                FILEPATH = this.FilePath,
                OLDFILEPATH = this.OldFilePath,
                FILETYPE = (int)this.SelectedFileType,
                OBSERVERTYPE = this.ObserverType ? 1 : 0,
            };
        }

        // Vm変更するとき
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
