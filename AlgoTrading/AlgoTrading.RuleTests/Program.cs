using AlgoTrading.Models;
using AlgoTrading.Models.Rules;
using System.Text.Json;

if (await DatabaseTestCommand.TryRunAsync(args)) return;

int assertionCount = 0;
DateTimeOffset origin = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
void Assert(bool condition, string message)
{
    assertionCount++;
    if (!condition) throw new Exception(message);
}
void Throws(Action action, string message)
{
    bool rejected = false;
    try { action(); } catch (ArgumentException) { rejected = true; }
    Assert(rejected, message);
}
CompletedCandle[] Series(string timeframe, int intervalMinutes, params decimal[] closes) => closes.Select((close, index) =>
    new CompletedCandle(new Candle(1, timeframe, origin.AddMinutes(index * intervalMinutes).UtcDateTime, close, close, close, close, 10),
        origin.AddMinutes((index + 1) * intervalMinutes))).ToArray();
RuleMarketData Market(params (string Timeframe, CompletedCandle[] Candles)[] series) =>
    new(1, series.ToDictionary(entry => entry.Timeframe, entry => (IReadOnlyList<CompletedCandle>)entry.Candles));
BoundRule Bound(ConditionDefinition condition, string timeframe = "15 minute") => RuleBinder.Bind(new RuleDefinition("Test rule", timeframe, condition));
RuleStatus Status(ConditionDefinition condition, RuleMarketData data, int minutes, string timeframe = "15 minute") =>
    Bound(condition, timeframe).BindData(data).Evaluate(origin.AddMinutes(minutes)).Status;
ComparisonCondition Equal(ValueDefinition left, double right) => new(left, ComparisonOperator.Equal, new NumberValue(right));
var closeValue = new CandleValue("15 minute");
var closeAboveTwo = new ComparisonCondition(closeValue, ComparisonOperator.GreaterThan, new NumberValue(2));
var crossedAboveTwo = new ComparisonCondition(closeValue, ComparisonOperator.CrossedAbove, new NumberValue(2));
var crossedBelowTwo = new ComparisonCondition(closeValue, ComparisonOperator.CrossedBelow, new NumberValue(2));
var basicData = Market(("15 minute", Series("15 minute", 15, 1, 3, 2, 1, 4)));
Assert(Status(crossedAboveTwo, basicData, 14) == RuleStatus.InsufficientData, "Cannot evaluate an unfinished candle");
Assert(Status(crossedAboveTwo, basicData, 15) == RuleStatus.InsufficientData, "First crossover needs previous history");
Assert(Status(crossedAboveTwo, basicData, 30) == RuleStatus.Matched, "Historical crossover uses current index");
Assert(Status(crossedAboveTwo, basicData, 45) == RuleStatus.NotMatched, "Historical result must not use final array entries");
Assert(Status(crossedBelowTwo, basicData, 60) == RuleStatus.Matched, "Cross below includes previous equality");
Assert(Status(crossedAboveTwo, basicData, 75) == RuleStatus.Matched, "Cross above threshold");
Assert(Bound(crossedAboveTwo).BindData(basicData).Evaluate(origin.AddMinutes(44)).EvaluatedAt == origin.AddMinutes(30), "Evaluation snaps to completed clock");

var relativeStrength = new IndicatorValue(IndicatorFunction.RelativeStrengthIndex, "15 minute", 2);
var smoothedRelativeStrength = new IndicatorValue(IndicatorFunction.SimpleMovingAverage, "15 minute", 2, relativeStrength);
var sampleData = Market(("15 minute", Series("15 minute", 15, 10, 12, 11, 13, 12)));
Assert(Status(new ComparisonCondition(relativeStrength, ComparisonOperator.GreaterThan, new NumberValue(85.71)), sampleData, 60) == RuleStatus.Matched, "Relative strength uses normalized averages");
Assert(Status(new ComparisonCondition(smoothedRelativeStrength, ComparisonOperator.GreaterThan, new NumberValue(76.19)), sampleData, 60) == RuleStatus.Matched, "Nested average recovers after indicator warmup");
Assert(Status(Equal(smoothedRelativeStrength, 0), sampleData, 45) == RuleStatus.InsufficientData, "Nested warmup requires full valid window");
var relativeStrengthCross = new ComparisonCondition(relativeStrength, ComparisonOperator.CrossedAbove, new NumberValue(80));
Assert(Status(relativeStrengthCross, sampleData, 60) == RuleStatus.Matched, "Indicator output crosses threshold, not source price");
var lowCandles = Series("15 minute", 15, 10, 12, 11, 13);
for (int index = 0; index < lowCandles.Length; index++)
{
    var candle = lowCandles[index].Candle;
    lowCandles[index] = new CompletedCandle(new Candle(1, "15 minute", candle.Timestamp, candle.Open, candle.High, 4 - index, candle.Close, 10), lowCandles[index].ClosedAt);
}
var lowStrength = new IndicatorValue(relativeStrength.Function, relativeStrength.Timeframe, relativeStrength.Length, new CandleValue("15 minute", CandleField.Low), relativeStrength.Multiplier);
Assert(Status(Equal(lowStrength, 0), Market(("15 minute", lowCandles)), 60) == RuleStatus.Matched, "Low source is respected");

