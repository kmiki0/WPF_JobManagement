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
        // 対象リストの運用処理の処理FLGを取得
        Task<DataTable> CreateJobList(List<JobUnyoCtlModel> targetList);
    }

    /// <summary> 
    /// ユーザー保存　押下イベント
    /// </summary> 
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
                    manager.SaveUserFilePath(manager.CacheKey_UserId, userId.ToString());
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
        /// 画面更新　押下イベント
        /// </summary> 
        public async Task<DataTable> CreateJobList(List<JobUnyoCtlModel> targetList)
        {
            return new DataTable();
        }
    }
}
