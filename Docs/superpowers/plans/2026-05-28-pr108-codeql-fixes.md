# PR #108 — CodeQL Findings Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Resolve all CodeQL alerts on PR #108 — 6 "Uncontrolled data used in path expression" (path injection) and 11 "Log entries created from user input" (log injection) — with minimal, robust changes.

**Architecture:** Two independent fixes, both in `Stockflow.Webserver`. (1) Harden `FileScenarioRepository.PathFor` so the user id is canonicalized and verified to stay inside the scenarios root — a single guard that clears all 6 path-injection alerts. (2) Add a tiny `LogSanitizer.Clean` helper that strips CR/LF and wrap every user-controlled string argument in the two affected controllers — clears all 11 log-injection alerts.

**Tech Stack:** .NET 10, ASP.NET Core. No new NuGet packages, no new test project (none exists for the Webserver; verification is build + CodeQL re-scan on push).

---

## Context for the implementer

- The repo's pure engine (`Stockflow.Simulation`) is **not** touched. All changes live in `Sources/Stockflow.Webserver/`.
- There is **no** xUnit project covering the Webserver (only `Stockflow.Tests.Simulation`). Spinning one up for two small security fixes is out of scope for a "quick fix"; verification here is `dotnet build` plus the CodeQL re-scan that runs automatically on push to PR #108.
- The CodeQL alerts are listed at `https://github.com/mcauzzi/Stockflow/security/code-scanning` (alerts 16–21 = path injection, 4–15 = log injection).
- Existing exceptions live in `Sources/Stockflow.Webserver/Scenarios/IScenarioRepository.cs`: `InvalidScenarioIdException(string id)`, `ScenarioNotFoundException`, `ScenarioAlreadyExistsException`.

### Why these fixes satisfy CodeQL

- **Path injection:** `ValidateId` already blocks path separators via the regex `^[a-zA-Z0-9._-]{1,64}$`, so traversal is not actually exploitable today. But CodeQL's dataflow does not recognise that regex as a sanitizer. Canonicalizing with `Path.GetFullPath` and asserting the result stays under the root **is** a guard CodeQL recognises, and adds genuine defense-in-depth.
- **Log injection:** Several log calls fire on the *rejected-id* path (e.g. `POST → 400 invalid id '{Id}'`), where the id has **not** passed validation and could contain `\r\n`. Stripping CR/LF before logging is the recognised mitigation.

---

## File Structure

- **Create:** `Sources/Stockflow.Webserver/Logging/LogSanitizer.cs` — single static helper, one responsibility: strip CR/LF from strings bound for logs.
- **Modify:** `Sources/Stockflow.Webserver/Scenarios/FileScenarioRepository.cs` — rewrite `PathFor` to canonicalize + containment-check.
- **Modify:** `Sources/Stockflow.Webserver/Controllers/ScenarioController.cs` — wrap user-controlled string args (`id`, `scenario.Id`) in log calls with `LogSanitizer.Clean`.
- **Modify:** `Sources/Stockflow.Webserver/Controllers/SessionController.cs` — wrap `req.ScenarioId` (`{Sid}`) in log call at line 86 with `LogSanitizer.Clean`. (Other args there are `Guid` and not user-controlled strings.)

---

## Task 1: Harden the scenario file path against traversal

**Files:**
- Modify: `Sources/Stockflow.Webserver/Scenarios/FileScenarioRepository.cs:87`

- [ ] **Step 1: Rewrite `PathFor` to canonicalize and verify containment**

Replace the current one-line method:

```csharp
    private string PathFor(string id) => Path.Combine(_scenariosPath, $"{id}.json");
```

with:

```csharp
    private string PathFor(string id)
    {
        var root      = Path.GetFullPath(_scenariosPath);
        var candidate = Path.GetFullPath(Path.Combine(root, $"{id}.json"));

        var rootWithSep = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(rootWithSep, StringComparison.Ordinal))
            throw new InvalidScenarioIdException(id);

        return candidate;
    }
```

