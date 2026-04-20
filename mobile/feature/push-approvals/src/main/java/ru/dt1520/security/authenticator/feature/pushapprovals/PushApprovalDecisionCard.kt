package ru.dt1520.security.authenticator.feature.pushapprovals

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.unit.dp

@Composable
internal fun PushApprovalDecisionCard(
    summary: PushApprovalSummary,
    buttonsEnabled: Boolean,
    isApproving: Boolean,
    isDenying: Boolean,
    onApproveClick: () -> Unit,
    onDenyClick: () -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth()
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            Text(
                text = summary.title,
                style = MaterialTheme.typography.titleMedium
            )
            Text(
                text = summary.supportingText,
                style = MaterialTheme.typography.bodyMedium
            )
            Text(
                text = summary.expiresInDisplay,
                style = MaterialTheme.typography.bodySmall,
                color = if (summary.isExpiringSoon) {
                    MaterialTheme.colorScheme.error
                } else {
                    MaterialTheme.colorScheme.onSurfaceVariant
                }
            )

            Button(
                modifier = Modifier
                    .fillMaxWidth()
                    .testTag(PushApprovalsTestTags.ApproveButton),
                enabled = buttonsEnabled,
                onClick = onApproveClick
            ) {
                Text(
                    text = if (isApproving) {
                        "Подтверждение..."
                    } else {
                        "Подтвердить"
                    }
                )
            }

            OutlinedButton(
                modifier = Modifier
                    .fillMaxWidth()
                    .testTag(PushApprovalsTestTags.DenyButton),
                enabled = buttonsEnabled,
                onClick = onDenyClick
            ) {
                Text(
                    text = if (isDenying) {
                        "Отклонение..."
                    } else {
                        "Отклонить"
                    }
                )
            }
        }
    }
}
