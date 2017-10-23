using System;
using System.Reflection;

namespace SolverEngines.EngineFitting
{
    /// <summary>
    /// Property describing parameters which will be input into an engine solver
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public class EngineParameter : Attribute
    {
        virtual public bool IsFitResult()
        {
            return false;
        }
    }

    /// <summary>
    /// Property describing pieces of data which will be used to fit particular engine parameters (those having the EngineFitResult property)
    /// Examples: static thrust, TSFC
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public class EngineFitData : EngineParameter { }

    /// <summary>
    /// Property describing engine parameters which will be fit based on EngineFitData
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public class EngineFitResult : EngineParameter
    {
        public override bool IsFitResult()
        {
            return true;
        }
    }

    /// <summary>
    /// Describes a field which has the EngineParameter (or derived from) property
    /// </summary>
    public struct EngineParameterInfo
    {
        public readonly object Module;
        public readonly FieldInfo Field;
        public readonly EngineParameter Param;

        public EngineParameterInfo(object module, FieldInfo field, EngineParameter param)
        {
            Module = module;
            Field = field;
            Param = param;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is EngineParameterInfo))
                return false;
            EngineParameterInfo info = (EngineParameterInfo)obj;
            return (Module == info.Module) & (Field == info.Field) & (Param == info.Param);
        }

        public override int GetHashCode()
        {
            return Module.GetHashCode() ^ Field.GetHashCode() ^ Param.GetHashCode();
        }

        public static bool operator ==(EngineParameterInfo p1, EngineParameterInfo p2)
        {
            return p1.Equals(p2);
        }

        public static bool operator !=(EngineParameterInfo p1, EngineParameterInfo p2)
        {
            return !p1.Equals(p2);
        }

        public string Name
        {
            get
            {
                return Field.Name;
            }
        }

        public Type FieldType
        {
            get
            {
                return Field.FieldType;
            }
        }

        public bool IsFitResult()
        {
            return Param.IsFitResult();
        }

        /// <summary>
        /// Gets the value of the associated field
        /// </summary>
        /// <returns>Value of the field, as the type of the field</returns>
        public object GetValue()
        {
            return Field.GetValue(Module);
        }

        /// <summary>
        /// Gets the value of the associated field as a string by calling ToString() on the field value
        /// </summary>
        /// <returns>Field value as a string</returns>
        public string GetValueStr()
        {
            return GetValue().ToString();
        }

        /// <summary>
        /// Set the value of the field to the value in a string
        /// Attempts to convert the string to the field type, will throw an exception if this fails
        /// </summary>
        /// <param name="value">Field value as a string</param>
        public void SetValue(string value)
        {
            Field.SetValue(Module, Convert.ChangeType(value, FieldType));
        }

        /// <summary>
        /// Check whether the value of this field agrees with the value in a config node
        /// If node is null, will return false
        /// Attempts to convert the string value from node into the type of the field, will throw an exception if this fails
        /// </summary>
        /// <param name="node">Config node to check</param>
        /// <returns>true, if the value of the field is equal to the value in the config node, false otherwise</returns>
        public bool EqualsValueInNode(ConfigNode node)
        {
            if (node == null)
                return false;
            string databaseString = node.GetValue(Name);
            return GetValueStr().Equals(databaseString ?? null);
        }

        /// <summary>
        /// Try to set the value of this field from the value in a config node
        /// If the field cannot be found in the config node, will return false
        /// If the type conversion fails, will return false
        /// </summary>
        /// <param name="node">Config node to read the value from</param>
        /// <returns>true if the operation was successful, false otherwise</returns>
        public bool SetValueFromNode(ConfigNode node)
        {
            string value = node.GetValue(Name);
            if (value != null)
            {
                try
                {
                    SetValue(value);
                }
                catch
                {
                    return false;
                }
                return true;
            }
            
            return false;
        }
    }
}
