# Publishing a release to nuget.org

The workflow `.github/workflows/build.yml` publishes `Org.Eclipse.CycloneDDS`
automatically when a tag is pushed. The tag must equal `VERSION`, optionally
prefixed with `v`, for example `11.1.0` or `v11.1.0`.

Desktop tests, Android/iOS cross-builds, package assembly, package-consumer tests
on all desktop targets, and the final archive must succeed before publishing.
The publish job downloads the verified `nuget-candidate` artifact, rechecks its
identity/platforms and uploads it without rebuilding. Branch pushes, pull requests
and manual `workflow_dispatch` runs do not publish.

## One-time nuget.org setup

1. Sign in to nuget.org with the account that will publish the package. The account
   must be entitled to publish `Org.Eclipse.CycloneDDS`: if it already exists,
   obtain ownership/organization permissions first. A matching policy does not
   grant rights to another owner's package or reserved prefix.
2. Open your account menu, then **Trusted Publishing**, and create a policy:

   | Setting | Value for this repository |
   | --- | --- |
   | Repository Owner | `trittsv` |
   | Repository | `cyclonedds-csharp` |
   | Workflow File | `build.yml` (filename only) |
   | Environment | `nuget` |
   | Package pattern | `Org.Eclipse.CycloneDDS` |
   | Scopes | Publish new packages and publish new versions |

3. Select the policy owner that owns/will own the package: your account or the
   appropriate NuGet organization. Use the exact package pattern rather than `*`.
4. If NuGet marks the policy temporarily active, complete the first successful
   publication within its seven-day window or reactivate it before publishing.

For a new available package ID, allow publishing a new package in the policy;
the first successful upload creates the package. No placeholder package upload
or long-lived API key is required. For another GitHub owner/repository, adjust
the policy to match that repository.

## One-time GitHub setup

In the repository, go to **Settings → Environments → New environment**, create
`nuget`, and add this **environment variable**:

| Variable | Value |
| --- | --- |
| `NUGET_USER` | The nuget.org account's profile username, not its email address |

The workflow reads `${{ vars.NUGET_USER }}`; the username is not an API key.
For organization-owned policies, this is still the individual NuGet account
username used for the trusted policy, rather than a GitHub username.

Configure the environment's deployment rules to allow the release tags. Leave
required reviewers disabled for fully automatic publishing; enabling them
deliberately makes the job wait for approval. GitHub Actions must be allowed to
execute the official `NuGet/login` action and issue an OIDC token. The publish
job already declares `id-token: write`; other jobs retain read-only permissions.

No GitHub secret, stored `NUGET_API_KEY`, Azure application, client ID, or client
secret is needed. `NuGet/login` exchanges the job's OIDC token for a temporary
API key immediately before pushing. The workflow supplies that output to the
push step's `NUGET_API_KEY` environment variable; you do not configure it yourself.

## Release

Commit all release changes, including `VERSION`, before tagging. Release builds
also require the matching native Cyclone DDS tag, as described in
[the build guide](manual/building.rst). Then, for `VERSION` equal to `11.1.0`:

```sh
git tag v11.1.0
git push origin v11.1.0
```

Push the tag only after merging/committing the publishing workflow. Its contents
are taken from the tagged commit. Wait for the workflow's **Publish verified
release to nuget.org** job. NuGet may take additional time to validate/index the
uploaded package before it appears in searches.

To recover from a transient publish failure, rerun the failed jobs in the original
tag-push workflow while its artifacts remain available. The push uses
`--skip-duplicate`, so an already uploaded version is skipped on rerun. NuGet
versions cannot be overwritten; code/package changes require a new version/tag.
Artifacts used by the publish job have one-day retention, so after expiry rerun
all jobs to recreate and reverify the candidate.

## Authentication troubleshooting

- Missing `NUGET_USER`: add it under the `nuget` environment's **Variables**.
- No matching trust policy: check owner, repository, `build.yml`, environment
  `nuget`, policy ownership, account username and policy activation status.
- Permission denied on push: check package ownership, reserved prefixes and
  policy scopes/pattern. The first upload requires the new-package scope.
- Publish job waits: check the `nuget` environment's reviewers/deployment rules.
- Tag/version mismatch: set `VERSION` and tag to the same version; the optional
  `v` prefix is supported. Changing the workflow on a branch does not alter an
  existing tagged commit.

Official references: [NuGet Trusted Publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing)
and [NuGet/login](https://github.com/NuGet/login).
