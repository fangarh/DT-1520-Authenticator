export interface ChecklistItem {
  label: string;
  detail: string;
}

export interface DocumentationSection {
  id: string;
  title: string;
  lead: string;
  items: ChecklistItem[];
}

export interface Workflow {
  title: string;
  permission: string;
  path: string;
  steps: string[];
  audit: string;
}

export const repoRoots = [
  "backend/",
  "admin/",
  "mobile/",
  "infra/",
  "installer-ui/",
  "docs/",
  "lib/",
  "rdb_stand/",
  "OTP/",
] as const;

export const quickStartCommands = [
  "cd .\\backend",
  "powershell -ExecutionPolicy Bypass -File .\\scripts\\initialize-postgres.ps1",
  "$env:OTPAUTH_ADMIN_PASSWORD = '<set-a-strong-password>'",
  "dotnet run --project .\\OtpAuth.Migrations\\OtpAuth.Migrations.csproj -- upsert-admin-user operator enrollments.read tenants.write tenants.read enrollments.write webhooks.read webhooks.write devices.read devices.write integration-clients.read integration-clients.write",
  "cd ..\\admin",
  "npm install",
  "npm run build",
] as const;

export const sections: DocumentationSection[] = [
  {
    id: "overview",
    title: "Обзор продукта",
    lead: "DT-1520 Authenticator - отдельный 2FA/MFA server для TOTP, backup codes, push approval, device lifecycle и admin/operator workflows.",
    items: [
      {
        label: "Архитектура",
        detail: "Modular monolith: Api, Worker, Application, Domain, Infrastructure и Migrations в backend/OtpAuth.slnx.",
      },
      {
        label: "Admin UI",
        detail: "React/Vite runtime shell в admin/ работает через cookie admin session, CSRF и permission-based endpoints.",
      },
      {
        label: "Android",
        detail: "mobile/ содержит TOTP-first приложение, secure storage, push inbox, biometric-gated approve path и QR device onboarding.",
      },
    ],
  },
  {
    id: "setup",
    title: "Локальный запуск",
    lead: "Локальная разработка опирается на PostgreSQL, backend runtime, admin shell и отдельные verification scripts.",
    items: [
      {
        label: "Backend verification",
        detail: "Из backend/ запускай scripts/verify-backend.ps1; он учитывает sequential build requirement для artifacts/obj.",
      },
      {
        label: "Admin app",
        detail: "Из admin/ доступны npm test, npm run build и npm run test:e2e; API base задается через VITE_ADMIN_API_BASE_URL.",
      },
      {
        label: "Mobile checks",
        detail: "Из mobile/ запускаются Gradle unit tests; live verification требует emulator и Android MCP по правилам проекта.",
      },
    ],
  },
  {
    id: "deployment",
    title: "Docker и installer",
    lead: "infra/ содержит compose packaging, installer engine и ghostring-specific pilot profile.",
    items: [
      {
        label: "Default package",
        detail: "infra/docker-compose.yml поднимает postgres, redis, api, worker и admin; bootstrap вынесен в отдельный profile.",
      },
      {
        label: "Installer",
        detail: "infra/scripts/install.ps1 поддерживает Install, Update, Recover, PreflightOnly, DryRun и sanitized JSON report.",
      },
      {
        label: "Ghostring",
        detail: "infra/docker-compose.ghostring.yml использует existing PostgreSQL, отдельный Redis и host-level nginx template.",
      },
    ],
  },
  {
    id: "admin",
    title: "Admin UI",
    lead: "Admin panel покрывает tenant directory, selected-tenant management, enrollment, integration client onboarding, QR device onboarding, webhook subscriptions, delivery visibility и user device support.",
    items: [
      {
        label: "Session",
        detail: "Login, logout, session и csrf-token опубликованы под /api/v1/admin/auth/*; session lifetime сейчас 8 часов.",
      },
      {
        label: "Permissions",
        detail: "Кодовый source of truth: tenants.read/write, enrollments.read/write, webhooks.read/write, devices.read/write, integration-clients.read/write в AdminPermissions.cs.",
      },
      {
        label: "Secrets",
        detail: "Provisioning artifacts и integration client secret показываются только в момент start/create/rotate, держатся только в текущем UI state и не должны попадать в localStorage, audit или read path.",
      },
      {
        label: "Client onboarding",
        detail: "Operator flow: tenant-centric setup starts with tenants.read/write quick create, then generated API client secret is handed to the external backend once and lifecycle stays under integration-clients.read/write.",
      },
      {
        label: "Tenant management",
        detail: "Selected tenant page is the primary operator path: API client lifecycle, user devices, QR issue, runtime callback policy and reports work without copying tenant/application IDs between blocks.",
      },
      {
        label: "Reports",
        detail: "Tenant reports summarize recent delivery status, callback/webhook health, selected-user devices, QR artifact statuses and last approval/device activity markers without exposing payloads or tokens.",
      },
      {
        label: "QR device onboarding",
        detail: "Operator flow выдает one-time opaque activation payload as QR, держит payload только в текущем UI state, а list/detail/revoke не раскрывают payload/hash.",
      },
    ],
  },
  {
    id: "android",
    title: "Android",
    lead: "Android приложение остается фактором пользователя: хранит локальные TOTP secrets, показывает pending push и требует biometric/PIN gate для approve.",
    items: [
      {
        label: "Provisioning",
        detail: "feature:provisioning импортирует otpauth URI и передает secret только через secure-save boundary.",
      },
      {
        label: "Runtime",
        detail: "feature:totp-codes генерирует offline TOTP; feature:push-approvals показывает pending challenges и history.",
      },
      {
        label: "Device onboarding",
        detail: "feature:device-onboarding сканирует v1 QR envelope с runtimeBaseUrl и one-time activationPayload; legacy dac payload остается временным fallback.",
      },
      {
        label: "QR activation",
        detail: "Основной app UI запускает CameraX + ML Kit QR scanner; успешная активация вызывает /api/v1/devices/activate-onboarding через QR runtime URL, затем хранит runtime URL в encrypted device session для refresh/pending/approve/deny после restart.",
      },
      {
        label: "Pilot helper",
        detail: "mobile/app/src/debug/PilotDeviceActivationActivity.kt остается debug-only helper и не является production onboarding.",
      },
    ],
  },
  {
    id: "security",
    title: "Security model",
    lead: "Документация фиксирует secret boundaries и запрещает выводить raw tokens, signing material, pushToken, callback payloads и private URLs with credentials.",
    items: [
      {
        label: "Integration clients",
        detail: "Admin create/rotate генерируют client_secret server-side и показывают его один раз; scopes/status меняются только через CSRF-protected admin commands.",
      },
      {
        label: "Token invalidation",
        detail: "Secret rotation, scope changes и deactivate/reactivate обновляют persisted auth state; уже выданные access tokens с устаревшим iat отклоняются runtime validation.",
      },
      {
        label: "Callbacks",
        detail: "Challenge callbacks и webhooks подписываются X-OTPAuth-Signature; callback URL policy явно настроена как PublicInternet, PrivateNetwork или LocalDevelopment, а delivery details в admin UI sanitized.",
      },
      {
        label: "Audit",
        detail: "Security audit trail append-only и хранит sanitized lifecycle events без secret material.",
      },
      {
        label: "Device onboarding",
        detail: "Activation payload возвращается только один раз при create, не хранится plaintext, не появляется в list/read path, не содержит trusted tenant/user claims и блокируется через consume, TTL или revoke; QR runtimeBaseUrl является public routing metadata и хранится на Android только в encrypted device session.",
      },
    ],
  },
  {
    id: "sdk",
    title: ".NET SDK",
    lead: "lib/ содержит prerelease NuGet SDK для backend-интеграций, ASP.NET Core helpers и desktop-safe approval polling без desktop-held integration secrets.",
    items: [
      {
        label: "Packages",
        detail: "Dt1520.Authenticator.Client, Dt1520.Authenticator.AspNetCore и Dt1520.Authenticator.Desktop target net8.0, имеют package README и XML docs, packable через lib/Dt1520.Authenticator.slnx.",
      },
      {
        label: "Getting started",
        detail: "Integrator создает client в Admin UI, хранит generated secret только на backend, регистрирует AddDt1520Authenticator, создает challenge и коммитит business change только после approved.",
      },
      {
        label: "Callback validation",
        detail: "ASP.NET Core helper валидирует X-OTPAuth-Signature over original HttpRequest.Body bytes; JSON parsing и business commit идут только после successful validation.",
      },
      {
        label: "Sample backend flow",
        detail: "lib/samples/aspnetcore-protected-operation/README.md описывает start challenge, callback receive, status polling для browser/desktop и online TOTP fallback.",
      },
      {
        label: "Desktop boundary",
        detail: "Dt1520.Authenticator.Desktop опрашивает только integrator backend status endpoint и не содержит client_secret, bearer token, callback signing secret или direct DT-1520 base URL.",
      },
      {
        label: "Versioning",
        detail: "Текущий prerelease 0.1.0-alpha.1 следует SemVer; совместимость первого пакета привязана к DT-1520 v1 HTTP contract.",
      },
      {
        label: "Prerelease handoff",
        detail: "lib/PRERELEASE-HANDOFF.md фиксирует verification gate, package inspection checklist, security review и SDK APIs для rdb_stand/.",
      },
    ],
  },
  {
    id: "roadmap",
    title: "SDK и reference stand",
    lead: "lib/ уже содержит официальный .NET SDK workspace с typed Client APIs; rdb_stand/ теперь содержит воспроизводимый Reference Desktop + Backend scaffold, WPF demo shell и live wiring runbook.",
    items: [
      {
        label: "NuGet SDK",
        detail: "Dt1520.Authenticator.Client уже покрывает OAuth token flow, challenges, online TOTP verify, device routing lookup, push target selection и framework-agnostic callback signature verification.",
      },
      {
        label: "ASP.NET Core helpers",
        detail: "Dt1520.Authenticator.AspNetCore регистрирует SDK через DI/IHttpClientFactory, валидирует backend-hosted options и проверяет callback signatures over original HttpRequest.Body bytes без raw payload logging.",
      },
      {
        label: "Desktop helpers",
        detail: "Dt1520.Authenticator.Desktop моделирует approval session и polling только против integrator backend: без client_secret, bearer token, direct DT-1520 base URL или зависимости на Client package.",
      },
      {
        label: "Reference stand",
        detail: "rdb_stand/ содержит ASP.NET Core reference backend, console desktop shell, WPF MVVM demo shell с encrypted local MVP settings, sanitized --preflight/live-readiness checks, live env-var runbook и tests для start challenge, signed callback, callback URL policy, status polling и online TOTP fallback.",
      },
      {
        label: "WPF demo storage",
        detail: "DesktopWpfTest сохраняет demo/live wiring form values в encrypted JSON под LocalAppData, но это не production secret store; TOTP code остается transient UI input и не сохраняется.",
      },
      {
        label: "Limitations",
        detail: "Desktop helper не применяет business changes и не заменяет backend callback validation; debug-only activation helper остается только pilot tooling.",
      },
    ],
  },
];

