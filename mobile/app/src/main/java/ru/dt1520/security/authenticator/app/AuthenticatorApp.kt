package ru.dt1520.security.authenticator.app

import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.fragment.app.FragmentActivity
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.delay
import ru.dt1520.security.authenticator.BuildConfig
import ru.dt1520.security.authenticator.app.deviceruntime.DeviceRuntimeSessionInvalidatedException
import ru.dt1520.security.authenticator.app.deviceruntime.DeviceRuntimeSessionManager
import ru.dt1520.security.authenticator.app.deviceruntime.DeviceRuntimeTransportException
import ru.dt1520.security.authenticator.app.deviceruntime.HttpDeviceRuntimeTransport
import ru.dt1520.security.authenticator.app.pushapprovals.AndroidBiometricPushApprovalGate
import ru.dt1520.security.authenticator.app.pushapprovals.DefaultPushApprovalDecisionCoordinator
import ru.dt1520.security.authenticator.app.pushapprovals.DeviceRuntimePushApprovalDeviceRuntime
import ru.dt1520.security.authenticator.app.pushapprovals.PushApprovalDecisionCoordinator
import ru.dt1520.security.authenticator.core.ui.theme.DT1520AuthenticatorTheme
import ru.dt1520.security.authenticator.feature.pushapprovals.PendingPushApproval
import ru.dt1520.security.authenticator.feature.pushapprovals.PushApprovalDecisionResult
import ru.dt1520.security.authenticator.feature.pushapprovals.PushApprovalsRoute
import ru.dt1520.security.authenticator.feature.pushapprovals.PushDecisionHistoryEntry
import ru.dt1520.security.authenticator.feature.provisioning.ProvisioningRoute
import ru.dt1520.security.authenticator.feature.totpcodes.TotpCodesRoute
import ru.dt1520.security.authenticator.security.storage.AndroidKeystoreSecureDeviceSessionStore
import ru.dt1520.security.authenticator.security.storage.AndroidKeystoreSecurePushDecisionHistoryStore
import ru.dt1520.security.authenticator.security.storage.AndroidKeystoreSecureTotpSecretStore
import ru.dt1520.security.authenticator.security.storage.SecureTotpSecretStore
import ru.dt1520.security.authenticator.security.storage.StoredTotpSecret

