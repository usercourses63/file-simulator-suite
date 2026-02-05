// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

// Suppress async suffix requirement for test methods
[assembly: SuppressMessage("Style", "VSTHRD200:Use \"Async\" suffix for async methods",
    Justification = "Test method naming convention does not require Async suffix")]

// Suppress ConfigureAwait requirements for test methods
[assembly: SuppressMessage("Reliability", "CA2007:Consider calling ConfigureAwait on the awaited task",
    Justification = "Test methods don't need ConfigureAwait")]

// Suppress nullable reference warnings for test assertions
[assembly: SuppressMessage("Style", "IDE0270:Use coalesce expression",
    Justification = "Explicit null checks improve test readability")]