export const workflows: Workflow[] = [
  {
    title: "TOTP enrollment",
    permission: "enrollments.read + enrollments.write",
    path: "admin/src/features/enrollment-workspace",
    steps: ["Load current", "Start", "Confirm", "Replace", "Revoke"],
    audit: "admin_totp_enrollment.* и totp_enrollment.* события пишутся sanitized.",
  },
  {
    title: "Webhook subscriptions",
    permission: "webhooks.read + webhooks.write",
    path: "admin/src/features/webhook-subscriptions",
    steps: ["Load by tenant", "Create/update HTTPS subscription", "Deactivate через isActive=false"],
    audit: "admin_webhook_subscription.* без raw secrets и без payload echo.",
  },
  {
    title: "Delivery statuses",
    permission: "webhooks.read",
    path: "admin/src/features/delivery-statuses",
    steps: ["Filter", "Inspect inventory", "Read sanitized destination/timing/error metadata"],
    audit: "Read-only workspace, replay/retry actions отсутствуют.",
  },
  {
    title: "Tenant directory",
    permission: "tenants.read + tenants.write",
    path: "admin/src/features/tenant-directory + /api/v1/admin/tenants",
    steps: ["List tenants", "Inspect tenant directory", "Manual create", "Quick create tenant + application + API client"],
    audit: "admin_tenant.* пишет sanitized metadata без client_secret/client_secret_hash.",
  },
  {
    title: "Tenant management",
    permission: "tenants.read + integration-clients.* + devices.* + webhooks.read",
    path: "admin/src/features/tenant-management",
    steps: ["Open selected tenant", "Manage API clients", "Load selected-user devices", "Issue QR", "Read runtime policy", "Refresh report snapshot"],
    audit: "Uses existing sanitized admin read/command paths; no client_secret/client_secret_hash/pushToken/raw callback payload/QR payload persistence.",
  },
  {
    title: "Integration clients",
    permission: "integration-clients.read + integration-clients.write",
    path: "admin/src/features/integration-clients + /api/v1/admin/.../integration-clients",
    steps: ["List by tenant", "Inspect sanitized metadata", "Create with whitelisted scopes", "Copy/discard one-time generated secret", "Rotate one-time secret", "Update scopes", "Deactivate/reactivate"],
    audit: "admin_integration_client.* пишет sanitized metadata без client_secret/client_secret_hash.",
  },
  {
    title: "QR device onboarding",
    permission: "devices.read + devices.write",
    path: "admin/src/features/device-onboarding + /api/v1/admin/.../device-onboarding-artifacts",
    steps: ["List by tenant/user/application", "Create one-time QR artifact", "Show QR only in current UI state", "Discard payload", "Revoke pending artifact"],
    audit: "admin_device_onboarding.* пишет sanitized metadata без activation payload/code hash.",
  },
  {
    title: "User devices",
    permission: "devices.read + devices.write",
    path: "admin/src/features/user-devices",
    steps: ["Find by tenant + externalUserId", "Inspect active/revoked/blocked", "Revoke active device"],
    audit: "admin_device.revoked не раскрывает installationId, deviceName, pushToken или publicKey.",
  },
];

