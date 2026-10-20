---
name: dotnet-guidelines
description: C# and .NET coding conventions covering design process, naming, comments, control flow, interfaces, API design, error handling, project structure, and unit tests. Apply when writing, reviewing, editing, or refactoring C# code, or when answering questions about .NET naming, API shape, or style. These are conventions, not rigid rules; when they disagree with the codebase's existing conventions, match the codebase and mention the tension briefly.
---

# .NET Programming Guidelines

These are conventions for writing C# and .NET code, not rigid rules. Treat every section here as a tool to reach for, not a rule to enforce. Treat every code snippet below as an example of the pattern, not a template to copy verbatim. When a guideline and the user's existing codebase conventions disagree, match the codebase and mention the tension briefly. A project's `.editorconfig` is part of its conventions and wins over this file.

## Design Process

- Design and build software to be tried early, and don't hesitate to throw away the clumsy parts and rebuild them.
- Design through constant iteration and refinement. Treat a first pass, a draft API, a first naming attempt, as a starting point to revise together, not a final answer, and expect it to change as the conversation continues. This can mean detouring to build a tool first, when that's what lightens the task; expect to throw some of those tools out once you're done with them.
- Use structured, conversational Q&A to keep critical thinking with the user. When a design decision is genuinely open, e.g. which interface to reach for, how monolithic a solution actually is, whether an abstraction earns its keep, ask rather than deciding on the user's behalf.
- Favor Occam's razor: entities must not be multiplied beyond necessity. In practice this means weighing the language feature or abstraction against how easily a programmer can actually reach for its power, not against the feature's theoretical ceiling. Where it holds, the simplest way to write the code is also the most pragmatic one.[^backcompat]

[^backcompat]: Backward compatibility is the standing exception. A library that targets `netstandard2.0` or an older .NET release, or that multi-targets several frameworks, often can't use newer APIs and language features that would simplify it, and ends up with `#if NET8_0_OR_GREATER` branches or polyfills. Platform-specific code carries the same cost through `[SupportedOSPlatform]` and `OperatingSystem.IsWindows()` checks. That extra complexity isn't a violation of the razor: it's the cost of the constraint, not a failure to look for the simplest path within it.

## 1. Fundamentals

Clarity at the point of use is the goal that everything else here serves. When a naming choice, a parameter name, or a comment could go two ways, pick whichever reads more clearly at the call site, even if it costs a few extra characters. Clarity beats brevity.

- If you can't build or run the code yourself with the .NET SDK, the user can run it themselves the way they'd download and try a program, so hand off drafts for them to try rather than holding code back until it feels finished. Say clearly what you did and didn't compile.
- Make it easy to write, test, and run programs.
- Write programs, and methods, to do one thing and do it well. A method or an executable (command-line tools especially) can take on as much functionality as it needs, as long as all of it still serves that one intended purpose; the constraint is about scope, not size. (Recommended, but not required, for GUIs.)

## 2. Naming Conventions

- Follow .NET casing: PascalCase for namespaces, types, methods, properties, events, and constants; camelCase for parameters and locals; an `I` prefix for interfaces and a `T` prefix for type parameters. Private fields follow the codebase, usually `camelCase` or `_camelCase`.
- Name variables, parameters, and type parameters after the role they play, not the type they happen to be. A `string` parameter might still be named `title` or `identifier` depending on what it's for, and a type parameter is `TKey` or `TResult` rather than a bare `T` when there's more than one.
- Include every word needed to remove ambiguity for someone reading the *call site*, not just the declaration.
- Omit words that don't carry information at the use site. Every word in a name should earn its place. Don't repeat the type name in its members: `Order.Total`, not `Order.OrderTotal`.
- When a type alone is too weak to convey a parameter's role (e.g. two `int` parameters in a row), the name has to do that work instead.
- In the rare case where the most natural name collides with a C# keyword, rename it rather than reach for a verbatim identifier. A parameter naturally called `event` becomes `eventName` or `simEvent`, not `@event`.

