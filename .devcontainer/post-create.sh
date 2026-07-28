#!/usr/bin/env bash
set -euo pipefail

# Install mise if missing, then restore packages.
if ! command -v mise >/dev/null 2>&1; then
  curl -fsSL https://mise.run | sh
fi
MISE="${HOME}/.local/bin/mise"
grep -qF 'mise activate bash' "${HOME}/.bashrc" 2>/dev/null || echo "eval \"\$(${MISE} activate bash)\"" >> "${HOME}/.bashrc"
"${MISE}" trust
"${MISE}" run restore