var fastAverage = new IndicatorValue(IndicatorFunction.SimpleMovingAverage, "15 minute", 20);
var slowAverage = new IndicatorValue(IndicatorFunction.SimpleMovingAverage, "2 hour", 20);
var mixedCross = new ComparisonCondition(fastAverage, ComparisonOperator.CrossedAbove, slowAverage);
var fastPrices = Enumerable.Repeat(9m, 168).ToArray();
fastPrices[^1] = 31;
var slowPrices = Enumerable.Repeat(10m, 22).ToArray();
slowPrices[^1] = 10000;
var mixedData = Market(("15 minute", Series("15 minute", 15, fastPrices)), ("2 hour", Series("2 hour", 120, slowPrices)));
Assert(Status(mixedCross, mixedData, 2520) == RuleStatus.Matched, "Twenty-period mixed timeframe crossover aligns close times");
Assert(Status(mixedCross, mixedData, 2519) == RuleStatus.NotMatched, "No partial fifteen-minute candle");
slowPrices[20] = 50;
var changedSlowData = Market(("15 minute", Series("15 minute", 15, fastPrices)), ("2 hour", Series("2 hour", 120, slowPrices)));
Assert(Status(mixedCross, changedSlowData, 2520) == RuleStatus.NotMatched, "Simultaneous two-hour close participates");
var truncatedData = Market(("15 minute", Series("15 minute", 15, fastPrices)), ("2 hour", Series("2 hour", 120, slowPrices.Take(21).ToArray())));
Assert(Status(mixedCross, truncatedData, 2520) == Status(mixedCross, changedSlowData, 2520), "Future rows do not change historical results");

var unknown = Equal(new IndicatorValue(IndicatorFunction.SimpleMovingAverage, "15 minute", 100), 0);
Assert(Status(new NotCondition(unknown), basicData, 30) == RuleStatus.InsufficientData, "Not must not convert unavailable into match");
Assert(Status(new ConditionGroup(GroupOperator.All, [unknown, Equal(new NumberValue(1), 2)]), basicData, 30) == RuleStatus.NotMatched, "False dominates unknown in all group");
Assert(Status(new ConditionGroup(GroupOperator.Any, [unknown, Equal(new NumberValue(1), 1)]), basicData, 30) == RuleStatus.Matched, "True dominates unknown in any group");
Assert(Status(new ConditionGroup(GroupOperator.All, [RuleModelCompatibilityData.DisabledComparison(), closeAboveTwo]), basicData, 30) == RuleStatus.Matched, "Disabled condition is omitted");
Assert(Status(new ConditionGroup(GroupOperator.Any, [new ConditionGroup(GroupOperator.All, [closeAboveTwo, crossedAboveTwo]), Equal(new NumberValue(0), 1)]), basicData, 30) == RuleStatus.Matched, "Nested group meaning preserved");
Assert(Status(Equal(new ArithmeticValue(new NumberValue(1), ArithmeticOperator.Divide, new NumberValue(0)), 1), basicData, 30) == RuleStatus.InsufficientData, "Division by zero is unavailable");
Assert(Status(Equal(new RoundValue(new ArithmeticValue(new NumberValue(5), ArithmeticOperator.Divide, new NumberValue(2))), 3), basicData, 30) == RuleStatus.Matched, "Arithmetic brackets and rounding");
Assert(Status(Equal(new PreviousValue(closeValue, "15 minute", 1), 1), basicData, 30) == RuleStatus.Matched, "Previous candle offset");
Assert(Status(Equal(new CandleValue("15 minute", CandleField.Close, 1), 1), basicData, 30) == RuleStatus.Matched, "Direct candle offset");
Assert(Status(Equal(new PreviousValue(closeValue, "15 minute", 10), 1), basicData, 30) == RuleStatus.InsufficientData, "Insufficient offset history");
Assert(Status(Equal(new CountValue(closeAboveTwo, "15 minute", 3), 1), basicData, 45) == RuleStatus.Matched, "Count within full window");
Assert(Status(Equal(new CountValue(crossedAboveTwo, "15 minute", 3), 1), basicData, 60) == RuleStatus.Matched, "Count of historical crossovers");
Assert(Status(Equal(new CountValue(crossedAboveTwo, "15 minute", 3), 1), basicData, 45) == RuleStatus.InsufficientData, "Count does not treat unknown history as false");

