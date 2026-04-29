package ru.dt1520.security.authenticator.app.pushapprovals

import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.setValue
import androidx.compose.ui.platform.LocalContext
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import androidx.lifecycle.LifecycleOwner
import kotlinx.coroutines.delay

@Composable
internal fun PushPendingRefreshEffect(
    refreshKey: Any?,
    pollingIntervalMillis: Long = DEFAULT_PENDING_PUSH_REFRESH_INTERVAL_MILLIS,
    onRefresh: suspend () -> Unit
) {
    val validatedPollingIntervalMillis = requireConservativePendingPushRefreshInterval(pollingIntervalMillis)
    val currentOnRefresh by rememberUpdatedState(onRefresh)
    val lifecycleOwner = LocalContext.current as? LifecycleOwner
    val policy = remember {
        PushPendingRefreshPolicy()
    }
    var isForegroundActive by remember {
        mutableStateOf(false)
    }
    var resumeSignal by remember {
        mutableIntStateOf(0)
    }

    DisposableEffect(lifecycleOwner, policy) {
        val lifecycle = lifecycleOwner?.lifecycle
        if (lifecycle == null) {
            onDispose {
                policy.onPauseOrStop()
                isForegroundActive = false
            }
        } else {
            fun applyLifecycleAction(action: PushPendingRefreshAction) {
                when (action) {
                    PushPendingRefreshAction.RefreshNow -> {
                        isForegroundActive = true
                        resumeSignal += 1
                    }

                    PushPendingRefreshAction.Stop -> isForegroundActive = false
                    PushPendingRefreshAction.Skip -> Unit
                }
            }

            if (lifecycle.currentState.isAtLeast(Lifecycle.State.RESUMED)) {
                applyLifecycleAction(policy.onResume())
            }

            val observer = LifecycleEventObserver { _, event ->
                when (event) {
                    Lifecycle.Event.ON_RESUME -> applyLifecycleAction(policy.onResume())
                    Lifecycle.Event.ON_PAUSE,
                    Lifecycle.Event.ON_STOP,
                    Lifecycle.Event.ON_DESTROY -> applyLifecycleAction(policy.onPauseOrStop())

                    else -> Unit
                }
            }
            lifecycle.addObserver(observer)

            onDispose {
                lifecycle.removeObserver(observer)
                policy.onPauseOrStop()
                isForegroundActive = false
            }
        }
    }

    LaunchedEffect(
        refreshKey,
        isForegroundActive,
        resumeSignal,
        validatedPollingIntervalMillis
    ) {
        if (!isForegroundActive) {
            return@LaunchedEffect
        }

        currentOnRefresh()

        while (true) {
            delay(validatedPollingIntervalMillis)
            if (policy.onPollingTick() == PushPendingRefreshAction.RefreshNow) {
                currentOnRefresh()
            }
        }
    }
}
