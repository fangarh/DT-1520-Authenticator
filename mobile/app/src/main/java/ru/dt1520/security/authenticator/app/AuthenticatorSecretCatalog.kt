package ru.dt1520.security.authenticator.app

import java.util.Locale
import ru.dt1520.security.authenticator.feature.provisioning.ProvisioningImportPreview
import ru.dt1520.security.authenticator.security.storage.SecureTotpSecretStorageException
import ru.dt1520.security.authenticator.security.storage.SecureTotpSecretStore
import ru.dt1520.security.authenticator.security.storage.StoredTotpSecret
import ru.dt1520.security.authenticator.totp.domain.TotpCredential

internal suspend fun loadStoredSecrets(
    store: SecureTotpSecretStore
): List<StoredTotpSecret> = store.list()
    .map { account ->
        store.read(account) ?: throw SecureTotpSecretStorageException(
            message = "Stored secret entry disappeared during refresh."
        )
    }
    .sortedBy { it.account.displayName.lowercase(Locale.ROOT) }

internal fun ProvisioningImportPreview.toStoredSecret(): StoredTotpSecret = StoredTotpSecret(
    account = credential.account,
    secret = credential.secret.toBase32(),
    digits = credential.digits,
    algorithm = credential.algorithm
)

internal fun List<StoredTotpSecret>.toTotpCredentials(): List<TotpCredential> =
    map(StoredTotpSecret::toCredential)