var upperBand = new IndicatorValue(IndicatorFunction.BollingerUpperBand, "15 minute", 2, Multiplier: 2);
var lowerBand = new IndicatorValue(IndicatorFunction.BollingerLowerBand, upperBand.Timeframe, upperBand.Length, upperBand.Source, upperBand.Multiplier);
Assert(Status(Equal(upperBand, 14), sampleData, 60) == RuleStatus.Matched, "Upper Bollinger output");
Assert(Status(Equal(lowerBand, 10), sampleData, 60) == RuleStatus.Matched, "Lower Bollinger output");
Assert(Status(Equal(new IndicatorValue(IndicatorFunction.BollingerMiddleBand, upperBand.Timeframe, upperBand.Length, upperBand.Source, upperBand.Multiplier), 12), sampleData, 60) == RuleStatus.Matched, "Middle Bollinger output");
Assert(Status(Equal(new IndicatorValue(IndicatorFunction.ExponentialMovingAverage, "15 minute", 1), 13), sampleData, 60) == RuleStatus.Matched, "Exponential average length one");

var trendCandles = new[] { (High: 12m, Low: 8m, Close: 10m), (High: 14m, Low: 10m, Close: 12m), (High: 13m, Low: 9m, Close: 11m), (High: 10m, Low: 6m, Close: 7m), (High: 11m, Low: 7m, Close: 9m), (High: 17m, Low: 13m, Close: 16m) }
    .Select((prices, index) => new CompletedCandle(new Candle(1, "15 minute", origin.UtcDateTime, prices.Close, prices.High, prices.Low, prices.Close, 0), origin.AddMinutes(15 * (index + 1)))).ToArray();
var superTrend = new IndicatorValue(IndicatorFunction.SuperTrend, "15 minute", 1, Multiplier: 1);
double[] expectedTrend = [6, 8, 8, 13, 13, 7];
for (int index = 0; index < expectedTrend.Length; index++)
    Assert(Status(Equal(superTrend, expectedTrend[index]), Market(("15 minute", trendCandles)), (index + 1) * 15) == RuleStatus.Matched, "SuperTrend trailing bands and reversal");

