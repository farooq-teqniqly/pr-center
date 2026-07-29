#!/usr/bin/env bash
#
# Fails when an OpenSpec change under openspec/changes/ has all of its tasks
# complete but has not been archived. The project archives a change in the same
# PR that ships it (see CLAUDE.md, "Pull requests"), so a finished-but-active
# change is an unmet merge precondition.
#
# A change is exempt while it is still in progress: no tasks.md yet
# (proposal-only), or a tasks.md that still has unchecked "- [ ]" items.
#
set -euo pipefail

changes_dir="openspec/changes"
violations=()

if [ ! -d "$changes_dir" ]; then
  echo "No $changes_dir directory; nothing to check."
  exit 0
fi

for change in "$changes_dir"/*/; do
  # The archive/ folder holds already-archived changes -- skip it.
  case "$change" in
  "$changes_dir"/archive/) continue ;;
  esac

  [ -d "$change" ] || continue

  tasks="${change}tasks.md"
  # No tasks file yet: the change is still being drafted (proposal-only).
  [ -f "$tasks" ] || continue

  # Count checked and unchecked task checkboxes at the start of a list item.
  # Accept any Markdown list marker GitHub renders as a task: -, *, + or an
  # ordered "N." marker, with any leading indent and any spacing before the box.
  marker='^[[:space:]]*([-*+]|[0-9]+\.)[[:space:]]+\['
  unchecked=$(grep -cE "${marker} \]" "$tasks" || true)
  checked=$(grep -cE "${marker}[xX]\]" "$tasks" || true)

  # In progress (has open tasks) or has no tasks at all: not yet completable.
  if [ "$unchecked" -ne 0 ] || [ "$checked" -eq 0 ]; then
    continue
  fi

  violations+=("$(basename "$change")")
done

if [ "${#violations[@]}" -ne 0 ]; then
  echo "The following OpenSpec change(s) have all tasks complete but are not archived:"
  for v in "${violations[@]}"; do
    echo "  - $v"
  done
  echo
  echo "Archive each in this PR before merging:"
  echo "  openspec archive <change-name> -y"
  echo "(moves it under openspec/changes/archive/ and folds its deltas into the main specs)."
  exit 1
fi

echo "OpenSpec archive check passed: no completed-but-unarchived changes."