Notes:
- `InvalidScenarioIdException` is already in scope (same `Stockflow.Webserver.Scenarios` namespace, defined in `IScenarioRepository.cs`).
- All six flagged call sites (`Get` line 46, `Create` line 56, `Delete` line 80, and the `File.Exists`/`File.OpenRead`/`File.Create` that consume the result) now receive a path that has passed the guard, so all six alerts clear from one change.

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build Sources/Stockflow.Webserver/`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Sources/Stockflow.Webserver/Scenarios/FileScenarioRepository.cs
git commit -m "fix(webserver): canonicalize scenario path to close CodeQL path-injection alerts"
```

---

## Task 2: Add the log sanitizer helper

**Files:**
- Create: `Sources/Stockflow.Webserver/Logging/LogSanitizer.cs`

- [ ] **Step 1: Create the helper**

```csharp
namespace Stockflow.Webserver.Logging;

/// <summary>
/// Removes CR/LF from user-controlled strings before they reach the logger,
/// preventing forged/injected log entries (CodeQL "Log entries created from user input").
/// </summary>
internal static class LogSanitizer
{
    public static string Clean(string? value) =>
        value is null ? string.Empty : value.Replace("\r", "").Replace("\n", "");
}
```

- [ ] **Step 2: Build to verify it compiles**

Run: `dotnet build Sources/Stockflow.Webserver/`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add Sources/Stockflow.Webserver/Logging/LogSanitizer.cs
git commit -m "feat(webserver): add LogSanitizer helper to strip CRLF from logged user input"
```

---

## Task 3: Sanitize user input in ScenarioController logs

**Files:**
- Modify: `Sources/Stockflow.Webserver/Controllers/ScenarioController.cs`

- [ ] **Step 1: Add the using directive**

After line 2 (`using Stockflow.Webserver.Scenarios;`), add:

```csharp
using Stockflow.Webserver.Logging;
```

- [ ] **Step 2: Wrap `id` in the `Get` action (lines 32 and 39)**

Change line 32 from:

```csharp
                logger.LogDebug("GET /api/scenarios/{Id} → 404", id);
```
to:
```csharp
                logger.LogDebug("GET /api/scenarios/{Id} → 404", LogSanitizer.Clean(id));
```

Change line 39 from:

```csharp
            logger.LogWarning("GET /api/scenarios/{Id} → 400 invalid id", id);
```
to:
```csharp
            logger.LogWarning("GET /api/scenarios/{Id} → 400 invalid id", LogSanitizer.Clean(id));
```

- [ ] **Step 3: Wrap `scenario.Id` in the `Create` action (lines 50, 55, 60)**

Change line 50 from:

```csharp
            logger.LogInformation("POST /api/scenarios → created '{Id}'", scenario.Id);
```
to:
```csharp
            logger.LogInformation("POST /api/scenarios → created '{Id}'", LogSanitizer.Clean(scenario.Id));
```

Change line 55 from:

```csharp
            logger.LogWarning("POST /api/scenarios → 400 invalid id '{Id}'", scenario.Id);
```
to:
```csharp
            logger.LogWarning("POST /api/scenarios → 400 invalid id '{Id}'", LogSanitizer.Clean(scenario.Id));
```

Change line 60 from:

```csharp
            logger.LogWarning("POST /api/scenarios → 409 '{Id}' already exists", scenario.Id);
```
to:
```csharp
            logger.LogWarning("POST /api/scenarios → 409 '{Id}' already exists", LogSanitizer.Clean(scenario.Id));
```

- [ ] **Step 4: Wrap both args in the `Update` action (lines 70 and 77)**

Change line 70 from:

```csharp
            logger.LogWarning("PUT /api/scenarios/{Id} → 400 body id '{BodyId}' mismatch", id, scenario.Id);
```
to:
```csharp
            logger.LogWarning("PUT /api/scenarios/{Id} → 400 body id '{BodyId}' mismatch", LogSanitizer.Clean(id), LogSanitizer.Clean(scenario.Id));
```

Change line 77 from:

```csharp
            logger.LogInformation("PUT /api/scenarios/{Id} → updated", id);
```
to:
```csharp
            logger.LogInformation("PUT /api/scenarios/{Id} → updated", LogSanitizer.Clean(id));
