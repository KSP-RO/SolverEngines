using System.Reflection;
using UnityEngine;

namespace SolverEngines.EngineFitting
{
    /// <summary>
    /// A class which keeps track of fitted engine parameters
    /// Addon is destroyed in loading screen, but static members can still be accessed
    /// Also provides methods for checking whether a plugin has updated, either by version of by checksum
    /// </summary>
    public static class EngineDatabase
    {
        // This basically just ensures that the database gets saved to file on the next scene change
        private class PersistenceObject : MonoBehaviour
        {
            private void Awake()
            {
                LoadDatabase();
            }

            private void OnDestroy()
            {
                if (database == null)
                    return;

                SaveDatabase();
            }
        }

        private static PersistenceObject persistenceObject;

        public static readonly Assembly SolverEnginesAssembly = typeof(EngineDatabase).Assembly;
        public static readonly string SolverEnginesVersion = SolverEnginesAssembly.GetVersion().ToString();
        public static readonly string SolverEnginesAssemblyChecksum = SolverEnginesAssembly.GetChecksum();

        private static readonly string configPath = FileUtil.JoinPath(SolverEnginesAssembly.GetDirectory(), "PluginData", "SolverEngines", "EngineDatabse.cfg");
        private static readonly string databaseName = "SolverEnginesDatabase";
        private static ConfigNode database = null;

        private static AssemblyChecksumCache checksumCache = new AssemblyChecksumCache();

        static EngineDatabase()
        {
            EnsurePersistenceObject();
        }

        public static void ModuleManagerPostLoad()
        {
            EnsurePersistenceObject();
        }

        private static void EnsurePersistenceObject()
        {
            if (persistenceObject != null) return;
            GameObject go = new GameObject("EngineDatabasePersistenceObject");
            persistenceObject = go.AddComponent<PersistenceObject>();
        }

        /// <summary>
        /// Loads the engine database from file
        /// </summary>
        public static void LoadDatabase()
        {
            ConfigNode node = ConfigNode.Load(configPath);
            if (node != null)
                database = node.GetNode(databaseName);
            if (database == null)
                database = new ConfigNode(databaseName);
        }

        /// <summary>
        /// Saves the engine database to file
        /// </summary>
        public static void SaveDatabase()
        {
#if DEBUG
            Debug.Log("[SolverEngines] Saving engine database");
#endif
            string dirName = System.IO.Path.GetDirectoryName(configPath);
            if (!System.IO.Directory.Exists(dirName))
                System.IO.Directory.CreateDirectory(dirName);
            ConfigNode saveNode = new ConfigNode();
            saveNode.AddNode(database);
            saveNode.Save(configPath);
        }

        /// <summary>
        /// Searches for engine in the database
        /// </summary>
        /// <param name="engine">Engine module to search for.  Will use engine class, part name, and engineID to identify it</param>
        /// <returns>ConfigNode associated with engine if found, otherwise null</returns>
        public static ConfigNode GetNodeForEngine(IEngineIdentifier engine)
        {
            string partName = engine.EnginePartName;
            string engineType = engine.EngineTypeName;
            string engineID = engine.EngineID;

            ConfigNode partNode = database.GetNode(partName);
            if (partNode != null)
            {
                foreach (ConfigNode moduleNode in partNode.GetNodes(engineType))
                {
                    if (moduleNode.GetValue("engineID") == engineID)
                        return moduleNode;
                }
            }

            return null;
        }

        /// <summary>
        /// Store fitted engine parameters in the database so they can be accessed later
        /// </summary>
        /// <param name="engine">Engine to associated this config node with</param>
        /// <param name="node">Config node describing engine parameters (both input parameters and fitted parameters)</param>
        public static void SetNodeForEngine(IEngineIdentifier engine, ConfigNode node)
        {
            string partName = engine.EnginePartName;
            string engineType = engine.EngineTypeName;
            string engineID = engine.EngineID;

            Assembly assembly = engine.GetType().Assembly;

            node.SetValue("engineID", engineID, true);
            node.SetValue("DeclaringAssemblyVersion", assembly.GetVersion().ToString(), true);
            node.SetValue("DeclaringAssemblyChecksum", checksumCache.GetChecksum(assembly), true);
            node.SetValue("SolverEnginesVersion", SolverEnginesVersion, true);
            node.SetValue("SolverEnginesAssemblyChecksum", SolverEnginesAssemblyChecksum, true);

            ConfigNode partNode = database.GetNode(partName);
            int nodeIndex = 0;

            if (partNode != null)
            {
                ConfigNode[] moduleNodes = partNode.GetNodes(engineType);
                for (int i = 0; i < moduleNodes.Length; i++)
                {
                    ConfigNode mNode = moduleNodes[i];
                    if (mNode.GetValue("engineID") == engineID)
                    {
                        nodeIndex = i;
                    }
                }
            }
            else
            {
                partNode = new ConfigNode(partName);
                database.AddNode(partNode);
                nodeIndex = 0;
            }

            partNode.SetNode(engineType, node, nodeIndex, true);
        }

        /// <summary>
        /// Checks whether plugins have updated for a particular engine, thus necessitating that the engine parameters be fit again
        /// Checks version and checksum of SolverEngines and whichever assembly declares the type of engine
        /// Looks for values DeclaringAssemblyVersion, DeclaringAssemblyChecksum, SolverEnginesVersion, SolverEnginesAssemblyChecksum in node
        /// </summary>
        /// <param name="engine">Engine module to check.  Only used to find its declaring assembly.  Can be null</param>
        /// <param name="node">ConfigNode to check for versions and checksums</param>
        /// <returns></returns>
        public static bool PluginUpdateCheck(object engine, ConfigNode node)
        {
            bool result = false;
            if (engine != null)
            {
                Assembly assembly = engine.GetType().Assembly;
                result |= (assembly.GetVersion().ToString() != node.GetValue("DeclaringAssemblyVersion"));
                result |= (checksumCache.GetChecksum(assembly) != node.GetValue("DeclaringAssemblyChecksum"));
            }
            result |= (SolverEnginesVersion != node.GetValue("SolverEnginesVersion"));
            result |= (SolverEnginesAssemblyChecksum != node.GetValue("SolverEnginesAssemblyChecksum"));
            return result;
        }
    }
}