@Composable
internal fun AuthenticatorApp(
    secureStoreOverride: SecureTotpSecretStore? = null,
    deviceRuntimeManagerOverride: DeviceRuntimeSessionManager? = null,
    pushApprovalDecisionCoordinatorOverride: PushApprovalDecisionCoordinator? = null,
    deviceRuntimeBaseUrl: String? = BuildConfig.DEVICE_RUNTIME_BASE_URL.takeIf { it.isNotBlank() },
    pendingPushApprovals: List<PendingPushApproval> = emptyList(),
    pushDecisionHistory: List<PushDecisionHistoryEntry> = emptyList(),
    onApprovePushChallenge: suspend (PendingPushApproval) -> PushApprovalDecisionResult = {
        PushApprovalDecisionResult.Success
    },
    onDenyPushChallenge: suspend (PendingPushApproval) -> PushApprovalDecisionResult = {
        PushApprovalDecisionResult.Success
    },
    currentEpochSecondsProvider: () -> Long = { System.currentTimeMillis() / 1_000 },
    clockTickDelayMillis: Long = 1_000L
) {
    val context = LocalContext.current
    val activity = context as? FragmentActivity
    val secureStore = secureStoreOverride ?: remember(context) {
        AndroidKeystoreSecureTotpSecretStore.create(context)
    }
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
    var storedSecrets by remember {
        mutableStateOf<List<StoredTotpSecret>>(emptyList())
    }
    var runtimeErrorMessage by remember {
        mutableStateOf<String?>(null)
    }
    var pushRuntimeStatusMessage by remember {
        mutableStateOf<String?>(null)
    }
    var runtimePushApprovals by remember {
        mutableStateOf<List<PendingPushApproval>>(pendingPushApprovals)
    }
    var runtimePushDecisionHistory by remember {
        mutableStateOf<List<PushDecisionHistoryEntry>>(pushDecisionHistory)
    }
    var currentEpochSeconds by remember {
        mutableStateOf(currentEpochSecondsProvider())
    }

    suspend fun refreshStoredSecrets() {
        runCatching {
            loadStoredSecrets(secureStore)
        }.fold(
            onSuccess = { secrets ->
                storedSecrets = secrets
                runtimeErrorMessage = null
            },
            onFailure = {
                storedSecrets = emptyList()
                runtimeErrorMessage =
                    "Не удалось прочитать сохраненные TOTP-учетные записи. Перезапустите приложение и повторите попытку."
            }
        )
    }

    suspend fun refreshPendingPushApprovals() {
        val runtimeManager = deviceRuntimeManager
        if (runtimeManager == null) {
            if (pushApprovalDecisionCoordinator == null) {
                runtimePushApprovals = pendingPushApprovals
            }
            pushRuntimeStatusMessage = null
            return
        }

        runCatching {
            runtimeManager.listPendingPushApprovals()
        }.fold(
            onSuccess = { approvals ->
                runtimePushApprovals = approvals
                pushRuntimeStatusMessage = null
            },
            onFailure = { exception ->
                runtimePushApprovals = emptyList()
                pushRuntimeStatusMessage = exception.toPushRuntimeStatusMessage()
            }
        )
    }

    suspend fun refreshPushDecisionHistory() {
        val coordinator = pushApprovalDecisionCoordinator
        if (coordinator == null) {
            runtimePushDecisionHistory = pushDecisionHistory
            return
        }

        runtimePushDecisionHistory = coordinator.listDecisionHistory()
    }

    fun applyDecisionResult(result: PushApprovalDecisionResult) {
        if (result is PushApprovalDecisionResult.Failure) {
            result.statusMessage?.let { message ->
                pushRuntimeStatusMessage = message
            }
            if (result.shouldClearPendingChallenges) {
                runtimePushApprovals = emptyList()
            }
        }
    }

    LaunchedEffect(secureStore) {
        refreshStoredSecrets()
    }

    LaunchedEffect(deviceRuntimeManager, pendingPushApprovals) {
        refreshPendingPushApprovals()
    }

    LaunchedEffect(pushApprovalDecisionCoordinator, pushDecisionHistory) {
        refreshPushDecisionHistory()
    }

    LaunchedEffect(currentEpochSecondsProvider, clockTickDelayMillis) {
        while (true) {
            currentEpochSeconds = currentEpochSecondsProvider()
            delay(clockTickDelayMillis)
        }
    }

    DT1520AuthenticatorTheme {
        Scaffold(modifier = Modifier.fillMaxSize()) { innerPadding ->
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .verticalScroll(rememberScrollState())
                    .padding(innerPadding)
                    .padding(16.dp),
                verticalArrangement = Arrangement.spacedBy(16.dp)
            ) {
                PushApprovalsRoute(
                    pendingChallenges = if (deviceRuntimeManager == null &&
                        pushApprovalDecisionCoordinator == null
                    ) {
                        pendingPushApprovals
                    } else {
                        runtimePushApprovals
                    },
                    decisionHistory = if (pushApprovalDecisionCoordinator == null) {
                        pushDecisionHistory
                    } else {
                        runtimePushDecisionHistory
                    },
                    currentEpochSeconds = currentEpochSeconds,
                    statusMessage = pushRuntimeStatusMessage,
                    onApproveChallenge = { challenge ->
                        val coordinator = pushApprovalDecisionCoordinator
                        if (coordinator == null) {
                            onApprovePushChallenge(challenge)
                        } else {
                            val result = coordinator.approve(challenge)
                            applyDecisionResult(result)
                            if (result == PushApprovalDecisionResult.Success) {
                                runtimePushApprovals = runtimePushApprovals.filterNot {
                                    it.id == challenge.id
                                }
                                refreshPendingPushApprovals()
                                refreshPushDecisionHistory()
                            }

                            result
                        }
                    },
                    onDenyChallenge = { challenge ->
                        val coordinator = pushApprovalDecisionCoordinator
                        if (coordinator == null) {
                            onDenyPushChallenge(challenge)
                        } else {
                            val result = coordinator.deny(challenge)
                            applyDecisionResult(result)
                            if (result == PushApprovalDecisionResult.Success) {
                                runtimePushApprovals = runtimePushApprovals.filterNot {
                                    it.id == challenge.id
                                }
                                refreshPendingPushApprovals()
                                refreshPushDecisionHistory()
                            }

                            result
                        }
                    }
                )

                ProvisioningRoute(
                    onSaveImport = { preview ->
                        secureStore.save(preview.toStoredSecret())
                        refreshStoredSecrets()
                    }
                )

                runtimeErrorMessage?.let { message ->
                    Text(
                        text = message,
                        color = MaterialTheme.colorScheme.error,
                        style = MaterialTheme.typography.bodyMedium
                    )
                }

                TotpCodesRoute(
                    credentials = storedSecrets.toTotpCredentials(),
                    currentEpochSeconds = currentEpochSeconds,
                    onRemoveAccount = { account ->
                        secureStore.delete(account)
                        refreshStoredSecrets()
                    }
                )
            }
        }
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
