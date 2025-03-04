using System.IO;
using System.IO.Compression;

namespace JobManagementApp.Helpers
{
    public static class FileHelper
    {
        public static void CopyFile(string sourcePath, string destinationPath)
        {
            if (File.Exists(sourcePath))
            {
                File.Copy(sourcePath, destinationPath, true);
            }
        }

        public static void CompressFiles(string sourceFolder, string outputZipPath)
        {
            if (Directory.Exists(sourceFolder))
            {
                //ZipFile.CreateFromDirectory(sourceFolder, outputZipPath);
            }
        }
    }
}
