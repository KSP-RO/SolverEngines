using System;
using System.IO;

namespace SolverEngines
{
    public static class FileUtil
    {
        /// <summary>
        /// Generate and MD5 hash for a particular file
        /// </summary>
        /// <param name="filename">File to generate hash for</param>
        /// <returns>Hash as a hexidecimal string separated by dashes</returns>
        public static string FileHash(string filename)
        {
            byte[] hash = null;
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    hash = md5.ComputeHash(stream);
                }
            }

            return BitConverter.ToString(hash);
        }

        public static string JoinPath(params string[] paths)
        {
            string resultPath = paths[0];

            for (int i = 1; i < paths.Length; i++)
            {
                resultPath = Path.Combine(resultPath, paths[i]);
            }

            return Path.GetFullPath(resultPath);
        }
    }
}
