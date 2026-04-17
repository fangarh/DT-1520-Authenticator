package ru.dt1520.security.authenticator.feature.totpcodes

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test
import ru.dt1520.security.authenticator.totp.domain.TotpAccountDescriptor

class TotpCodesRemovalWorkflowTest {
    private val account = TotpAccountDescriptor(
        issuer = "DT 1520",
        accountName = "operator@example.local"
    )

    @Test
    fun requestRemovalTargetsChosenAccount() {
        val nextState = TotpCodesRemovalWorkflow.requestRemoval(
            state = TotpCodesRemovalState(),
            account = account
        )

        assertEquals(account, nextState.pendingRemovalAccount)
        assertNull(nextState.removingAccount)
    }

    @Test
    fun cancelRemovalClearsPendingAndRunningFlags() {
        val nextState = TotpCodesRemovalWorkflow.cancelRemoval(
            state = TotpCodesRemovalState(
                pendingRemovalAccount = account,
                removingAccount = account
            )
        )

        assertNull(nextState.pendingRemovalAccount)
        assertNull(nextState.removingAccount)
    }

    @Test
    fun markRemovalStartedAndFinishedResetsFlow() {
        val startedState = TotpCodesRemovalWorkflow.markRemovalStarted(
            state = TotpCodesRemovalState(
                pendingRemovalAccount = account
            ),
            account = account
        )
        val finishedState = TotpCodesRemovalWorkflow.markRemovalFinished(
            state = startedState,
            account = account
        )

        assertEquals(account, startedState.removingAccount)
        assertNull(finishedState.pendingRemovalAccount)
        assertNull(finishedState.removingAccount)
    }
}
