#!/usr/bin/env bash
# PreToolUse Bash — require explicit user confirmation for every `git commit` and `git push`,
# whatever the permission mode (auto mode included). Read-only git and `git add` are untouched.

input=$(cat)
tool=$(printf '%s' "$input" | jq -r '.tool_name // ""')
[[ "$tool" == "Bash" ]] || exit 0

command_text=$(printf '%s' "$input" | jq -r '.tool_input.command // ""')
one_line=" $(printf '%s' "$command_text" | tr '\n' ' ') "

# Only git option flags may sit between `git` and the subcommand,
# so `git log --grep commit` or `git diff -- push.md` do not match.
subcommand=$(printf '%s' "$one_line" \
  | grep -Eo '(^|[;&|]| )git( +-[A-Za-z-]+( +[^ ]+)?)* +(commit|push)([ ;&|]|$)' \
  | grep -Eo '(commit|push)' | head -1)
[[ -z "$subcommand" ]] && exit 0

cat <<EOF
{
  "hookSpecificOutput": {
    "hookEventName": "PreToolUse",
    "permissionDecision": "ask",
    "permissionDecisionReason": "git ${subcommand} requires explicit confirmation in this repository."
  }
}
EOF
exit 0
