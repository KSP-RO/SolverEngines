namespace SolverEngines.EngineFitting
{
    public interface IEngineIdentifier
    {
        string EnginePartName { get; }
        string EngineTypeName { get; }
        string EngineID { get; }
        string EngineConfigName { get; }
    }
}
