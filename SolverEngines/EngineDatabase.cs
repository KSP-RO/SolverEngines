using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;
using KSP;
using SolverEngines;

namespace SolverEngines
{
    /// <summary>
    /// A class which keeps track of fitted engine parameters
    /// Addon is destroyed in loading screen, but static members can still be accessed
    /// Also provides methods for checking whether a plugin has updated, either by version of by checksum
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Instantly, false)]
    class EngineDatabase : MonoBehaviour
    {
        private static readonly string configPath = KSPUtil.ApplicationRootPath.Replace("\\", "/") + "GameData/SolverEngines/Plugins/PluginData/SolverEngines/EngineDatabse.cfg";
        private static readonly string databaseName = "SolverEnginesDatabase";

        public static readonly Assembly SolverEnginesAssembly = null;
        public static readonly string SolverEnginesVersion = null;
        public static readonly string SolverEnginesAssemblyChecksum = null;

        private static ConfigNode database = null;

        /// <summary>
        /// Static constructor
        /// </summary>
        static EngineDatabase()
        {
            SolverEnginesAssembly = MethodBase.GetCurrentMethod().DeclaringType.Assembly;
            SolverEnginesVersion = SolverEnginesAssembly.GetVersion().ToString();
            SolverEnginesAssemblyChecksum = SolverEnginesAssembly.GetChecksum();
        }

        public void Awake()
        {
            LoadDatabase();
        }

        public void OnDestroy()
        {
            if (database == null)
                return;

            SaveDatabase();
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
        public static ConfigNode GetNodeForEngine(ModuleEnginesSolver engine)
        {
            string partName = engine.part.name;
            string engineType = engine.GetType().Name;
            string engineID = engine.engineID;

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
        public static void SetNodeForEngine(ModuleEnginesSolver engine, ConfigNode node)
        {
            string partName = engine.part.name;
            string engineType = engine.GetType().Name;
            string engineID = engine.engineID;

            Assembly assembly = engine.GetType().Assembly;

            node.SetValue("engineID", engineID, true);
            node.SetValue("DeclaringAssemblyVersion", assembly.GetVersion().ToString(), true);
            node.SetValue("DeclaringAssemblyChecksum", assembly.GetChecksum(), true);
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

            SaveDatabase();
        }

        /// <summary>
        /// Checks whether plugins have updated for a particular engine, thus necessitating that the engine parameters be fit again
        /// Checks version and checksum of SolverEngines and whichever assembly declares the type of engine
        /// Looks for values DeclaringAssemblyVersion, DeclaringAssemblyChecksum, SolverEnginesVersion, SolverEnginesAssemblyChecksum in node
        /// </summary>
        /// <param name="engine">Engine module to check.  Only used to find its declaring assembly.  Can be null</param>
        /// <param name="node">ConfigNode to check for versions and checksums</param>
        /// <returns></returns>
        public static bool PluginUpdateCheck(ModuleEnginesSolver engine, ConfigNode node)
        {
            bool result = false;
            if (engine != null)
            {
                Assembly assembly = engine.GetType().Assembly;
                result |= (assembly.GetVersion().ToString() != node.GetValue("DeclaringAssemblyVersion"));
                result |= (assembly.GetChecksum() != node.GetValue("DeclaringAssemblyChecksum"));
            }
            result |= (SolverEnginesVersion != node.GetValue("SolverEnginesVersion"));
            result |= (SolverEnginesAssemblyChecksum != node.GetValue("SolverEnginesAssemblyChecksum"));
            return result;
        }
    }
}
