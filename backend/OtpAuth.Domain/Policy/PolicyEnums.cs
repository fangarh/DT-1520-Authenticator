namespace OtpAuth.Domain.Policy;

public enum FactorType
{
    Unknown = 0,
    Totp = 1,
    Push = 2,
    BackupCode = 3,
}

public enum OperationType
{
    Unknown = 0,
    Login = 1,
    StepUp = 2,
    DeviceActivation = 3,
    TotpEnrollment = 4,
    BackupCodeRecovery = 5,
}

public enum DeviceTrustState
{
    Unknown = 0,
    None = 1,
    Pending = 2,
    Active = 3,
    Revoked = 4,
    Blocked = 5,
}

public enum DeploymentProfile
{
    Unknown = 0,
    Cloud = 1,
    OnPrem = 2,
    AirGapped = 3,
}

public enum EnvironmentMode
{
    Unknown = 0,
    Production = 1,
    Sandbox = 2,
    Development = 3,
}

public enum ChallengePurpose
{
    Unknown = 0,
    Authentication = 1,
    StepUp = 2,
    Enrollment = 3,
    Recovery = 4,
}

public enum UserStatus
{
    Unknown = 0,
    Active = 1,
    Suspended = 2,
    Blocked = 3,
}

public enum EnrollmentInitiationSource
{
    Unknown = 0,
    Admin = 1,
    TrustedIntegration = 2,
    SelfService = 3,
}
