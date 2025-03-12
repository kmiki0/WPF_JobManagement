using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DICSSLORA.ACmnFunc;
using DICSSLORA.ACmnIni;
using DICSSLORA.ACmnLog;
using DICSSLORA.ACmnOra;

namespace JobManagementApp.Manager
{
    public class DatabaseManager
    {
        public DICSSLORA.ACmnIni.clsMngIniFile pobjIniFile; // INIファイル
        public DICSSLORA.ACmnLog.clsMngLogFile pobjActLog; // 操作ログ
        public DICSSLORA.ACmnLog.clsMngLogFile pobjErrLog; // エラーログ
        
        //public DICSSLORA.ACmnOra.clsMngOracle pobjOraDb; // データベース

        private static DatabaseManager _instance;

        private static readonly object _lock = new object();

        public DICSSLORA.ACmnOra.clsMngOracle pobjOraDb { get; private set; }

        private DatabaseManager()
        {
            var pobjIniFile = new clsMngIniFile(clsDefineCnst.pcnstININAME);
            // 読み込みエラー
            pobjIniFile.pGetInfo();

            // 操作ログインスタンス生成 
            pobjActLog = LogFile.Instance;
            // エラーログインスタンス生成
            pobjErrLog = ErrLogFile.Instance;

            // ORACLEインスタンス生成 
            pobjOraDb = new DICSSLORA.ACmnOra.clsMngOracle(
                pobjIniFile.pGetItemString("DB", "DATA_SOURCE"),
                pobjIniFile.pGetItemString("DB", "USER_ID"),
                pobjIniFile.pGetItemString("DB", "PASSWORD"),
                pobjIniFile.pGetItemInt("DB", "RTRY_SLEEP"),
                pobjIniFile.pGetItemInt("DB", "RTRY_CNT"),
                pobjActLog, pobjErrLog, clsMngOracle.peOraCom.ODP);
        }

        // インスタンスを取得するためのプロパティ
        public static DatabaseManager Instance
        {
            get
            {
                lock (_lock)
                {
                    return _instance ?? new DatabaseManager();
                }
            }
        }
    }

}
