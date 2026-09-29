namespace Crm.IntegrationTests.Infra;

/// <summary>
/// Testes que mudam o que vale para todos (configurações, ordem das etapas). O banco é compartilhado, então
/// eles rodam sozinhos, depois dos testes em paralelo, e restauram o estado no fim.
/// </summary>
[CollectionDefinition(nameof(EstadoGlobal), DisableParallelization = true)]
public sealed class EstadoGlobal;