export const troubleshooting = [
  "CSRF/login за reverse proxy: проверь ReverseProxy__Enabled и trusted OTPAUTH_RUNTIME_NETWORK_CIDR.",
  "Worker unhealthy: смотри sanitized heartbeat /tmp/otpauth-worker/heartbeat.json и docker compose ps.",
  "Webhook/callback delivery failed: проверяй HTTPS endpoint, signature secret и Admin Delivery Statuses.",
  "Callback URL rejected: проверь active policy в /api/v1/admin/runtime-configuration; PublicInternet strict, PrivateNetwork для closed contours, LocalDevelopment только для local/demo.",
  "Integration client не получает token после rotation/deactivate: проверь статус client-а, назначенные scopes и что внешняя система использует последний one-time secret.",
  "Operator не видит tenant directory: проверь наличие tenants.read/write у admin user через list-admin-users.",
  "Operator не видит Integration clients: проверь наличие integration-clients.read/write у admin user через list-admin-users.",
  "Operator не видит QR onboarding workspace: проверь devices.read/write и что браузерный flow использует /api/v1/admin/device-onboarding-artifacts.",
  "SDK callback validation падает: проверь, что backend передает helper-у original HttpRequest.Body bytes до reserialization и использует актуальный callback signing secret.",
  "SDK desktop polling timeout: desktop должен показать non-approval/ retry state и продолжать опрашивать только integrator backend, не DT-1520 напрямую.",
  "Android push не near-real-time при Provider=logging: это polling/debug limitation до production push provider.",
] as const;
