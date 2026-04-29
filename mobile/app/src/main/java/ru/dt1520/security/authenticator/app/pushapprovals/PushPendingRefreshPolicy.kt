package ru.dt1520.security.authenticator.app.pushapprovals

internal const val DEFAULT_PENDING_PUSH_REFRESH_INTERVAL_MILLIS: Long = 4_000L
internal const val MIN_PENDING_PUSH_REFRESH_INTERVAL_MILLIS: Long = 3_000L
internal const val MAX_PENDING_PUSH_REFRESH_INTERVAL_MILLIS: Long = 5_000L

internal enum class PushPendingRefreshAction {
    RefreshNow,
    Stop,
    Skip
}

internal class PushPendingRefreshPolicy {
    var isForegroundActive: Boolean = false
        private set

    fun onResume(): PushPendingRefreshAction {
        isForegroundActive = true
        return PushPendingRefreshAction.RefreshNow
    }

    fun onPauseOrStop(): PushPendingRefreshAction {
        isForegroundActive = false
        return PushPendingRefreshAction.Stop
    }

    fun onPollingTick(): PushPendingRefreshAction {
        return if (isForegroundActive) {
            PushPendingRefreshAction.RefreshNow
        } else {
            PushPendingRefreshAction.Skip
        }
    }
}

internal fun requireConservativePendingPushRefreshInterval(intervalMillis: Long): Long {
    require(intervalMillis in MIN_PENDING_PUSH_REFRESH_INTERVAL_MILLIS..MAX_PENDING_PUSH_REFRESH_INTERVAL_MILLIS) {
        "Pending push refresh interval must stay between 3 and 5 seconds."
    }

    return intervalMillis
}
