#!/usr/bin/env bash
# PreToolUse Edit|Write — `assignment/` holds the task inputs (brief, original mockup, mockup
# analysis) and is read-only. Implementation notes and decisions go to `docs/`.

input=$(cat)
file=$(printf '%s' "$input" | jq -r '.tool_input.file_path // ""')

case "$file" in
  "$CLAUDE_PROJECT_DIR"/assignment/*) ;;
  *) exit 0 ;;
esac

echo "Blocked by assignment-guard: assignment/ is read-only (task inputs). Write to docs/ instead, or ask the user to change the input files." >&2
exit 2
