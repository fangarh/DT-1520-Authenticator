package ru.dt1520.security.authenticator.feature.pushapprovals

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Card
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.unit.dp
import java.time.ZoneId
import java.time.format.DateTimeFormatter

@Composable
internal fun PushDecisionHistorySection(
    history: List<PushDecisionHistoryEntry>,
    modifier: Modifier = Modifier
) {
    Column(
        modifier = modifier,
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        Text(
            text = "Последние решения на устройстве",
            style = MaterialTheme.typography.titleMedium
        )

        if (history.isEmpty()) {
            Text(
                text = "Локальная история решений пока пуста.",
                modifier = Modifier.testTag(PushApprovalsTestTags.HistoryEmptyState)
            )
            return@Column
        }

        history.forEach { entry ->
            val title = entry.operationDisplayName?.takeIf { it.isNotBlank() }
                ?: PushApprovalCopy.defaultTitle(entry.operationType)
            val supportingText = entry.username?.takeIf { it.isNotBlank() }
                ?.let { username -> "Пользователь: $username" }
                ?: "Решение сохранено локально без transport-данных."
            val timestampText = HISTORY_DATE_FORMATTER.format(entry.decidedAt)

            Card(
                modifier = Modifier
                    .fillMaxWidth()
                    .testTag(PushApprovalsTestTags.HistoryEntry)
            ) {
                Column(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(16.dp),
                    verticalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    Text(
                        text = title,
                        style = MaterialTheme.typography.titleSmall
                    )
                    Text(
                        text = supportingText,
                        style = MaterialTheme.typography.bodyMedium
                    )
                    Text(
                        text = "${PushApprovalCopy.historyDecisionLabel(entry.decision)} • $timestampText",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            }
        }
    }
}

private val HISTORY_DATE_FORMATTER: DateTimeFormatter =
    DateTimeFormatter.ofPattern("dd.MM.yyyy HH:mm")
        .withZone(ZoneId.systemDefault())
