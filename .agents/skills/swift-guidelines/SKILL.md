---
name: swift-guidelines
description: Swift workflow and conventions for working in a real package, covering toolchain and tools-version choices, language modes, Apple availability on cross-platform code, verification, naming, comments, control flow, protocols, extensions, API design, error handling, concurrency, package structure, and Swift Testing. Use this whenever Swift comes up in any form, including writing, editing, refactoring, reviewing, or explaining Swift code; changing or reviewing .swift files or Package.swift; or answering a Swift API-design or style question, even if nobody asks for "guidelines" or a "style guide".
---

# Swift Guidelines

These are defaults. When one disagrees with the project's existing conventions or instructions, follow the project and mention the tension briefly. The examples show patterns and aren't templates to copy.

## Workflow

### Before changing code

- Read `Package.swift`. Note the tools version, language modes, `platforms`, and targets.
- Read the code around the change and match its style. Formatting comes from the project's `.editorconfig`, `.swift-format`, or format script, not from this file.
- Check which toolchains and platforms CI covers, so you know what your own run can't show.
- Ask when a design decision is open, such as which protocol to use or whether an abstraction earns its keep. Expect first drafts of APIs and names to change.

### After changing code

1. Run `swift build` and `swift test`. Use `swift test --filter <name>` while iterating, then run the whole suite.
2. Run the project's format script if it has one. `swift format` ships with the toolchain.
3. Run the program when the change affects what it does. For a library, call it from a small scratch package outside the repository, which also proves the public API is enough. Passing tests alone don't show it works.
4. Report what you checked and what you couldn't, such as Apple platforms from a Linux machine.

### Reviewing Swift

Beyond correctness, look for:

- New standard library or Foundation APIs that need a newer Apple OS than the package supports.
- Changes to public names, signatures, or lookup results that existing callers would notice.
- Syntax or APIs newer than the declared tools version implies.
- Force unwraps, `try!`, and `@unchecked Sendable` without a stated reason.

### Larger changes

Break a change that spans many files or systems into named, sequential phases, starting with scaffolding. Leave the project working after each phase. If the project has its own process for this, follow that instead.

## Design

- Build things to be tried early, and rebuild the clumsy parts rather than patching around them. Detouring to build a throwaway tool is fine when it lightens the task.
- Apply Occam's razor: don't add language features or abstractions beyond what the problem needs. Weigh a feature by how easily a programmer can use its power, not by its theoretical ceiling.
- A function or tool should do one thing well. This limits scope, not size, and matters less for GUIs.
- Supporting older platforms can force more complex code. That complexity is the cost of the constraint, not a failure of the razor.

## Toolchains and Compatibility

Three settings are easy to mix up, and they fail differently:

| Setting | Controls | When it's too new |
| --- | --- | --- |
| Tools version | Which toolchains can read the manifest | The toolchain refuses the package |
| Language mode | How the source is interpreted | Newer compilers still accept older modes |
| Apple availability | Which APIs exist on the user's OS | Compile error on Apple platforms only |

### Tools version

**Set `swift-tools-version` to the oldest toolchain you actually build and test against**, not the newest one installed. If only one toolchain is tested, that is the honest value.

- **Raising it drops older toolchains.** Dependency resolution treats those releases as ineligible and falls back to an earlier release, or fails if none qualifies.
- **Lowering it proves nothing by itself.** The tools version doesn't limit syntax: a newer compiler accepts newer syntax under an older tools version, then an older toolchain fails on it. To support an older toolchain, add it to CI or install it with `swiftly` and run the build there.
- **It also sets the default language mode.** Below 6.0 the default is mode 5, and from 6.0 it's mode 6. Set `swiftLanguageModes` explicitly to change the mode on its own.

### Language mode

Adopt a newer manifest and a newer language mode as separate steps when that's easier. Features can differ by mode: a bare `/a+/` regex literal compiles in mode 6 but not in mode 5, where `#/a+/#` works instead.

