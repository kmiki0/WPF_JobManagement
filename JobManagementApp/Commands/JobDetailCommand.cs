using JobManagementApp.BaseClass;
using JobManagementApp.Manager;
using JobManagementApp.Models;
using JobManagementApp.Services;
using JobManagementApp.ViewModels;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace JobManagementApp.Commands
{
    /// <summary>
    /// ジョブ管理の詳細画面　コマンド
    /// </summary>
    class JobDetailCommand : JobCommandArgument
    {
        private readonly JobDetailViewModel _vm;
        private readonly IJobDetailModel _if;

        public JobDetailCommand(JobDetailViewModel VM, IJobDetailModel IF)
        {
            _vm = VM ?? throw new ArgumentNullException(nameof(VM));
            _if = IF ?? throw new ArgumentNullException(nameof(IF));
        }


        /// <summary> 
        /// 画面項目 読み込み処理
        /// </summary> 
        public async void LoadViewModel()
        {
            try
            {
                // 入力検証
                if (string.IsNullOrWhiteSpace(_vm.Scenario) || string.IsNullOrWhiteSpace(_vm.Eda))
                {
                    ShowErrorMessage("シナリオと枝番が設定されていません。");
                    return;
                }

                // ローディング状態の設定
                _vm.IsButtonEnabled = false;

                var result = await _if.GetJobManegment(_vm.Scenario, _vm.Eda);

                if (result != null && !string.IsNullOrEmpty(result.SCENARIO))
                {
                    // UIスレッドで画面項目に設定
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _vm.Scenario = result.SCENARIO ?? "";
                        _vm.Eda = result.EDA ?? "";
                        _vm.Id = result.ID ?? "";
                        _vm.Name = result.NAME ?? "";
                        _vm.SelectedExecution = (emExecution)result.EXECUTION;
                        _vm.ExecCommnad = result.EXECCOMMNAD ?? "";
                        _vm.SelectedStatus = (emStatus)result.STATUS;
                        _vm.BeforeJob = result.BEFOREJOB ?? "";
                        _vm.JobBoolean = result.JOBBOOLEAN != 0;
                        _vm.Receive = result.RECEIVE ?? "";
                        _vm.Send = result.SEND ?? "";
                        _vm.Memo = (result.MEMO ?? "").Replace("\\n", Environment.NewLine);

                        //  FROMSERVERの設定（DatabaseDisplayInfoから該当するものを検索）
                        var savedFromServer = result.FROMSERVER ?? "";
                        if (!string.IsNullOrEmpty(savedFromServer) && _vm.cmbFromServer != null)
                        {
                            var matchingDb = _vm.cmbFromServer.FirstOrDefault(db => db.Name == savedFromServer);
                            _vm.SelectedFromServer = matchingDb;
                        }
                    });
                }
                else
                {
                    ShowWarningMessage($"シナリオ '{_vm.Scenario}' 枝番 '{_vm.Eda}' のジョブが見つかりませんでした。");
                }
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"LoadViewModel エラー: {ex.Message}");
                ShowErrorMessage("ジョブデータの読み込みに失敗しました。ネットワーク接続とデータベース接続を確認してください。");
            }
            finally
            {
                _vm.IsButtonEnabled = true;
            }
        }

        /// <summary> 
        /// 登録ボタン クリック処理
        /// </summary> 
        public async void UpdateButton_Click(object _)
        {
            try
            {
                // ボタン処理可能か
                if (!_vm.IsButtonEnabled) return;

                // 入力検証
                var validationResult = ValidateInput();
                if (!validationResult.IsValid)
                {
                    ShowValidationErrors(validationResult.Errors);
                    return;
                }

                _vm.IsButtonEnabled = false;

                // テーブル型に格納
                var job = CreateJobFromViewModel();

                // 登録処理 実施
                var updateResult = await _if.UpdateJobManegment(job);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (updateResult)
                    {
                        ShowSuccessMessage("ジョブ管理の更新が完了しました。");
                        
                        // DetailViewModelの値をEventHandler<JobListItemViewModel>型でセット
                        _vm.RequestClose_event();

                        // 自身を閉じる
                        _vm.window?.Close();

                        LogFile.WriteLog($"ジョブ管理を正常に更新しました: {_vm.Scenario}-{_vm.Eda}");
                    }
                    else
                    {
                        ShowErrorMessage("ジョブ管理の更新に失敗しました。入力内容を確認してください。");
                    }
                });
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"UpdateButton_Click エラー: {ex.Message}");
                ShowErrorMessage("更新処理中にエラーが発生しました。システム管理者に連絡してください。");
            }
            finally
            {
                _vm.IsButtonEnabled = true;
            }
        }

        /// <summary> 
        /// 閉じるボタン クリック処理
        /// </summary> 
        public void CloseButton_Click(object _)
        {
            try
            {
                // ボタン処理可能か
                if (!_vm.IsButtonEnabled) return;

                _vm.window?.Close();
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"CloseButton_Click エラー: {ex.Message}");
                // クローズ処理のエラーは通常ユーザーに表示する必要はない
            }
        }

        /// <summary> 
        /// 削除ボタン クリック処理
        /// </summary> 
        public async void DeleteButton_Click(object _)
        {
            try
            {
                // ボタン処理可能か
                if (!_vm.IsButtonEnabled) return;

                // 削除確認
                var confirmResult = MessageBox.Show(
                    $"ジョブ '{_vm.Id}' を削除してもよろしいですか？\nこの操作は取り消すことができません。",
                    "削除確認",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning,
                    MessageBoxResult.No,
                    MessageBoxOptions.DefaultDesktopOnly);

                if (confirmResult != MessageBoxResult.Yes)
                {
                    return;
                }

                // 入力検証
                if (string.IsNullOrWhiteSpace(_vm.Scenario) || string.IsNullOrWhiteSpace(_vm.Eda))
                {
                    ShowErrorMessage("削除対象のシナリオまたは枝番が設定されていません。");
                    return;
                }

                _vm.IsButtonEnabled = false;

                // 削除処理 実施
                var deleteResult = await _if.DeleteJobManegment(_vm.Scenario, _vm.Eda);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (deleteResult)
                    {
                        ShowSuccessMessage("ジョブ管理の論理削除フラグを立てました。");
                        
                        // 自身を閉じる
                        _vm.window?.Close();

                        LogFile.WriteLog($"ジョブ管理を正常に削除しました: {_vm.Scenario}-{_vm.Eda}");
                    }
                    else
                    {
                        ShowErrorMessage("ジョブ管理の削除に失敗しました。対象のジョブが存在するか確認してください。");
                    }
                });
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"DeleteButton_Click エラー: {ex.Message}");
                ShowErrorMessage("削除処理中にエラーが発生しました。システム管理者に連絡してください。");
            }
            finally
            {
                _vm.IsButtonEnabled = true;
            }
        }

        /// <summary> 
        /// シナリオ　フォーカスアウト処理
        /// </summary> 
        public async void ScenarioTextBox_LostFocus(object _)
        {
            try
            {
                // 入力検証
                if (string.IsNullOrWhiteSpace(_vm.Scenario))
                {
                    return; // 空の場合は何もしない
                }

                var newEda = await _if.GetNewEda(_vm.Scenario);
                
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _vm.Eda = newEda.ToString();
                });

                LogFile.WriteLog($"新しい枝番を取得しました: {_vm.Scenario} -> {newEda}");
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"ScenarioTextBox_LostFocus エラー: {ex.Message}");
                ShowErrorMessage("枝番の自動設定に失敗しました。手動で入力してください。");
            }
        }

        /// <summary>
        /// データベース名一覧を読み込み
        /// </summary>
        public async void LoadDatabaseNames()
        {
            try
            {
                var databaseInfos = await _if.GetAvailableDatabaseDisplayInfos();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _vm.cmbFromServer = databaseInfos;
                    
                    // デフォルト値を設定
                    if (databaseInfos.Length > 0 && _vm.SelectedFromServer == null)
                    {
                        _vm.SelectedFromServer = databaseInfos[0];
                    }
                });
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"LoadDatabaseNames エラー: {ex.Message}");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _vm.cmbFromServer = new DatabaseDisplayInfo[0];
                });
            }
        }

        #region ヘルパーメソッド

        /// <summary>
        /// 入力検証を実行
        /// </summary>
        private ValidationResult ValidateInput()
        {
            var errors = new List<string>();

            // 必須項目チェック
            if (string.IsNullOrWhiteSpace(_vm.Scenario))
                errors.Add("シナリオは必須です。");

            if (string.IsNullOrWhiteSpace(_vm.Eda))
                errors.Add("枝番は必須です。");

            if (string.IsNullOrWhiteSpace(_vm.Id))
                errors.Add("ジョブIDは必須です。");

            if (string.IsNullOrWhiteSpace(_vm.Name))
                errors.Add("ジョブ名は必須です。");

            // 文字数制限チェック
            if (_vm.Id?.Length > 50)
                errors.Add("ジョブIDは50文字以内で入力してください。");

            if (_vm.Name?.Length > 100)
                errors.Add("ジョブ名は100文字以内で入力してください。");

            // 枝番の数値チェック
            if (!int.TryParse(_vm.Eda, out int edaValue) || edaValue < 1)
                errors.Add("枝番は1以上の数値で入力してください。");

            // FROMSERVER検証
            if (_vm.SelectedFromServer == null)
                errors.Add("運用処理管理検索先を選択してください。");

            // 特殊文字チェック（SQLインジェクション対策）
            if (ContainsDangerousCharacters(_vm.Scenario) || 
                ContainsDangerousCharacters(_vm.Eda) ||
                ContainsDangerousCharacters(_vm.Id))
            {
                errors.Add("シナリオ、枝番、ジョブIDに使用できない文字が含まれています。");
            }

            return new ValidationResult(errors.Count == 0, errors);
        }

        /// <summary>
        /// 危険な文字が含まれているかチェック
        /// </summary>
        private bool ContainsDangerousCharacters(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            var dangerousChars = new[] { "'", "\"", ";", "--", "/*", "*/", "DROP", "DELETE", "INSERT", "UPDATE" };
            return dangerousChars.Any(dangerous => input.ToUpper().Contains(dangerous));
        }

        /// <summary>
        /// ViewModelからJobManegmentオブジェクトを作成
        /// </summary>
        private JobManegment CreateJobFromViewModel()
        {
            return new JobManegment
            {
                SCENARIO = _vm.Scenario?.Trim() ?? "",
                EDA = _vm.Eda?.Trim() ?? "",
                ID = _vm.Id?.Trim() ?? "",
                NAME = _vm.Name?.Trim() ?? "",
                EXECUTION = (int)_vm.SelectedExecution,
                EXECCOMMNAD = _vm.ExecCommnad?.Trim() ?? "",
                STATUS = (int)_vm.SelectedStatus,
                BEFOREJOB = _vm.BeforeJob?.Trim() ?? "",
                JOBBOOLEAN = _vm.JobBoolean ? 1 : 0,
                RECEIVE = _vm.Receive?.Trim() ?? "",
                SEND = _vm.Send?.Trim() ?? "",
                MEMO = (_vm.Memo?.Replace(Environment.NewLine, "\\n"))?.Trim() ?? "",
                FROMSERVER = _vm.SelectedFromServer?.Name?.Trim() ?? ""
            };
        }

        /// <summary>
        /// エラーメッセージ表示
        /// </summary>
        private void ShowErrorMessage(string message)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error, 
                        MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
                });
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"ShowErrorMessage エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 警告メッセージ表示
        /// </summary>
        private void ShowWarningMessage(string message)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(message, "警告", MessageBoxButton.OK, MessageBoxImage.Warning, 
                        MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
                });
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"ShowWarningMessage エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 成功メッセージ表示
        /// </summary>
        private void ShowSuccessMessage(string message)
        {
            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(message, "完了", MessageBoxButton.OK, MessageBoxImage.Information, 
                        MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
                });
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"ShowSuccessMessage エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 検証エラー表示
        /// </summary>
        private void ShowValidationErrors(List<string> errors)
        {
            try
            {
                var message = "以下のエラーを修正してください：\n\n" + string.Join("\n", errors);
                ShowErrorMessage(message);
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"ShowValidationErrors エラー: {ex.Message}");
            }
        }

        #endregion

        #region 内部クラス

        /// <summary>
        /// 検証結果クラス
        /// </summary>
        private class ValidationResult
        {
            public bool IsValid { get; }
            public List<string> Errors { get; }

            public ValidationResult(bool isValid, List<string> errors)
            {
                IsValid = isValid;
                Errors = errors ?? new List<string>();
            }
        }

        #endregion
    }
}