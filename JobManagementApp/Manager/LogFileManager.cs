using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobManagementApp.Manager
{
    public class LogFile
    {
        private static LogFile _instance;
        private static readonly object _lock = new object();

        // プライベートコンストラクタ
        private LogFile() { }

        public static LogFile Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new LogFile();
                    }
                    return _instance;
                }
            }
        }

        public void WriteLog(string message)
        {
            // ログ書き込み処理
            Console.WriteLine($"Log: {message}");
        }
    }

    public class ErrorLogFile
    {
        private static ErrorLogFile _instance;
        private static readonly object _lock = new object();

        // プライベートコンストラクタ
        private ErrorLogFile() { }

        public static ErrorLogFile Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new ErrorLogFile();
                    }
                    return _instance;
                }
            }
        }

        public void WriteErrorLog(string message)
        {
            // エラーログ書き込み処理
            Console.WriteLine($"Error Log: {message}");
        }
    }
}

namespace SingletonExample
{

    class Program
    {
        static void Main(string[] args)
        {
            // ログファイル用インスタンスの取得と使用
            LogFile logFile = LogFile.Instance;
            logFile.WriteLog("This is a log message.");

            // エラーログファイル用インスタンスの取得と使用
            ErrorLogFile errorLogFile = ErrorLogFile.Instance;
            errorLogFile.WriteErrorLog("This is an error log message.");
        }
    }
}