### Apple availability

On Apple platforms the standard library ships with the OS, so an API can need a newer OS than the toolchain suggests. `Regex` is the classic case: a toolchain that has it still reports `'Regex' is only available in macOS 13.0 or newer` for an older deployment target. `Mutex` from `Synchronization` has the same kind of limit.

**A Linux build doesn't show this.** Availability isn't enforced there, so `Regex`, `Mutex`, and even `@available(macOS 99, *)` compile without complaint. Cross-platform packages need a macOS CI job, and when you can't run one, check the API's availability in Apple's documentation and say it's unverified. Don't state exact OS versions from memory.

There are two ways to handle a newer API:

```swift
// Raise the floor for every Apple user of the package, in Package.swift.
platforms: [.macOS(.v13), .iOS(.v16)],
```

```swift
// Keep the floor, and guard the newer path.
if #available(macOS 13, iOS 16, *) {
  useRegex()
} else {
  useManualScan()
}
```

Raising the floor is simpler, but it's a breaking change for those users. Check whether a simpler API already covers the need before doing either. Foundation also differs across platforms, so test on each one a package claims to support.

## Naming

Clarity at the point of use comes first, even at the cost of a few extra characters.

- Name variables, parameters, and associated types after their role, not their type. A `String` might be `title` or `identifier`.
- Include every word needed to remove ambiguity at the call site, and drop words that carry no information there.
- When a type is too weak to convey a role, such as two `Int` parameters in a row, the name has to do that work.
- If the natural name collides with a keyword or native type, adjust it rather than fight the collision. A `Bool` case naturally called `true` becomes `isTrue`.

### Terminology

- Prefer a common word over an obscure one with the same meaning. Keep terms of art for when they capture something that would otherwise be lost, and use them in their established sense.
- Avoid abbreviations, especially nonstandard ones.
- Follow Swift and platform precedent rather than simplifying a name at the cost of conflicting with convention.

## Comments

- Keep comments sparse, since well-named code needs fewer of them.
- Write DocC comments for all `public` APIs, and elsewhere only when naming can't carry the meaning.
- Describe what the code does now, not its history. "Renamed from X" and "previously did Y" belong in commit messages.
- Write plain, short sentences without em dashes.
- Document the complexity of any computed property that isn't O(1), since readers assume property access is cheap.

## Control Flow

Prefer flat, linear logic over deep branching.

- Use `switch` where it fits.
- Otherwise use `if`, and invert it or use `guard` when that removes a level of nesting.
- Filter loops with `where` instead of wrapping the body in `if`: `for value in values where value > 500 { }`.

## Protocols

Compose a type's behavior from small protocols rather than sharing it through class inheritance. Each protocol should represent one capability, and a type conforms to as many as it needs. This gives shared behavior and interchangeable types without a forced single lineage or a base class bloated with behavior only some subclasses use.

```swift
protocol Adoptable {
  var ownerName: String { get }
  var name: String { get }
}

protocol Protecting {
  func protect()
}

struct Cat: Adoptable, Protecting {
  var ownerName: String
  var name: String

  func protect() { print("\(name) protects the home.") }
}

func adopt(_ pet: some Adoptable) {
  print("\(pet.ownerName) adopted \(pet.name).")
}
```

Naming:

- If the type *is something*, use a noun: `Sequence`, `View`, `Repository`.
- If it *is doing something*, use an `ing` adjective: `Loading`, `Generating`.
- If *something is done to* it, use an `able` or `ible` adjective: `Comparable`, `Codable`.

Using protocols as types:

- Prefer `some Protocol` for parameters and return types. It keeps the concrete type known to the compiler, and it works with protocols that have associated types.
- Use `any Protocol` when the concrete type really varies, such as a mixed array. It stores the value in a box. Write `any` explicitly even where the compiler allows a bare protocol name.
- Use `associatedtype` and `Self` sparingly.
- Group related protocols with a `typealias`: `typealias CityData = WeatherData & LocationData`.
- Take care with `Any`, `AnyObject`, and unconstrained generics, which make ambiguous overloads easy to create.

