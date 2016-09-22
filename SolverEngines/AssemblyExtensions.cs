using System;
using System.IO;
using System.Reflection;

namespace SolverEngines
{
    public static class AssemblyExtensions
    {
        /// <summary>
        /// Gets a string describing the version of an assembly
        /// </summary>
        /// <param name="assembly">Assembly to find the version of</param>
        /// <returns>String describing the assembly version</returns>
        public static Version GetVersion(this Assembly assembly) => assembly.GetName().Version;

        /// <summary>
        /// Get an MD5 checksum for a particular assembly
        /// Finds the assembly file and uses it to generate a checksum
        /// </summary>
        /// <param name="assembly">Assembly to generate checksum for</param>
        /// <returns>Checksum as a string.  Represented in hexadecimal separated by dashes</returns>
        public static string GetChecksum(this Assembly assembly) => FileUtil.FileHash(assembly.Location);

        /// <summary>
        /// Gets the path of the directory where this assembly is located
        /// </summary>
        /// <param name="assembly">Assembly to find the location of</param>
        /// <returns>Path of the directory where this assembly is located</returns>
        public static string GetDirectory(this Assembly assembly) => Path.GetDirectoryName(assembly.Location);
    }
}
