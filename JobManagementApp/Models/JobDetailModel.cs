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
    public interface IJobDetailModel
    {
        // シナリオから登録可能な枝番 取得
        Task<int> GetNewEda(string scenario);
        // ジョブ管理テーブル 取得
        Task<JobManegment> GetJobManegment(string scenario, string eda);
        // ジョブ管理テーブル 更新
        Task<bool> UpdateJobManegment(JobManegment job);
        // ジョブ管理テーブル 削除
        Task<bool> DeleteJobManegment(string scenario, string eda);
        // 利用可能なデータベース名一覧 取得
        Task<DatabaseDisplayInfo[]> GetAvailableDatabaseDisplayInfos();
    }

    public class JobDetailModel : IJobDetailModel
    {
        /// <summary> 
        /// シナリオから登録可能な枝番 取得
        /// </summary> 
        public async Task<int> GetNewEda(string scenario)
        {
            return await Task.Run(() =>
            {
                // 枝番の最大値 取得
                return JobService.GetMaxEda(scenario);;
            }).ContinueWith(x =>
            {
                if (x.Result.Rows.Count > 0)
                {
                    // 取得出来れば 枝番 +1 
                    return int.Parse(x.Result.Rows[0]["EDA"].ToString()) + 1;
                }
                else
                {
                    // データない場合 初期値 1
                    return 1;
                }
            });
        }

        /// <summary> 
        /// ジョブ管理テーブル 更新
        /// </summary> 
        public async Task<JobManegment> GetJobManegment(string scenario, string eda)
        {
            return await Task.Run(() =>
            {
                return JobService.GetJobManegment(scenario, eda);
            }).ContinueWith(x =>
            {
                if (x.Result.Rows.Count > 0)
                {
                    // 取得内容をテーブル型にセットして返す
                    return new JobManegment
                    {
                        SCENARIO = x.Result.Rows[0]["SCENARIO"].ToString(),
                        EDA = x.Result.Rows[0]["EDA"].ToString(),
                        ID = x.Result.Rows[0]["ID"].ToString(),
                        NAME = x.Result.Rows[0]["NAME"].ToString(),
                        EXECUTION = int.Parse(x.Result.Rows[0]["EXECUTION"].ToString()),
                        EXECCOMMNAD = x.Result.Rows[0]["ExecCommnad"].ToString(),
                        STATUS = int.Parse(x.Result.Rows[0]["STATUS"].ToString()),
                        BEFOREJOB = x.Result.Rows[0]["BeforeJob"].ToString(),
                        JOBBOOLEAN = int.Parse(x.Result.Rows[0]["JOBBOOLEAN"].ToString()),
                        RECEIVE = x.Result.Rows[0]["Receive"].ToString(),
                        SEND = x.Result.Rows[0]["SEND"].ToString(),
                        MEMO = x.Result.Rows[0]["MEMO"].ToString().Replace("\\n", Environment.NewLine),
                        FROMSERVER = x.Result.Rows[0]["FROMSERVER"]?.ToString() ?? ""
                    };
                }
                else
                {
                    // 値取れない場合、空を返す
                    return new JobManegment();
                }
            });
        }

        /// <summary> 
        /// 利用可能なデータベース名一覧を取得
        /// </summary> 
        public async Task<DatabaseDisplayInfo[]> GetAvailableDatabaseDisplayInfos()
        {
            return await Task.Run(() =>
            {
                return JobService.GetAvailableDatabaseDisplayInfos();
            }).ContinueWith(x =>
            {
                if (x.Result != null && x.Result.Length > 0)
                {
                    return x.Result;
                }
                else
                {
                    ErrLogFile.WriteLog("GetAvailableDatabaseDisplayInfos: データベース情報が取得できませんでした");
                    return new DatabaseDisplayInfo[0];
                }
            });
        }

        /// <summary> 
        /// ジョブ管理テーブル 更新
        /// </summary> 
        public async Task<bool> UpdateJobManegment(JobManegment job)
        {
            return await Task.Run(() => 
            { 
                return JobService.UpdateJobManegment(job); 
            }).ContinueWith(x => 
            {
                return x.Result;
            });
        }

        /// <summary> 
        /// ジョブ管理テーブル 削除
        /// </summary> 
        public async Task<bool> DeleteJobManegment(string scenario, string eda)
        {
            return await Task.Run(() => 
            { 
                return JobService.DeleteJobManegment(scenario, eda); 
            }).ContinueWith(x => 
            {
                return x.Result;
            });
        }
    }
}
