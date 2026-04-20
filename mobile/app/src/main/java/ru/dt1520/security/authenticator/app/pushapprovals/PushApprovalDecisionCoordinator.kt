package ru.dt1520.security.authenticator.app.pushapprovals

import java.time.Instant
import ru.dt1520.security.authenticator.app.deviceruntime.DeviceRuntimeSessionInvalidatedException
import ru.dt1520.security.authenticator.app.deviceruntime.DeviceRuntimeTransportException
import ru.dt1520.security.authenticator.feature.pushapprovals.PendingPushApproval
import ru.dt1520.security.authenticator.feature.pushapprovals.PushApprovalDecisionResult
import ru.dt1520.security.authenticator.feature.pushapprovals.PushDecisionHistoryDecision
import ru.dt1520.security.authenticator.feature.pushapprovals.PushDecisionHistoryEntry
import ru.dt1520.security.authenticator.security.storage.SecurePushDecisionHistoryStore
import ru.dt1520.security.authenticator.security.storage.StoredPushDecisionHistoryDecision
import ru.dt1520.security.authenticator.security.storage.StoredPushDecisionHistoryEntry

internal interface PushApprovalDecisionCoordinator {
    suspend fun listDecisionHistory(): List<PushDecisionHistoryEntry>

    suspend fun approve(challenge: PendingPushApproval): PushApprovalDecisionResult

    suspend fun deny(challenge: PendingPushApproval): PushApprovalDecisionResult
}

internal class DefaultPushApprovalDecisionCoordinator(
    private val runtime: PushApprovalDeviceRuntime,
    private val biometricGate: PushApprovalBiometricGate,
    private val historyStore: SecurePushDecisionHistoryStore,
    private val currentInstantProvider: () -> Instant = { Instant.now() },
    private val historyLimit: Int = SecurePushDecisionHistoryStore.DEFAULT_HISTORY_LIMIT
) : PushApprovalDecisionCoordinator {
    override suspend fun listDecisionHistory(): List<PushDecisionHistoryEntry> {
        return runCatching {
            historyStore.list(limit = historyLimit)
        }.getOrDefault(emptyList())
            .map { entry ->
                PushDecisionHistoryEntry(
                    operationType = entry.operationType,
                    operationDisplayName = entry.operationDisplayName,
                    username = entry.username,
                    decision = when (entry.decision) {
                        StoredPushDecisionHistoryDecision.Approved ->
                            PushDecisionHistoryDecision.Approved

                        StoredPushDecisionHistoryDecision.Denied ->
                            PushDecisionHistoryDecision.Denied
                    },
                    decidedAt = Instant.ofEpochSecond(entry.decidedAtEpochSeconds)
                )
            }
    }

    override suspend fun approve(
        challenge: PendingPushApproval
    ): PushApprovalDecisionResult {
        return when (val gateResult = biometricGate.verifyApproval(challenge)) {
            PushApprovalBiometricGateResult.Verified ->
                runDecision(
                    challenge = challenge,
                    decision = StoredPushDecisionHistoryDecision.Approved,
                    genericFailureMessage = APPROVE_FAILURE_MESSAGE
                ) {
                    runtime.approvePushChallenge(challenge)
                }

            is PushApprovalBiometricGateResult.Rejected ->
                PushApprovalDecisionResult.Failure(gateResult.userMessage)
        }
    }

    override suspend fun deny(
        challenge: PendingPushApproval
    ): PushApprovalDecisionResult {
        return runDecision(
            challenge = challenge,
            decision = StoredPushDecisionHistoryDecision.Denied,
            genericFailureMessage = DENY_FAILURE_MESSAGE
        ) {
            runtime.denyPushChallenge(challenge)
        }
    }

    private suspend fun runDecision(
        challenge: PendingPushApproval,
        decision: StoredPushDecisionHistoryDecision,
        genericFailureMessage: String,
        operation: suspend () -> Unit
    ): PushApprovalDecisionResult {
        return try {
            operation()
            persistHistoryEntry(challenge, decision)
            PushApprovalDecisionResult.Success
        } catch (exception: DeviceRuntimeSessionInvalidatedException) {
            PushApprovalDecisionResult.Failure(
                userMessage = DEVICE_SESSION_INVALIDATED_MESSAGE,
                statusMessage = DEVICE_SESSION_INVALIDATED_MESSAGE,
                shouldClearPendingChallenges = true
            )
        } catch (exception: DeviceRuntimeTransportException) {
            PushApprovalDecisionResult.Failure(genericFailureMessage)
        } catch (exception: Throwable) {
            PushApprovalDecisionResult.Failure(genericFailureMessage)
        }
    }

    private suspend fun persistHistoryEntry(
        challenge: PendingPushApproval,
        decision: StoredPushDecisionHistoryDecision
    ) {
        runCatching {
            historyStore.append(
                entry = StoredPushDecisionHistoryEntry(
                    operationType = challenge.operationType,
                    operationDisplayName = challenge.operationDisplayName,
                    username = challenge.username,
                    decision = decision,
                    decidedAtEpochSeconds = currentInstantProvider().epochSecond
                ),
                limit = historyLimit
            )
        }
    }

    private companion object {
        const val APPROVE_FAILURE_MESSAGE: String =
            "Не удалось подтвердить запрос на этом устройстве. Повторите попытку после обновления соединения."
        const val DENY_FAILURE_MESSAGE: String =
            "Не удалось отклонить запрос на этом устройстве. Повторите попытку после обновления соединения."
        const val DEVICE_SESSION_INVALIDATED_MESSAGE: String =
            "Сессия устройства истекла, отозвана или заблокирована. Привяжите устройство заново."
    }
}