## 3. Use Terminology Well

A term of art is a word or phrase with a precise, specialized meaning inside a particular field.

- Reach for a common word over an obscure one when both convey the same meaning. Don't say "epidermis" if "skin" works. Save terms of art for when they capture something that would otherwise be lost.
- If you do use a term of art, use it correctly, in its established sense.
- Avoid abbreviations, especially nonstandard ones. An abbreviation is effectively its own term of art: the reader has to know the expansion to understand it.
- Follow existing .NET and platform precedent rather than optimizing a name for a total beginner at the cost of conflicting with established convention. That includes acronym casing: two-letter acronyms stay uppercase (`IOStream`, `IPAddress`), longer ones are PascalCase (`HtmlParser`, `XmlReader`).

## 4. Comments

- Keep comments sparse. Well-named code needs fewer of them.
- Write XML documentation comments (`///`) where naming alone can't carry the meaning, and always for `public` APIs. A `public` API deserves the fuller treatment: `<summary>`, plus `<param>`, `<returns>`, and `<exception>` where they add information.
- Comments describe what the code does *now*, not its history. Skip past-tense, changelog-style notes ("renamed from X", "previously did Y", "refactored to..."). A comment is documentation, not a commit message.
- Write plain, short sentences without em dashes.
- Keep properties cheap. Readers treat property access as free, because fields trained them to. If getting a value is O(n), does I/O, or can throw for reasons beyond invalid state, make it a method (`GetTotal()`, `ComputeChecksum()`). If a property can't avoid being expensive, document its complexity.

## 5. Control Flow

- Keep control flow to simple constructs, and prefer flat, linear logic over deep branching.
- Avoid nesting conditions:
  - Reach for `switch` statements and `switch` expressions with pattern matching where they fit.
  - Where `switch` isn't the right shape, use `if`, and invert it when that removes a level of nesting.
  - Filter at the top of a loop body with `if (...) continue;` instead of wrapping the body in an `if`, or filter with LINQ when the code isn't performance-sensitive: `foreach (var value in values.Where(v => v > 500)) { }`.
- Prefer pattern matching over type checks followed by casts: `if (shape is Circle circle)`, not `if (shape is Circle) { var circle = (Circle)shape; }`.

## 6. Interfaces

> An interface defines a contract. Any class or struct that implements the interface must provide an implementation of the members it defines.

Interfaces are .NET's tool for compositional design, and a type can implement as many of them as it needs, even though it can inherit from only one class.

Prefer composing a type's behavior from several small, single-purpose interfaces over sharing it through class inheritance. Each interface should represent one capability: one thing the implementing type can do, or have done to it, not a bundle of unrelated behavior. A concrete type implements as many of these interfaces as it actually needs, so its full behavior comes from the union of them rather than from a shared base class only part of which is relevant.

```csharp
// Composing a type's behavior from multiple small interfaces, instead of
// bundling every capability into one base class.
public interface IAdoptable
{
    string OwnerName { get; set; }
    string Name { get; }
    void Feed();
    void Play();
}

public interface IProtecting
{
    void Protect();
}

// Cat's full behavior is the union of two interfaces, not inheritance from
// one shared Pet base class.
public sealed class Cat(string ownerName, string name) : IAdoptable, IProtecting
{
    private const string Emoji = "🐈";

    public string OwnerName { get; set; } = ownerName;
    public string Name { get; } = name;

    public void Feed() => Console.WriteLine($"{Emoji} {Name} eats.");
    public void Play() => Console.WriteLine($"{Emoji} {Name} plays.");
    public void Protect() => Console.WriteLine($"{Emoji} {Name} protects the home.");
}

static void Adopt(IAdoptable pet) =>
    Console.WriteLine($"{pet.OwnerName} adopted {pet.Name}.");

var josie = new Cat("Catarina", "Josie");
Adopt(josie); // Catarina adopted Josie.
```

