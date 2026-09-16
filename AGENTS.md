# AGENTS.md

This file is the working guide for AI agents modifying this repository (TaskFlow backend).

## Token & Efficiency Rules

1. **Strict Context Boundary:** Read/search inside the target layer first (`src/TaskFlow.<Layer>/`). Do not run broad project-wide globs unless the change is genuinely cross-layer.
2. **Model Usage:** Use light models for simple edits (renames, field additions, DTO tweaks). Do not prompt for deep reasoning on routine changes.

## Project Overview

- .NET 9 ASP.NET Core Web API following Clean Architecture.
- Stack: EF Core + PostgreSQL (Npgsql), JWT Bearer auth, SignalR, Hangfire, Supabase S3 (AWSSDK.S3), BCrypt, Wangkanai.Detection.
- Domain: task management — tasks, projects, meetings, analytics, authentication, user devices, notifications.
- UI text and DTO field names are English.

## Repository Structure

```text
src/
  TaskFlow.API/             Presentation: Controllers, Hubs, MiddleWare, Services, Program.cs, appsettings*.json
  TaskFlow.Application/     Business logic: Services, Interfaces, DTOs, Validators
  TaskFlow.Infrastructure/  EF Core (Persistence), Repositories, Auth, Jobs, Storage, Configurations
  TaskFlow.Domain/          Entities, Enums, Common (BaseEntity / AuditableEntity)
```

## Layer Dependencies (must not be violated)

```text
API            -> Application + Infrastructure
Infrastructure -> Application -> Domain
Domain         -> (nothing)
```

- **Infrastructure must NOT reference API** (Controllers, Hubs, Middleware). SignalR Hub and its `IHubContext` sender live in API.
- Application knows interfaces, not implementations.

## Dependency Injection (critical)

Each layer registers its own services in its own `DependencyInjection.cs`:

| Layer | File | Method | Registers |
|---|---|---|---|
| API | `src/TaskFlow.API/DependencyInjection.cs` | `AddAPI()` | Hub-based senders (e.g. `INotificationSender`) |
| Application | `src/TaskFlow.Application/DependencyInjection.cs` | `AddApplication()` | All business services (Auth, Task, Project, User, Device, Notification, Meeting, Analytics) |
| Infrastructure | `src/TaskFlow.Infrastructure/DependencyInjection.cs` | `AddInfrastructure(configuration)` | DbContext, JWT, Hangfire, S3, repositories, storage impl |

Rules:
- `Program.cs` only calls `AddApplication()`, `AddAPI()`, `AddInfrastructure(configuration)`. **Never register DI inline in Program.cs.**
- Business-logic services belong in `Application/Services` and are registered in `AddApplication`, **never** in `AddInfrastructure`.
- Repositories/interfaces: interface in `Application/Interfaces`, implementation in `Infrastructure/Repositories`.

## Conventions

- Controllers: `[ApiController] [Authorize] [Route("api/<resource>")]`; get current user via `User.FindFirstValue(ClaimTypes.NameIdentifier)`.
- DTOs live in `Application/DTOs`; entities in `Domain/Entities`; enums in `Domain/Enums`.
- Namespaces: `TaskFlow.API.*`, `TaskFlow.Application.*`, `TaskFlow.Infrastructure.*`, `TaskFlow.Domain.*`.
- Entity `TaskItem` maps to the `Tasks` DbSet (`DbSet<TaskItem> Tasks`), status enum `TaskFlow.Domain.Enums.TaskStatus` (Todo, InProgress, InPreview, Done).

## Auth

- JWT config keys under `Jwt`: `Secret`, `Issuer`, `Audience`, `ExpiryMinutes` (10). userId = claim `sub` = `ClaimTypes.NameIdentifier`.
- Refresh token expiry 7 days; devices (fingerprint + pushToken) capped at 3 per user.

## Notification Feature

- SignalR hub mapped at `/hubs/notification` (`NotificationHub` in API); realtime event name `"NotificationReceived"` carrying a `NotificationDto`.
- `NotificationService.CreateAndSendAsync(...)` (Application) both saves a row to the `Notifications` table and pushes realtime via `INotificationSender` (`NotificationSender` in API using `IHubContext`).
- Deduplication via `DeduplicationKey` (e.g. `task-overdue-{taskId}`). Types: `1` TaskDeadlineApproaching, `2` TaskOverdue, `3` MeetingReminder.
- Hangfire recurring job (every 10 min, cron `*/10 * * * *`) in `Infrastructure/Jobs` auto-scans tasks — overdue, and deadline approaching within 2h — then calls `CreateAndSendAsync`. Jobs are registered by `NotificationJobScheduler` (IHostedService).

## Dev URLs / Ports

- `http` profile: `http://localhost:5292`; `https` profile: `https://localhost:7081`.
- CORS allows `http://localhost:5173` (frontend).

## Verification

```bash
dotnet build            # must be 0 warnings, 0 errors
dotnet ef migrations add <Name>   # from Infrastructure when schema changes
dotnet ef database update
```

## Rules

- Never log tokens, passwords, or secrets. `appsettings.json` is gitignored (holds secrets locally; keep it that way).
- Keep changes focused; do not refactor unrelated code.
- Match existing code style and naming. Avoid adding comments unless they explain non-obvious behavior.
