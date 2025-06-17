using System.Windows;
using JobManagementApp.Views;
using JobManagementApp.ViewModels;
using JobManagementApp.Models;
using JobManagementApp.Helpers;
using System.Data;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using JobManagementApp.Manager;
using System.Threading.Tasks;

namespace JobManagementApp.Commands
{
    public class MainCommand
    {
        private readonly MainViewModel _vm;
        private readonly IMainModel _if;

        // 運用処理管理R　更新タイマー
        private DispatcherTimer _regularTimer;

        public MainCommand(MainViewModel VM, IMainModel IF)
        {
            _vm = VM;
            _if = IF;

            Init();
        }

        /// <summary> 
        /// 初期化
        /// </summary> 
        private void Init()
        {
            // 画面更新日時
            _vm.DisplayUpdateDate = DateTime.Now.ToString("yyyy/MM/dd HH:mm");
             

            // キャッシュ読み込み
            UserFileManager manager = new UserFileManager();
            _vm.UserId = manager.GetCache(manager.CacheKey_UserId);
            var getSearchTime = manager.GetCache(manager.CacheKey_SearchTime);
            _vm.SearchFromDate = getSearchTime == "" ? DateTime.Now.ToString("yyyy/MM/dd ") + "00:00" : DateTime.Now.ToString("yyyy/MM/dd ") + getSearchTime;
            _vm.SearchToDate = DateTime.Now.ToString("yyyy/MM/dd") + " 23:59";

            // JOBリスト 作成
            CreateJobList();

            // 定期処理 開始
            StartRegularTime();

            // 検索条件のコンボボックス 読み込み
            SetComboBox();
        }

        /// <summary> 
        /// 検索欄　開閉イベント
        /// </summary> 
        public void SearchAreaVisibility_Toggle(object arg)
        {
            bool isOpen = arg.ToString() == "1" ? true : false;
            _vm.BorderHeight = isOpen ? 102 : 0;
        }

