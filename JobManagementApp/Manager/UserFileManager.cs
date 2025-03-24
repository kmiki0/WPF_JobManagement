using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace JobManagementApp.Manager
{

    public class UserFileManager
    {
        private string fileName = "UserCache";

        private string filePath;
        private Dictionary<string, string> Cache;

        public string CacheKey_UserId = "CACHEKEYUSERID";
        public string CacheKey_FilePath = "CACHEKEYFILEPATH";
        public string CacheKey_SearchTime = "CACHEKEYSEARCHTime";

        public UserFileManager()
        {
            // プロジェクトのディレクトリにファイルを作成
            string projectDirectory = AppDomain.CurrentDomain.BaseDirectory;
            filePath = Path.Combine(projectDirectory, fileName);
            Cache = new Dictionary<string, string>();

            // ファイルが存在しない場合、新規作成
            if (!File.Exists(filePath))
            {
                using (File.Create(filePath)) { }
            }
            else
            {
                // ファイルが存在する場合、内容を読み込む
                LoadFromFile();
            }
        }

        // ユーザーIDとファイルパスを保存するメソッド
        public void SaveCache(string key, string userFilePath = "")
        {
            if (!string.IsNullOrEmpty(userFilePath))
            {
                Cache[key] = userFilePath;
            }
            else if (!Cache.ContainsKey(key))
            {
                Cache[key] = string.Empty;
            }
            SaveToFile();
        }

        // Cacheキーに対応する値を取得するメソッド
        public string GetCache(string key)
        {
            if (Cache.ContainsKey(key))
            {
                return Cache[key];
            }
            return string.Empty;
        }

        // ディクショナリの内容をファイルに保存するメソッド
        private void SaveToFile()
        {
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                foreach (var entry in Cache)
                {
                    writer.WriteLine($"{entry.Key},{entry.Value}");
                }
            }
        }

        // ファイルからディクショナリの内容を読み込むメソッド
        private void LoadFromFile()
        {
            using (StreamReader reader = new StreamReader(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var parts = line.Split(',');
                    if (parts.Length == 2)
                    {
                        Cache[parts[0]] = parts[1];
                    }
                }
            }
        }
    }
}
