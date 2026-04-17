package ru.dt1520.security.authenticator.feature.provisioning

import ru.dt1520.security.authenticator.totp.domain.OtpAuthUriParser
import ru.dt1520.security.authenticator.totp.domain.TotpAccountDescriptor
import ru.dt1520.security.authenticator.totp.domain.TotpAlgorithm
import ru.dt1520.security.authenticator.totp.domain.TotpCredential
import ru.dt1520.security.authenticator.totp.domain.TotpSecret

data class ProvisioningDraft(
    val otpAuthUri: String = "",
    val manualIssuer: String = "",
    val manualAccountName: String = "",
    val manualSecret: String = ""
) {
    val canPreviewOtpAuthImport: Boolean
        get() = otpAuthUri.isNotBlank()

    val canPreviewManualImport: Boolean
        get() = manualIssuer.isNotBlank() &&
            manualAccountName.isNotBlank() &&
            manualSecret.isNotBlank()

    fun buildOtpAuthPreview(): ProvisioningImportPreview = ProvisioningImportPreview(
        credential = OtpAuthUriParser.parse(otpAuthUri.trim()),
        source = ProvisioningImportSource.OtpAuthUri
    )

    fun buildManualPreview(): ProvisioningImportPreview {
        require(canPreviewManualImport) {
            "Manual import requires issuer, account name and secret."
        }

        return ProvisioningImportPreview(
            credential = TotpCredential(
                account = TotpAccountDescriptor(
                    issuer = manualIssuer.trim(),
                    accountName = manualAccountName.trim()
                ),
                secret = TotpSecret.fromBase32(manualSecret.trim()),
                digits = TotpCredential.DEFAULT_DIGITS,
                algorithm = TotpAlgorithm.Sha1
            ),
            source = ProvisioningImportSource.ManualEntry
        )
    }
}
