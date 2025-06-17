using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JobManagementApp.Configuration;
using JobManagementApp.Manager;
using JobManagementApp.Models;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;

namespace JobManagementApp.Services
{
    /// <summary>
    /// SQLまとめ - Oracle.ManagedDataAccess.Client対応版
    /// </summary>
    public static class JobService
    {
        #region 運用処理管理R

        /// <summary> 
        /// シナリオと枝番から、運用処理管理Rの情報取得
        /// </summary> 
        public static DataTable GetUnyoData(List<JobUnyoCtlModel> args, string fromDate, string toDate)
        {
            DataTable dt = new DataTable();

            // 入力検証
            if (args == null || args.Count <= 0) 
            {
                ErrLogFile.WriteLog("GetUnyoData: 引数が無効です");
                return new DataTable();
            }

            if (string.IsNullOrWhiteSpace(fromDate) || string.IsNullOrWhiteSpace(toDate))
            {
                ErrLogFile.WriteLog("GetUnyoData: 日付パラメータが無効です");
                return new DataTable();
            }

            // 日付フォーマット検証
            if (!IsValidDateFormat(fromDate) || !IsValidDateFormat(toDate))
            {
                ErrLogFile.WriteLog("GetUnyoData: 日付フォーマットが無効です");
                return new DataTable();
            }

            try
            {
                var sql = @"
                    WITH JOBID_UPDDT AS (
                        SELECT 
                            UNYO.JOBID,
                            MAX(UNYO.UPDDT) AS UPDDT,
                            JOB_M.SCENARIO,
                            JOB_M.EDA
                        FROM L1_UNYOCTL UNYO 
                        LEFT JOIN JOB_MANEGMENT JOB_M ON UNYO.JOBID = JOB_M.ID 
                        WHERE UNYO.UPDDT BETWEEN TO_DATE(:fromDate, 'YYYY/MM/DD HH24:MI') 
                                            AND TO_DATE(:toDate, 'YYYY/MM/DD HH24:MI')
                        AND ({0})
                        GROUP BY UNYO.JOBID, JOB_M.SCENARIO, JOB_M.EDA
                        UNION ALL
                        SELECT 
                            UNYO.JOBID,
                            MAX(UNYO.UPDDT) AS UPDDT,
                            JOB_M.SCENARIO,
                            JOB_M.EDA
                        FROM L1_UNYOCTL UNYO 
                        LEFT JOIN JOB_MANEGMENT JOB_M ON UNYO.JOBID = JOB_M.ID 
                        WHERE UNYO.UPDDT >= TO_DATE(:fromDate, 'YYYY/MM/DD HH24:MI') 
                        AND ({0})
                        GROUP BY UNYO.JOBID, JOB_M.SCENARIO, JOB_M.EDA
                    )
                    SELECT 
                        UNYO.JOBID,
                        UNYO.SYRFLG,
                        TO_CHAR(UNYO.UPDDT, 'YYYY/MM/DD HH24:MI:SS') AS UPDDT,
                        JU.SCENARIO,
                        JU.EDA
                    FROM L1_UNYOCTL UNYO 
                    INNER JOIN JOBID_UPDDT JU ON UNYO.JOBID = JU.JOBID 
                                            AND UNYO.UPDDT = JU.UPDDT";

                var parameters = new List<OracleParameter>
                {
                    new OracleParameter("fromDate", OracleDbType.Varchar2, fromDate, ParameterDirection.Input),
                    new OracleParameter("toDate", OracleDbType.Varchar2, toDate, ParameterDirection.Input)
                };

                // 動的条件を構築
                var conditionParts = new List<string>();
                for (int i = 0; i < args.Count; i++)
                {
                    var scenario = args[i].Scenario?.Trim() ?? "";
                    var eda = args[i].Eda?.Trim() ?? "";
                    
                    // 入力検証
                    if (!IsValidScenario(scenario) || !IsValidEda(eda))
                    {
                        ErrLogFile.WriteLog($"GetUnyoData: 無効なシナリオまたは枝番 - {scenario}/{eda}");
                        continue;
                    }

                    var scenarioParam = $"scenario{i}";
                    var edaParam = $"eda{i}";
                    
                    conditionParts.Add($"(JOB_M.SCENARIO = :{scenarioParam} AND JOB_M.EDA = :{edaParam})");
                    
                    parameters.Add(new OracleParameter(scenarioParam, OracleDbType.Varchar2, scenario, ParameterDirection.Input));
                    parameters.Add(new OracleParameter(edaParam, OracleDbType.Varchar2, eda, ParameterDirection.Input));
                }

                if (conditionParts.Count == 0)
                {
                    ErrLogFile.WriteLog("GetUnyoData: 有効な検索条件がありません");
                    return new DataTable();
                }

                // 最終的なSQLを構築
                var finalSql = string.Format(sql, string.Join(" OR ", conditionParts));

                if (!DatabaseManager.Instance.ExecuteSelect(finalSql, parameters, ref dt))
                {
                    throw new Exception("ORACLE データ取得エラー");
                }

                LogFile.WriteLog($"GetUnyoData: {dt.Rows.Count}件のデータを取得しました");
            }
            catch (Exception e)
            {
                ErrLogFile.WriteLog($"GetUnyoData エラー: {e.Message}");
                throw new Exception($"運用処理管理Rデータ取得エラー: {e.Message}");
            }

            return dt;
        }

