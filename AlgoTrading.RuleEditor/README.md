# AlgoTrading Rule Editor

A separate Windows desktop project for visually authoring strategy rules. It references the existing AlgoTrading.Models project. No database connections or order execution are included.

## Open and debug

Open AlgoTrading.RuleEditor.slnx in Visual Studio and set AlgoTrading.RuleEditor as the startup project. The project targets .NET 10 for Windows with Windows Presentation Foundation. It needs no additional NuGet packages.

Expected folders:

    D:\MyWorkspace\AlgoTrading.RuleEditor
    D:\MyWorkspace\AlgoTrading\AlgoTrading.Models

From the editor project directory:

    dotnet build AlgoTrading.RuleEditor.csproj
    dotnet run --project AlgoTrading.RuleEditor.csproj

## Use the editor

1. Choose New for a blank rule, or use the included moving-average breakout example.
2. Name the strategy and select the completed-candle evaluation timeframe.
3. Add condition rows and nested groups. Groups can match All, Any, or None of their enabled children.
4. Select either value in a row. Choose a candle field, number, indicator, calculation, rounded value, previous value or count.
5. To nest indicators, choose Indicator and edit Source. Repeat as needed. For example, the source of a Simple Moving Average can be a Relative Strength Index.
6. Choose an ordinary comparison or crossed above / crossed below between the two values.
7. Use the arrows to reorder, Duplicate to copy, Comment to add a note, and Enabled to omit a condition temporarily. Undo and Redo restore whole-document edits.
8. Validate and read the summary. Save rule writes the existing AlgoTrading.Models RuleDefinition JSON format. Open rule reads that same format, including the earlier database test's rule.json.

Changing the evaluation clock does not silently change each indicator's timeframe. Each indicator retains its own selection. Changing an indicator's own timeframe also moves a direct candle source that used the same old timeframe; separately configured nested sources retain their timeframes.

Value dialogs edit independent copies. Apply commits the change; Cancel leaves the rule unchanged. A Count expression opens a condition editor for the condition being counted. Numeric entry uses a dot as the decimal separator. No forming-candle evaluation option is exposed.

The editor supports the indicators already implemented by AlgoTrading.Models. It does not claim complete Chartink feature parity: exchange segment selection, a stock universe, pivot-specific selections, live scanning and broker/order controls are not part of this project. Timeframe keys describe the required data; the strategy data provider must supply those series. The editor does not manufacture unavailable timeframes.

## Classical code layout

Every new class and enumeration has its own file. Editable models use explicit fields, constructors and property accessors. Event handlers and helper methods are named, with ordinary block bodies. There are no records, primary constructors, lambdas, anonymous methods, local functions, expression-bodied members, target-typed construction, object initializer expressions, collection-expression shorthand, implicit local types or conditional-expression shorthand in the authored C# source.

The referenced AlgoTrading.Models library already contains records and other compact syntax. Those existing files are not rewritten. RuleDocumentAdapter is the explicit serialization boundary between the editor's classical mutable classes and the library's immutable rule definitions; this preserves Enabled and Comment without introducing initializer shorthand in the editor.

Useful debugging locations:

- Windows/MainWindow.xaml.cs: new/open/save, validation and document-level undo/redo.
- Controls/ConditionEditorControl.cs: condition rows and nested groups.
- Windows/ValueEditorWindow.xaml.cs: numeric fields and nested indicator/calculation inputs.
- Windows/ConditionEditorWindow.xaml.cs: conditions inside Count.
- Services/RuleDocumentAdapter.cs: conversion to and from the shared rule definitions.
- Services/EditorHistory.cs: independent document snapshots.
- Verification/EditorVerification.cs: deterministic checks for serialization, validation and execution.

Generated WPF files under obj and the referenced library follow their own code-generation/style conventions. They are not authored editor source.

## Verification

Run the application with the verification argument and an absolute report path:

    AlgoTrading.RuleEditor.exe --verify C:\path\editor-verification.txt

The command runs without opening the editor, writes a report and returns a nonzero exit code on failure. The checks include all seven value types, nested indicator discovery, group negation, comments, unavailable rules, independent copies, undo/redo and execution on completed candles through the referenced library.

Undo retains up to one hundred document edits. Saving requires a valid executable rule; incomplete groups remain editable but cannot be saved as executable definitions. A future strategy engine can read a saved file with RuleDefinitionJson.Deserialize and bind it through RuleBinder.Bind.