## Extensions

An extension needs a reason beyond splitting a file. If pasting every extension back into the type's main declaration would change nothing about what it does, the type shouldn't have been split.

Good reasons:

- **Protocol defaults.** Give defaults only for the protocol's own requirements, and sparingly. A conforming type can implement a requirement more simply, such as a synchronous method satisfying an `async` requirement.
- **Public API.** When a type has both internal and public surface, put the public API in an extension directly below the type, marked `// MARK: - Public API`. Keep private members in the type, and skip the split if everything is public. Stored properties must stay in the type, but an extension can hold computed ones. Members of a `public extension` are public by default.
- **Conformances and shared utilities.** Extensions can add a conformance outside a type's own file, or add a utility to a type you don't own. Put the utility on the most general type that fits, such as `clamped(to:)` on `Comparable`.

## API Design

### Names

- Favor names that read as grammatical English at the call site.
- Start factory methods with `make`: `x.makeIterator()`.
- The first argument to an initializer or factory shouldn't form a phrase with the base name: `x.makeWidget(cogCount: 47)`.
- Name functions after their side effects:
  - No side effects: a noun phrase, such as `x.distance(to: y)`.
  - Side effects: an imperative verb phrase, such as `x.sort()` or `x.append(y)`.
  - For mutating and nonmutating pairs, a verb operation uses the imperative for mutating and `ed` or `ing` for nonmutating (`sort`, `sorted`). A noun operation uses the noun for nonmutating and a `form` prefix for mutating (`union`, `formUnion`).
- Booleans read as assertions about the receiver: `x.isEmpty`, `line1.intersects(line2)`.

### Argument labels

- Omit labels when the arguments can't usefully be told apart: `min(a, b)`, `zip(first, second)`.
- Omit the first label in a value-preserving conversion: `Int64(someUInt32)`.
- When the first argument is part of a prepositional phrase, start the label at the preposition: `x.removeBoxes(havingLength: 12)`, `x.move(from: start, to: end)`.
- Otherwise, if it's part of a grammatical phrase, fold those words into the base name and omit the label: `x.addSubview(y)`.

## Error Handling

- Use `guard` for early exits.
- Propagate errors with `try`. Avoid `try!` and force unwraps unless failure truly can't happen, and say why when it isn't obvious.
- Use `defer` for cleanup that has to run on every return path.
- Validate input and report failures explicitly. Don't return a success-shaped value that hides one.

## Concurrency

- Don't mark a function `async` unless it actually suspends.
- Prefer value types that are naturally `Sendable`. Treat data-race errors in mode 6 as design feedback rather than something to silence.
- Use `@unchecked Sendable` only when you're protecting the state yourself, such as with a lock, and say how in a comment.

## Package Structure

- Library conventions, such as the public API split, assume other code will import the package. In an executable that nothing imports, keep them only where they aid clarity.
- A monolithic package has one library or executable target holding all of its code, across as many files as make sense. Small tools and GUIs are often monolithic, including SwiftUI apps and those using drop-in replacements such as SwiftCrossUI. This describes the current target structure, so revisit it as the project grows.

## Tests

- Use Swift Testing (`import Testing`, `@Test`) rather than XCTest for new tests, unless the suite already uses XCTest.
- Reduce duplication with shared helpers above the tests that use them, and generate cases with `@Test(arguments:)`. Two argument lists produce every combination, so pair them with `zip()` when you want them matched by position.
- Cover both the internals and the public API, since each can hide bugs the other misses. `@testable import` reaches internal code.
- Keep fixtures realistic. Hand-written samples tend to contain only the shapes their author already knows about, so test against real data when it's available.
