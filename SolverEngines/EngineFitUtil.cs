using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;

namespace SolverEngines
{
    /// <summary>
    /// Property describing parameters which will be input into an engine solver
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Field)]
    public class EngineParameter : Attribute { }

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
    public class EngineFitResult : EngineParameter { }

    /// <summary>
    /// Describes a field which has the EngineParameter (or derived from) property
    /// </summary>
    public struct EngineParameterInfo
    {
        public readonly ModuleEnginesSolver Module;
        public readonly FieldInfo Field;
        public readonly EngineParameter Param;

        public EngineParameterInfo(ModuleEnginesSolver module, FieldInfo field, EngineParameter param)
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

        public object GetValue()
        {
            return Field.GetValue(Module);
        }

        public string GetValueStr()
        {
            return GetValue().ToString();
        }

        public void SetValue(string value)
        {
            if (value == null)
                return;
            Field.SetValue(Module, Convert.ChangeType(value, Field.FieldType));
        }

        public bool EqualsValueInNode(ConfigNode node)
        {
            string databaseString = node.GetValue(Field.Name);
            object databaseValue = null;
            if (databaseString != null)
                databaseValue = Convert.ChangeType(node.GetValue(Field.Name), Field.FieldType);
            return Field.GetValue(Module).Equals(databaseValue);
        }

        public void SetValueFromNode(ConfigNode node)
        {
            SetValue(node.GetValue(Field.Name));
        }
    }
}
