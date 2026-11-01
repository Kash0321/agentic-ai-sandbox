using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sandbox.Application.Sales;
using Sandbox.Domain.Results;

namespace Sandbox.Application.FaultInjection;

/// <summary>
/// Induce latencia y el error transitorio <c>SERVICE_UNAVAILABLE</c> según las reglas configuradas (design D7). El plan
/// de cada llamada se decide antes de ejecutarla, así que el patrón de fallos no depende de lo que tarde ni de lo que
/// devuelva la operación.
/// </summary>
public sealed partial class FaultInjector
{
    private readonly Lock _gate = new();
    private readonly RuleState[] _rules;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<FaultInjector> _logger;

    public FaultInjector(IOptions<FaultInjectionOptions> options, TimeProvider timeProvider, ILogger<FaultInjector> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        var settings = options.Value;
        _rules = [.. settings.Rules.Select((rule, index) => new RuleState(rule, settings.Seed + index))];
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta <paramref name="operation"/> aplicando el plan: primero la latencia y después el fallo antes o después
    /// de ejecutar, o la operación sin cambios.
    /// </summary>
    public async Task<Result<T>> RunAsync<T>(
        string operationName,
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var plan = Plan(operationName);

        if (plan.Latency > TimeSpan.Zero)
        {
            LogLatency(_logger, operationName, plan.LatencyRules, plan.Latency.TotalMilliseconds);
            await Task.Delay(plan.Latency, _timeProvider, cancellationToken);
        }

        if (plan.FailingRule is not { } failingRule)
        {
            return await operation(cancellationToken);
        }

        if (plan.Timing == FaultTiming.After)
        {
            // El resultado real se descarta: así se simula una respuesta perdida tras una operación que sí ocurrió.
            await operation(cancellationToken);
        }

        LogFault(_logger, operationName, failingRule, plan.Timing);
        return Result.Failure<T>(OperationErrors.ServiceUnavailable());
    }

    // Todas las reglas que cubren la operación avanzan su recuento y su secuencia aleatoria en cada llamada, falle o
    // no otra regla. Random no es seguro entre hilos, por eso todo ocurre con el cerrojo tomado.
    private FaultPlan Plan(string operationName)
    {
        if (_rules.Length == 0)
        {
            return FaultPlan.None;
        }

        lock (_gate)
        {
            var latency = TimeSpan.Zero;
            List<int> latencyRules = [];
            int? failingRule = null;
            var timing = FaultTiming.Before;

            for (var index = 0; index < _rules.Length; index++)
            {
                var rule = _rules[index];
                if (!rule.Covers(operationName))
                {
                    continue;
                }

                var fails = rule.NextCallFails();
                if (rule.Settings.Latency > TimeSpan.Zero)
                {
                    latency += rule.Settings.Latency;
                    latencyRules.Add(index);
                }

                if (fails && failingRule is null)
                {
                    failingRule = index;
                    timing = rule.Settings.Timing;
                }
            }

            return new FaultPlan(latency, latencyRules, failingRule, timing);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Fallo inducido en {Operation}: regla {Rule}, momento {Timing}")]
    private static partial void LogFault(ILogger logger, string operation, int rule, FaultTiming timing);

    [LoggerMessage(Level = LogLevel.Information, Message = "Latencia inducida en {Operation}: {LatencyMs} ms de las reglas {Rules}")]
    private static partial void LogLatency(ILogger logger, string operation, IReadOnlyList<int> rules, double latencyMs);

    private sealed class RuleState(FaultRule settings, int seed)
    {
        private readonly Random _random = new(seed);
        private int _calls;

        public FaultRule Settings { get; } = settings;

        public bool Covers(string operationName) =>
            Settings.Operation == FaultOperations.All || Settings.Operation == operationName;

        public bool NextCallFails()
        {
            _calls++;
            return Settings.Trigger switch
            {
                FaultTrigger.Always => true,
                FaultTrigger.FirstCalls => _calls <= Settings.Count,
                FaultTrigger.Probability => _random.NextDouble() < Settings.Probability,
                _ => false,
            };
        }
    }
}
