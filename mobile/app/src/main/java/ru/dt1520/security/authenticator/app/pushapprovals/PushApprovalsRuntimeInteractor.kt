package ru.dt1520.security.authenticator.app.pushapprovals

import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.platform.LocalContext
import androidx.fragment.app.FragmentActivity
import ru.dt1520.security.authenticator.app.deviceruntime.DeviceRuntimeSessionInvalidatedException
import ru.dt1520.security.authenticator.app.deviceruntime.DeviceRuntimeSessionManager
import ru.dt1520.security.authenticator.app.deviceruntime.DeviceRuntimeTransportException
import ru.dt1520.security.authenticator.app.deviceruntime.HttpDeviceRuntimeTransport
import ru.dt1520.security.authenticator.feature.pushapprovals.PendingPushApproval
import ru.dt1520.security.authenticator.feature.pushapprovals.PushApprovalDecisionResult
import ru.dt1520.security.authenticator.feature.pushapprovals.PushDecisionHistoryEntry
import ru.dt1520.security.authenticator.security.storage.AndroidKeystoreSecureDeviceSessionStore
import ru.dt1520.security.authenticator.security.storage.AndroidKeystoreSecurePushDecisionHistoryStore

internal data class PushApprovalsPendingLoadResult(
    val challenges: List<PendingPushApproval>,
    val statusMessage: String?
)

internal data class PushApprovalsDecisionUpdate(
    val result: PushApprovalDecisionResult,
    val pendingChallenges: List<PendingPushApproval>,
    val decisionHistory: List<PushDecisionHistoryEntry>,
    val statusMessage: String?
)

internal class PushApprovalsRuntimeInteractor(
    private val listPendingChallenges: (suspend () -> List<PendingPushApproval>)? = null,
    private val listDecisionHistory: (suspend () -> List<PushDecisionHistoryEntry>)? = null,
    private val approveChallenge: suspend (PendingPushApproval) -> PushApprovalDecisionResult = {
        PushApprovalDecisionResult.Success
    },
    private val denyChallenge: suspend (PendingPushApproval) -> PushApprovalDecisionResult = {
        PushApprovalDecisionResult.Success
    }
) {
    suspend fun loadPendingChallenges(
        fallbackChallenges: List<PendingPushApproval>
    ): PushApprovalsPendingLoadResult {
        val pendingLoader = listPendingChallenges ?: return PushApprovalsPendingLoadResult(
            challenges = fallbackChallenges,
            statusMessage = null
        )

        return runCatching {
            pendingLoader()
        }.fold(
            onSuccess = { challenges ->
                PushApprovalsPendingLoadResult(
                    challenges = challenges,
                    statusMessage = null
                )
            },
            onFailure = { exception ->
                PushApprovalsPendingLoadResult(
                    challenges = emptyList(),
                    statusMessage = exception.toPushRuntimeStatusMessage()
                )
            }
        )
    }

    suspend fun loadDecisionHistory(
        fallbackHistory: List<PushDecisionHistoryEntry>
    ): List<PushDecisionHistoryEntry> = listDecisionHistory?.invoke() ?: fallbackHistory

    suspend fun approve(
        challenge: PendingPushApproval,
        currentPendingChallenges: List<PendingPushApproval>,
        currentDecisionHistory: List<PushDecisionHistoryEntry>
    ): PushApprovalsDecisionUpdate = executeDecision(
        challenge = challenge,
        currentPendingChallenges = currentPendingChallenges,
        currentDecisionHistory = currentDecisionHistory,
        operation = approveChallenge
    )

    suspend fun deny(
        challenge: PendingPushApproval,
        currentPendingChallenges: List<PendingPushApproval>,
        currentDecisionHistory: List<PushDecisionHistoryEntry>
    ): PushApprovalsDecisionUpdate = executeDecision(
        challenge = challenge,
        currentPendingChallenges = currentPendingChallenges,
        currentDecisionHistory = currentDecisionHistory,
        operation = denyChallenge
    )

    private suspend fun executeDecision(
        challenge: PendingPushApproval,
        currentPendingChallenges: List<PendingPushApproval>,
        currentDecisionHistory: List<PushDecisionHistoryEntry>,
        operation: suspend (PendingPushApproval) -> PushApprovalDecisionResult
    ): PushApprovalsDecisionUpdate {
        val result = operation(challenge)
        if (result is PushApprovalDecisionResult.Failure) {
            return PushApprovalsDecisionUpdate(
                result = result,
                pendingChallenges = if (result.shouldClearPendingChallenges) {
                    emptyList()
                } else {
                    currentPendingChallenges
                },
                decisionHistory = currentDecisionHistory,
                statusMessage = result.statusMessage
            )
        }

        val filteredPendingChallenges = currentPendingChallenges.filterNot { existingChallenge ->
            existingChallenge.id == challenge.id
        }
        val pendingLoadResult = loadPendingChallenges(filteredPendingChallenges)

        return PushApprovalsDecisionUpdate(
            result = result,
            pendingChallenges = pendingLoadResult.challenges,
            decisionHistory = loadDecisionHistory(currentDecisionHistory),
            statusMessage = pendingLoadResult.statusMessage
        )
    }
}

