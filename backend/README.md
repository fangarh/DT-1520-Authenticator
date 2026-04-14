# Backend

## Solution

- `OtpAuth.slnx`

## Projects

- `OtpAuth.Api` - HTTP API and integration endpoints
- `OtpAuth.Worker` - background jobs, outbox processing, delivery tasks
- `OtpAuth.Application` - application services and use cases
- `OtpAuth.Domain` - domain model and core business rules
- `OtpAuth.Infrastructure` - persistence, integrations, and adapters

## Commands

```powershell
cd .\backend
dotnet restore .\OtpAuth.slnx
dotnet build .\OtpAuth.slnx
```

The solution is configured to avoid parallel build workers because this workspace hits intermittent file locking in `artifacts\obj` during multi-node MSBuild.

## Current state

This is a scaffold only. No production logic is implemented yet.
