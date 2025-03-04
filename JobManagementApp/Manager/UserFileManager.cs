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
        private Dictionary<string, string> userFilePaths;

        public string CacheKey_UserId = "CACHEKEYUSERID";
        public string CacheKey_FilePath = "CACHEKEYFILEPATH";

        public UserFileManager()
        {


            // プロジェクトのディレクトリにファイルを作成
            string projectDirectory = AppDomain.CurrentDomain.BaseDirectory;
            filePath = Path.Combine(projectDirectory, fileName);
            userFilePaths = new Dictionary<string, string>();

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
        public void SaveUserFilePath(string userId, string userFilePath = "")
        {
            if (!string.IsNullOrEmpty(userFilePath))
            {
                userFilePaths[userId] = userFilePath;
            }
            else if (!userFilePaths.ContainsKey(userId))
            {
                userFilePaths[userId] = string.Empty;
            }
            SaveToFile();
        }

        // ユーザーIDに対応するファイルパスを取得するメソッド
        public string GetUserFilePath(string userId)
        {
            if (userFilePaths.ContainsKey(userId))
            {
                return userFilePaths[userId];
            }
            return string.Empty;
        }

        // ディクショナリの内容をファイルに保存するメソッド
        private void SaveToFile()
        {
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                foreach (var entry in userFilePaths)
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
                        userFilePaths[parts[0]] = parts[1];
                    }
                }
            }
        }
    }
}
