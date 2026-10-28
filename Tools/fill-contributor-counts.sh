#!/bin/sh
# rbs runs this right before `docker build` ([build] pre-command in rbs.toml). It does what docker.yml's
# "Fill contributor counts" step did before rbs replaced that workflow (#880): wwwroot/contributorInfo.json is
# committed with zero counts (#613), and this fills them in so the published image shows them.
#
# Only on CSharpAPI, the branch rbs publishes from. Other branches build the image they test, with the committed
# zeros, and don't spend time on GitHub API calls.
#
# ContributorStatsSync writes the file only after a successful run, so if this fails the committed zeros stay. rbs
# then builds without it and marks the build row with a warning (pre-command-optional), as docker.yml did.
set -e

if [ "$GITHUB_REF_NAME" = "CSharpAPI" ]; then
  dotnet run --configuration Release --project Tools/ContributorStatsSync -- --counts-only
fi
