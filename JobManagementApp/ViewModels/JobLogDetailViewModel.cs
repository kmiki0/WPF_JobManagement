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
    }
}