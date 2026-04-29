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
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.delay
import ru.dt1520.security.authenticator.BuildConfig
import ru.dt1520.security.authenticator.app.deviceonboarding.resolveDefaultDeviceRuntimeBaseUrl
import ru.dt1520.security.authenticator.app.deviceonboarding.rememberDeviceOnboardingQrScanner
import ru.dt1520.security.authenticator.app.deviceruntime.DeviceRuntimeSessionManager
import ru.dt1520.security.authenticator.app.deviceruntime.HttpDeviceRuntimeTransport
import ru.dt1520.security.authenticator.app.pushapprovals.DEFAULT_PENDING_PUSH_REFRESH_INTERVAL_MILLIS
import ru.dt1520.security.authenticator.app.pushapprovals.PushApprovalDecisionCoordinator
import ru.dt1520.security.authenticator.app.pushapprovals.PushPendingRefreshEffect
import ru.dt1520.security.authenticator.app.pushapprovals.rememberPushApprovalsRuntimeInteractor
import ru.dt1520.security.authenticator.core.ui.theme.DT1520AuthenticatorTheme
import ru.dt1520.security.authenticator.feature.deviceonboarding.DeviceOnboardingActivationResult
import ru.dt1520.security.authenticator.feature.deviceonboarding.DeviceOnboardingPayload
import ru.dt1520.security.authenticator.feature.deviceonboarding.DeviceOnboardingRoute
import ru.dt1520.security.authenticator.feature.pushapprovals.PendingPushApproval
import ru.dt1520.security.authenticator.feature.pushapprovals.PushApprovalDecisionResult
import ru.dt1520.security.authenticator.feature.pushapprovals.PushApprovalsRoute
import ru.dt1520.security.authenticator.feature.pushapprovals.PushDecisionHistoryEntry
import ru.dt1520.security.authenticator.feature.provisioning.ProvisioningRoute
import ru.dt1520.security.authenticator.feature.totpcodes.TotpCodesRoute
import ru.dt1520.security.authenticator.security.storage.AndroidKeystoreSecureDeviceSessionStore
import ru.dt1520.security.authenticator.security.storage.AndroidKeystoreSecureTotpSecretStore
import ru.dt1520.security.authenticator.security.storage.SecureTotpSecretStore
import ru.dt1520.security.authenticator.security.storage.StoredTotpSecret

