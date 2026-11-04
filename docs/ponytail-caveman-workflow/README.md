# Ponytail + Caveman Workflow

Two skills, two jobs. They compose: ponytail decides **what code to write**, caveman decides **how to write the reply**. Use both at once.

- **Ponytail** = lazy senior dev. Smallest complete change. Decision rules for code.
- **Caveman** = terse voice. Fluff gone, every technical fact kept. Compresses the reply, not the logic.

Source skills (full rules live there, not duplicated here):

- `~/.kiro/skills/ponytail/SKILL.md`
- `~/.kiro/skills/caveman/SKILL.md`
- `~/.kiro/skills/ultracave/SKILL.md` (caveman at max compression)
- `~/.kiro/skills/megacave/SKILL.md` (caveman in Classical Chinese)

## When each runs

| Situation | Ponytail | Caveman |
|-----------|----------|---------|
| Writing / fixing / refactoring code | decides the change | — |
| Choosing a dependency | decides (reuse first) | — |
| Any chat reply | — | decides the wording |
| Review / audit | ranks findings, what to cut | one line per finding |

Rule of thumb: ponytail shapes the diff, caveman shapes the sentence. Neither touches the other's floor.

## Activate

Kiro = instruction-only host. No slash commands. Say it in chat:

- `ponytail` — on, default level (full). `ponytail lite` / `ponytail ultra` / `ponytail off`.
- `caveman` — on. `ultracave` (default here, max compression) / `megacave` (文言文).
- Stop: `stop ponytail` / `stop caveman` or `normal mode`.

Both stay on the whole session until stopped.

## Levels

**Ponytail**

| Level | Behavior |
|-------|----------|
| lite | Build what was asked. Name the smaller option, let user pick. |
| full | Smallest complete change. Default. |
| ultra | Also push back on any part the need does not justify, before building. |

**Caveman**

| Mode | Behavior |
|------|----------|
| caveman | Terse voice, articles optional. |
| ultracave | Fragments, payload only. Max compression. |
| megacave | Classical Chinese register. |

## Shared floor — never cut, either skill

- Code, commands, paths, API names, error strings: verbatim.
- Negations: not / never / no / only / except. A dropped negation costs more than every token saved.
- Numbers and units: exact.
- Validation at trust boundaries, error handling that prevents data loss, security, accessibility, anything the user asked for.
- Existing comments in files you edit: keep unless asked to change.

## Break the compression — write plain prose

Both skills agree: drop terseness and use full sentences for these.

1. Security warning.
2. Irreversible action — confirm first.
3. Step order a fragment could scramble.
4. User confused or repeats the question.
5. A question to the user, with its options.
6. Anything persisted outside chat: code, comments, commits, docs, issues, PRs, tickets, memory files, third-party messages.

## Terms

| Term | Meaning |
|------|---------|
| floor | What a skill never cuts, no matter the compression. |
| ceremony | Greetings, hedging, recaps, closers. Caveman deletes these. |
| payload | Code, commands, paths, numbers, errors. Verbatim always. |
| smallest complete change | Finishes every part the task needs (callers, tests, fixtures), nothing extra. |
| shortcut comment | `shortcut: <the limit>, <when to upgrade>` — marks a deliberate shortcut. |
| scope | Every place a change must reach. Extra features are not scope. |