var fullRelativeStrength = new IndicatorValue(IndicatorFunction.RelativeStrengthIndex, "15 minute", 14);
var fullSmoothedStrength = new IndicatorValue(IndicatorFunction.SimpleMovingAverage, "15 minute", 14, fullRelativeStrength);
var fullUpperBand = new IndicatorValue(upperBand.Function, upperBand.Timeframe, 20, upperBand.Source, upperBand.Multiplier);
ConditionDefinition[] requestedRules =
[
    new ConditionGroup(GroupOperator.All, [new ComparisonCondition(fullRelativeStrength, ComparisonOperator.CrossedAbove, fullSmoothedStrength), new ComparisonCondition(fullRelativeStrength, ComparisonOperator.CrossedAbove, new NumberValue(61.8)), new ComparisonCondition(fullSmoothedStrength, ComparisonOperator.GreaterThanOrEqual, new NumberValue(50))]),
    new ConditionGroup(GroupOperator.All, [new ComparisonCondition(closeValue, ComparisonOperator.GreaterThan, fullUpperBand), new ComparisonCondition(fullRelativeStrength, ComparisonOperator.CrossedAbove, new NumberValue(50))]),
    new ConditionGroup(GroupOperator.All, [new ComparisonCondition(closeValue, ComparisonOperator.CrossedAbove, new IndicatorValue(superTrend.Function, superTrend.Timeframe, 7, superTrend.Source, 3)), new ComparisonCondition(closeValue, ComparisonOperator.GreaterThan, fullUpperBand), new ComparisonCondition(fullRelativeStrength, ComparisonOperator.GreaterThan, new NumberValue(50))])
];
for (int index = 0; index < requestedRules.Length; index++)
{
    var definition = new RuleDefinition($"Requested example {index + 1}", "15 minute", requestedRules[index]);
    string saved = RuleDefinitionJson.Serialize(definition);
    var roundTripped = RuleDefinitionJson.Deserialize(saved);
    Assert(RuleBinder.Bind(roundTripped).Description == RuleBinder.Bind(definition).Description, "Saved rule round trip");
    Assert(RuleBinder.Bind(roundTripped).BindData(sampleData).Evaluate(origin.AddMinutes(60)).Status != RuleStatus.Matched, "Example cannot signal with insufficient history");
}
Assert(Bound(requestedRules[0]).Plan.IndicatorCalculations.Count == 2, "Shared nested indicators expanded once");
Assert(Bound(mixedCross).Plan.RequiredTimeframes.Count == 2, "Required timeframe discovery");
var editableChildren = new List<ConditionDefinition> { closeAboveTwo };
var frozen = Bound(new ConditionGroup(GroupOperator.All, editableChildren));
editableChildren.Clear();
Assert(frozen.BindData(basicData).Evaluate(origin.AddMinutes(30)).IsMatch, "Binding snapshots editable definitions");
var mutableCandles = Series("15 minute", 15, 1, 3);
var copiedMarket = Market(("15 minute", mutableCandles));
mutableCandles[1] = mutableCandles[0];
Assert(Status(crossedAboveTwo, copiedMarket, 30) == RuleStatus.Matched, "Market snapshot is immutable");
Throws(() => Bound(new ConditionGroup(GroupOperator.All, [])), "Empty groups rejected");
Throws(() => Bound(Equal(new CandleValue("15 minute", CandlesAgo: -1), 0)), "Future offsets rejected");
Throws(() => Bound(Equal(new IndicatorValue(IndicatorFunction.SimpleMovingAverage, "15 minute", 0), 0)), "Zero period rejected");
Throws(() => Bound(Equal(new IndicatorValue(IndicatorFunction.SuperTrend, "15 minute", 7, closeValue), 0)), "Misleading SuperTrend source rejected");
Throws(() => Bound(mixedCross).BindData(basicData), "Missing timeframe reported at binding");
Throws(() => Market(("15 minute", [trendCandles[0], trendCandles[0]])), "Duplicate close times rejected");
Throws(() => Bound(Equal(new NumberValue(double.NaN), 0)), "Nonfinite threshold rejected");
Throws(() => Bound(new ComparisonCondition(closeValue, (ComparisonOperator)999, new NumberValue(0))), "Unknown operator rejected");
// Every indicator must produce the same historical result with and without future rows.
var variedPrices = Enumerable.Range(0, 70).Select(index => 100m + index % 9 * 3 - index % 4 * 2).ToArray();
var allCandles = Series("15 minute", 15, variedPrices);
foreach (IndicatorFunction function in Enum.GetValues<IndicatorFunction>())
{
    var indicator = new IndicatorValue(function, "15 minute", 5, Multiplier: 2);
    var comparison = new ComparisonCondition(indicator, ComparisonOperator.GreaterThan, new NumberValue(100));
    var completeExecution = Bound(comparison).BindData(Market(("15 minute", allCandles)));
    for (int candleCount = 10; candleCount <= 60; candleCount += 10)
    {
        var prefixExecution = Bound(comparison).BindData(Market(("15 minute", allCandles.Take(candleCount).ToArray())));
        var timestamp = origin.AddMinutes(candleCount * 15);
        Assert(completeExecution.Evaluate(timestamp) == prefixExecution.Evaluate(timestamp), "Indicator must be causal: " + function);
    }
}
var equalityAtCurrent = new ComparisonCondition(new NumberValue(62), ComparisonOperator.CrossedAbove, new NumberValue(62));
Assert(Status(equalityAtCurrent, basicData, 30) == RuleStatus.NotMatched, "Current equality is not a crossover");
Assert(Status(new ComparisonCondition(closeValue, ComparisonOperator.NotEqual, new IndicatorValue(IndicatorFunction.SimpleMovingAverage, "15 minute", 100)), basicData, 30) == RuleStatus.InsufficientData, "NotEqual cannot match unavailable data");
var emptyMarket = Market(("15 minute", Array.Empty<CompletedCandle>()));
Assert(Status(closeAboveTwo, emptyMarket, 30) == RuleStatus.InsufficientData, "Empty completed series is unavailable");
var differentOffsetCandles = Series("15 minute", 15, 1, 3).Select(candle => new CompletedCandle(candle.Candle, candle.ClosedAt.ToOffset(TimeSpan.FromHours(5.5)))).ToArray();
Assert(Status(crossedAboveTwo, Market(("15 minute", differentOffsetCandles)), 30) == RuleStatus.Matched, "Timestamp offsets compare the same instants");
var staticFirst = Bound(new ConditionGroup(GroupOperator.All, [crossedAboveTwo, Equal(new NumberValue(5), 6)]));
Assert(staticFirst.BindData(basicData).Evaluate(origin.AddMinutes(45)).Explanation.Contains("5 equals 6"), "Ordinary comparisons evaluated first in all group");
var malformedDocument = RuleDefinitionJson.Serialize(new RuleDefinition("Saved", "15 minute", closeAboveTwo)).Replace("\"Operator\": \"GreaterThan\",", "");
bool malformedRejected = false;
try { RuleDefinitionJson.Deserialize(malformedDocument); } catch (JsonException) { malformedRejected = true; }
Assert(malformedRejected, "Missing saved operator cannot silently become greater-than");
ConditionDefinition deeplyNested = closeAboveTwo;
for (int depth = 0; depth < 60; depth++) deeplyNested = new NotCondition(deeplyNested);
Throws(() => Bound(deeplyNested), "Excessive nesting rejected");

