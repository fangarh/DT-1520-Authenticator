package ru.dt1520.security.authenticator.feature.totpcodes

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
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.launch
import ru.dt1520.security.authenticator.core.ui.AppSection
import ru.dt1520.security.authenticator.totp.domain.TotpAccountDescriptor
import ru.dt1520.security.authenticator.totp.domain.TotpCredential

@Composable
fun TotpCodesRoute(
    credentials: List<TotpCredential>,
    currentEpochSeconds: Long,
    onRemoveAccount: suspend (TotpAccountDescriptor) -> Unit,
    modifier: Modifier = Modifier
) {
    var removalState by remember {
        mutableStateOf(TotpCodesRemovalState())
    }
    val coroutineScope = rememberCoroutineScope()
    val uiState = remember(credentials, currentEpochSeconds) {
        TotpCodesPresenter.present(
            credentials = credentials,
            epochSeconds = currentEpochSeconds
        )
    }

    AppSection(
        title = "Offline TOTP Codes",
        description = "Коды генерируются локально из сохраненных секретов. Provisioning artifact не раскрывается повторно после сохранения.",
        modifier = modifier
    ) {
        if (uiState.isEmpty) {
            Text(
                text = uiState.emptyMessage,
                modifier = Modifier.testTag(TotpCodesTestTags.EmptyStateMessage)
            )
            return@AppSection
        }

        Column(
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            uiState.summaries.forEach { summary ->
                val isPendingRemoval = removalState.isPendingRemoval(summary.account)
                val isRemoving = removalState.isRemoving(summary.account)
                val statusText = if (summary.isExpiringSoon) {
                    "Код скоро обновится."
                } else {
                    "Код действителен на текущем устройстве офлайн."
                }

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
                            text = summary.account.displayName,
                            style = MaterialTheme.typography.titleMedium
                        )
                        Text(
                            text = summary.formattedCode,
                            style = MaterialTheme.typography.headlineMedium,
                            fontFamily = FontFamily.Monospace
                        )
                        Text(
                            text = "Обновится через ${summary.remainingSeconds}с",
                            style = MaterialTheme.typography.bodyMedium
                        )
                        Text(
                            text = statusText,
                            style = MaterialTheme.typography.bodySmall
                        )

                        if (isPendingRemoval) {
                            Text(
                                text = "Удаление с устройства необратимо. Секрет останется только в защищенном хранилище до завершения операции.",
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.error
                            )
                            Column(
                                verticalArrangement = Arrangement.spacedBy(8.dp)
                            ) {
                                Button(
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .testTag(TotpCodesTestTags.ConfirmRemoveButton),
                                    enabled = !isRemoving,
                                    onClick = {
                                        coroutineScope.launch {
                                            removalState = TotpCodesRemovalWorkflow.markRemovalStarted(
                                                state = removalState,
                                                account = summary.account
                                            )

                                            runCatching {
                                                onRemoveAccount(summary.account)
                                            }

                                            removalState = TotpCodesRemovalWorkflow.markRemovalFinished(
                                                state = removalState,
                                                account = summary.account
                                            )
                                        }
                                    }
                                ) {
                                    Text(
                                        text = if (isRemoving) {
                                            "Удаление..."
                                        } else {
                                            "Подтвердить удаление"
                                        }
                                    )
                                }
                                OutlinedButton(
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .testTag(TotpCodesTestTags.CancelRemoveButton),
                                    enabled = !isRemoving,
                                    onClick = {
                                        removalState = TotpCodesRemovalWorkflow.cancelRemoval(removalState)
                                    }
                                ) {
                                    Text(text = "Отмена")
                                }
                            }
                        } else {
                            OutlinedButton(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .testTag(TotpCodesTestTags.RemoveButton),
                                onClick = {
                                    removalState = TotpCodesRemovalWorkflow.requestRemoval(
                                        state = removalState,
                                        account = summary.account
                                    )
                                }
                            ) {
                                Text(text = "Удалить с устройства")
                            }
                        }
                    }
                }
            }
        }
    }
}
