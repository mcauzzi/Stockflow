namespace Stockflow.Simulation.Core;

/// <summary>
/// Tolleranze numeriche condivise dal motore di simulazione.
/// </summary>
public static class SimMath
{
    /// <summary>
    /// Tolleranza per i confronti sul Progress di un'entità. <c>Speed * deltaTime</c>
    /// viene sommato tick dopo tick e l'aritmetica float accumula errore: senza una
    /// soglia un'entità può restare un tick in più ferma appena sotto 1.0. Usata da
    /// OneWayConveyor, ConveyorTurn, DiverterLogic e MergeLogic.
    /// </summary>
    public const float ProgressEpsilon = 1e-4f;

    /// <summary>
    /// True quando il Progress di un'entità ha raggiunto la fine del componente,
    /// cioè è &gt;= 1.0 entro <see cref="ProgressEpsilon"/>.
    /// </summary>
    public static bool ProgressComplete(float progress) => progress >= 1.0f - ProgressEpsilon;
}
