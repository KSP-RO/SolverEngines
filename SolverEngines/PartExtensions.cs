namespace SolverEngines
{
    public static class PartExtensions
    {
        public static bool HasParsedPrefab(this Part part) => (part.partInfo != null);
    }
}