        /// <summary> 
        /// 日付項目がDateTime型に変換可能か
        /// </summary> 
        public void CheckSearchDateTime(string convertVal, bool isFrom)
        {
            DateTime date;
            // 文字列をDateTimeに変換できるか判断
            if (DateTime.TryParse(convertVal, out date) == false)
            {
                var textName = isFrom ? "検索日付(開始)" : "検索日付(終了)";
                MessageBox.Show($"[検索項目] {textName} ： 日付変換できない形式が入力されてます。",
                    "メッセージ", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary> 
        /// クリアボタン　押下イベント
        /// </summary> 
        public void ClearButton_Click(object _)
        {
            _vm.Scenario = "";
            _vm.JobId = "";
            _vm.SelectedRecv = "";
            _vm.SelectedSend = "";
        }

        /// <summary> 
        /// 検索ボタン　押下イベント
        /// </summary> 
        public void SearchButton_Click(object _)
        {
            // ボタン処理可能か
            if (!_vm.IsButtonEnabled) return;
            _vm.IsButtonEnabled = false;

            // 日付範囲検証を追加
            if (!ValidateDateRange(_vm.SearchFromDate, _vm.SearchToDate))
            {
                _vm.IsButtonEnabled = true; // ボタンを再有効化
                return;
            }

            // TreeView状態　初期化
            _vm.IsExpanded = false;

            _if.GetSearchJobList(_vm.Scenario, _vm.JobId, _vm.SelectedRecv, _vm.SelectedSend).ContinueWith(x =>
            {
                // データが取得出来ない場合
                if (x.Result.Count <= 0)
                {
                    MessageBox.Show("検索条件に合う ジョブが見つかりませんでした。",
                        "メッセージ", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
                }
                else
                {
                    // シナリオごとにグルーピング
                    var groupingList = x.Result.GroupBy(y => y.Scenario);
                    var allList = new List<JobListItemViewModel>();
                    foreach (var group in groupingList)
                    {
                        allList.Add(new JobListItemViewModel
                        {
                            IsScenarioGroup = true,
                            Scenario = "",
                            Eda = "",
                            Id = group.Key,
                            Name = ConvertScenarioJapanese(group.Key),
                            Children = new ObservableCollection<JobListItemViewModel>(group.ToList())
                        });
                    }

                    _vm.Jobs = new ObservableCollection<JobListItemViewModel>(allList);

                    // 運用処理管理Rの検索
                    GetUnyoCtlData();
                }

                // ボタン使用可に戻す
                _vm.IsButtonEnabled = true;
            });

            // キャッシュ保存処理
            UserFileManager manager = new UserFileManager();
            // 読み込みユーザー
            if (manager.GetCache(manager.CacheKey_UserId) != _vm.UserId)
            {
                manager.SaveCache(manager.CacheKey_UserId, _vm.UserId);
            }
            // 検索日付 From
            DateTime date;
            if (DateTime.TryParse(_vm.SearchFromDate, out date))
            {
                var saveTime = date.Hour.ToString("00") + ":" + date.Minute.ToString("00");
                if (manager.GetCache(manager.CacheKey_SearchTime) != saveTime)
                {
                    manager.SaveCache(manager.CacheKey_SearchTime, saveTime);
                }
            }
        }

        /// <summary> 
        /// ユーザー保存　押下イベント
        /// </summary> 
        public void SaveUserButton_Click(object _)
        {
            if (_if.SaveCacheUser(_vm.UserId))
            {
                MessageBox.Show("キャッシュに保存しました。", "メッセージ", MessageBoxButton.OK, MessageBoxImage.Information, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
            }
            else
            {
                MessageBox.Show("キャッシュに保存に失敗しました。", "メッセージ", MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
            }
        }

    # region RefreshButton
        /// <summary> 
        /// 画面更新　押下イベント
        /// </summary> 
        public void RefreshButton_Click(object _)
        {
            // ボタン処理可能か
            if (!_vm.IsButtonEnabled) return;
            _vm.IsButtonEnabled = false;

            // 検索条件があるかチェック
            bool hasSearchConditions = HasSearchConditions();

            if (hasSearchConditions)
            {
                // 検索条件がある場合は検索処理を実行
                ExecuteSearchProcess();
            }
            else
            {
                // 検索条件がない場合は従来の全件更新処理
                ExecuteFullRefreshProcess();
            }
        }

        /// <summary>
        /// 検索処理を実行（SearchButton_Clickと同じ処理）
        /// </summary>
        private void ExecuteSearchProcess()
        {
            try
            {
                // 日付範囲検証を追加
                if (!ValidateDateRange(_vm.SearchFromDate, _vm.SearchToDate))
                {
                    _vm.IsButtonEnabled = true; // ボタンを再有効化
                    return;
                }

                // TreeView状態　初期化
                _vm.IsExpanded = false;

                _if.GetSearchJobList(_vm.Scenario, _vm.JobId, _vm.SelectedRecv, _vm.SelectedSend).ContinueWith(x =>
                {
                    // データが取得出来ない場合
                    if (x.Result.Count <= 0)
                    {
                        MessageBox.Show("検索条件に合う ジョブが見つかりませんでした。",
                            "メッセージ", MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
                    }
                    else
                    {
                        // シナリオごとにグルーピング
                        var groupingList = x.Result.GroupBy(y => y.Scenario);
                        var allList = new List<JobListItemViewModel>();
                        foreach (var group in groupingList)
                        {
                            allList.Add(new JobListItemViewModel
                            {
                                IsScenarioGroup = true,
                                Scenario = "",
                                Eda = "",
                                Id = group.Key,
                                Name = ConvertScenarioJapanese(group.Key),
                                Children = new ObservableCollection<JobListItemViewModel>(group.ToList())
                            });
                        }

                        _vm.Jobs = new ObservableCollection<JobListItemViewModel>(allList);

                        // 運用処理管理Rの検索
                        GetUnyoCtlData();
                    }

                    // ボタン使用可に戻す
                    _vm.IsButtonEnabled = true;
                });

                // キャッシュ保存処理（SearchButton_Clickと同じ）
                SaveSearchCache();
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"ExecuteSearchProcess エラー: {ex.Message}");
                _vm.IsButtonEnabled = true;
            }
        }

        /// <summary>
        /// 全件更新処理を実行（従来のRefreshButton_Click処理）
        /// </summary>
        private void ExecuteFullRefreshProcess()
        {
            try
            {
                Task.Run(() => CreateJobList()).ContinueWith(x => { 
                    // ボタン使用可に戻す
                    _vm.IsButtonEnabled = true;
                });
                // 項目初期化
                _vm.IsExpanded = false;
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"ExecuteFullRefreshProcess エラー: {ex.Message}");
                _vm.IsButtonEnabled = true;
            }
        }

        /// <summary>
        /// 検索キャッシュの保存処理
        /// </summary>
        private void SaveSearchCache()
        {
            try
            {
                UserFileManager manager = new UserFileManager();
                // 読み込みユーザー
                if (manager.GetCache(manager.CacheKey_UserId) != _vm.UserId)
                {
                    manager.SaveCache(manager.CacheKey_UserId, _vm.UserId);
                }
                // 検索日付 From
                DateTime date;
                if (DateTime.TryParse(_vm.SearchFromDate, out date))
                {
                    var saveTime = date.Hour.ToString("00") + ":" + date.Minute.ToString("00");
                    if (manager.GetCache(manager.CacheKey_SearchTime) != saveTime)
                    {
                        manager.SaveCache(manager.CacheKey_SearchTime, saveTime);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"SaveSearchCache エラー: {ex.Message}");
            }
        }

    # endregion

        /// <summary> 
        /// ジョブ追加　押下イベント
        /// </summary> 
        public void NewJobButton_Click(object _)
        {
            var vm = new JobDetailViewModel(new JobDetailModel());
            //vm.RequestClose += DetailWindow_RequestClose;
            var detailWindow = new JobDetailWindow(vm);
            var window = detailWindow as Window;
            // ウィンドウの表示位置　調整
            WindowHelper.SetWindowLocation(ref window);
            vm.window = detailWindow;
            detailWindow.DataContext = vm;
            detailWindow.ShowDialog();
        }

        /// <summary> 
        /// TreeViewの開閉チェックボックス　変更イベント
        /// </summary> 
        public void TreeViewCheckBox_Toggle()
        {
            foreach (var job in _vm.Jobs)
            {
                job.IsOpenTreeView = _vm.IsExpanded;
                if (job.Children.Count > 0)
                {
                    TreeViewAllChange(job.Children);
                }
            }
        }
        // 再帰的に検索
        private void TreeViewAllChange(ObservableCollection<JobListItemViewModel> jobs)
        {
            foreach (var job in jobs)
            {
                job.IsOpenTreeView = _vm.IsExpanded;
                if (job.Children.Count > 0)
                {
                    TreeViewAllChange(job.Children);
                }
            }
        }

        /// <summary> 
        /// JOBリスト 作成
        /// </summary> 
        private void CreateJobList()
        {
            // 受信出来次第、画面更新
            _if.CreateJobList(_vm.UserId).ContinueWith(x =>
            {
                // データが取得出来ない場合
                if (x.Result.Count <= 0) return;

                // シナリオごとにグルーピング
                var groupingList = x.Result.GroupBy(y => y.Scenario);
                var allList = new List<JobListItemViewModel>();
                foreach (var group in groupingList)
                {
                    allList.Add(new JobListItemViewModel
                    {
                        IsScenarioGroup = true,
                        Scenario = "",
                        Eda = "",
                        Id = group.Key,
                        Name = ConvertScenarioJapanese(group.Key),
                        Children = new ObservableCollection<JobListItemViewModel>(group.ToList())
                    });
                }

                _vm.Jobs = new ObservableCollection<JobListItemViewModel>(allList);

                // 運用処理管理Rの検索
                GetUnyoCtlData();
            });
        }

        /// <summary> 
        /// 検索条件のコンボボックス取得
        /// </summary> 
        private void SetComboBox()
        {
            _if.GetRecvSend().ContinueWith(x =>
            {
                // 受信先 データあれば、セット
                if (x.Result.Item1.Count > 0)
                {
                    _vm.cmdRecv = x.Result.Item1.ToArray();
                }
                // 送信先 データあれば、セット
                if (x.Result.Item2.Count > 0)
                {
                    _vm.cmdSend = x.Result.Item2.ToArray();
                }
            });
        }

        /// <summary> 
        /// 定期実行 タイマー 開始
        /// </summary> 
        private void StartRegularTime()
        {
            _regularTimer = new DispatcherTimer();
            _regularTimer.Interval = TimeSpan.FromSeconds(60);
            _regularTimer.Tick += Regular_Tick;
            _regularTimer.Start();
        }

        private void Regular_Tick(object sender, EventArgs e)
        {
            _vm.DisplayUpdateDate = DateTime.Now.ToString("yyyy/MM/dd HH:mm");
            GetUnyoCtlData();
        }

        public void GetUnyoCtlData()
        {
            // 日付範囲検証を追加
            if (!ValidateDateRange(_vm.SearchFromDate, _vm.SearchToDate)) { return; }

            // 抽出する条件　待機中、実行中
            List<emStatus> whereStatus = new List<emStatus> { emStatus.WAIT, emStatus.RUN };
            var jobList = FindByStatus(_vm.Jobs, whereStatus);

            // 運用処理管理R　検索用リスト
            var args = new List<JobUnyoCtlModel>();
            foreach (var job in jobList)
            {
                if (job.Scenario != "" && job.Eda != "")
                {
                    args.Add(new JobUnyoCtlModel
                    {
                        Scenario = job.Scenario,
                        Eda = job.Eda,
                        Id = job.Id,
                        SyrFlg = job.Status.ToString(),
                        UpdDt = job.UpdateDate
                    });
                }
            }

            // 検索日付 チェック
            var from = CheckSearchDate(_vm.SearchFromDate, true);
            var to = CheckSearchDate(_vm.SearchToDate, false);
            
            //運用処理管理Rの取得
            _if.GetUnyoData(args, from, to).ContinueWith(x => 
            {
                // 更新するリストに値がある場合のみ、更新
                if (x.Result.Count > 0)
                {
                    var updList = x.Result;
                    UpdateJobsFromUnyoData(_vm.Jobs, ref updList);
                }
            });
        }

        /// <summary> 
        /// Job取得したデータを元に画面データを更新
        /// </summary> 
        private void UpdateJobsFromUnyoData(ObservableCollection<JobListItemViewModel> jobs, ref List<JobUnyoCtlModel> unyoData)
        {
            foreach (var job in jobs)
            {
                // シナリオグループ以外のみ　更新
                if (job.IsScenarioGroup == false)
                {
                    var unyo = unyoData.Where(x => x.Scenario == job.Scenario && x.Eda == job.Eda).FirstOrDefault();
                    // 見つからない場合 スキップ
                    if (unyo != null)
                    {
                        emStatus emstatus = Enum.TryParse(unyo.SyrFlg, out emstatus) ? emstatus : emStatus.WAIT;

                        // 更新
                        job.Status = emstatus;
                        job.UpdateDate = unyo.UpdDt;

                        // 更新したらリストから削除
                        unyoData.Remove(unyo);
                    }
                }

                // 更新するデータが存在するとき
                if (unyoData.Count > 0)
                {
                    // ネスト先 検索
                    UpdateJobsFromUnyoData(job.Children, ref unyoData);
                }
                else
                {
                    continue;
                }
            }
        }

        /// <summary> 
        /// Jobs ジョブ実行方法で再帰的に検索
        /// </summary> 
        private List<JobListItemViewModel> FindByStatus(ObservableCollection<JobListItemViewModel> jobs, List<emStatus> whereStatus)
        {
            var resultList = new List<JobListItemViewModel>();

            foreach (var job in jobs)
            {
                if (whereStatus.Contains(job.Status))
                {
                    resultList.Add(job);
                }

                resultList.AddRange(FindByStatus(job.Children ,whereStatus));
            }

            return resultList;
        }


        /// <summary>
        /// 🆕 日付範囲の検証
        /// </summary>
        private bool ValidateDateRange(string fromDate, string toDate)
        {
            try
            {
                if (!DateTime.TryParse(fromDate, out DateTime from))
                {
                    MessageBox.Show("開始日時の形式が正しくありません。", "入力エラー", 
                        MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
                    return false;
                }

                if (!DateTime.TryParse(toDate, out DateTime to))
                {
                    MessageBox.Show("終了日時の形式が正しくありません。", "入力エラー", 
                        MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
                    return false;
                }

                // 開始日時が終了日時より後の場合
                if (from >= to)
                {
                    MessageBox.Show("開始日時は終了日時より前に設定してください。", "入力エラー", 
                        MessageBoxButton.OK, MessageBoxImage.Warning, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
                    return false;
                }

                // 検索範囲が長すぎる場合の警告（3日間）
                if ((to - from).TotalDays > 3)
                {
                    var result = MessageBox.Show(
                        $"検索範囲が{(to - from).TotalDays:F1}日間と長期間です。\n処理に時間がかかる可能性があります。続行しますか？", 
                        "確認", 
                        MessageBoxButton.YesNo, MessageBoxImage.Question, 
                        MessageBoxResult.No, MessageBoxOptions.DefaultDesktopOnly);
                    
                    return result == MessageBoxResult.Yes;
                }

                return true;
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"ValidateDateRange エラー: {ex.Message}");
                MessageBox.Show("日付検証中にエラーが発生しました。", "システムエラー", 
                    MessageBoxButton.OK, MessageBoxImage.Error, MessageBoxResult.OK, MessageBoxOptions.DefaultDesktopOnly);
                return false;
            }
        }

        /// <summary> 
        /// 検索日付の型チェックと回避処理
        /// </summary> 
        /// <param name="isFrom">From条件の場合、True</param>
        private string CheckSearchDate(string date, bool isFrom)
        {
            string result;
            DateTime dateValue;

            // 文字列をDateTimeに変換できるか判断
            if (DateTime.TryParse(date, out dateValue))
            {
                // 指定された形式に変換
                result = dateValue.ToString("yyyy/MM/dd HH:mm");
            }
            else
            {
                // From/To によって、対処方法変える
                if (isFrom)
                {
                    // Fromで変換できない場合、本日日付 + 00:00
                    result = DateTime.Now.ToString("yyyy/MM/dd") + " 00:00";
                }
                else
                {
                    // Toで変換できない場合、本日日付 + 23:59
                    result = DateTime.Now.ToString("yyyy/MM/dd") + " 23:59";
                }
            }

            return result;
        }


        /// <summary> 
        /// シナリオキーから日本語名を取得
        /// </summary> 
        private string ConvertScenarioJapanese(string scenario)
        {
            switch (scenario) 
            {
                case "DH1001":
                    return "当日実績";
                case "DH0302":
                    return "移送入庫(DICS-EDI)";
                case "DH0404":
                    return "格納後出荷／着発／工場直出荷／テリ外出荷(DICS-EDI)";
                case "DH0204":
                    return "移送出庫(DICS-EDI)";
                case "DH0011":
                    return "新販売基幹の画面での完成入庫計上（デバン予定入庫No）";
                case "DH0008":
                    return "新販売基幹の画面での完成入庫計上（外部システム入庫No呼び出し）";
                case "DH0007":
                    return "自社国内生産（完成入庫・PDA計上（管轄内移送）【直移管（臨海 ）】";
                case "DH0006":
                    return "完成入庫・PDA計上（格納・管轄間移送）【プル型】";
                case "DH0005":
                    return "完成入庫・払出指示・号車採番後計上（管轄内移送）【直移管（滋賀）】";
                case "DH0004":
                    return "完成入庫・払出指示・移送確定後計上（管轄間移送）【後補充】";
                case "DH0003":
                    return "完成入庫・払出指示後計上（格納）【格納】";
                case "DH0002":
                    return "自社国内生産（完成入庫時計上）【格納】";
                case "DH0001":
                    return "自社国内生産（手計上）";
                case "DH0105":
                    return "後補充";
                case "DH0104":
                    return "工事品（改装品含む）（自動積付）";
                case "DH0103":
                    return "工事品（改装品含む）（手動積付）";
                case "DH0102":
                    return "自動移送（自動積付）";
                case "DH0101":
                    return "自動移送（手動積付）";
                case "DH0010":
                    return "新販売基幹の画面での完成入庫計上（予定外品）";
                case "DH0009":
                    return "物流基幹（輸入品）の画面での完成入庫計上";
                case "DH0107":
                    return "プル型";
                case "DH0109":
                    return "計画移送";
                case "DH0110":
                    return "移送依頼";
                case "DH0111":
                    return "マニュアル移送（手動積付）";
                case "DH0112":
                    return "マニュアル移送（自動積付）";
                case "DH0201":
                    return "移送出庫";
                case "DH1310":
                    return "供給枠";
                case "DH1102":
                    return "マスタ(顧客管理マスタ)";
                case "DH0501":
                    return "倉庫別在庫情報";
                case "DH0301":
                    return "移送入庫(DICS-S)";
                case "DH0203":
                    return "移送出庫(DICS-SLオーケー／DICS-SL)";
                case "DH0202":
                    return "移送出庫(DICS-S拠点)";
                case "DH0303":
                    return "移送明細(DICS-SL)";
                case "DH0401":
                    return "格納後出荷／着発／工場直出荷／テリ外出荷";
                case "DH1402":
                    return "横流し系（物流経費～TIMS-BO）";
                case "DH1401":
                    return "横流し系（物流経費～経理SAP）";
                case "DH1309":
                    return "製品割当";
                case "DH1308":
                    return "出荷伝票登録（バッチ）情報";
                case "DH1304":
                    return "運賃検収";
                case "DH1302":
                    return "受注情報";
                case "DH1301":
                    return "出荷機番";
                case "DH1201":
                    return "出荷指示(例外出荷)";
                case "DH1101":
                    return "マスタ系";
                case "DH1303":
                    return "配車リターン";
                case "DH1103":
                    return "マスタ(DICS起点)";
                case "DH0801":
                    return "システム照合（標準品・工事品）";
                case "DH0403":
                    return "格納後出荷／着発／工場直出荷／テリ外出荷(DICS-SL)";
                case "DH0703":
                    return "品区変更対象通知";
                case "DH0702":
                    return "来歴情報作成";
                case "DH0701":
                    return "機番訂正";
                case "DH0402":
                    return "格納後出荷／着発／工場直出荷／テリ外出荷(DICS-S)";
                case "DH0601":
                    return "在庫鮮度管理からの品区変更";
                default:
                    return "特定できないシナリオID";
            }
        }
    }
}