```

- [ ] **Step 5: Wrap `id` in the `Delete` action (lines 98 and 101)**

Change line 98 from:

```csharp
                logger.LogWarning("DELETE /api/scenarios/{Id} → 404", id);
```
to:
```csharp
                logger.LogWarning("DELETE /api/scenarios/{Id} → 404", LogSanitizer.Clean(id));
```

Change line 101 from:

```csharp
            logger.LogInformation("DELETE /api/scenarios/{Id} → deleted", id);
```
to:
```csharp
            logger.LogInformation("DELETE /api/scenarios/{Id} → deleted", LogSanitizer.Clean(id));
```

- [ ] **Step 6: Build to verify it compiles**

Run: `dotnet build Sources/Stockflow.Webserver/`
Expected: Build succeeded, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add Sources/Stockflow.Webserver/Controllers/ScenarioController.cs
git commit -m "fix(webserver): sanitize user ids in ScenarioController logs (CodeQL log-injection)"
```

---

## Task 4: Sanitize user input in SessionController log

**Files:**
- Modify: `Sources/Stockflow.Webserver/Controllers/SessionController.cs`

- [ ] **Step 1: Add the using directive**

After line 7 (`using Stockflow.Webserver.Sessions;`), add:

```csharp
using Stockflow.Webserver.Logging;
```

- [ ] **Step 2: Wrap `req.ScenarioId` in `LoadScenario` (line 86)**

Change line 86 from:

```csharp
            logger.LogWarning("POST /api/sessions/{Id}/scenario/load → 404 scenario '{Sid}'", id, req.ScenarioId);
```
to:
```csharp
            logger.LogWarning("POST /api/sessions/{Id}/scenario/load → 404 scenario '{Sid}'", id, LogSanitizer.Clean(req.ScenarioId));
```

Note: `id` here is a `Guid` route param (`{id:guid}`) — not a user-controlled string, so it is left as-is. Only `{Sid}` (`req.ScenarioId`, from the request body) was flagged.

- [ ] **Step 3: Build to verify it compiles**

Run: `dotnet build Sources/Stockflow.Webserver/`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add Sources/Stockflow.Webserver/Controllers/SessionController.cs
git commit -m "fix(webserver): sanitize scenarioId in SessionController log (CodeQL log-injection)"
```

---

## Task 5: Full build + push for CodeQL re-scan

- [ ] **Step 1: Build the whole solution**

Run: `dotnet build Stockflow.slnx`
Expected: Build succeeded, 0 errors, 0 warnings introduced by these changes.

- [ ] **Step 2: Run existing tests (ensure no regression)**

Run: `dotnet test`
Expected: All existing simulation tests pass (no Webserver tests exist, so this only confirms nothing else broke).

- [ ] **Step 3: Push the branch and let CodeQL re-scan**

```bash
git push
```
Expected: The CodeQL workflow on PR #108 re-runs and the 17 alerts (6 path-injection + 11 log-injection) close as fixed.

---

## Self-Review

**Spec coverage:**
- Path injection alerts 16–21 (FileScenarioRepository lines 47/57/81/82/91/97) → all originate from `PathFor`; Task 1 hardens that single method. ✔
- Log injection alerts 4–15 (ScenarioController lines 32/39/50/55/60/70×2/77/86/98/101) → Task 3 wraps every user-controlled string arg. ✔
- Log injection alert 8 (SessionController line 86, `{Sid}`) → Task 4. ✔

**Placeholder scan:** No TBD/TODO/"handle edge cases"; every edit shows exact before/after code. ✔

**Type consistency:** `LogSanitizer.Clean(string?)` defined in Task 2 (namespace `Stockflow.Webserver.Logging`), used with matching signature in Tasks 3–4; `using Stockflow.Webserver.Logging;` added in both controllers. `InvalidScenarioIdException(string id)` reused in Task 1 matches its definition in `IScenarioRepository.cs`. ✔

**Tradeoff noted:** No TDD test added because the Webserver has no test project; creating one for two small security fixes contradicts the "quick fix" goal. Verification is build + CodeQL re-scan. If desired later, a `Stockflow.Tests.Webserver` project could add a `PathFor` traversal test and a `LogSanitizer.Clean` test.
