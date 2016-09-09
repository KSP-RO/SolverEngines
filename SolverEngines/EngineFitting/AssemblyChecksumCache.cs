using System.Collections.Generic;
using System.Reflection;

namespace SolverEngines.EngineFitting
{
    public class AssemblyChecksumCache
    {
        private Dictionary<Assembly, string> checksums = new Dictionary<Assembly, string>();

        public string GetChecksum(Assembly assembly)
        {
            string checksum;
            if (!checksums.TryGetValue(assembly, out checksum))
            {
                checksum = assembly.GetChecksum();
                checksums[assembly] = checksum;
            }
            return checksum;
        }
    }
}
