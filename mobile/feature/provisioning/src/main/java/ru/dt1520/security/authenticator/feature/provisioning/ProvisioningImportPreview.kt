package ru.dt1520.security.authenticator.feature.provisioning

import ru.dt1520.security.authenticator.totp.domain.TotpCredential

data class ProvisioningImportPreview(
    val credential: TotpCredential,
    val source: ProvisioningImportSource
) {
    val summary: String
        get() = credential.account.displayName
}

sealed class ProvisioningImportSource(
    val label: String
) {
    data object OtpAuthUri : ProvisioningImportSource("Provisioning URI")

    data object ManualEntry : ProvisioningImportSource("Manual fallback")
}
