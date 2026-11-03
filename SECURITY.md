# Security policy

## Supported versions

Security fixes are made to the latest release. Please update before reporting.

## Reporting a vulnerability

Please **do not open a public issue** for a security problem. Use GitHub's private reporting instead:
**Security tab → Report a vulnerability** on this repository. If that option is unavailable, contact the maintainer privately
through the contact details on their GitHub profile.

Include the FlowCommand version, Windows version, what you did, and what you expected. You can expect an acknowledgement, and
a fix or an explanation as soon as the maintainer is able. This is a volunteer project, so there is no guaranteed timeline.

## What counts

Examples of in-scope problems:

- A way to make FlowCommand run something outside its action catalogue (for example through crafted AI output, a crafted
  dictated phrase, or a crafted `config.json` field that should have been rejected).
- Bypassing validation, the enabled-action allowlist, risk levels or required confirmations.
- Exposure of API keys (in logs, the config file, network requests to anyone but the chosen provider, or on disk unencrypted).
- The key listener recording, storing or transmitting anything other than "was this the trigger key".
- Closing or terminating protected system processes.

Out of scope: problems that need an attacker who already controls your Windows account or your `config.json`, the behaviour
of the AI providers themselves, and findings in third-party software.

## How FlowCommand is designed to limit risk

- The AI only chooses from a fixed action list; its answer is validated before anything runs.
- There is no shell, script, arbitrary-file or registry action.
- URLs and file paths come from configuration, never from the AI.
- API keys are encrypted with Windows DPAPI for the current user and are not written to logs or `config.json`.
- The only network traffic is the request to the AI provider you configured.