// Compare the original requested rules against direct calculations with sufficient data.
// Independent Wilder calculations and a direct moving window provide the expected first rule.
var pricesForSignals = Enumerable.Range(0, 500).Select(index => 100m + (decimal)(20 * Math.Sin(index * 0.47) + 8 * Math.Sin(index * 1.11))).ToArray();
var signalCandles = Series("15 minute", 15, pricesForSignals);
var signalMarket = Market(("15 minute", signalCandles));
var originalIndicatorData = new InstrumentCandleData(1, "15 minute", signalCandles.Select(entry => entry.Candle).ToArray());
originalIndicatorData.AddIndicatorData(Indicator.RSI, null, 14);
originalIndicatorData.AddIndicatorData(Indicator.BollingerBands, null, 20, 2.0);
originalIndicatorData.AddIndicatorData(Indicator.SuperTrend, null, 7, 3.0);
double[] strengthValues = originalIndicatorData.IndicatorData!["RSI_Close_14"];
double[] upperValues = originalIndicatorData.IndicatorData["BollingerBands_Close_20_2_Upper"];
double[] trendValues = originalIndicatorData.IndicatorData["SuperTrend_HighLowClose_7_3"];
double[] averageStrengthValues = Enumerable.Range(0, signalCandles.Length).Select(index => index < 27 ? double.NaN : strengthValues.Skip(index - 13).Take(14).Average()).ToArray();
var executions = requestedRules.Select(condition => Bound(condition).BindData(signalMarket)).ToArray();
for (int index = 28; index < signalCandles.Length; index++)
{
    bool[] expected =
    [
        strengthValues[index - 1] <= averageStrengthValues[index - 1] && strengthValues[index] > averageStrengthValues[index]
            && strengthValues[index - 1] <= 61.8 && strengthValues[index] > 61.8 && averageStrengthValues[index] >= 50,
        (double)pricesForSignals[index] > upperValues[index] && strengthValues[index - 1] <= 50 && strengthValues[index] > 50,
        (double)pricesForSignals[index - 1] <= trendValues[index - 1] && (double)pricesForSignals[index] > trendValues[index]
            && (double)pricesForSignals[index] > upperValues[index] && strengthValues[index] > 50
    ];
    for (int exampleIndex = 0; exampleIndex < executions.Length; exampleIndex++)
        Assert(executions[exampleIndex].Evaluate(signalCandles[index].ClosedAt).IsMatch == expected[exampleIndex], "Requested strategy agrees with direct calculation");
}
var breakoutPrices = Enumerable.Repeat(100m, 30).Concat(new[] { 200m, 201m }).ToArray();
var breakoutMarket = Market(("15 minute", Series("15 minute", 15, breakoutPrices)));
foreach (ConditionDefinition requestedRule in requestedRules)
{
    var execution = Bound(requestedRule).BindData(breakoutMarket);
    Assert(execution.Evaluate(origin.AddMinutes(31 * 15)).IsMatch, "Requested strategy signals on completed breakout candle");
    Assert(!execution.Evaluate(origin.AddMinutes(32 * 15)).IsMatch, "A crossover is not a persistent above condition");
}
assertionCount += new CrossoverEventTests().Run();
Console.WriteLine($"Passed {assertionCount} regression assertions.");




