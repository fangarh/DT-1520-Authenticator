# ERD

```mermaid
erDiagram
    TENANT ||--o{ APPLICATION_CLIENT : owns
    TENANT ||--o{ USER : contains
    TENANT ||--o{ CHALLENGE : scopes
    TENANT ||--o{ AUDIT_EVENT : records

    USER ||--o{ FACTOR_ENROLLMENT : has
    USER ||--o{ DEVICE : registers
    USER ||--o{ BACKUP_CODE : owns
    USER ||--o{ CHALLENGE : receives

    APPLICATION_CLIENT ||--o{ CHALLENGE : initiates

    FACTOR_ENROLLMENT ||--o| TOTP_SECRET : configures
    FACTOR_ENROLLMENT ||--o{ AUDIT_EVENT : affects

    CHALLENGE ||--o{ CHALLENGE_ATTEMPT : contains
    CHALLENGE ||--o{ AUDIT_EVENT : emits
    DEVICE ||--o{ AUDIT_EVENT : appears_in

    TENANT {
        uuid id PK
        string name
        string status
        string deployment_mode
        timestamp created_at
    }

    APPLICATION_CLIENT {
        uuid id PK
        uuid tenant_id FK
        string name
        string client_type
        string auth_method
        string client_secret_hash
    }

    USER {
        uuid id PK
        uuid tenant_id FK
        string external_subject_id
        string username
        string email
        string phone
        string status
    }

    FACTOR_ENROLLMENT {
        uuid id PK
        uuid user_id FK
        string factor_type
        string status
        timestamp created_at
        timestamp confirmed_at
        timestamp last_used_at
    }

    TOTP_SECRET {
        uuid enrollment_id PK
        string secret_ciphertext
        string secret_kek_version
        int digits
        int period_seconds
        string algorithm
    }

    DEVICE {
        uuid id PK
        uuid user_id FK
        string platform
        string device_name
        string push_token
        string public_key
        string attestation_status
        timestamp last_seen_at
        timestamp revoked_at
    }

    CHALLENGE {
        uuid id PK
        uuid tenant_id FK
        uuid application_client_id FK
        uuid user_id FK
        string operation_type
        string factor_type
        string status
        timestamp expires_at
        timestamp approved_at
        timestamp denied_at
        string correlation_id
    }

    CHALLENGE_ATTEMPT {
        uuid id PK
        uuid challenge_id FK
        string attempt_type
        string result
        string ip
        string user_agent
        timestamp created_at
    }

    BACKUP_CODE {
        uuid id PK
        uuid user_id FK
        string code_hash
        timestamp used_at
    }

    AUDIT_EVENT {
        uuid id PK
        uuid tenant_id FK
        string event_type
        string actor_type
        string actor_id
        string subject_id
        string severity
        json payload_json
        timestamp created_at
    }
```

## Комментарии

- `TOTP_SECRET` выделен отдельно, чтобы проще изолировать доступ к чувствительным данным.
- `Challenge` является центром runtime-процесса.
- `AuditEvent` намеренно отделен от бизнес-таблиц и должен проектироваться с учетом долгого хранения и экспорта.
