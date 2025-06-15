using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DICSSLORA.ACmnFunc;
using DICSSLORA.ACmnIni;
using DICSSLORA.ACmnLog;

namespace JobManagementApp.Manager
{
    public class LogFile
    {
        private static clsMngLogFile _instance;
        private static readonly object _lock = new object();

        public static clsMngLogFile Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "nomal.log");
                        if (!File.Exists(logPath)) using (File.Create(logPath)) { }
                        _instance = new clsMngLogFile(logPath, 5);;
                    }
                    return _instance;
                }
            }
        }

        public static void WriteLog(string message)
        {
            Instance.pWriteLog( clsMngLogFile.peLogLevel.Level3, "JobApp", "JobApp", "clsMngOracle", message );
        }
    }

    public class ErrLogFile
    {
        private static clsMngLogFile _instance;
        private static readonly object _lock = new object();

        public static clsMngLogFile Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
                        if (!File.Exists(logPath)) using (File.Create(logPath)) { }
                        _instance = new clsMngLogFile(logPath, 5);;
                    }
                    return _instance;
                }
            }
        }

        public static void WriteLog(string message)
        {
            Instance.pWriteLog( clsMngLogFile.peLogLevel.Level3, "JobApp", "Error", "clsMngOracle", message );
        }
    }

}
