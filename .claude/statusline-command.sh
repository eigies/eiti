#!/usr/bin/env bash
# Claude Code status line script

input=$(cat)

parse() {
  node -e "
    const d = JSON.parse(process.argv[1]);
    const key = process.argv[2].split('.');
    let v = d;
    for (const k of key) v = v && v[k];
    process.stdout.write(v != null ? String(v) : '');
  " "$input" "$1" 2>/dev/null
}

cwd=$(parse "workspace.current_dir")
[ -z "$cwd" ] && cwd=$(parse "cwd")
vim_mode=$(parse "vim.mode")
agent_name=$(parse "agent.name")
used_pct=$(parse "context_window.used_percentage")
model=$(parse "model.id")
[ -z "$model" ] && model=$(parse "model.name")
[ -z "$model" ] && model=$(parse "model")

# Shorten home directory to ~
home="$HOME"
short_cwd="${cwd/#$home/~}"
# Show only last 2 path components to keep it short
short_cwd=$(echo "$short_cwd" | awk -F'/' '{if(NF>2) print "…/"$(NF-1)"/"$NF; else print $0}')

# Shorten model name: claude-sonnet-4-6 → sonnet-4-6
short_model=$(echo "$model" | sed 's/claude-//')

# Git branch
git_branch=""
if [ -n "$cwd" ] && git -C "$cwd" rev-parse --git-dir >/dev/null 2>&1; then
  git_branch=$(GIT_OPTIONAL_LOCKS=0 git -C "$cwd" symbolic-ref --short HEAD 2>/dev/null)
fi

parts=()

# 📁 Current directory (blue)
if [ -n "$short_cwd" ]; then
  parts+=("$(printf '\033[34m📁 %s\033[0m' "$short_cwd")")
fi

# 🌿 Git branch (yellow)
if [ -n "$git_branch" ]; then
  parts+=("$(printf '\033[33m🌿 %s\033[0m' "$git_branch")")
fi

# Mode: vim mode takes priority, then agent name
if [ -n "$vim_mode" ]; then
  case "$vim_mode" in
    INSERT) parts+=("$(printf '\033[32mINSERT\033[0m')") ;;
    NORMAL) parts+=("$(printf '\033[36mNORMAL\033[0m')") ;;
    *)      parts+=("$(printf '\033[36m%s\033[0m' "$vim_mode")") ;;
  esac
elif [ -n "$agent_name" ]; then
  parts+=("$(printf '\033[35m🤖 %s\033[0m' "$agent_name")")
fi

# 🤖 Model (magenta)
if [ -n "$short_model" ]; then
  parts+=("$(printf '\033[35m🤖 %s\033[0m' "$short_model")")
fi

# 📊 Context usage (green/yellow/red)
if [ -n "$used_pct" ]; then
  used_int=$(printf '%.0f' "$used_pct")
  if [ "$used_int" -ge 80 ]; then
    color='\033[31m'
  elif [ "$used_int" -ge 50 ]; then
    color='\033[33m'
  else
    color='\033[32m'
  fi
  parts+=("$(printf "${color}📊 %s%%\033[0m" "$used_int")")
fi

# Join with separator
sep="$(printf '\033[90m | \033[0m')"
result=""
for part in "${parts[@]}"; do
  if [ -z "$result" ]; then
    result="$part"
  else
    result="${result}${sep}${part}"
  fi
done

printf '%s' "$result"
