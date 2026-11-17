#!/usr/bin/env bash
# PreToolUse Edit|Write — refuse content that looks like a real secret. Only strong indicators are
# matched: a false positive that blocks work is worse than a miss here.
#
# Local-only development values (the docker-compose Postgres password, appsettings.Development.json)
# are not secrets and are exempt.

input=$(cat)
file=$(printf '%s' "$input" | jq -r '.tool_input.file_path // ""')

case "$file" in
  *docker-compose*.yml|*appsettings.Development.json|*.env|*.env.*) exit 0 ;;
esac

content=$(printf '%s' "$input" | jq -r '
  .tool_input.new_string //
  .tool_input.content //
  ([.tool_input.edits[]?.new_string] | join("\n")) //
  ""
')

block() {
  echo "Blocked by secrets-scan: $1 in $(basename "$file"). Use user-secrets or an environment variable (see README)." >&2
  exit 2
}

printf '%s' "$content" | grep -qE '(AKIA|ASIA)[0-9A-Z]{16}' && block "AWS access key"
printf '%s' "$content" | grep -qE 'gh[pousr]_[A-Za-z0-9]{30,}' && block "GitHub token"
printf '%s' "$content" | grep -qE 'sk-ant-[A-Za-z0-9_-]{20,}' && block "Anthropic API key"
printf '%s' "$content" | grep -qE 'sk_(live|test)_[A-Za-z0-9]{24,}' && block "Stripe secret key"
printf '%s' "$content" | grep -qE 'xox[baprs]-[0-9A-Za-z-]{10,}' && block "Slack token"
printf '%s' "$content" | grep -qE 'Bearer[[:space:]]+ey[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}' && block "bearer JWT"
printf '%s' "$content" | grep -qE 'postgres(ql)?://[^:/@[:space:]]+:[^@[:space:]$]{6,}@' && block "PostgreSQL URL with an inline password"

# A connection string with a literal password; placeholders like ${...} or {{...}} are fine.
if printf '%s' "$content" | grep -qiE '(Host|Server)=[^;"]*;[^"]*Password=[^;"$%{[:space:]]{6,}'; then
  block "connection string with a literal password"
fi

exit 0