@Composable
internal fun AuthenticatorApp(
    secureStoreOverride: SecureTotpSecretStore? = null,
    deviceRuntimeManagerOverride: DeviceRuntimeSessionManager? = null,
    pushApprovalDecisionCoordinatorOverride: PushApprovalDecisionCoordinator? = null,
    deviceRuntimeBaseUrl: String? = BuildConfig.DEVICE_RUNTIME_BASE_URL.takeIf { it.isNotBlank() },
    devicePushTokenResolver: suspend (DeviceRuntimeSessionManager) -> String? =
        ::resolveDevicePushTokenForCurrentBuild,
    pendingPushApprovals: List<PendingPushApproval> = emptyList(),
    pushDecisionHistory: List<PushDecisionHistoryEntry> = emptyList(),
    onScanDeviceOnboardingQrPayload: (suspend () -> String?)? = null,
    onActivateDeviceOnboardingPayload: (suspend (DeviceOnboardingPayload) -> DeviceOnboardingActivationResult)? = null,
    onApprovePushChallenge: suspend (PendingPushApproval) -> PushApprovalDecisionResult = {
        PushApprovalDecisionResult.Success
    },
    onDenyPushChallenge: suspend (PendingPushApproval) -> PushApprovalDecisionResult = {
        PushApprovalDecisionResult.Success
    },
    currentEpochSecondsProvider: () -> Long = { System.currentTimeMillis() / 1_000 },
    clockTickDelayMillis: Long = 1_000L,
    pendingPushRefreshIntervalMillis: Long = DEFAULT_PENDING_PUSH_REFRESH_INTERVAL_MILLIS
) {
    val context = LocalContext.current
    val secureStore = secureStoreOverride ?: remember(context) {
        AndroidKeystoreSecureTotpSecretStore.create(context)
    }
    val deviceSessionStore = remember(context) {
        AndroidKeystoreSecureDeviceSessionStore.create(context)
    }
    var persistedDeviceRuntimeBaseUrl by remember {
        mutableStateOf<String?>(null)
    }
    LaunchedEffect(deviceSessionStore) {
        persistedDeviceRuntimeBaseUrl = runCatching {
            deviceSessionStore.readSession()?.runtimeBaseUrl
        }.getOrNull()
    }
    val effectiveDeviceRuntimeBaseUrl = resolveDefaultDeviceRuntimeBaseUrl(
        configuredBaseUrl = deviceRuntimeBaseUrl,
        persistedBaseUrl = persistedDeviceRuntimeBaseUrl
    )
    val deviceRuntimeManagerFactory: (String) -> DeviceRuntimeSessionManager = remember(deviceSessionStore) {
        { baseUrl ->
            DeviceRuntimeSessionManager(
                sessionStore = deviceSessionStore,
                transport = HttpDeviceRuntimeTransport(baseUrl),
                runtimeBaseUrl = baseUrl
            )
        }
    }
    val defaultDeviceRuntimeManager = remember(deviceRuntimeManagerFactory, effectiveDeviceRuntimeBaseUrl) {
        effectiveDeviceRuntimeBaseUrl?.let { baseUrl ->
            deviceRuntimeManagerFactory(baseUrl)
        }
    }
    val deviceRuntimeManager = deviceRuntimeManagerOverride ?: defaultDeviceRuntimeManager
    val pushApprovalsRuntimeInteractor = rememberPushApprovalsRuntimeInteractor(
        deviceRuntimeManagerOverride = deviceRuntimeManager,
        pushApprovalDecisionCoordinatorOverride = pushApprovalDecisionCoordinatorOverride,
        deviceRuntimeBaseUrl = effectiveDeviceRuntimeBaseUrl,
        onApprovePushChallenge = onApprovePushChallenge,
        onDenyPushChallenge = onDenyPushChallenge
    )
    val defaultQrScanner = rememberDeviceOnboardingQrScanner()
    val scanDeviceOnboardingQrPayload = onScanDeviceOnboardingQrPayload ?: defaultQrScanner
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

    val activateDeviceOnboardingPayload: suspend (DeviceOnboardingPayload) -> DeviceOnboardingActivationResult =
        onActivateDeviceOnboardingPayload ?: { payload ->
            activateDefaultDeviceOnboardingPayload(
                payload = payload,
                configuredBaseUrl = deviceRuntimeBaseUrl,
                defaultRuntimeManager = defaultDeviceRuntimeManager,
                runtimeManagerOverride = deviceRuntimeManagerOverride,
                runtimeManagerFactory = deviceRuntimeManagerFactory,
                secureStore = secureStore,
                deviceName = android.os.Build.MODEL.takeIf(String::isNotBlank) ?: "Android device",
                devicePushTokenResolver = devicePushTokenResolver,
                updatePersistedRuntimeBaseUrl = { activationRuntimeBaseUrl ->
                    persistedDeviceRuntimeBaseUrl = activationRuntimeBaseUrl ?: persistedDeviceRuntimeBaseUrl
                },
                refreshStoredSecrets = { refreshStoredSecrets() }
            )
        }

    suspend fun refreshPendingPushApprovals() {
        val pendingLoadResult = pushApprovalsRuntimeInteractor.loadPendingChallenges(
            fallbackChallenges = runtimePushApprovals
        )
        runtimePushApprovals = pendingLoadResult.challenges
        pushRuntimeStatusMessage = pendingLoadResult.statusMessage
    }

    LaunchedEffect(secureStore) {
        refreshStoredSecrets()
    }

    PushPendingRefreshEffect(
        refreshKey = pushApprovalsRuntimeInteractor,
        pollingIntervalMillis = pendingPushRefreshIntervalMillis,
        onRefresh = { refreshPendingPushApprovals() }
    )

    LaunchedEffect(pushApprovalsRuntimeInteractor, pushDecisionHistory) {
        runtimePushDecisionHistory = pushApprovalsRuntimeInteractor.loadDecisionHistory(
            fallbackHistory = pushDecisionHistory
        )
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
                    pendingChallenges = runtimePushApprovals,
                    decisionHistory = runtimePushDecisionHistory,
                    currentEpochSeconds = currentEpochSeconds,
                    statusMessage = pushRuntimeStatusMessage,
                    onApproveChallenge = { challenge ->
                        val update = pushApprovalsRuntimeInteractor.approve(
                            challenge = challenge,
                            currentPendingChallenges = runtimePushApprovals,
                            currentDecisionHistory = runtimePushDecisionHistory
                        )
                        runtimePushApprovals = update.pendingChallenges
                        runtimePushDecisionHistory = update.decisionHistory
                        pushRuntimeStatusMessage = update.statusMessage
                        update.result
                    },
                    onDenyChallenge = { challenge ->
                        val update = pushApprovalsRuntimeInteractor.deny(
                            challenge = challenge,
                            currentPendingChallenges = runtimePushApprovals,
                            currentDecisionHistory = runtimePushDecisionHistory
                        )
                        runtimePushApprovals = update.pendingChallenges
                        runtimePushDecisionHistory = update.decisionHistory
                        pushRuntimeStatusMessage = update.statusMessage
                        update.result
                    }
                )

                DeviceOnboardingRoute(
                    onScanQrPayload = scanDeviceOnboardingQrPayload,
                    onActivatePayload = activateDeviceOnboardingPayload
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