This gets the practical benefits inheritance offers, shared behavior and interchangeable types, without its costs: no forced single lineage, no base class bloated with behavior only some subclasses use, no subclass stuck inheriting something it has to override away. "Composition" here means exactly this: a type implementing several independently useful interfaces at once, assembling its behavior from separate pieces, rather than extending one continuous hierarchy. Seal classes that aren't designed for inheritance.

Naming (always with the `I` prefix):
- If the implementer *is something*, name the interface with a noun: `IRepository`, `IBlockDevice`, `ILogger`.
- If *something is done to* the implementer, or it can do something, name it with an `able` or `ible` adjective: `IComparable`, `IDisposable`, `IEnumerable`.
- If the implementer *is doing something*, an `ing` adjective can work (`IProtecting`), though .NET precedent leans toward nouns and `able` adjectives, so check the codebase first.

Other tools:
- Used sparingly, generics with constraints (`where T : IComparable<T>`) let a method work over any implementer without boxing or casting. Static abstract interface members (`INumber<T>` and friends) cover operators and factory methods in generic code.
- Accept and return the narrowest interface that does the job: take `IEnumerable<T>` or `IReadOnlyList<T>` instead of `List<T>`, and return `IReadOnlyList<T>` to hide the concrete collection. This keeps the concrete type free to change.
- Take extra care with unconstrained polymorphism (`object`, `dynamic`, unconstrained type parameters). It's easy to create ambiguous overload sets this way, and `dynamic` gives up compile-time checking entirely.
- When a newer API only exists on some platforms or frameworks, guard the call with `OperatingSystem.Is...()` or `#if`, mark it with `[SupportedOSPlatform]`, and keep both paths behind the same interface.

## 7. Fluent API Design

- Favor method names that read as clear verb phrases at the call site: `order.Cancel()`, `stream.CopyTo(destination)`. Properties and types are nouns or noun phrases.
- Follow .NET naming patterns for common shapes:
  - `Create` for factory methods, e.g. `Widget.Create(cogCount: 47)`.
  - `Get` for methods that compute or fetch a value; a cheap, side-effect-free value is a property instead.
  - `To` for conversions that produce a new object (`ToArray()`, `ToString()`), and `As` for views over the same data (`AsSpan()`, `AsReadOnly()`).
  - `Try` for operations that are expected to fail sometimes: return `bool` and hand back the result through an `out` parameter, e.g. `int.TryParse(text, out var value)`.
  - An `Async` suffix on methods that return `Task` or `ValueTask`, and a `CancellationToken` as the last parameter.
- For a mutating/nonmutating pair, name them so the difference is visible:
  - The mutating form is an imperative verb that changes the receiver: `list.Sort()`, `builder.Append(x)`.
  - The nonmutating form returns a new value and reads as a description of that value: LINQ's `items.Order()`, or a `With` prefix on immutable types, e.g. `point.WithX(3)`. Records get this for free through `with` expressions.
- A Boolean property or method should read as an assertion about the receiver: `IsEmpty`, `HasValue`, `CanSeek`, `line1.Intersects(line2)`.

### Parameters and named arguments

```csharp
void Move(Point from, Point to);
piece.Move(from: start, to: end);
```

- Order parameters from most to least important, and keep the same order across overloads. Put `out` parameters and the `CancellationToken` last.
- Name a parameter for its role, since callers can use the name at the call site.
- Use named arguments at the call site when a literal's meaning isn't obvious: `Save(path, overwrite: true)`, not `Save(path, true)`. When the meaning isn't obvious even then, replace the `bool` with an enum.
- Skip names when the arguments can't usefully be told apart, e.g. `Math.Min(a, b)`, `first.Zip(second)`.
- Prefer overloads or optional parameters to long parameter lists. Past three or four parameters, consider an options type or record.

## 8. Error Handling

