#!/usr/bin/env bash
# PreToolUse Bash — deny a `git commit` or `gh pr create|edit` whose message carries an
# AI-attribution trailer (Co-Authored-By: Claude, "Generated with Claude Code", session links).
#
# The agent harness asks for such a trailer by default; a written rule alone does not hold
# against it, so this guard refuses the command and the message has to be fixed first.
# Scans the command text (-m, heredocs) and any file passed with -F / --file / --body-file.

input=$(cat)

if command -v jq >/dev/null 2>&1; then
  tool=$(printf '%s' "$input" | jq -r '.tool_name // ""')
  command_text=$(printf '%s' "$input" | jq -r '.tool_input.command // ""')
else
  tool=$(printf '%s' "$input" | sed -n 's/.*"tool_name"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/p' | head -1)
  command_text=$input
fi

[[ "$tool" == "Bash" ]] || exit 0

one_line=" $(printf '%s' "$command_text" | tr '\n' ' ') "

is_commit=$(printf '%s' "$one_line" | grep -Ec '(^|[;&|("]| )git( +-[A-Za-z-]+( +[^ ]+)?)* +commit([ ;&|)]|$)')
is_pr=$(printf '%s' "$one_line" | grep -Ec '(^|[;&|("]| )gh +pr +(create|edit)([ ;&|)]|$)')
[[ "$is_commit" == 0 && "$is_pr" == 0 ]] && exit 0

text=$command_text
for path in $(printf '%s' "$one_line" \
  | grep -Eo '( -F|--file|--body-file)[ =]+("[^"]+"|'"'"'[^'"'"']+'"'"'|[^ ;&|"]+)' \
  | sed -E 's/^ *(-F|--file|--body-file)[ =]+//; s/^["'"'"']//; s/["'"'"']$//'); do
  [[ -f "$path" ]] && text="$text"$'\n'"$(cat "$path")"
done

pattern='co-authored-by:.*(claude|anthropic)|claude-session:|claude\.ai/code|generated (with|by) \[?claude|🤖 generated'
hit=$(printf '%s' "$text" | grep -Eio "$pattern" | head -1)
[[ -z "$hit" ]] && exit 0

what="commit message"
[[ "$is_commit" == 0 ]] && what="pull request description"
reason="The ${what} carries an AI-attribution trailer (\"${hit}\"). This repository forbids Co-Authored-By: Claude, session links and 'Generated with Claude' lines (see CLAUDE.md). Remove it and run the command again."
reason=${reason//\\/\\\\}
reason=${reason//\"/\\\"}

cat <<EOF
{
  "hookSpecificOutput": {
    "hookEventName": "PreToolUse",
    "permissionDecision": "deny",
    "permissionDecisionReason": "${reason}"
  }
}
EOF
exit 0
