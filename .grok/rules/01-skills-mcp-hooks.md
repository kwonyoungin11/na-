# Skills / MCP / hooks

Project Grok skills, MCP servers, and hooks are on.

## Skills

Load from `.grok/skills/`. C++ / design work still uses the `cpp-*` pack.

Forced by hooks at the usage point:

- **superpowers** (TDD) — before production `.cpp` / `.h` / `.cs`
- **unlazy** — before claiming substantial work done

Old Python trees under `.agents/skills/*/scripts` are scripts, not Grok skills. Do not add `SKILL.md` there.

## MCP

Project servers live in `.grok/config.toml` under `[mcp_servers]`. Discover with `search_tool`, then call with `use_tool`. Do not guess parameter names.

## Hooks

Project hooks live in `.grok/hooks/`. Command paths are cwd-relative (`.grok\hooks\...`). Do not put `${GROK_WORKSPACE_ROOT}` in a hook `command`.
