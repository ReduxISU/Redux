#!/usr/bin/env bash
# Post-create for the Redux devcontainer.
#
# The .NET 10 runtime comes from the base image. This script only layers in mise
# (the unified task runner) and restores NuGet packages so the container is ready
# to `mise run build|test|run`. See CLAUDE.md "Toolchain layering (DECIDED)".
set -euo pipefail

# 1. Install mise if the base image doesn't already provide it.
if ! command -v mise >/dev/null 2>&1; then
  curl -fsSL https://mise.run | sh
fi
MISE="${HOME}/.local/bin/mise"

# 2. Activate mise for future interactive shells.
ACTIVATE="eval \"\$(${MISE} activate bash)\""
grep -qF "${ACTIVATE}" "${HOME}/.bashrc" 2>/dev/null || echo "${ACTIVATE}" >> "${HOME}/.bashrc"

# 3. Trust this repo's mise.toml, then warm the NuGet cache via the shared task.
"${MISE}" trust
"${MISE}" run restore
