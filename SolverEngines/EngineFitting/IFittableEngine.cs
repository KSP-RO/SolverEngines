namespace SolverEngines.EngineFitting
{
    public interface IFittableEngine : IEngineIdentifier
    {
        bool CanFitEngine { get; }

        void DoEngineFit();

        void PushFitParamsToSolver();
    }
}
