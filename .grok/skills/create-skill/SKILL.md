---
name: create-skill
description: Use when the user asks to create, edit, or verify a Grok skill for this repo (스킬, 스킬크리에이터, /create-skill). Writes `.grok/skills/<name>/SKILL.md` only. Slash /create-skill.
---

# Create a project skill

This repo's skill creator. Output is a Grok skill other sessions can load.

## Where

`A:/c/.grok/skills/<name>/SKILL.md`

Never write `SKILL.md` under `.agents/skills` (Python leftovers). Never install hooks or MCP to "make the skill work".

Name: `lowercase-hyphens`, 2–64 chars, start/end letter or digit.

## Frontmatter

```markdown
---
name: the-name
description: Use when [triggers, files, symptoms]. Slash /the-name.
---
```

Description = when to load, in third person. Include Korean domain words the user actually says (전장 맵, 1위, 수집, 표).

## Body

- Overview in 1–2 sentences
- Exact commands, paths, tokens for this repo
- Route to existing `cpp-*` / `cpp-design-*` instead of copying them
- Common mistakes from `.agents/MISTAKES.md` when relevant
- Keep SKILL.md short. Extra material in `references/` one level down

## After write

1. Read the file back
2. Confirm `name` matches the folder
3. Tell the user `/name`

## C++ work

New C++ procedure → also add a row to `.grok/skills/README.md` and a route line in `cpp-2026`.

## Do not

- Generic "be a 15-agent team" skills
- Skills that tell the agent to use MCP
- Duplicate the 1위 toggle as a new visual skill