- Use guard clauses for early exits: validate at the top of a method and return or throw, so the main path stays flat.
- Validate arguments with the throw helpers: `ArgumentNullException.ThrowIfNull(value)`, `ArgumentException.ThrowIfNullOrEmpty(name)`, `ArgumentOutOfRangeException.ThrowIfNegative(count)`. They name the parameter for you.
- Throw the most specific exception that fits, and never throw `Exception` or `SystemException` themselves. Exceptions are for exceptional cases; use the `Try` pattern for failures callers should expect and handle.
- Catch only what you can handle, and catch it as specifically as you can. Catching `Exception` belongs at a top-level boundary (a request handler, a command loop, a plugin host) where the failure is logged or reported. Rethrow with `throw;`, not `throw ex;`, to keep the stack trace.
- Enable nullable reference types, and avoid the null-forgiving operator (`!`). It plays the same role as a force unwrap: handle the `null` case or fix the annotation instead.
- Use `using` declarations (and `await using`) to dispose resources when the scope exits, and `try`/`finally` for other cleanup that has to run regardless of which path a method takes.

## 9. Project Structure

This covers anything that outgrows a single code block: a library, a multi-project solution, a monolithic project, or just a task that needs more than a snippet to solve.

- Library-shaped conventions, such as a minimal `public` surface, XML docs on every public member, and binary compatibility, assume a project meant to be consumed by other code. If it's an executable, apply them where they still serve clarity, and skip them where they'd just add ceremony to a program nothing else will ever reference. Some solutions are monolithic instead.
  - "Monolithic" here means the solution has no separation into library, demo, and executable projects: there's just the one project, and all its code lives there. The code can still be spread across as many files and folders as makes sense; monolithic describes the solution's project structure, not its file count.
  - Small executables, command-line tools, and GUI apps are typically monolithic.
  - A demo can graduate into a fully functional executable or GUI over time. Treat "monolithic" as a snapshot of the solution's current structure, not a permanent label; revisit it as the project grows.
- Put one top-level type in each file, named after the type. Folders mirror namespaces, and namespaces are file-scoped.
- Keep shared `using` directives in a `GlobalUsings.cs` file (or `<Using>` items in the project file) rather than repeating them in every file.
- Default to `internal` and widen to `public` only when another project needs it. Use `InternalsVisibleTo` for test projects rather than making things public for tests.
- Once a solution has several projects, move shared build settings into `Directory.Build.props` and package versions into `Directory.Packages.props` (central package management).
- Large-scale file I/O changes should ship in named phases (Phase A/B, Phase 1/2, or a three-to-five-phase plan), always in a predictable sequence, and always starting with scaffolding.
  - As a guide rather than a rule, close each phase out with a bulleted task list of the phases already done and the ones still coming, marking off (`[x]`) whatever's complete. It's a lightweight form of memory retention across a rollout, not a required deliverable.

## 10. Unit Tests

This section applies to projects with unit tests.

- Match the test framework the solution already uses. For a new test project, default to xUnit, and name it after the project it tests with a `.Tests` suffix (`Venture.OS.Tests`).
- Reduce duplication:
  - Put commonly used helper methods, records, and constants in the test class or a shared fixture, above the tests that share them.
  - Use `[Theory]` with `[InlineData]` (or `[MemberData]` for computed cases) to generate multiple cases from one test method, e.g. `[InlineData("1", "jumped")]`, `[InlineData("🦊", "leaped")]`. Each `InlineData` row is one case; to cover the cross product of two lists, build the rows in a `[MemberData]` source.
- Interfaces and small fakes can shape test fixtures the same way they shape production code. Prefer a hand-written fake implementing a small interface over a mocking library when the interface is small.
- Keep tests deterministic: no real clocks, random seeds, network, or shared mutable state between tests.
- Cover both the internal pipeline and the public API. A test suite that only exercises the public surface can miss bugs in the internals it doesn't observe directly, and one that only exercises internals can miss integration problems the public API surface would catch.
