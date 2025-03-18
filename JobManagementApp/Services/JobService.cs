using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JobManagementApp.Manager;
using JobManagementApp.Models;

namespace JobManagementApp.Services
{
    public static class JobService
    {
        #region 運用処理管理R
        /// <summary> 
        /// シナリオと枝番から、運用処理管理Rの情報取得
        /// </summary> 
        public static DataTable GetUnyoData(List<JobUnyoCtlModel> args)
        {
            DataTable dt = new DataTable();
            StringBuilder sql = new StringBuilder();

            // 検索項目　ない場合、空を返す
            if (args.Count <= 0) return new DataTable();

            try
            {
                // SQL作成
                // シナリオと枝番からジョブIDを取得し、更新日付が一番新しいものを取得
                sql.Append(" with JOBID_UPDDT as ( ");
                sql.Append("     select");
                sql.Append("         UNYO.JOBID");
                sql.Append("         , MAX(UNYO.UPDDT) as UPDDT ");
                sql.Append("         , JOB_M.SCENARIO ");
                sql.Append("         , JOB_M.EDA ");
                sql.Append("     from");
                sql.Append("         L1_UNYOCTL UNYO ");
                sql.Append("         left join JOB_MANEGMENT JOB_M ");
                sql.Append("             on UNYO.JOBID = JOB_M.ID ");
                sql.Append("     where");
                // リストにあるものをすべて条件に追加
                bool isFirst = true;
                foreach (var arg in args)
                {
                    if (isFirst)
                    {
                        isFirst = false;
                    }
                    else
                    {
                        sql.Append($"        or ");
                    }
                    sql.Append($"        (JOB_M.SCENARIO = '{arg.Scenario}' and JOB_M.EDA = {arg.Eda}) ");
                }
                sql.Append("     group by");
                sql.Append("         UNYO.JOBID");
                sql.Append("       , JOB_M.SCENARIO ");
                sql.Append("       , JOB_M.EDA ");
                sql.Append(" ) ");

                // 上で取得した更新日付を元に運用処理管理Rを検索
                sql.Append(" select");
                sql.Append("     UNYO.JOBID");
                sql.Append("     , UNYO.SYRFLG");
                sql.Append("     , UNYO.UPDDT ");
                sql.Append("     , JU.SCENARIO ");
                sql.Append("     , JU.EDA ");
                sql.Append(" from");
                sql.Append("     L1_UNYOCTL UNYO ");
                sql.Append("     inner join JOBID_UPDDT JU ");
                sql.Append("         on UNYO.JOBID = JU.JOBID ");
                sql.Append("         and UNYO.UPDDT = JU.UPDDT");

                // SQL実行
                var pobjOraDb = DatabaseManager.Instance.pobjOraDb;
                if (pobjOraDb.pSelectOra(sql.ToString(), ref dt) == false)
                {
                    throw new Exception("ORACLE データ取得エラー");
                }
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }

            return dt;
        }

