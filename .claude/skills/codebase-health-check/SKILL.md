---
name: codebase-health-check
description: This skill should be used when the user asks to "run a codebase health check", "do a maintenance review", "audit the codebase", "review code health", "check for tech debt", "review dependencies", or wants a periodic whole-codebase health assessment that files findings as labeled GitHub issues. Also trigger when the user mentions running this "every few months" or asks what should be improved across the codebase.
version: 0.1.0
---

# Codebase Health Check

A periodic (every-few-months) whole-codebase health assessment for the photo-booth-take-two repository. Examines nine dimensions, triages findings by priority and area, and files each as a labeled GitHub issue after user approval.

This skill is **read-only until the confirmation gate** at Phase 3. No code is modified; no issues are filed without approval.

Load `references/dimension-checklist.md` now for the detailed per-dimension questions before beginning Phase 0.

---

## Workflow

### Phase 0 — Orient

1. Read `CLAUDE.md`, `SPEC.md`, `README.md` to understand the intended architecture and user-facing behavior.
2. Search for prior health-check runs by querying GitHub: `gh issue list --state all --search "codebase health check" --limit 10`. Note the date of the most recent tracking issue; focus subsequent analysis on what could have changed since then.
3. Record the current date (available from `date` in Bash). It will appear in the tracking issue title.

### Phase 1 — Assess

Spawn parallel subagents (using the `Agent` tool with `subagent_type: "Explore"` or `"general-purpose"`) to cover the nine dimensions concurrently. Assign each agent a specific dimension and its checklist from `references/dimension-checklist.md`. Collate results before proceeding.

**Dimensions to assess** (see `references/dimension-checklist.md` for detailed questions):

1. **Architecture & code organization** — layer-boundary integrity, clean-arch dependency direction, dead code, naming consistency.
2. **Documentation accuracy & drift** — cross-check `CLAUDE.md`/`SPEC.md`/`README.md` claims against actual code, config, and commands.
3. **Security** — evaluate the controls described in `CLAUDE.md §Security` against the current implementation; check input validation, secret handling.
4. **Dependencies** — necessity, license acceptability, maintenance status, CVEs, and "would we still choose this today". **Use `WebSearch` and `WebFetch`** for CVE lookups, license checks, npm/NuGet package status, and GitHub activity.
5. **Test coverage & usefulness** — per-layer gaps, `[TestCategory("Integration")]` classification correctness, assertion-quality conventions.
6. **Observability & logging** — Serilog usage, log levels, diagnosability of the unattended booth at runtime.
7. **CI/CD & build hygiene** — workflow file health, compiler strictness, reproducibility, missing tooling config.
8. **Performance & resilience** — error handling, resilience patterns, hotspots beyond the existing load test.
9. **Accessibility & i18n** — frontend a11y, EN/ES i18n coverage, guest-gallery UX correctness.

For the **security** and **dependencies** dimensions, always fetch current information from the internet:
- Discover current backend packages from `Directory.Packages.props` and current frontend packages from `src/PhotoBooth.Web/package.json` before searching — do not rely on a hardcoded list.
- Search for CVEs on each discovered package.
- Verify licenses are OSI-approved and compatible with this project (MIT/Apache-2/BSD preferred).
- Check GitHub repository activity (last commit, open issues, stars) to assess maintenance health.

### Phase 2 — Triage & Group

For each raw finding from Phase 1:

1. Discard findings that are not actionable (observations, style opinions without impact, things already tracked in open issues).
2. Assign **exactly one** `priority` label and **at least one** `area` label from the taxonomy below.
3. Write a one-line rationale summarizing impact and evidence.
4. Deduplicate: if two findings cover the same root cause, merge them.
5. **Group related findings** into potential PRs. Two or more issues belong in the same group when they: touch the same file or subsystem, would be fixed by the same type of change (e.g. "upgrade all frontend deps"), or are conceptually related enough that one PR fixing them is lower-risk than separate PRs. Name each group with a short action phrase (e.g. "Frontend dependency upgrades", "Improve camera error recovery", "Documentation cleanup"). Groups are displayed in the tracking issue so an agent can be instructed to fix an entire group in one PR.

A finding can belong to only one group. Findings with no natural sibling form a "Standalone" group.

**Priority labels:**
| Label | Color | Meaning |
|---|---|---|
| `priority:high` | `#b60205` | Security risk, data loss, or blocks normal operation |
| `priority:medium` | `#fbca04` | Degrades quality, causes confusion, or accumulates debt |
| `priority:low` | `#0e8a16` | Nice-to-have improvement, low risk if deferred |

