# Indicator recovery fixture

Synthetic, hand-derived data; not SQL or exchange data. Six completed 15-minute candles have closes 10,10,10,10,10,11. RSI length is 3. The first three observations are unavailable. Zero gains and losses then give 50 (the existing evaluator's convention). The final positive change with no losses gives 100. Default source must equal explicit Close. Prefix-only calculations must equal corresponding full-history values.