        /// <summary>
        /// ジョブ管理からJOBIDを取得し、運用処理管理RのSEQが大きいもので更新
        /// </summary>
        public static bool UpdateUnyoCtrl(string scenario, string eda)
        {
            // 入力検証
            if (!IsValidScenario(scenario) || !IsValidEda(eda))
            {
                ErrLogFile.WriteLog($"UpdateUnyoCtrl: 無効なパラメータ - {scenario}/{eda}");
                return false;
            }

            try
            {
                var sql = @"
                    UPDATE L1_UNYOCTL
                    SET SYRFLG = 0,
                        UPDDT = SYSDATE
                    WHERE SEQ = (
                        SELECT MAX(UNYO.SEQ)
                        FROM L1_UNYOCTL UNYO
                        JOIN JOB_MANEGMENT JOB_M ON UNYO.JOBID = JOB_M.ID
                        WHERE JOB_M.SCENARIO = :scenario
                        AND JOB_M.EDA = :eda
                    )";

                var parameters = new List<OracleParameter>
                {
                    new OracleParameter("scenario", OracleDbType.Varchar2, scenario.Trim(), ParameterDirection.Input),
                    new OracleParameter("eda", OracleDbType.Varchar2, eda.Trim(), ParameterDirection.Input)
                };

                if (!DatabaseManager.Instance.ExecuteNonQuery(sql, parameters))
                {
                    throw new Exception("運用処理管理Rの更新に失敗しました");
                }

                LogFile.WriteLog($"UpdateUnyoCtrl: 正常に更新しました - {scenario}/{eda}");
                return true;
            }
            catch (Exception e)
            {
                ErrLogFile.WriteLog($"UpdateUnyoCtrl エラー: {e.Message}");
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

            // 入力検証
            if (!IsValidScenario(scenario))
            {
                ErrLogFile.WriteLog($"GetMaxEda: 無効なシナリオ - {scenario}");
                return dt;
            }

            try
            {
                var sql = @"
                    SELECT MAX(EDA) AS EDA 
                    FROM JOB_MANEGMENT 
                    WHERE SCENARIO = :scenario 
                    AND RRSJFLG = 0
                    GROUP BY SCENARIO";

                var parameters = new List<OracleParameter>
                {
                    new OracleParameter("scenario", OracleDbType.Varchar2, scenario.Trim(), ParameterDirection.Input)
                };

                if (!DatabaseManager.Instance.ExecuteSelect(sql, parameters, ref dt))
                {
                    throw new Exception("ORACLE データ取得エラー");
                }

                LogFile.WriteLog($"GetMaxEda: シナリオ {scenario} の最大枝番を取得しました");
            }
            catch (Exception e)
            {
                ErrLogFile.WriteLog($"GetMaxEda エラー: {e.Message}");
                throw new Exception($"最大枝番取得エラー: {e.Message}");
            }

            return dt;
        }

        /// <summary>
        /// シナリオと枝番から、ジョブ管理を取得
        /// </summary>
        public static DataTable GetJobManegment(string scenario, string eda)
        {
            DataTable dt = new DataTable();

            // 入力検証
            if (!IsValidScenario(scenario) || !IsValidEda(eda))
            {
                ErrLogFile.WriteLog($"GetJobManegment: 無効なパラメータ - {scenario}/{eda}");
                return dt;
            }

            try
            {
                var sql = @"
                    SELECT 
                        SCENARIO, EDA, ID, NAME, EXECUTION, EXECCOMMNAD, 
                        STATUS, BEFOREJOB, JOBBOOLEAN, RECEIVE, SEND, MEMO, FROMSERVER
                    FROM JOB_MANEGMENT 
                    WHERE RRSJFLG = 0 
                    AND SCENARIO = :scenario 
                    AND EDA = :eda";

                var parameters = new List<OracleParameter>
                {
                    new OracleParameter("scenario", OracleDbType.Varchar2, scenario.Trim(), ParameterDirection.Input),
                    new OracleParameter("eda", OracleDbType.Varchar2, eda.Trim(), ParameterDirection.Input)
                };

                if (!DatabaseManager.Instance.ExecuteSelect(sql, parameters, ref dt))
                {
                    throw new Exception("ORACLE データ取得エラー");
                }
            }
            catch (Exception e)
            {
                ErrLogFile.WriteLog($"GetJobManegment エラー: {e.Message}");
                throw new Exception($"ジョブ管理データ取得エラー: {e.Message}");
            }

            return dt;
        }

        /// <summary>
        /// テーブル型のパラメータを元に、更新, 登録を実行する
        /// </summary>
        public static bool UpdateJobManegment(JobManegment job)
        {
            // 入力検証
            var validationResult = ValidateJobManegment(job);
            if (!validationResult.IsValid)
            {
                ErrLogFile.WriteLog($"UpdateJobManegment: 入力検証エラー - {string.Join(", ", validationResult.Errors)}");
                return false;
            }

            try
            {
                var sql = @"
                    MERGE INTO JOB_MANEGMENT JM
                    USING (SELECT :scenario AS SCENARIO, :eda AS EDA FROM dual) TMP
                    ON (JM.SCENARIO = TMP.SCENARIO AND JM.EDA = TMP.EDA AND JM.RRSJFLG = 0)
                    WHEN MATCHED THEN
                        UPDATE SET
                            JM.ID = :id,
                            JM.NAME = :name,
                            JM.EXECUTION = :execution,
                            JM.EXECCOMMNAD = :execcommnad,
                            JM.STATUS = :status,
                            JM.BEFOREJOB = :beforejob,
                            JM.JOBBOOLEAN = :jobboolean,
                            JM.RECEIVE = :receive,
                            JM.SEND = :send,
                            JM.MEMO = :memo,
                            JM.FROMSERVER = :fromserver
                    WHEN NOT MATCHED THEN
                        INSERT (SCENARIO, EDA, ID, NAME, EXECUTION, EXECCOMMNAD, STATUS, BEFOREJOB, JOBBOOLEAN, RECEIVE, SEND, MEMO, FROMSERVER, RRSJFLG)
                        VALUES (:scenario, :eda, :id, :name, :execution, :execcommnad, :status, :beforejob, :jobboolean, :receive, :send, :memo, :fromserver, 0)";

                var parameters = new List<OracleParameter>
                {
                    new OracleParameter("scenario", OracleDbType.Varchar2, job.SCENARIO?.Trim() ?? "", ParameterDirection.Input),
                    new OracleParameter("eda", OracleDbType.Varchar2, job.EDA?.Trim() ?? "", ParameterDirection.Input),
                    new OracleParameter("id", OracleDbType.Varchar2, job.ID?.Trim() ?? "", ParameterDirection.Input),
                    new OracleParameter("name", OracleDbType.Varchar2, job.NAME?.Trim() ?? "", ParameterDirection.Input),
                    new OracleParameter("execution", OracleDbType.Int32, job.EXECUTION, ParameterDirection.Input),
                    new OracleParameter("execcommnad", OracleDbType.Varchar2, job.EXECCOMMNAD?.Trim() ?? "", ParameterDirection.Input),
                    new OracleParameter("status", OracleDbType.Int32, job.STATUS, ParameterDirection.Input),
                    new OracleParameter("beforejob", OracleDbType.Varchar2, job.BEFOREJOB?.Trim() ?? "", ParameterDirection.Input),
                    new OracleParameter("jobboolean", OracleDbType.Int32, job.JOBBOOLEAN, ParameterDirection.Input),
                    new OracleParameter("receive", OracleDbType.Varchar2, job.RECEIVE?.Trim() ?? "", ParameterDirection.Input),
                    new OracleParameter("send", OracleDbType.Varchar2, job.SEND?.Trim() ?? "", ParameterDirection.Input),
                    new OracleParameter("memo", OracleDbType.Varchar2, job.MEMO?.Trim() ?? "", ParameterDirection.Input),
                    new OracleParameter("fromserver", OracleDbType.Varchar2, job.FROMSERVER?.Trim() ?? "", ParameterDirection.Input)
                };

                if (!DatabaseManager.Instance.ExecuteNonQuery(sql, parameters))
                {
                    throw new Exception("ジョブ管理の更新に失敗しました");
                }

                LogFile.WriteLog($"UpdateJobManegment: {job.SCENARIO}/{job.EDA} を正常に更新しました");
                return true;
            }
            catch (Exception e)
            {
                ErrLogFile.WriteLog($"UpdateJobManegment エラー: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// ジョブ管理 論理削除
        /// </summary>
        public static bool DeleteJobManegment(string scenario, string eda)
        {
            // 入力検証
            if (!IsValidScenario(scenario) || !IsValidEda(eda))
            {
                ErrLogFile.WriteLog($"DeleteJobManegment: 無効なパラメータ - {scenario}/{eda}");
                return false;
            }

            try
            {
                var sql = @"
                    UPDATE JOB_MANEGMENT
                    SET RRSJFLG = 1,
                        UPDDT = SYSDATE
                    WHERE SCENARIO = :scenario
                    AND EDA = :eda
                    AND RRSJFLG = 0";

                var parameters = new List<OracleParameter>
                {
                    new OracleParameter("scenario", OracleDbType.Varchar2, scenario.Trim(), ParameterDirection.Input),
                    new OracleParameter("eda", OracleDbType.Varchar2, eda.Trim(), ParameterDirection.Input)
                };

                if (!DatabaseManager.Instance.ExecuteNonQuery(sql, parameters))
                {
                    throw new Exception("ジョブ管理の削除に失敗しました");
                }

                LogFile.WriteLog($"DeleteJobManegment: {scenario}/{eda} を正常に削除しました");
                return true;
            }
            catch (Exception e)
            {
                ErrLogFile.WriteLog($"DeleteJobManegment エラー: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 受信先と送信先の一意のリストを取得
        /// </summary>
        public static DataTable GetDistinctForRecvSend()
        {
            DataTable dt = new DataTable();

            try
            {
                var sql = @"
                    SELECT DISTINCT 
                        'RECV' AS Type,
                        RECEIVE AS VAL 
                    FROM JOB_MANEGMENT 
                    WHERE RRSJFLG = 0 
                    AND RECEIVE IS NOT NULL 
                    AND LENGTH(TRIM(RECEIVE)) > 0
                    UNION 
                    SELECT DISTINCT 
                        'SEND' AS Type,
                        SEND AS VAL 
                    FROM JOB_MANEGMENT 
                    WHERE RRSJFLG = 0 
                    AND SEND IS NOT NULL 
                    AND LENGTH(TRIM(SEND)) > 0
                    ORDER BY Type, VAL";

                var parameters = new List<OracleParameter>();

                if (!DatabaseManager.Instance.ExecuteSelect(sql, parameters, ref dt))
                {
                    throw new Exception("ORACLE データ取得エラー");
                }
            }
            catch (Exception e)
            {
                ErrLogFile.WriteLog($"GetDistinctForRecvSend エラー: {e.Message}");
                throw new Exception($"受信先・送信先データ取得エラー: {e.Message}");
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

            // 入力検証
            if (!IsValidScenario(scenario) || !IsValidEda(eda) || 
                string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(filePath))
            {
                ErrLogFile.WriteLog($"GetJobLinkFile: 無効なパラメータ - {scenario}/{eda}/{fileName}/{filePath}");
                return dt;
            }

            try
            {
                var sql = @"
                    SELECT
                        JM.ID AS JOBID,
                        JL.SCENARIO,
                        JL.EDA,
                        JL.FILENAME,
                        JL.FILEPATH,
                        JL.FILETYPE,
                        JL.FILECOUNT,
                        JL.OBSERVERTYPE
                    FROM JOB_LINKFILE JL
                    LEFT JOIN JOB_MANEGMENT JM ON JL.SCENARIO = JM.SCENARIO 
                                              AND JL.EDA = JM.EDA
                                              AND JM.RRSJFLG = 0
                    WHERE JL.SCENARIO = :scenario 
                    AND JL.EDA = :eda 
                    AND JL.FILENAME = :filename
                    AND JL.FILEPATH = :filepath";

                var parameters = new List<OracleParameter>
                {
                    new OracleParameter("scenario", OracleDbType.Varchar2, scenario.Trim(), ParameterDirection.Input),
                    new OracleParameter("eda", OracleDbType.Varchar2, eda.Trim(), ParameterDirection.Input),
                    new OracleParameter("filename", OracleDbType.Varchar2, fileName.Trim(), ParameterDirection.Input),
                    new OracleParameter("filepath", OracleDbType.Varchar2, filePath.Trim(), ParameterDirection.Input)
                };

                if (!DatabaseManager.Instance.ExecuteSelect(sql, parameters, ref dt))
                {
                    throw new Exception("ORACLE データ取得エラー");
                }

                LogFile.WriteLog($"GetJobLinkFile: {scenario}/{eda}/{fileName} のファイル情報を取得しました");
            }
            catch (Exception e)
            {
                ErrLogFile.WriteLog($"GetJobLinkFile エラー: {e.Message}");
                throw new Exception($"ジョブ関連ファイルデータ取得エラー: {e.Message}");
            }

            return dt;
        }

        /// <summary>
        /// シナリオと枝番から、ジョブ周辺ファイル データを取得
        /// </summary>
        public static DataTable GetJobLinkFile(string scenario, string eda)
        {
            DataTable dt = new DataTable();

            // 入力検証
            if (!IsValidScenario(scenario) || !IsValidEda(eda))
            {
                ErrLogFile.WriteLog($"GetJobLinkFile: 無効なパラメータ - {scenario}/{eda}");
                return dt;
            }

            try
            {
                var sql = @"
                    SELECT
                        JM.ID,
                        JL.SCENARIO,
                        JL.EDA,
                        JL.FILENAME,
                        JL.FILEPATH,
                        JL.FILETYPE,
                        JL.FILECOUNT,
                        JL.OBSERVERTYPE
                    FROM JOB_LINKFILE JL 
                    LEFT JOIN JOB_MANEGMENT JM ON JL.SCENARIO = JM.SCENARIO 
                                              AND JL.EDA = JM.EDA 
                                              AND JM.RRSJFLG = 0
                    WHERE JL.SCENARIO = :scenario 
                    AND JL.EDA = :eda 
                    ORDER BY JL.FILETYPE, JL.FILENAME";

                var parameters = new List<OracleParameter>
                {
                    new OracleParameter("scenario", OracleDbType.Varchar2, scenario.Trim(), ParameterDirection.Input),
                    new OracleParameter("eda", OracleDbType.Varchar2, eda.Trim(), ParameterDirection.Input)
                };

                if (!DatabaseManager.Instance.ExecuteSelect(sql, parameters, ref dt))
                {
                    throw new Exception("ORACLE データ取得エラー");
                }

                LogFile.WriteLog($"GetJobLinkFile: {scenario}/{eda} の関連ファイル {dt.Rows.Count}件を取得しました");
            }
            catch (Exception e)
            {
                ErrLogFile.WriteLog($"GetJobLinkFile エラー: {e.Message}");
                throw new Exception($"ジョブ関連ファイルリスト取得エラー: {e.Message}");
            }

            return dt;
        }

        /// <summary>
        /// ジョブ関連ファイルを新規登録
        /// </summary>
        public static bool UpdateJobLinkFile(JobLinkFile job)
        {
            // 入力検証
            var validationResult = ValidateJobLinkFile(job);
            if (!validationResult.IsValid)
            {
                ErrLogFile.WriteLog($"UpdateJobLinkFile: 入力検証エラー - {string.Join(", ", validationResult.Errors)}");
                return false;
            }

            try
            {
                var sql = @"
                    INSERT INTO JOB_LINKFILE (
                        SCENARIO, EDA, FILENAME, FILEPATH, FILETYPE, FILECOUNT, OBSERVERTYPE
                    ) VALUES (
                        :scenario, :eda, :filename, :filepath, :filetype, :filecount, :observertype
                    )";

                var parameters = new List<OracleParameter>
                {
                    new OracleParameter("scenario", OracleDbType.Varchar2, job.SCENARIO?.Trim() ?? "", ParameterDirection.Input),
                    new OracleParameter("eda", OracleDbType.Varchar2, job.EDA?.Trim() ?? "", ParameterDirection.Input),
                    new OracleParameter("filename", OracleDbType.Varchar2, job.FILENAME?.Trim() ?? "", ParameterDirection.Input),
                    new OracleParameter("filepath", OracleDbType.Varchar2, job.FILEPATH?.Trim() ?? "", ParameterDirection.Input),
                    new OracleParameter("filetype", OracleDbType.Int32, job.FILETYPE, ParameterDirection.Input),
                    new OracleParameter("filecount", OracleDbType.Int32, job.FILECOUNT, ParameterDirection.Input),
                    new OracleParameter("observertype", OracleDbType.Int32, job.OBSERVERTYPE, ParameterDirection.Input)
                };

                if (!DatabaseManager.Instance.ExecuteNonQuery(sql, parameters))
                {
                    throw new Exception("ジョブ関連ファイルの登録に失敗しました");
                }

                LogFile.WriteLog($"UpdateJobLinkFile: {job.SCENARIO}/{job.EDA}/{job.FILENAME} を正常に登録しました");
                return true;
            }
            catch (Exception e)
            {
                ErrLogFile.WriteLog($"UpdateJobLinkFile エラー: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// ジョブ関連ファイル　物理削除
        /// </summary>
        public static bool DeleteJobLinkFile(JobLinkFile job)
        {
            // 入力検証
            if (!IsValidScenario(job?.SCENARIO) || !IsValidEda(job?.EDA) ||
                string.IsNullOrWhiteSpace(job?.OLDFILENAME) || string.IsNullOrWhiteSpace(job?.OLDFILEPATH))
            {
                ErrLogFile.WriteLog("DeleteJobLinkFile: 無効なパラメータです");
                return false;
            }

            try
            {
                var sql = @"
                    DELETE FROM JOB_LINKFILE
                    WHERE SCENARIO = :scenario
                    AND EDA = :eda
                    AND FILENAME = :oldfilename
                    AND FILEPATH = :oldfilepath";

                var parameters = new List<OracleParameter>
                {
                    new OracleParameter("scenario", OracleDbType.Varchar2, job.SCENARIO.Trim(), ParameterDirection.Input),
                    new OracleParameter("eda", OracleDbType.Varchar2, job.EDA.Trim(), ParameterDirection.Input),
                    new OracleParameter("oldfilename", OracleDbType.Varchar2, job.OLDFILENAME.Trim(), ParameterDirection.Input),
                    new OracleParameter("oldfilepath", OracleDbType.Varchar2, job.OLDFILEPATH.Trim(), ParameterDirection.Input)
                };

                if (!DatabaseManager.Instance.ExecuteNonQuery(sql, parameters))
                {
                    throw new Exception("ジョブ関連ファイルの削除に失敗しました");
                }

                LogFile.WriteLog($"DeleteJobLinkFile: {job.SCENARIO}/{job.EDA}/{job.OLDFILENAME} を正常に削除しました");
                return true;
            }
            catch (Exception e)
            {
                ErrLogFile.WriteLog($"DeleteJobLinkFile エラー: {e.Message}");
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
            DataTable dt = new DataTable();

            try
            {
                var sql = @"
                    SELECT
                        JOB_M.SCENARIO,
                        JOB_M.EDA,
                        JOB_M.ID,
                        JOB_M.NAME,
                        JOB_M.EXECUTION,
                        JOB_M.EXECCOMMNAD,
                        JOB_M.STATUS,
                        JOB_M.BEFOREJOB,
                        JOB_M.JOBBOOLEAN,
                        JOB_M.RECEIVE,
                        JOB_M.SEND,
                        JOB_M.MEMO
                    FROM JOB_MANEGMENT JOB_M 
                    LEFT JOIN JOB_OWENREUSER JOB_O ON JOB_M.SCENARIO = JOB_O.SCENARIO 
                                                   AND JOB_M.EDA = JOB_O.EDA 
                    WHERE JOB_M.RRSJFLG = 0";

                var parameters = new List<OracleParameter>();

                // ユーザーが設定されている場合、条件追加
                //if (!string.IsNullOrWhiteSpace(userId) && IsValidUserId(userId))
                //{
                //    sql += " AND JOB_O.USERID = :userid";
                //    parameters.Add(new OracleParameter("userid", OracleDbType.Varchar2, userId.Trim(), ParameterDirection.Input));
                //}

                sql += @"
                    ORDER BY JOB_M.SCENARIO, JOB_M.EDA";

                if (!DatabaseManager.Instance.ExecuteSelect(sql, parameters, ref dt))
                {
                    throw new Exception("ORACLE データ取得エラー");
                }
            }
            catch (Exception e)
            {
                ErrLogFile.WriteLog($"LoadJobs エラー: {e.Message}");
                throw new Exception($"ジョブリスト読み込みエラー: {e.Message}");
            }

            return dt;
        }

        /// <summary>
        /// Main画面 検索条件付き
        /// </summary>
        public static DataTable GetSearchJobList(string scenario, string jobId, string recv, string send)
        {
            DataTable dt = new DataTable();

            try
            {
                var sql = @"
                    SELECT
                        JOB_M.SCENARIO,
                        JOB_M.EDA,
                        JOB_M.ID,
                        JOB_M.NAME,
                        JOB_M.EXECUTION,
                        JOB_M.EXECCOMMNAD,
                        JOB_M.STATUS,
                        JOB_M.BEFOREJOB,
                        JOB_M.JOBBOOLEAN,
                        JOB_M.RECEIVE,
                        JOB_M.SEND,
                        JOB_M.MEMO
                    FROM JOB_MANEGMENT JOB_M 
                    LEFT JOIN JOB_OWENREUSER JOB_O ON JOB_M.SCENARIO = JOB_O.SCENARIO 
                                                   AND JOB_M.EDA = JOB_O.EDA 
                    WHERE JOB_M.RRSJFLG = 0";

                var parameters = new List<OracleParameter>();

                // 条件　シナリオ (複数検索)
                if (!string.IsNullOrWhiteSpace(scenario))
                {
                    var scenarios = scenario.Replace(',', ' ').Split(' ')
                        .Where(s => !string.IsNullOrWhiteSpace(s) && IsValidScenario(s.Trim()))
                        .Select(s => s.Trim())
                        .ToArray();

                    if (scenarios.Length > 0)
                    {
                        var scenarioConditions = new List<string>();
                        for (int i = 0; i < scenarios.Length; i++)
                        {
                            var paramName = $"scenario{i}";
                            scenarioConditions.Add($"JOB_M.SCENARIO = :{paramName}");
                            parameters.Add(new OracleParameter(paramName, OracleDbType.Varchar2, scenarios[i], ParameterDirection.Input));
                        }
                        sql += $" AND ({string.Join(" OR ", scenarioConditions)})";
                    }
                }

                // 条件　ジョブID (複数検索)
                if (!string.IsNullOrWhiteSpace(jobId))
                {
                    var jobIds = jobId.Replace(',', ' ').Split(' ')
                        .Where(j => !string.IsNullOrWhiteSpace(j) && IsValidJobId(j.Trim()))
                        .Select(j => j.Trim())
                        .ToArray();

                    if (jobIds.Length > 0)
                    {
                        var jobIdConditions = new List<string>();
                        for (int i = 0; i < jobIds.Length; i++)
                        {
                            var paramName = $"jobid{i}";
                            jobIdConditions.Add($"JOB_M.ID = :{paramName}");
                            parameters.Add(new OracleParameter(paramName, OracleDbType.Varchar2, jobIds[i], ParameterDirection.Input));
                        }
                        sql += $" AND ({string.Join(" OR ", jobIdConditions)})";
                    }
                }

                // 条件　受信先
                if (!string.IsNullOrWhiteSpace(recv) && IsValidRecvSend(recv))
                {
                    sql += " AND JOB_M.RECEIVE = :recv";
                    parameters.Add(new OracleParameter("recv", OracleDbType.Varchar2, recv.Trim(), ParameterDirection.Input));
                }

                // 条件　送信先
                if (!string.IsNullOrWhiteSpace(send) && IsValidRecvSend(send))
                {
                    sql += " AND JOB_M.SEND = :send";
                    parameters.Add(new OracleParameter("send", OracleDbType.Varchar2, send.Trim(), ParameterDirection.Input));
                }

                sql += " ORDER BY JOB_M.SCENARIO, JOB_M.EDA";

                if (!DatabaseManager.Instance.ExecuteSelect(sql, parameters, ref dt))
                {
                    throw new Exception("ORACLE データ取得エラー");
                }

                LogFile.WriteLog($"GetSearchJobList: {dt.Rows.Count}件のジョブを検索しました");
            }
            catch (Exception e)
            {
                ErrLogFile.WriteLog($"GetSearchJobList エラー: {e.Message}");
                throw new Exception($"ジョブ検索エラー: {e.Message}");
            }

            return dt;
        }

        /// <summary>
        /// 利用可能なデータベース表示情報一覧を取得
        /// </summary>
        public static DatabaseDisplayInfo[] GetAvailableDatabaseDisplayInfos()
        {
            try
            {
                var configManager = DatabaseConfigurationManager.Instance;
                var allDatabases = configManager.GetAllDatabases();
                
                var displayInfos = new List<DatabaseDisplayInfo>();
                
                foreach (var db in allDatabases)
                {
                    var displayInfo = new DatabaseDisplayInfo
                    {
                        Name = db.Name,
                        Address = db.Address,
                        Schema = db.Schema ?? "未設定",
                        DisplayText = $"IP : {db.Address},  Table : {(string.IsNullOrEmpty(db.Schema) ? "未設定" : db.Schema)}"
                    };
                    
                    displayInfos.Add(displayInfo);
                }
                
                LogFile.WriteLog($"GetAvailableDatabaseDisplayInfos: {displayInfos.Count}件のデータベース表示情報を取得しました");
                return displayInfos.ToArray();
            }
            catch (Exception e)
            {
                ErrLogFile.WriteLog($"GetAvailableDatabaseDisplayInfos エラー: {e.Message}");
                return new DatabaseDisplayInfo[0];
            }
        }

        #endregion

        #region 入力検証メソッド

        /// <summary>
        /// シナリオの妥当性をチェック
        /// </summary>
        private static bool IsValidScenario(string scenario)
        {
            if (string.IsNullOrWhiteSpace(scenario))
                return false;

            var trimmed = scenario.Trim();
            
            // 長さチェック（通常のシナリオIDは6文字程度）
            if (trimmed.Length > 20)
                return false;

            // 英数字とハイフンのみ許可
            return System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[A-Za-z0-9\-]+$");
        }

        /// <summary>
        /// 枝番の妥当性をチェック
        /// </summary>
        private static bool IsValidEda(string eda)
        {
            if (string.IsNullOrWhiteSpace(eda))
                return false;

            var trimmed = eda.Trim();
            
            // 数値チェック
            if (!int.TryParse(trimmed, out int value))
                return false;

            // 範囲チェック（1-9999）
            return value >= 1 && value <= 9999;
        }

        /// <summary>
        /// ジョブIDの妥当性をチェック
        /// </summary>
        private static bool IsValidJobId(string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobId))
                return false;

            var trimmed = jobId.Trim();
            
            // 長さチェック
            if (trimmed.Length > 50)
                return false;

            // 英数字、ハイフン、アンダースコアのみ許可
            return System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[A-Za-z0-9\-_]+$");
        }

        /// <summary>
        /// ユーザーIDの妥当性をチェック
        /// </summary>
        private static bool IsValidUserId(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return false;

            var trimmed = userId.Trim();
            
            // 長さチェック
            if (trimmed.Length > 30)
                return false;

            // 英数字のみ許可
            return System.Text.RegularExpressions.Regex.IsMatch(trimmed, @"^[A-Za-z0-9]+$");
        }

        /// <summary>
        /// 受信先・送信先の妥当性をチェック
        /// </summary>
        private static bool IsValidRecvSend(string recvSend)
        {
            if (string.IsNullOrWhiteSpace(recvSend))
                return false;

            var trimmed = recvSend.Trim();
            
            // 長さチェック
            if (trimmed.Length > 100)
                return false;

            // 基本的な文字のみ許可（日本語含む）
            return !ContainsDangerousCharacters(trimmed);
        }

        /// <summary>
        /// 日付フォーマットの妥当性をチェック
        /// </summary>
        private static bool IsValidDateFormat(string date)
        {
            return DateTime.TryParseExact(date, "yyyy/MM/dd HH:mm", null, 
                System.Globalization.DateTimeStyles.None, out _);
        }

        /// <summary>
        /// 危険な文字が含まれているかチェック
        /// </summary>
        private static bool ContainsDangerousCharacters(string input)
        {
            if (string.IsNullOrEmpty(input))
                return false;

            var dangerousPatterns = new[] { 
                "'", "\"", ";", "--", "/*", "*/", 
                "DROP", "DELETE", "INSERT", "UPDATE", "TRUNCATE", "ALTER",
                "EXEC", "EXECUTE", "UNION", "SELECT", "xp_", "sp_"
            };
            
            var upperInput = input.ToUpper();
            return dangerousPatterns.Any(pattern => upperInput.Contains(pattern));
        }

        /// <summary>
        /// JobManegmentオブジェクトの妥当性をチェック
        /// </summary>
        private static ValidationResult ValidateJobManegment(JobManegment job)
        {
            var errors = new List<string>();

            if (job == null)
            {
                errors.Add("ジョブオブジェクトがnullです");
                return new ValidationResult(false, errors);
            }

            if (!IsValidScenario(job.SCENARIO))
                errors.Add("シナリオが無効です");

            if (!IsValidEda(job.EDA))
                errors.Add("枝番が無効です");

            if (!IsValidJobId(job.ID))
                errors.Add("ジョブIDが無効です");

            if (string.IsNullOrWhiteSpace(job.NAME) || job.NAME.Length > 100)
                errors.Add("ジョブ名が無効です（1-100文字）");

            if (job.EXECUTION < 0 || job.EXECUTION > 10)
                errors.Add("実行方法が無効です");

            if (job.STATUS < 0 || job.STATUS > 10)
                errors.Add("ステータスが無効です");

            if (job.JOBBOOLEAN < 0 || job.JOBBOOLEAN > 1)
                errors.Add("ジョブ実行可否が無効です");

            if (!string.IsNullOrEmpty(job.FROMSERVER) && job.FROMSERVER.Length > 50)
                errors.Add("検索先データベース名が無効です（50文字以内）");

            return new ValidationResult(errors.Count == 0, errors);
        }

        /// <summary>
        /// JobLinkFileオブジェクトの妥当性をチェック
        /// </summary>
        private static ValidationResult ValidateJobLinkFile(JobLinkFile job)
        {
            var errors = new List<string>();

            if (job == null)
            {
                errors.Add("ジョブファイルオブジェクトがnullです");
                return new ValidationResult(false, errors);
            }

            if (!IsValidScenario(job.SCENARIO))
                errors.Add("シナリオが無効です");

            if (!IsValidEda(job.EDA))
                errors.Add("枝番が無効です");

            if (string.IsNullOrWhiteSpace(job.FILENAME) || job.FILENAME.Length > 255)
                errors.Add("ファイル名が無効です");

            if (string.IsNullOrWhiteSpace(job.FILEPATH) || job.FILEPATH.Length > 500)
                errors.Add("ファイルパスが無効です");

            if (job.FILETYPE < 0 || job.FILETYPE > 10)
                errors.Add("ファイルタイプが無効です");

            if (job.FILECOUNT < 1 || job.FILECOUNT > 100)
                errors.Add("ファイル数が無効です");

            return new ValidationResult(errors.Count == 0, errors);
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