**Area labels:**
| Label | Color | Meaning |
|---|---|---|
| `area:frontend` | `#0075ca` | React/TypeScript/Vite source in `src/PhotoBooth.Web/` |
| `area:backend` | `#1d76db` | .NET C# projects under `src/` |
| `area:tests` | `#e4e669` | Test projects under `tests/` |
| `area:documentation` | `#cfd3d7` | Markdown docs (CLAUDE.md, SPEC.md, README.md) |
| `area:security` | `#d73a4a` | Security controls, headers, rate limiting, CVEs |
| `area:dependencies` | `#0366d6` | NuGet or npm package choices |
| `area:ci-build` | `#5319e7` | GitHub Actions, build config, tooling files |

### Phase 3 — Present & Confirm

Present findings organized by group, then stop for approval. Use this format:

```
**Group: Frontend dependency upgrades** (1 PR)
| # | Title | Area(s) | Priority | Rationale |
|---|-------|---------|----------|-----------|
| 1 | Upgrade react-qr-code to v3 | dependencies, frontend | medium | Last release 18 mo ago; v3 drops legacy deps |
| 2 | Upgrade react-zoom-pan-pinch to v5 | dependencies, frontend | low | Maintenance patch; no CVEs |

**Group: Improve camera error recovery** (1 PR)
| # | Title | Area(s) | Priority | Rationale |
|---|-------|---------|----------|-----------|
| 3 | Handle OpenCV camera disconnect gracefully | backend | high | USB unplug leaves booth in broken state |
| 4 | Add ADB reconnection retry logic | backend | medium | Connection drop requires full restart |

**Standalone**
| # | Title | Area(s) | Priority | Rationale |
|---|-------|---------|----------|-----------|
| 5 | Replace Vite boilerplate README in src/PhotoBooth.Web | documentation | low | Default template README confuses contributors |
```

Then **stop and ask the user for approval** using `AskUserQuestion` with options: "File all issues", "Let me edit the list first", "Cancel". Do not proceed to Phase 4 until the user approves.

If the user asks to edit: accept their changes (remove rows, change priorities, regroup, rewrite titles) and confirm once more before proceeding.

### Phase 4 — Ensure Labels Exist

Before creating issues, idempotently ensure all needed labels exist:

```bash
gh label list --limit 100
```

For each `priority:*` and `area:*` label that is missing, create it:

```bash
gh label create "priority:high" --color "b60205" --description "Security risk, data loss, or blocks normal operation"
gh label create "area:backend" --color "1d76db" --description ".NET C# projects under src/"
```

Use a separate `gh label create` call per label. If a label already exists, the command will error — catch this with `|| true` or by checking the list first.

### Phase 5 — File Issues

For each approved finding, create one GitHub issue:

**Issue body format:**
```
## Finding

<one-paragraph description of the problem, with file:line references where possible>

## Why it matters

<impact on the codebase, users, or development velocity>

## Suggested fix

<concrete suggestion — library upgrade, code change, doc update, etc.>

## Effort estimate

<S / M / L>

---
_Part of health check tracking issue #<tracking-issue-number>_
```

**Issue creation command** (follow CLAUDE.md conventions — plain strings, no heredocs, no backticks):
```bash
gh issue create --title "<title>" --body "<body>" --label "priority:high" --label "area:security"
```

After all child issues are created, create the **tracking issue**:
- Title: `Codebase health check — <YYYY-MM-DD>`
- Body: findings organized by group (same grouping as Phase 3), with each group presented as a section. Format:

```
## Group: Frontend dependency upgrades

Fix together in one PR — all touch frontend deps in `src/PhotoBooth.Web/package.json`.

- [ ] #42 Upgrade react-qr-code to v3
- [ ] #43 Upgrade react-zoom-pan-pinch to v5

## Group: Improve camera error recovery

Fix together in one PR — all touch camera provider implementations under `src/PhotoBooth.Infrastructure/Camera/`.

- [ ] #44 Handle OpenCV camera disconnect gracefully
- [ ] #45 Add ADB reconnection retry logic

## Standalone

- [ ] #46 Replace Vite boilerplate README in src/PhotoBooth.Web

---
## Summary
| Priority | Count |
|----------|-------|
| High     | 1     |
| Medium   | 3     |
| Low      | 1     |

Dimensions with no findings: Architecture & code organization, Observability & logging
```

- Labels: none required (it's a meta issue).

Then comment on each child issue referencing the tracking issue number (optional if the body already includes it).

### Phase 6 — Report

Output a brief summary:
- Tracking issue URL
- Count of issues filed (by priority)
- Any dimensions where no findings were raised (explicitly, so the user knows the clean areas)
- Suggested cadence for the next run (default: 3 months)

---

## Conventions to honor

- Follow CLAUDE.md strictly: GitHub Flow, plain-string `gh` args, no heredocs, no `$()` substitution, no backticks in strings.
- This skill only **files issues** — it does not create branches, edit source files, or open PRs.
- For dependency CVE checks, search by package name + current year (e.g. "OpenCvSharp4 CVE 2026", "Serilog vulnerability 2026") and fetch the NuGet/npm page to verify the latest version.
- When in doubt about priority, prefer `medium` over `high` — reserve `high` for concrete security or correctness risks with evidence.
