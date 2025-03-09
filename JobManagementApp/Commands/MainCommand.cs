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
using JobManagementApp.Services;
using System.Windows.Threading;
using JobManagementApp.Manager;

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
            _vm.DisplayUpdateDate = DateTime.Now.ToString("yyyy/MM/dd HH:mm");

            // キャッシュ読み込み
            UserFileManager manager = new UserFileManager();
            _vm.UserId = manager.GetUserFilePath(manager.CacheKey_UserId);

            // JOBリスト 作成
            CreateJobList();

            // 定期処理 開始
            StartRegularTime();

        }

        /// <summary> 
        /// ユーザー保存　押下イベント
        /// </summary> 
        public void SaveUserButton_Click(object _)
        {
            if (_if.SaveCacheUser(_vm.UserId))
            {
                MessageBox.Show("キャッシュに保存しました。");
            }
            else
            {
                MessageBox.Show("キャッシュに保存に失敗しました。");
            }
        }

        /// <summary> 
        /// 画面更新　押下イベント
        /// </summary> 
        public void RefreshButton_Click(object _)
        {
            CreateJobList();
        }

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

            var unyoDataList = new List<JobUnyoCtlModel>();

            //運用処理管理Rの取得
            DataTable dataTable = JobService.GetUnyoData(args);
            if (dataTable.Rows.Count > 0)
            {
                foreach (DataRow row in dataTable.Rows)
                {
                    var updDt = row["UPDDT"].ToString();

                    DateTime updateDate = DateTime.ParseExact(updDt, "yyyy/MM/dd H:mm:ss", null);

                    // 更新日付が本日日付のみ処理
                    if (updateDate.ToString("yyyyMMdd") == DateTime.Now.ToString("yyyyMMdd"))
                    {
                        // eunm 対応 （ERROR = 3）
                        var flg = row["SYRFLG"].ToString();

                        unyoDataList.Add(new JobUnyoCtlModel
                        {
                            Scenario = row["SCENARIO"].ToString(),
                            Eda = row["EDA"].ToString(),
                            Id = row["JOBID"].ToString(),
                            SyrFlg = flg == "9" ? "3" : flg,
                            UpdDt = row["UPDDT"].ToString(),
                        });
                    }
                }
            }

            // 更新するリストに値がある場合のみ、更新
            if (unyoDataList.Count() > 0)
            {
                UpdateJobsFromUnyoData(_vm.Jobs, ref unyoDataList);
            }
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
