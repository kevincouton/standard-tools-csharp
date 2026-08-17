using System.Text.Json;

namespace StandardTools.Agent;

/// <summary>
/// Registry of all tools exposed to agents.
/// </summary>
public static class ToolRegistry
{
    public static IReadOnlyList<ToolDefinition> ListTools() =>
        new List<ToolDefinition>
        {
            Def(ToolNames.Health, "Return agent health status.", Obj(new())),
            Def(ToolNames.ListTools, "List all registered tool names.", Obj(new())),
            Def(ToolNames.IndicatorsCalculate, "Calculate a technical indicator from a price series.",
                Obj(new()
                {
                    ["dates"] = Arr("string", "date"),
                    ["opens"] = Arr("number"),
                    ["highs"] = Arr("number"),
                    ["lows"] = Arr("number"),
                    ["closes"] = Arr("number"),
                    ["volumes"] = Arr("integer"),
                    ["indicator"] = Str(),
                    ["params"] = Obj(new())
                }, new[] { "dates", "closes", "indicator" })),
            Def(ToolNames.MetricsRisk, "Compute risk metrics from a price series.",
                Obj(new() { ["values"] = Arr("number"), ["risk_free_rate"] = Num() }, new[] { "values" })),
            Def(ToolNames.MetricsReturn, "Compute return metrics from a price series.",
                Obj(new() { ["values"] = Arr("number"), ["risk_free_rate"] = Num() }, new[] { "values" })),
            Def(ToolNames.AnalysisRegression, "Run a linear regression of asset returns on benchmark returns.",
                Obj(new() { ["asset_returns"] = Arr("number"), ["benchmark_returns"] = Arr("number") }, new[] { "asset_returns", "benchmark_returns" })),
            Def(ToolNames.AnalysisOptions, "Price a European option using the Black-Scholes model.",
                Obj(new()
                {
                    ["spot"] = Num(),
                    ["strike"] = Num(),
                    ["risk_free_rate"] = Num(),
                    ["volatility"] = Num(),
                    ["time_to_maturity"] = Num(),
                    ["option_type"] = JsonEnum("string", new[] { "call", "put" })
                }, new[] { "spot", "strike", "risk_free_rate", "volatility", "time_to_maturity", "option_type" })),
            Def(ToolNames.RunBuyAndHold, "Run a buy-and-hold backtest on a price series.", BacktestSchema()),
            Def(ToolNames.RunSmaCrossover, "Run an SMA crossover backtest on a price series.", BacktestSchema(requiredParams: new[] { "fast", "slow" })),
            Def(ToolNames.RunWalkForward, "Run walk-forward optimization on a price series.",
                Obj(new()
                {
                    ["dates"] = Arr("string", "date"),
                    ["closes"] = Arr("number"),
                    ["strategy"] = Str(),
                    ["train_size"] = Int(),
                    ["test_size"] = Int(),
                    ["param_grid"] = Obj(new()),
                    ["metric"] = JsonEnum("string", new[] { "total_return", "sharpe", "win_rate" }),
                    ["initial_capital"] = Num(),
                    ["commission_rate"] = Num()
                }, new[] { "dates", "closes", "strategy", "train_size", "test_size" })),
            Def(ToolNames.RunMonteCarlo, "Run a Monte Carlo simulation over trades produced by a backtest.",
                Obj(new()
                {
                    ["dates"] = Arr("string", "date"),
                    ["closes"] = Arr("number"),
                    ["strategy"] = Str(),
                    ["simulations"] = Int(),
                    ["seed"] = Int(),
                    ["params"] = Obj(new()),
                    ["initial_capital"] = Num(),
                    ["commission_rate"] = Num()
                }, new[] { "dates", "closes", "strategy", "simulations" })),
            Def(ToolNames.PortfolioOptimize, "Run mean-variance portfolio optimization.",
                Obj(new()
                {
                    ["returns"] = NestedArr("number"),
                    ["labels"] = Arr("string"),
                    ["risk_free_rate"] = Num(),
                    ["objective"] = JsonEnum("string", new[] { "max_sharpe", "min_volatility", "target_return", "target_volatility" }),
                    ["target_return"] = Num(),
                    ["target_volatility"] = Num()
                }, new[] { "returns", "labels", "objective" })),
            Def(ToolNames.RiskParity, "Compute inverse-volatility risk-parity portfolio weights.",
                Obj(new()
                {
                    ["returns"] = NestedArr("number"),
                    ["labels"] = Arr("string")
                }, new[] { "returns", "labels" })),
            Def(ToolNames.BlackLitterman, "Run the simplified Black-Litterman model with expert views.",
                Obj(new()
                {
                    ["returns"] = NestedArr("number"),
                    ["labels"] = Arr("string"),
                    ["market_caps"] = Obj(new()),
                    ["views"] = Obj(new()),
                    ["tau"] = Num(),
                    ["risk_aversion"] = Num()
                }, new[] { "returns", "labels", "market_caps", "views" })),
            Def(ToolNames.RunScreener, "Screen a universe of tickers by fundamental criteria.",
                Obj(new() { ["tickers"] = Arr("string"), ["criteria"] = Obj(new()) }, new[] { "tickers" }))
        };

    public static ToolDefinition? FindTool(string name)
    {
        foreach (var tool in ListTools())
        {
            if (tool.Name == name)
                return tool;
        }
        return null;
    }

    private static ToolDefinition Def(string name, string description, object parameters) => new()
    {
        Name = name,
        Description = description,
        Parameters = JsonSerializer.SerializeToElement(parameters, AgentJsonOptions.Instance)
    };

    private static JsonElement BacktestSchema(string[]? requiredParams = null)
    {
        var properties = new Dictionary<string, object>
        {
            ["dates"] = Arr("string", "date"),
            ["closes"] = Arr("number"),
            ["initial_capital"] = Num(),
            ["commission_rate"] = Num()
        };

        if (requiredParams is not null)
        {
            foreach (var p in requiredParams)
                properties[p] = Str();
        }

        var required = new List<string> { "dates", "closes" };
        if (requiredParams is not null)
            required.AddRange(requiredParams);

        return JsonSerializer.SerializeToElement(Obj(properties, required), AgentJsonOptions.Instance);
    }

    private static Dictionary<string, object> Obj(Dictionary<string, object> properties, IEnumerable<string>? required = null)
    {
        var result = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties
        };
        if (required is not null)
            result["required"] = required.ToArray();
        return result;
    }

    private static Dictionary<string, object> Arr(string itemType, string? format = null)
    {
        var items = new Dictionary<string, object> { ["type"] = itemType };
        if (format is not null)
            items["format"] = format;
        return new Dictionary<string, object> { ["type"] = "array", ["items"] = items };
    }

    private static Dictionary<string, object> NestedArr(string itemType) =>
        new()
        {
            ["type"] = "array",
            ["items"] = new Dictionary<string, object> { ["type"] = "array", ["items"] = new Dictionary<string, object> { ["type"] = itemType } }
        };

    private static Dictionary<string, object> Str() => new() { ["type"] = "string" };
    private static Dictionary<string, object> Num() => new() { ["type"] = "number" };
    private static Dictionary<string, object> Int() => new() { ["type"] = "integer" };

    private static Dictionary<string, object> JsonEnum(string type, string[] values) =>
        new() { ["type"] = type, ["enum"] = values };
}
