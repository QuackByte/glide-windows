# Code signing

Glide's Windows releases are signed through [SignPath Foundation](https://signpath.org), which signs open-source projects for free. Its certificate is issued to SignPath Foundation, so Windows shows "SignPath Foundation" as the publisher.

The release workflow (`.github/workflows/release.yml`) signs twice per release: first Glide's own binaries, then the two installers that contain them. Until the settings below exist, it builds unsigned installers.

## Setting it up

1. Apply at <https://signpath.org/apply> once this repository is public. They check that the project is open source (MIT), maintained, and released from CI, and that the website has a code signing policy: <https://quackbyte.dev/glide/code-signing/>.
2. Once accepted, in SignPath:
   - Create the project with the slug `glide-windows`, linked to this GitHub repository, with a trusted build system for GitHub.com.
   - Add two artifact configurations, pasting the files in this folder: `app` ([app.xml](artifact-configurations/app.xml)) and `installers` ([installers.xml](artifact-configurations/installers.xml)).
   - Use the `release-signing` policy. Every signing request waits for an approver to approve it in SignPath; the workflow waits up to an hour.
   - Create a CI user and copy its API token.
3. In this repository's **Settings › Secrets and variables › Actions**:
   - Secret `SIGNPATH_API_TOKEN`: the CI user's token.
   - Variables `SIGNPATH_ORGANIZATION_ID` (from SignPath), `SIGNPATH_PROJECT_SLUG` = `glide-windows`, `SIGNPATH_POLICY_SLUG` = `release-signing`.

The uninstaller that Inno Setup embeds in each installer stays unsigned: Inno Setup can only sign it while compiling, and SignPath signs afterwards.

SmartScreen still warns about a new certificate until enough people have downloaded files signed with it.
