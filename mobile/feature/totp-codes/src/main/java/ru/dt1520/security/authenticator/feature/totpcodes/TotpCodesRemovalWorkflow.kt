package ru.dt1520.security.authenticator.feature.totpcodes

import ru.dt1520.security.authenticator.totp.domain.TotpAccountDescriptor

data class TotpCodesRemovalState(
    val pendingRemovalAccount: TotpAccountDescriptor? = null,
    val removingAccount: TotpAccountDescriptor? = null
) {
    fun isPendingRemoval(account: TotpAccountDescriptor): Boolean =
        pendingRemovalAccount == account

    fun isRemoving(account: TotpAccountDescriptor): Boolean =
        removingAccount == account
}

object TotpCodesRemovalWorkflow {
    fun requestRemoval(
        state: TotpCodesRemovalState,
        account: TotpAccountDescriptor
    ): TotpCodesRemovalState = state.copy(
        pendingRemovalAccount = account,
        removingAccount = null
    )

    fun cancelRemoval(state: TotpCodesRemovalState): TotpCodesRemovalState = state.copy(
        pendingRemovalAccount = null,
        removingAccount = null
    )

    fun markRemovalStarted(
        state: TotpCodesRemovalState,
        account: TotpAccountDescriptor
    ): TotpCodesRemovalState = state.copy(
        pendingRemovalAccount = account,
        removingAccount = account
    )

    fun markRemovalFinished(
        state: TotpCodesRemovalState,
        account: TotpAccountDescriptor
    ): TotpCodesRemovalState = if (state.removingAccount == account) {
        TotpCodesRemovalState()
    } else {
        state
    }
}