@Composable
internal fun rememberPushApprovalsRuntimeInteractor(
    deviceRuntimeManagerOverride: DeviceRuntimeSessionManager? = null,
    pushApprovalDecisionCoordinatorOverride: PushApprovalDecisionCoordinator? = null,
    deviceRuntimeBaseUrl: String? = null,
    onApprovePushChallenge: suspend (PendingPushApproval) -> PushApprovalDecisionResult = {
        PushApprovalDecisionResult.Success
    },
    onDenyPushChallenge: suspend (PendingPushApproval) -> PushApprovalDecisionResult = {
        PushApprovalDecisionResult.Success
    }
): PushApprovalsRuntimeInteractor {
    val context = LocalContext.current
    val activity = context as? FragmentActivity
    val deviceRuntimeManager = deviceRuntimeManagerOverride ?: remember(context, deviceRuntimeBaseUrl) {
        deviceRuntimeBaseUrl?.let { baseUrl ->
            DeviceRuntimeSessionManager(
                sessionStore = AndroidKeystoreSecureDeviceSessionStore.create(context),
                transport = HttpDeviceRuntimeTransport(baseUrl)
            )
        }
    }
    val pushApprovalDecisionCoordinator = pushApprovalDecisionCoordinatorOverride ?: remember(
        context,
        activity,
        deviceRuntimeManager
    ) {
        val runtimeManager = deviceRuntimeManager
        val hostActivity = activity
        if (runtimeManager == null || hostActivity == null) {
            null
        } else {
            DefaultPushApprovalDecisionCoordinator(
                runtime = DeviceRuntimePushApprovalDeviceRuntime(runtimeManager),
                biometricGate = AndroidBiometricPushApprovalGate(hostActivity),
                historyStore = AndroidKeystoreSecurePushDecisionHistoryStore.create(context)
            )
        }
    }

    return remember(
        deviceRuntimeManager,
        pushApprovalDecisionCoordinator,
        onApprovePushChallenge,
        onDenyPushChallenge
    ) {
        PushApprovalsRuntimeInteractor(
            listPendingChallenges = deviceRuntimeManager?.let { runtimeManager ->
                { runtimeManager.listPendingPushApprovals() }
            },
            listDecisionHistory = pushApprovalDecisionCoordinator?.let { coordinator ->
                { coordinator.listDecisionHistory() }
            },
            approveChallenge = { challenge ->
                pushApprovalDecisionCoordinator?.approve(challenge)
                    ?: onApprovePushChallenge(challenge)
            },
            denyChallenge = { challenge ->
                pushApprovalDecisionCoordinator?.deny(challenge)
                    ?: onDenyPushChallenge(challenge)
            }
        )
    }
}

private fun Throwable.toPushRuntimeStatusMessage(): String {
    return when (this) {
        is DeviceRuntimeSessionInvalidatedException ->
            "Сессия устройства истекла, отозвана или заблокирована. Привяжите устройство заново."

        is DeviceRuntimeTransportException ->
            "Не удалось синхронизировать ожидающие push-запросы. Проверьте соединение и повторите попытку."

        else ->
            "Не удалось синхронизировать ожидающие push-запросы. Повторите попытку позже."
    }
}
