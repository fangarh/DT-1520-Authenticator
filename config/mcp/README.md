# MCP Config

This folder contains repository-local MCP configuration examples.

## Android Studio

Android Studio reads `mcp.json` from its IDE settings location, not from this repository.

Use [android-studio.mcp.json.example](./android-studio.mcp.json.example) as the source template and paste it into:

- `File > Settings > Tools > AI > MCP Servers`

Notes:

- Android Studio currently works with HTTP MCP endpoints
- `stdio` MCP servers are not suitable there without a proxy
- local secrets should not be committed to the repository

## Codex

Use [codex.mcp.json.example](./codex.mcp.json.example) as a local per-user reference only.
