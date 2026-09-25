# Completion fixtures

All new CSVs are synthetic and hand-derived; no exchange OHLC is claimed.

basic-indicator-candles.csv preserves the earlier synthetic input as an external fixture. Period-3 SMA first value=2; EMA first value=2 then 2,2,2.5. RSI seed gains=2/3, losses=1/3 produces 66.6666667, and the final RSI is 80.95238095. Bands at index 2 use mean 2 and population standard deviation sqrt(2/3), producing 3.63299316 and 0.36700684 with multiplier 2. The same fixture exercises the counting cache stub without claiming its stub outputs are real indicator values.

daily-pivot-cases.csv follows the supplied SQL: P=(H+L+C)/3 (SQL decimal(25,6)), R1=2P-L, S1=2P-H, higher levels=P +/- n*(H-L), n=1..4. The asymmetric row exposes the old Classic level-3 mismatch. Both open and close qualify against inclusive ordered bounds. A flat row sets both flags. Raw values are used for qualification; SQL storage is decimal(18,4), observed read-only from the live schema. Source values must fit decimal(18,2).

daily-reference-candles.csv represents a deliberately short 75-minute synthetic session. Five 15-minute candles produce daily O=100,H=110,L=90,C=100,V=150. Hourly projection first candle O=100,H=110,L=90,C=103; final shortened 15-minute candle O=103,H=105,L=98,C=100. A 15-minute projection starts at O=100,H=104,L=99,C=102. These are not daily-OHLC copies. Monday is the source, Tuesday qualification, Wednesday a declared closure, Thursday activation.

integrated-indicators.csv uses close 1..7 and period 3. CompletedWarmupV1 EMA seeds with average 1,2,3=2; subsequent values 3,4,5,6. Outer EMA and SMA become 3 at index 4 and then 4,5. RSI of this EMA needs three changes and first becomes 100 at index 5. LegacyV1 retains first-price EMA seeding and is independently checked for early availability.
