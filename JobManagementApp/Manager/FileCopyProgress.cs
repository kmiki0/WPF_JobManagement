using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace JobManagementApp.Manager
{
    /// <summary>
    /// ファイルコピー進行状況
    /// </summary>
    public class FileCopyProgress : IDisposable
    {
        #region フィールドとプロパティ

        private bool _disposed = false;
        private readonly object _lockObject = new object();
        
        // コピー統計情報
        private int _totalCopyCount = 0;
        private long _totalBytesCopied = 0;
        private DateTime _startTime = DateTime.Now;
        
        // パフォーマンス設定
        private readonly int _bufferSize;
        private readonly int _maxRetryCount;
        private readonly TimeSpan _retryDelay;
        
        // 進行中のコピー操作追跡
        private readonly ConcurrentDictionary<string, CopyOperation> _activeCopyOperations;

        // イベント
        public event Action<string, string, int, int> ProgressChanged;

        #endregion

        #region コンストラクタ

        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="bufferSize">バッファサイズ（デフォルト: 1MB）</param>
        /// <param name="maxRetryCount">最大リトライ回数（デフォルト: 3回）</param>
        /// <param name="retryDelayMs">リトライ間隔（デフォルト: 1000ms）</param>
        public FileCopyProgress(int bufferSize = 1024 * 1024, int maxRetryCount = 3, int retryDelayMs = 1000)
        {
            _bufferSize = Math.Max(8192, bufferSize); // 最小8KB
            _maxRetryCount = Math.Max(1, maxRetryCount);
            _retryDelay = TimeSpan.FromMilliseconds(Math.Max(100, retryDelayMs));
            
            _activeCopyOperations = new ConcurrentDictionary<string, CopyOperation>();
            
            LogFile.WriteLog($"FileCopyProgress を初期化しました (バッファサイズ: {_bufferSize / 1024}KB, リトライ回数: {_maxRetryCount})");
        }

        #endregion

        #region 公開メソッド

        /// <summary>
        /// ファイルをコピーする（非同期・キャンセル対応）
        /// </summary>
        /// <param name="sourceFile">コピー元ファイルパス</param>
        /// <param name="destFile">コピー先ファイルパス</param>
        /// <param name="cancellationToken">キャンセレーショントークン</param>
        /// <param name="verifyIntegrity">整合性チェックを行うか（デフォルト: false）</param>
        /// <returns>コピー結果</returns>
        public async Task<CopyResult> CopyFileAsync(string sourceFile, string destFile, 
            CancellationToken cancellationToken = default, bool verifyIntegrity = false)
        {
            // 入力検証
            var validationResult = ValidateInputs(sourceFile, destFile);
            if (!validationResult.IsValid)
            {
                return CopyResult.Failure(validationResult.ErrorMessage);
            }

            if (_disposed)
            {
                return CopyResult.Failure("FileCopyProgress は既に破棄されています");
            }

            var operationId = Guid.NewGuid().ToString();
            var operation = new CopyOperation(sourceFile, destFile, operationId);
            
            try
            {
                _activeCopyOperations[operationId] = operation;
                
                // ファイル存在確認
                if (!File.Exists(sourceFile))
                {
                    return CopyResult.Failure($"コピー元ファイルが存在しません: {sourceFile}");
                }

                // コピー先ディレクトリの作成
                var destDirectory = Path.GetDirectoryName(destFile);
                if (!string.IsNullOrEmpty(destDirectory) && !Directory.Exists(destDirectory))
                {
                    Directory.CreateDirectory(destDirectory);
                }

                // ファイル情報取得
                var fileInfo = new FileInfo(sourceFile);
                long totalBytes = fileInfo.Length;
                int totalSizeKB = (int)Math.Round(totalBytes / 1024.0);

                operation.TotalBytes = totalBytes;
                operation.StartTime = DateTime.Now;

                // コピー必要性チェック
                if (!ShouldCopyFile(sourceFile, destFile))
                {
                    OnProgressChanged(sourceFile, destFile, totalSizeKB, 100);
                    LogFile.WriteLog($"ファイルコピーをスキップしました（既に最新）: {sourceFile}");
                    return CopyResult.Success($"ファイルは既に最新です: {Path.GetFileName(sourceFile)}");
                }

                // 実際のコピー実行
                var copyResult = await PerformCopyWithRetryAsync(operation, cancellationToken);
                
                if (copyResult.IsSuccess && verifyIntegrity)
                {
                    var verifyResult = await VerifyFileIntegrityAsync(sourceFile, destFile, cancellationToken);
                    if (!verifyResult.IsSuccess)
                    {
                        return verifyResult;
                    }
                }

                // 統計更新
                if (copyResult.IsSuccess)
                {
                    Interlocked.Increment(ref _totalCopyCount);
                    Interlocked.Add(ref _totalBytesCopied, totalBytes);
                }

                return copyResult;
            }
            catch (OperationCanceledException)
            {
                LogFile.WriteLog($"ファイルコピーがキャンセルされました: {sourceFile}");
                return CopyResult.Cancelled();
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"ファイルコピー予期しないエラー: {ex.Message}");
                return CopyResult.Failure($"予期しないエラー: {ex.Message}");
            }
            finally
            {
                _activeCopyOperations.TryRemove(operationId, out _);
                operation.EndTime = DateTime.Now;
            }
        }

        /// <summary>
        /// レガシーメソッドの互換性維持
        /// </summary>
        public async Task CopyFile(string sourceFile, string destFile, CancellationToken cancellationToken = default)
        {
            var result = await CopyFileAsync(sourceFile, destFile, cancellationToken);
            if (!result.IsSuccess)
            {
                throw new InvalidOperationException(result.ErrorMessage);
            }
        }

        /// <summary>
        /// 進行中のコピー操作をすべてキャンセル
        /// </summary>
        public void CancelAllOperations()
        {
            try
            {
                foreach (var operation in _activeCopyOperations.Values)
                {
                    operation.Cancel();
                }
                LogFile.WriteLog($"進行中のコピー操作 {_activeCopyOperations.Count} 件をキャンセルしました");
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"CancelAllOperations エラー: {ex.Message}");
            }
        }

        /// <summary>
        /// 統計情報の取得
        /// </summary>
        public CopyStatistics GetStatistics()
        {
            var elapsed = DateTime.Now - _startTime;
            return new CopyStatistics
            {
                TotalCopyCount = _totalCopyCount,
                TotalBytesCopied = _totalBytesCopied,
                ElapsedTime = elapsed,
                ActiveOperationCount = _activeCopyOperations.Count,
                AverageSpeedMBps = elapsed.TotalSeconds > 0 ? (_totalBytesCopied / (1024.0 * 1024.0)) / elapsed.TotalSeconds : 0
            };
        }

        #endregion

        #region プライベートメソッド

        /// <summary>
        /// 入力検証
        /// </summary>
        private ValidationResult ValidateInputs(string sourceFile, string destFile)
        {
            if (string.IsNullOrWhiteSpace(sourceFile))
                return ValidationResult.Invalid("コピー元ファイルパスが指定されていません");
            
            if (string.IsNullOrWhiteSpace(destFile))
                return ValidationResult.Invalid("コピー先ファイルパスが指定されていません");

            if (sourceFile.Length > 260 || destFile.Length > 260)
                return ValidationResult.Invalid("ファイルパスが長すぎます");

            try
            {
                // パス形式の検証
                Path.GetFullPath(sourceFile);
                Path.GetFullPath(destFile);
            }
            catch (Exception ex)
            {
                return ValidationResult.Invalid($"無効なファイルパス: {ex.Message}");
            }

            return ValidationResult.Valid();
        }

        /// <summary>
        /// リトライ付きコピー実行
        /// </summary>
        private async Task<CopyResult> PerformCopyWithRetryAsync(CopyOperation operation, CancellationToken cancellationToken)
        {
            Exception lastException = null;

            for (int attempt = 1; attempt <= _maxRetryCount; attempt++)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var result = await PerformSingleCopyAsync(operation, cancellationToken);
                    if (result.IsSuccess)
                    {
                        LogFile.WriteLog($"ファイルコピー完了: {operation.SourceFile} -> {operation.DestFile} (試行回数: {attempt})");
                        return result;
                    }

                    lastException = new Exception(result.ErrorMessage);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (UnauthorizedAccessException ex)
                {
                    lastException = ex;
                    ErrLogFile.WriteLog($"ファイルアクセス権限エラー (試行 {attempt}/{_maxRetryCount}): {ex.Message}");
                    
                    if (attempt == _maxRetryCount)
                        return CopyResult.Failure("ファイルアクセス権限がありません。管理者権限で実行してください。");
                }
                catch (DirectoryNotFoundException ex)
                {
                    lastException = ex;
                    return CopyResult.Failure($"ディレクトリが見つかりません: {ex.Message}");
                }
                catch (IOException ex)
                {
                    lastException = ex;
                    ErrLogFile.WriteLog($"ファイルI/Oエラー (試行 {attempt}/{_maxRetryCount}): {ex.Message}");
                    
                    if (attempt == _maxRetryCount)
                        return CopyResult.Failure("ファイルの読み書き中にエラーが発生しました。ファイルが使用中でないか確認してください。");
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    ErrLogFile.WriteLog($"ファイルコピーエラー (試行 {attempt}/{_maxRetryCount}): {ex.Message}");
                }

                // リトライ前の待機
                if (attempt < _maxRetryCount)
                {
                    await Task.Delay(_retryDelay, cancellationToken);
                }
            }

            return CopyResult.Failure($"最大リトライ回数に達しました: {lastException?.Message}");
        }

        /// <summary>
        /// 単一コピー実行
        /// </summary>
        private async Task<CopyResult> PerformSingleCopyAsync(CopyOperation operation, CancellationToken cancellationToken)
        {
            var buffer = new byte[_bufferSize];
            long totalBytesCopied = 0;

            using (var sourceStream = new FileStream(operation.SourceFile, FileMode.Open, FileAccess.Read, 
                FileShare.ReadWrite, _bufferSize, FileOptions.SequentialScan))
            using (var destStream = new FileStream(operation.DestFile, FileMode.Create, FileAccess.Write, 
                FileShare.None, _bufferSize, FileOptions.WriteThrough))
            {
                int bytesRead;
                while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    await destStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                    totalBytesCopied += bytesRead;
                    
                    operation.BytesCopied = totalBytesCopied;
                    
                    // 進行状況の報告
                    int progressPercentage = operation.TotalBytes > 0 ? 
                        (int)((totalBytesCopied * 100) / operation.TotalBytes) : 100;
                    
                    if (progressPercentage < 100)
                    {
                        int totalSizeKB = (int)Math.Round(operation.TotalBytes / 1024.0);
                        OnProgressChanged(operation.SourceFile, operation.DestFile, totalSizeKB, progressPercentage);
                    }
                }

                // ストリームのフラッシュを確実に実行
                await destStream.FlushAsync(cancellationToken);
            }

            // 最終進行状況の報告
            int finalSizeKB = (int)Math.Round(operation.TotalBytes / 1024.0);
            OnProgressChanged(operation.SourceFile, operation.DestFile, finalSizeKB, 100);

            // ファイル属性のコピー
            await CopyFileAttributesAsync(operation.SourceFile, operation.DestFile);

            return CopyResult.Success($"ファイルコピー完了: {Path.GetFileName(operation.SourceFile)}");
        }

        /// <summary>
        /// ファイル属性のコピー
        /// </summary>
        private async Task CopyFileAttributesAsync(string sourceFile, string destFile)
        {
            try
            {
                await Task.Run(() =>
                {
                    var sourceInfo = new FileInfo(sourceFile);
                    var destInfo = new FileInfo(destFile);
                    
                    destInfo.CreationTime = sourceInfo.CreationTime;
                    destInfo.LastWriteTime = sourceInfo.LastWriteTime;
                    destInfo.LastAccessTime = sourceInfo.LastAccessTime;
                    
                    // 可能であれば属性もコピー
                    try
                    {
                        destInfo.Attributes = sourceInfo.Attributes;
                    }
                    catch
                    {
                        // 属性設定に失敗してもコピー自体は成功とする
                    }
                });
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"ファイル属性コピーエラー（コピーは成功）: {ex.Message}");
            }
        }

        /// <summary>
        /// ファイル整合性検証
        /// </summary>
        private async Task<CopyResult> VerifyFileIntegrityAsync(string sourceFile, string destFile, CancellationToken cancellationToken)
        {
            try
            {
                using (var md5 = MD5.Create())
                {
                    var sourceHashTask = ComputeHashAsync(sourceFile, md5, cancellationToken);
                    var destHashTask = ComputeHashAsync(destFile, md5, cancellationToken);

                    var sourceHash = await sourceHashTask;
                    var destHash = await destHashTask;

                    if (!sourceHash.Equals(destHash, StringComparison.OrdinalIgnoreCase))
                    {
                        return CopyResult.Failure("ファイル整合性チェックに失敗しました");
                    }

                    LogFile.WriteLog($"ファイル整合性チェック完了: {Path.GetFileName(sourceFile)}");
                    return CopyResult.Success("整合性チェック完了");
                }
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"整合性チェックエラー: {ex.Message}");
                return CopyResult.Failure($"整合性チェックエラー: {ex.Message}");
            }
        }

        /// <summary>
        /// ファイルハッシュ計算
        /// </summary>
        private async Task<string> ComputeHashAsync(string filePath, HashAlgorithm hashAlgorithm, CancellationToken cancellationToken)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var buffer = new byte[_bufferSize];
                int bytesRead;
                
                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    hashAlgorithm.TransformBlock(buffer, 0, bytesRead, null, 0);
                }
                
                hashAlgorithm.TransformFinalBlock(new byte[0], 0, 0);
                return BitConverter.ToString(hashAlgorithm.Hash).Replace("-", "");
            }
        }

        /// <summary>
        /// コピー必要性判定
        /// </summary>
        private bool ShouldCopyFile(string sourceFile, string destFile)
        {
            try
            {
                if (!File.Exists(destFile))
                    return true;

                var sourceInfo = new FileInfo(sourceFile);
                var destInfo = new FileInfo(destFile);

                // サイズと更新日時の比較（1秒の誤差を許容）
                return sourceInfo.Length != destInfo.Length || 
                       Math.Abs((sourceInfo.LastWriteTime - destInfo.LastWriteTime).TotalSeconds) > 1;
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"ファイル比較エラー: {ex.Message}");
                return true; // エラーの場合は安全側に倒してコピーを実行
            }
        }

        /// <summary>
        /// 進行状況イベントの安全な発火
        /// </summary>
        private void OnProgressChanged(string sourceFile, string destFile, int totalSizeKB, int progressPercentage)
        {
            try
            {
                if (_disposed)
                    return;

                lock (_lockObject)
                {
                    if (!_disposed)
                    {
                        ProgressChanged?.Invoke(sourceFile, destFile, totalSizeKB, progressPercentage);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"ProgressChanged イベント発火エラー: {ex.Message}");
            }
        }

        #endregion

        #region 静的ユーティリティメソッド

        /// <summary>
        /// ファイルが使用中かどうかをチェック
        /// </summary>
        public static bool IsFileInUse(string filePath)
        {
            try
            {
                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    return false;
                }
            }
            catch (IOException)
            {
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 安全なファイル削除
        /// </summary>
        public static async Task<bool> TryDeleteFileAsync(string filePath, int maxRetries = 3, int delayMs = 500)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                        return true;
                    }
                    return true;
                }
                catch (IOException) when (i < maxRetries - 1)
                {
                    await Task.Delay(delayMs);
                }
                catch (Exception ex)
                {
                    ErrLogFile.WriteLog($"ファイル削除エラー: {ex.Message}");
                    return false;
                }
            }
            return false;
        }

        /// <summary>
        /// ディスク容量チェック
        /// </summary>
        public static bool HasSufficientDiskSpace(string path, long requiredBytes)
        {
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(path));
                return drive.AvailableFreeSpace >= requiredBytes * 1.1; // 10%のマージンを追加
            }
            catch (Exception ex)
            {
                ErrLogFile.WriteLog($"ディスク容量チェックエラー: {ex.Message}");
                return true; // エラーの場合は続行を許可
            }
        }

        #endregion

        #region IDisposable実装

        /// <summary>
        /// リソースの解放
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// リソースの解放（詳細）
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    lock (_lockObject)
                    {
                        LogFile.WriteLog("FileCopyProgress のリソース解放を開始します");

                        // 進行中の操作をキャンセル
                        CancelAllOperations();

                        // イベントハンドラーをクリア
                        ProgressChanged = null;

                        // 統計情報の最終出力
                        var stats = GetStatistics();
                        LogFile.WriteLog($"コピー統計 - 合計: {stats.TotalCopyCount}ファイル, " +
                                       $"容量: {stats.TotalBytesCopied / (1024.0 * 1024.0):F2}MB, " +
                                       $"平均速度: {stats.AverageSpeedMBps:F2}MB/s");

                        _disposed = true;
                        LogFile.WriteLog("FileCopyProgress のリソース解放が完了しました");
                    }
                }
                catch (Exception ex)
                {
                    ErrLogFile.WriteLog($"FileCopyProgress Dispose エラー: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// ファイナライザー
        /// </summary>
        ~FileCopyProgress()
        {
            Dispose(false);
        }

        #endregion

        #region 内部クラス

        /// <summary>
        /// コピー操作情報
        /// </summary>
        private class CopyOperation
        {
            public string SourceFile { get; }
            public string DestFile { get; }
            public string OperationId { get; }
            public long TotalBytes { get; set; }
            public long BytesCopied { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            private readonly CancellationTokenSource _cancellationTokenSource;

            public CopyOperation(string sourceFile, string destFile, string operationId)
            {
                SourceFile = sourceFile;
                DestFile = destFile;
                OperationId = operationId;
                _cancellationTokenSource = new CancellationTokenSource();
            }

            public void Cancel()
            {
                _cancellationTokenSource.Cancel();
            }
        }

        /// <summary>
        /// コピー結果
        /// </summary>
        public class CopyResult
        {
            public bool IsSuccess { get; private set; }
            public bool IsCancelled { get; private set; }
            public string Message { get; private set; }
            public string ErrorMessage { get; private set; }

            private CopyResult() { }

            public static CopyResult Success(string message = "")
            {
                return new CopyResult { IsSuccess = true, Message = message };
            }

            public static CopyResult Failure(string errorMessage)
            {
                return new CopyResult { IsSuccess = false, ErrorMessage = errorMessage };
            }

            public static CopyResult Cancelled()
            {
                return new CopyResult { IsSuccess = false, IsCancelled = true, ErrorMessage = "操作がキャンセルされました" };
            }
        }

        /// <summary>
        /// 検証結果
        /// </summary>
        private class ValidationResult
        {
            public bool IsValid { get; private set; }
            public string ErrorMessage { get; private set; }

            private ValidationResult() { }

            public static ValidationResult Valid()
            {
                return new ValidationResult { IsValid = true };
            }

            public static ValidationResult Invalid(string errorMessage)
            {
                return new ValidationResult { IsValid = false, ErrorMessage = errorMessage };
            }
        }

        /// <summary>
        /// コピー統計情報
        /// </summary>
        public class CopyStatistics
        {
            public int TotalCopyCount { get; set; }
            public long TotalBytesCopied { get; set; }
            public TimeSpan ElapsedTime { get; set; }
            public int ActiveOperationCount { get; set; }
            public double AverageSpeedMBps { get; set; }
        }

        #endregion
    }
}