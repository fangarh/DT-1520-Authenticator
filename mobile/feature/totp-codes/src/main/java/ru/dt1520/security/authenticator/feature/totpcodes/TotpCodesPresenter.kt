package ru.dt1520.security.authenticator.feature.totpcodes

import java.util.Locale
import ru.dt1520.security.authenticator.totp.domain.TotpCodeGenerator
import ru.dt1520.security.authenticator.totp.domain.TotpCredential

object TotpCodesPresenter {
    fun present(
        credentials: List<TotpCredential>,
        epochSeconds: Long
    ): TotpCodesUiState {
        if (credentials.isEmpty()) {
            return TotpCodesUiState(summaries = emptyList())
        }

        val summaries = credentials
            .sortedBy { it.account.displayName.lowercase(Locale.ROOT) }
            .map { credential ->
                val codeState = TotpCodeGenerator.generate(
                    credential = credential,
                    epochSeconds = epochSeconds
                )

                TotpCodeSummary(
                    account = credential.account,
                    code = codeState.code,
                    remainingSeconds = codeState.remainingSeconds
                )
            }

        return TotpCodesUiState(summaries = summaries)
    }
}
