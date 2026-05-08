# Skills MCP Usage

Use this skill set to inspect available skills and execute common automation tasks through MCP.

Recommended workflow:
1. Call `ListSkills` to discover IDs.
2. Call `SearchSkills` when you have a focused query.
3. Call `ReadSkill` to fetch complete instructions before execution.
4. Call `ReadSkillFile` when a skill references files using paths relative to its own folder.
5. Call `ListFiles` / `ReadFile` / `WriteFile` to manage workspace files.
6. Call `FindFiles` / `ReadFileLines` / `SearchText` / `ReplaceInFile` for code editing.
7. Call `CreateDirectory` / `DeletePath` / `CopyPath` / `MovePath` for path operations.
8. Call `ExecuteCommand` to run commands in a workspace directory.
