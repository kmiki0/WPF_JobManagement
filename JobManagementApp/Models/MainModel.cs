using JobManagementApp.Manager;
using JobManagementApp.Services;
using JobManagementApp.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobManagementApp.Models
{
    public interface IMainModel
    {
        // ユーザー保存
        bool SaveCacheUser(string userId);
        // 画面のジョブリスト取得
        Task<List<JobListItemViewModel>> CreateJobList(string userId);
        // 対象ジョブの運用処理管理R 取得
        Task<List<JobUnyoCtlModel>> GetUnyoData(List<JobUnyoCtlModel> jobs, string fromDate, string toDate);
        // 対象リストの運用処理の処理FLGを取得
        Task<DataTable> CreateJobList(List<JobUnyoCtlModel> targetList);
        // 検索条件を加えて、ジョブリストを取得
        Task<List<JobListItemViewModel>> GetSearchJobList(string scenario, string jobId, string recv, string send);
        // 受信先と送信先の一意のリストを取得
        Task<Tuple<List<string>, List<string>>> GetRecvSend();
    }

    public class MainModel : IMainModel
    {
        public bool SaveCacheUser(string userId)
        {
            bool result = false;

            try
            {
                if (!string.IsNullOrEmpty(userId?.ToString()))
                {
                    // キャッシュに保存
                    UserFileManager manager = new UserFileManager();
                    manager.SaveCache(manager.CacheKey_UserId, userId.ToString());
                    result = true;
                }
            }
            catch (Exception)
            {
                return false;
            }

            return result;
        }


        /// <summary> 
        /// 対象ジョブの運用処理管理R 取得
        /// </summary> 
        public async Task<List<JobUnyoCtlModel>> GetUnyoData(List<JobUnyoCtlModel> jobs, string fromDate, string toDate)
        {
            var result = new List<JobUnyoCtlModel>();

            await Task.Run(() =>
            {
                // ジョブ管理を検索する
                return JobService.GetUnyoData(jobs, fromDate, toDate);
            }).ContinueWith(x =>
            {
                if (x.Result.Rows.Count > 0)
                {
                    foreach (DataRow row in x.Result.Rows)
                    {
                        var updDt = row["UPDDT"].ToString();

                        DateTime updateDate = DateTime.ParseExact(updDt, "yyyy/MM/dd H:mm:ss", null);

                        // 更新日付が本日日付のみ処理
                        if (updateDate.ToString("yyyyMMdd") == DateTime.Now.ToString("yyyyMMdd"))
                        {
                            // eunm 対応 （ERROR = 3）
                            var flg = row["SYRFLG"].ToString();

                            result.Add(new JobUnyoCtlModel
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
            });
            return result;
        }

        /// <summary> 
        /// 画面更新　押下イベント
        /// </summary> 
        public async Task<List<JobListItemViewModel>> CreateJobList(string userId)
        {
            var result = new List<JobListItemViewModel>();

            // 前続ジョブ検索用
            var itemDict = new Dictionary<string, JobListItemViewModel>();

            await Task.Run(() =>
            {
                // ジョブ管理を検索する
                return JobService.LoadJobs(userId);
            }).ContinueWith(x =>
            {
                // データが取得出来たら、リストに格納していく
                foreach (DataRow row in x.Result.Rows)
                {
                    // 変換できない場合、WAIT
                    emStatus status = Enum.TryParse(row["STATUS"].ToString(), out status) ? status : emStatus.WAIT;
                    emExecution execution = Enum.TryParse(row["Execution"].ToString(), out execution) ? execution : emExecution.UNYO;

                    var item = new JobListItemViewModel{
                        Scenario = row["SCENARIO"].ToString(),
                        Eda = row["EDA"].ToString(),
                        Id = row["ID"].ToString(),
                        Name = row["NAME"].ToString(),
                        Execution = execution,
                        JobBoolean = int.Parse(row["JOBBOOLEAN"].ToString()) != 0,
                        Status = status
                    };

                    // vm を辞書に追加
                    itemDict[item.Id] = item;

                    var beforeJob = row["BEFOREJOB"].ToString();

                    // 前続ジョブがあるか確認
                    if (itemDict.TryGetValue(beforeJob, out var existingItem))
                    {
                        // 作成したUIに追加する
                        existingItem.Children.Add(item);
                    }
                    else
                    {
                        result.Add(item);
                    }
                }
            });

            return result;
        }

        /// <summary> 
        /// 受信先と送信先の一意のリストを取得
        /// </summary> 
        public async Task<Tuple<List<string>, List<string>>> GetRecvSend()
        {
            List<string> RecvList = new List<string>();
            List<string> SendList = new List<string>();

            await Task.Run(() =>
            {
                // 一意の受信先と送信先を取得
                return JobService.GetDistinctForRecvSend();
            }).ContinueWith(x =>
            {
                // データが取得出来たら、リストに格納していく
                foreach (DataRow row in x.Result.Rows)
                {
                    var type = row["Type"].ToString();

                    if (type == "RECV")
                    {
                        RecvList.Add(row["VAL"].ToString().Trim());
                    }
                    else if (type == "SEND")
                    {
                        SendList.Add(row["VAL"].ToString().Trim());
                    }
                }
            });

            return new Tuple<List<string>, List<string>>(RecvList, SendList);
        }


        /// <summary> 
        /// 画面更新　押下イベント
        /// </summary> 
        public async Task<DataTable> CreateJobList(List<JobUnyoCtlModel> targetList)
        {
            return new DataTable();
        }

        /// <summary> 
        /// 画面更新　押下イベント
        /// </summary> 

        public async　Task<List<JobListItemViewModel>> GetSearchJobList(string scenario, string jobId, string recv, string send)
        {
            var result = new List<JobListItemViewModel>();

            // 前続ジョブ検索用
            var itemDict = new Dictionary<string, JobListItemViewModel>();

            await Task.Run(() =>
            {
                // 条件加えて、検索
                return JobService.GetSearchJobList(scenario, jobId, recv, send);
            }).ContinueWith(x =>
            {
                // データが取得出来たら、リストに格納していく
                foreach (DataRow row in x.Result.Rows)
                {
                    // 変換できない場合、WAIT
                    emStatus status = Enum.TryParse(row["STATUS"].ToString(), out status) ? status : emStatus.WAIT;
                    emExecution execution = Enum.TryParse(row["Execution"].ToString(), out execution) ? execution : emExecution.UNYO;

                    var item = new JobListItemViewModel{
                        Scenario = row["SCENARIO"].ToString(),
                        Eda = row["EDA"].ToString(),
                        Id = row["ID"].ToString(),
                        Name = row["NAME"].ToString(),
                        Execution = execution,
                        JobBoolean = int.Parse(row["JOBBOOLEAN"].ToString()) != 0,
                        Status = status
                    };

                    // vm を辞書に追加
                    itemDict[item.Id] = item;

                    var beforeJob = row["BEFOREJOB"].ToString();

                    // 前続ジョブがあるか確認
                    if (itemDict.TryGetValue(beforeJob, out var existingItem))
                    {
                        // 作成したUIに追加する
                        existingItem.Children.Add(item);
                    }
                    else
                    {
                        result.Add(item);
                    }
                }
            });

            return result;
        }
    }
}
