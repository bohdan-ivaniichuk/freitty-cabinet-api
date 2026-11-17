#!/usr/bin/env bash
# PostToolUse Edit|Write — run `dotnet format whitespace` on the edited .cs file in the background,
# so formatting is never hand-tuned and never blocks the session. Silent on success and failure.

input=$(cat)
file=$(printf '%s' "$input" | jq -r '.tool_input.file_path // ""')

[[ "$file" == *.cs && -f "$file" ]] || exit 0

# Walk up from the file to its project.
dir=$(dirname "$file")
project=""
while [[ "$dir" != "/" && "$dir" != "." ]]; do
  project=$(find "$dir" -maxdepth 1 -name '*.csproj' 2>/dev/null | head -1)
  [[ -n "$project" ]] && break
  dir=$(dirname "$dir")
done
[[ -z "$project" ]] && exit 0

( dotnet format whitespace "$project" --include "$file" --no-restore --verbosity quiet >/dev/null 2>&1 ) &
disown
exit 0
