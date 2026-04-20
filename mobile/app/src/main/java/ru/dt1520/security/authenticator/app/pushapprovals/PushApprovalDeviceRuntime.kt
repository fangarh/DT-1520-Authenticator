package ru.dt1520.security.authenticator.app.pushapprovals

import ru.dt1520.security.authenticator.app.deviceruntime.DeviceRuntimeSessionManager
import ru.dt1520.security.authenticator.feature.pushapprovals.PendingPushApproval

internal interface PushApprovalDeviceRuntime {
    suspend fun approvePushChallenge(challenge: PendingPushApproval)

    suspend fun denyPushChallenge(challenge: PendingPushApproval)
}

internal class DeviceRuntimePushApprovalDeviceRuntime(
    private val runtimeManager: DeviceRuntimeSessionManager
) : PushApprovalDeviceRuntime {
    override suspend fun approvePushChallenge(challenge: PendingPushApproval) {
        runtimeManager.approvePushChallenge(challenge)
    }

    override suspend fun denyPushChallenge(challenge: PendingPushApproval) {
        runtimeManager.denyPushChallenge(challenge)
    }
}
