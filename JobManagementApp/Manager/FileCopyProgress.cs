using System;
using System.IO;
using System.Threading.Tasks;

namespace JobManagementApp.Manager
{
    public class FileCopyProgress
    {
        // ファイルコピーの進行状況を報告するイベント
        public event Action<string, string, int, int> ProgressChanged;
        // ファイルをコピーするメソッド
        public async Task CopyFile(string sourceFile, string destFile)
        {
            try
            {
                // 1MBのバッファ
                byte[] buffer = new byte[1024 * 1024]; 
                // ファイルの総サイズ
                long totalBytes = new FileInfo(sourceFile).Length;
                int totalSize = (int)Math.Round(totalBytes / 1024.0);
                // コピー済みのバイト数
                long totalBytesCopied = 0;

                // コピー実施する場合
                if (IsFileCopy(sourceFile, destFile))
                {
                    using (FileStream sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (FileStream destStream = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                    {
                        int bytesRead;
                        while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            destStream.Write(buffer, 0, bytesRead);
                            totalBytesCopied += bytesRead;
                            int progressPercentage = (int)((totalBytesCopied * 100) / totalBytes);
                            // 100%になった際に二重でファイル掴むことを防ぐため
                            if (progressPercentage < 100)
                            {
                                // 進行状況を報告するイベントを発生 (コピー元ファイルパス, コピー済パーセンテージ)
                                ProgressChanged?.Invoke(sourceFile, destFile, totalSize, progressPercentage);
                            }
                        }
                    }

                    // 進行状況を報告するイベントを発生 (コピー元ファイルパス, コピー済パーセンテージ)
                    ProgressChanged?.Invoke(sourceFile, destFile, totalSize, 100);

                    // 更新日付もコピーする
                    File.SetLastWriteTime(destFile, File.GetLastWriteTime(sourceFile));
                }
                else
                {
                    // ファイルコピーしない場合
                    ProgressChanged?.Invoke(sourceFile, destFile, totalSize, 100);
                }
            }
            catch (Exception e)
            {
                ErrLogFile.WriteLog(e.Message);
                throw;
            }
        }

        // ファイルコピーするか判断
        private bool IsFileCopy(string sourceFile, string destFile)
        {
            // コピー先ファイルが存在しない場合、コピー実施
            if (!File.Exists(destFile)) return true;

            // ファイル存在する場合、比較して判断
            FileInfo parentFile = new FileInfo(sourceFile); // コピー元
            FileInfo copyFile = new FileInfo(destFile); // コピー先

            // サイズと更新日付を比較して、一つでも違う場合、コピー実施する
            if (parentFile.Length != copyFile.Length || parentFile.LastWriteTime != copyFile.LastWriteTime)
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        //public void Dispose()
        //{
        //    this.Dispose();
        //}
    }
}