        /// <summary> 
        /// ジョブ管理からJOBIDを取得し、運用処理管理RのSEQが大きいもので更新
        /// </summary> 
        public static bool UpdateUnyoCtrl(string scenario, string eda)
        {
            StringBuilder sql = new StringBuilder();

            try
            {
                // SQL作成
                sql.Append(" UPDATE L1_UNYOCTL");
                sql.Append(" SET SYRFLG = 0,");
                sql.Append("     UPDDT = SYSDATE");
                sql.Append(" WHERE SEQ = (");
                sql.Append("     SELECT MAX(UNYO.SEQ)");
                sql.Append("     FROM L1_UNYOCTL UNYO");
                sql.Append("     JOIN JOB_MANEGMENT JOB_M");
                sql.Append("         ON UNYO.JOBID = JOB_M.ID");
                sql.Append($"    WHERE JOB_M.SCENARIO = '{scenario}'");
                sql.Append($"    AND JOB_M.EDA = {eda}");
                sql.Append(" )");

                // SQL実行
                if (DatabaseManager.Instance.pobjOraDb.pExecOra(sql.ToString(), DICSSLORA.ACmnOra.clsMngOracle.peTran.Yes ) == false)
                {
                    throw new Exception("ORACLE 運用処理管理Rの更新に失敗しました");
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        #endregion


        #region ジョブ管理
        /// <summary> 
        /// シナリオから最大の枝番を取得
        /// </summary> 
        public static DataTable GetMaxEda(string scenario)
        {
            DataTable dt = new DataTable();
            StringBuilder sql = new StringBuilder();

            try
            {
                // SQL作成
                sql.Append("select");
                sql.Append("  max(eda) as Eda ");
                sql.Append("from ");
                sql.Append("  JOB_MANEGMENT ");
                sql.Append("where");
                sql.Append($"  SCENARIO = '{scenario}' ");
                sql.Append("group by SCENARIO");

                // SQL取得
                if (DatabaseManager.Instance.pobjOraDb.pSelectOra(sql.ToString(), ref dt) == false)
                {
                    throw new Exception("ORACLE データ取得エラー");
                }
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }

            return dt;
        }

        /// <summary> 
        /// シナリオと枝番から、ジョブ管理を取得
        /// </summary> 
        public static DataTable GetJobManegment(string scenario, string eda)
        {
            DataTable dt = new DataTable();
            StringBuilder sql = new StringBuilder();

            try
            {
                // SQL作成
                sql.Append("select");
                sql.Append("  *");
                sql.Append("from ");
                sql.Append("  JOB_MANEGMENT ");
                sql.Append("where");
                sql.Append("  RRSJFLG = 0 ");
                sql.Append($" and SCENARIO = '{scenario}' ");
                sql.Append($" and EDA = {eda} ");

                // SQL取得
                var pobjOraDb = DatabaseManager.Instance.pobjOraDb;
                if (pobjOraDb.pSelectOra(sql.ToString(), ref dt) == false)
                {
                    throw new Exception("ORACLE データ取得エラー");
                }
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }

            return dt;
        }

        /// <summary> 
        /// テーブル型のパラメータを元に、更新, 登録を実行する
        /// </summary> 
        public static bool UpdateJobManegment(JobManegment job)
        {
            StringBuilder sql = new StringBuilder();

            try
            {
                // SQL作成
                sql.Append("merge into JOB_MANEGMENT JM");
                sql.Append($"  using (select '{job.SCENARIO}' as SCENARIO, '{job.EDA}' as EDA from dual) TMP");
                sql.Append("     on (JM.SCENARIO = TMP.SCENARIO and JM.EDA = TMP.EDA)");
                sql.Append("   when matched then");
                sql.Append("     update set");
                sql.Append($"      JM.ID = '{job.ID}'");
                sql.Append($"    , JM.NAME = '{job.NAME}'");
                sql.Append($"    , JM.EXECUTION = {job.EXECUTION}");
                sql.Append($"    , JM.EXECCOMMNAD = '{job.EXECCOMMNAD}'");
                sql.Append($"    , JM.STATUS = {job.STATUS}");
                sql.Append($"    , JM.BEFOREJOB = '{job.BEFOREJOB}'");
                sql.Append($"    , JM.JOBBOOLEAN = {job.JOBBOOLEAN}");
                sql.Append($"    , JM.RECEIVE = '{job.RECEIVE}'");
                sql.Append($"    , JM.SEND = '{job.SEND}'");
                sql.Append($"    , JM.MEMO = '{job.MEMO}'");
                sql.Append("   when not matched then");
                sql.Append("     insert (");
                sql.Append("       SCENARIO");
                sql.Append("     , EDA");
                sql.Append("     , ID");
                sql.Append("     , NAME");
                sql.Append("     , EXECUTION");
                sql.Append("     , EXECCOMMNAD");
                sql.Append("     , STATUS");
                sql.Append("     , BEFOREJOB");
                sql.Append("     , JOBBOOLEAN");
                sql.Append("     , RECEIVE");
                sql.Append("     , SEND");
                sql.Append("     , MEMO");
                sql.Append("     ) VALUES (");
                sql.Append($"      '{job.SCENARIO}'");
                sql.Append($"    , '{job.EDA}'");
                sql.Append($"    , '{job.ID}'");
                sql.Append($"    , '{job.NAME}'");
                sql.Append($"    ,  {job.EXECUTION}");
                sql.Append($"    , '{job.EXECCOMMNAD}'");
                sql.Append($"    , '{job.STATUS}'");
                sql.Append($"    , '{job.BEFOREJOB}'");
                sql.Append($"    ,  {job.JOBBOOLEAN}");
                sql.Append($"    , '{job.RECEIVE}'");
                sql.Append($"    , '{job.SEND}'");
                sql.Append($"    , '{job.MEMO}'");
                sql.Append("     )");

                // SQL実行
                if (DatabaseManager.Instance.pobjOraDb.pExecOra(sql.ToString(), DICSSLORA.ACmnOra.clsMngOracle.peTran.Yes ) == false)
                {
                    throw new Exception("ORACLE ジョブ管理の更新に失敗しました");
                }

                return true;
            }
            catch (Exception)
            {

                return false;
            }
        }

        /// <summary> 
        /// ジョブ管理 論理削除
        /// </summary> 
        public static bool DeleteJobManegment(string scenario, string eda)
        {
            StringBuilder sql = new StringBuilder();

            try
            {
                // SQL作成
                sql.Append(" update JOB_MANEGMENT");
                sql.Append(" set RRSJFLG = 1");
                sql.Append(" where ");
                sql.Append($"    SCENARIO = '{scenario}'");
                sql.Append($"    and EDA = {eda}");

                // SQL実行
                if (DatabaseManager.Instance.pobjOraDb.pExecOra(sql.ToString(), DICSSLORA.ACmnOra.clsMngOracle.peTran.Yes ) == false)
                {
                    throw new Exception("ORACLE ジョブ管理の更新に失敗しました");
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }

        }

        /// <summary> 
        /// 受信先と送信先の一意のリストを取得
        /// </summary> 
        public static DataTable GetDistinctForRecvSend()
        {
            DataTable dt = new DataTable();
            StringBuilder sql = new StringBuilder();

            try
            {
                // SQL作成
                sql.Append("select distinct ");
                sql.Append("    'RECV' AS Type ");
                sql.Append("   , RECEIVE AS VAL ");
                sql.Append("from JOB_MANEGMENT ");
                sql.Append("union ");
                sql.Append("select distinct ");
                sql.Append("    'SEND' AS Type ");
                sql.Append("   , SEND AS VAL ");
                sql.Append("from JOB_MANEGMENT ");

                // SQL取得
                var pobjOraDb = DatabaseManager.Instance.pobjOraDb;
                if (pobjOraDb.pSelectOra(sql.ToString(), ref dt) == false)
                {
                    throw new Exception("ORACLE データ取得エラー");
                }
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }

            return dt;
        }

        #endregion


        #region ジョブ関連ファイル
        /// <summary> 
        /// Key項目から、ジョブIDとジョブ関連ファイル(1件)を取得
        /// </summary> 
        public static DataTable GetJobLinkFile(string scenario, string eda, string fileName, string filePath)
        {
            DataTable dt = new DataTable();
            StringBuilder sql = new StringBuilder();

            try
            {
                // SQL作成
                sql.Append("select");
                sql.Append("    JM.ID as JOBID");
                sql.Append("    ,JL.SCENARIO");
                sql.Append("    ,JL.EDA");
                sql.Append("    ,JL.FILENAME");
                sql.Append("    ,JL.FILEPATH");
                sql.Append("    ,JL.FILETYPE");
                sql.Append("    ,JL.OBSERVERTYPE");
                sql.Append(" from ");
                sql.Append("    JOB_LINKFILE JL");
                sql.Append(" left join");
                sql.Append("    JOB_MANEGMENT JM");
                sql.Append(" on");
                sql.Append("    JL.SCENARIO = JM.SCENARIO");
                sql.Append("    and JL.EDA = JM.EDA");
                sql.Append(" where");
                sql.Append($"   JL.SCENARIO = '{scenario}' ");
                sql.Append($"   and JL.EDA = {eda} ");
                sql.Append($"   and JL.FILENAME = '{fileName}'");
                sql.Append($"   and JL.FILEPATH = '{filePath}' ");

                // SQL取得
                var pobjOraDb = DatabaseManager.Instance.pobjOraDb;
                if (pobjOraDb.pSelectOra(sql.ToString(), ref dt) == false)
                {
                    throw new Exception("ORACLE データ取得エラー");
                }
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }

            return dt;
        }

        /// <summary> 
        /// シナリオと枝番から、ジョブ周辺ファイル データを取得
        /// </summary> 
        public static DataTable GetJobLinkFile(string scenario, string eda)
        {
            DataTable dt = new DataTable();
            StringBuilder sql = new StringBuilder();

            try
            {
                // SQL作成
                sql.Append("select");
                sql.Append("  *　");
                sql.Append("from ");
                sql.Append("  JOB_LINKFILE ");
                sql.Append("where");
                sql.Append($" SCENARIO = '{scenario}' ");
                sql.Append($" and EDA = {eda} ");
                sql.Append("ORDER BY FILETYPE");

                // SQL取得
                if (DatabaseManager.Instance.pobjOraDb.pSelectOra(sql.ToString(), ref dt) == false)
                {
                    throw new Exception("ORACLE データ取得エラー");
                }
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }

            return dt;
        }

        /// <summary> 
        /// ジョブ関連ファイルを新規登録
        /// </summary> 
        public static bool UpdateJobLinkFile(JobLinkFile job)
        {
            StringBuilder sql = new StringBuilder();

            try
            {
                // SQL作成
                sql.Append("insert into JOB_LINKFILE (");
                sql.Append("  SCENARIO");
                sql.Append(", EDA");
                sql.Append(", FILENAME");
                sql.Append(", FILEPATH");
                sql.Append(", FILETYPE");
                sql.Append(", OBSERVERTYPE");
                sql.Append(") VALUES (");
                sql.Append($"  '{job.SCENARIO}'");
                sql.Append($", '{job.EDA}'");
                sql.Append($", '{job.FILENAME}'");
                sql.Append($", '{job.FILEPATH}'");
                sql.Append($",  {job.FILETYPE}");
                sql.Append($",  {job.OBSERVERTYPE}");
                sql.Append(")");

                // SQL実行
                if (DatabaseManager.Instance.pobjOraDb.pExecOra(sql.ToString(), DICSSLORA.ACmnOra.clsMngOracle.peTran.Yes ) == false)
                {
                    throw new Exception("ORACLE ジョブ関連ファイルの更新に失敗しました");
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary> 
        /// ジョブ関連ファイル　物理削除
        /// </summary> 
        public static bool DeleteJobLinkFile(JobLinkFile job)
        {
            StringBuilder sql = new StringBuilder();

            try
            {
                // SQL作成
                sql.Append(" delete from JOB_LINKFILE");
                sql.Append(" where ");
                sql.Append($"    SCENARIO = '{job.SCENARIO}'");
                sql.Append($"    and EDA = {job.EDA}");
                sql.Append($"    and FILENAME = '{job.OLDFILENAME}'");
                sql.Append($"    and FILEPATH = '{job.OLDFILEPATH}'");

                // SQL実行
                if (DatabaseManager.Instance.pobjOraDb.pExecOra(sql.ToString(), DICSSLORA.ACmnOra.clsMngOracle.peTran.Yes ) == false)
                {
                    throw new Exception("ORACLE ジョブ管理の更新に失敗しました");
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        #endregion


        #region Main画面読み込み
        /// <summary> 
        /// Main画面 初期ロード時
        /// </summary> 
        public static DataTable LoadJobs(string userId)
        {
            // とりあえず全件
            userId = "";

            DataTable dt = new DataTable();
            StringBuilder sql = new StringBuilder();

            try
            {
                // SQL作成
                sql.Append("select");
                sql.Append("  JOB_M.SCENARIO as SCENARIO,");
                sql.Append("  JOB_M.EDA as EDA,");
                sql.Append("  JOB_M.ID as ID,");
                sql.Append("  JOB_M.NAME as NAME,");
                sql.Append("  JOB_M.EXECUTION as EXECUTION,");
                sql.Append("  JOB_M.EXECCOMMNAD as EXECCOMMNAD,");
                sql.Append("  JOB_M.STATUS as STATUS,");
                sql.Append("  JOB_M.BEFOREJOB as BEFOREJOB,");
                sql.Append("  JOB_M.JOBBOOLEAN as JOBBOOLEAN,");
                sql.Append("  JOB_M.RECEIVE as RECEIVE,");
                sql.Append("  JOB_M.SEND as SEND,");
                sql.Append("  JOB_M.MEMO as MEMO ");
                sql.Append("from ");
                sql.Append("  JOB_MANEGMENT JOB_M ");
                sql.Append("left join ");
                sql.Append("  JOB_OWENREUSER JOB_O ");
                sql.Append("  on JOB_M.SCENARIO = JOB_O.SCENARIO ");
                sql.Append("  and JOB_M.EDA = JOB_O.EDA ");
                sql.Append("where");
                sql.Append("  JOB_M.RRSJFLG = 0 ");

                // ユーザーが設定されている場合、条件追加
                if (!string.IsNullOrEmpty(userId))
                {
                    sql.Append($"  and JOB_O.USERID = '{userId}'");
                }
                sql.Append("order by");
                sql.Append("  JOB_M.SCENARIO,");
                sql.Append("  JOB_M.EDA");

                // SQL取得
                if (DatabaseManager.Instance.pobjOraDb.pSelectOra(sql.ToString(), ref dt) == false)
                {
                    throw new Exception("ORACLE データ取得エラー");
                }
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }

            return dt;
        }

        /// <summary> 
        /// Main画面 検索条件付き
        /// </summary> 
        public static DataTable GetSearchJobList(string scenario, string jobId, string recv, string send)
        {

            DataTable dt = new DataTable();
            StringBuilder sql = new StringBuilder();

            try
            {
                // SQL作成
                sql.Append("select");
                sql.Append("  JOB_M.SCENARIO as SCENARIO,");
                sql.Append("  JOB_M.EDA as EDA,");
                sql.Append("  JOB_M.ID as ID,");
                sql.Append("  JOB_M.NAME as NAME,");
                sql.Append("  JOB_M.EXECUTION as EXECUTION,");
                sql.Append("  JOB_M.EXECCOMMNAD as EXECCOMMNAD,");
                sql.Append("  JOB_M.STATUS as STATUS,");
                sql.Append("  JOB_M.BEFOREJOB as BEFOREJOB,");
                sql.Append("  JOB_M.JOBBOOLEAN as JOBBOOLEAN,");
                sql.Append("  JOB_M.RECEIVE as RECEIVE,");
                sql.Append("  JOB_M.SEND as SEND,");
                sql.Append("  JOB_M.MEMO as MEMO ");
                sql.Append("from ");
                sql.Append("  JOB_MANEGMENT JOB_M ");
                sql.Append("left join ");
                sql.Append("  JOB_OWENREUSER JOB_O ");
                sql.Append("  on JOB_M.SCENARIO = JOB_O.SCENARIO ");
                sql.Append("  and JOB_M.EDA = JOB_O.EDA ");
                sql.Append("where");
                sql.Append("  JOB_M.RRSJFLG = 0 ");

                // 条件　シナリオ (複数検索)
                if (!string.IsNullOrEmpty(scenario))
                {
                    string[] scenarios = scenario.Split(',');

                    sql.Append($"  and JOB_M.SCENARIO in (");
                    // 「,」で複数件対応
                    for (int i = 0; i < scenarios.Count(); i++)
                    {
                        // 2回目以降、カンマ付与
                        if (i > 0) sql.Append($" ,");
                        sql.Append($"'{scenarios[i].Trim()}'");
                    }
                    sql.Append($") ");
                }

                // 条件　ジョブID (複数検索)
                if (!string.IsNullOrEmpty(jobId))
                {
                    string[] jobIds = jobId.Split(',');

                    sql.Append($"  and JOB_M.ID in (");
                    // 「,」で複数件対応
                    for (int i = 0; i < jobIds.Count(); i++)
                    {
                        // 2回目以降、カンマ付与
                        if (i > 0) sql.Append($" ,");
                        sql.Append($"'{jobIds[i].Trim()}'");
                    }
                    sql.Append($") ");
                }

                // 条件　受信先
                if (!string.IsNullOrEmpty(recv))
                {
                    sql.Append($"  and JOB_M.RECEIVE = '{recv}'");
                }
                // 条件　送信先
                if (!string.IsNullOrEmpty(send))
                {
                    sql.Append($"  and JOB_M.SEND = '{send}'");
                }

                sql.Append("order by");
                sql.Append("  JOB_M.SCENARIO,");
                sql.Append("  JOB_M.EDA");

                // SQL取得
                if (DatabaseManager.Instance.pobjOraDb.pSelectOra(sql.ToString(), ref dt) == false)
                {
                    throw new Exception("ORACLE データ取得エラー");
                }
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }

            return dt;
        }
        #endregion
    }
}

