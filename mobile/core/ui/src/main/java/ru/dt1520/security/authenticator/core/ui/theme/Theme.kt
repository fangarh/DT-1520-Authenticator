package ru.dt1520.security.authenticator.core.ui.theme

import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable

private val LightColors = lightColorScheme(
    primary = Cyan600,
    onPrimary = Slate100,
    secondary = Cyan400,
    tertiary = Coral500,
    background = Slate100,
    onBackground = Slate950,
    surface = Slate100,
    onSurface = Slate950
)

private val DarkColors = darkColorScheme(
    primary = Cyan400,
    onPrimary = Slate950,
    secondary = Cyan600,
    tertiary = Coral500,
    background = Slate950,
    onBackground = Slate100,
    surface = Slate900,
    onSurface = Slate100
)

@Composable
fun DT1520AuthenticatorTheme(
    darkTheme: Boolean = false,
    content: @Composable () -> Unit
) {
    MaterialTheme(
        colorScheme = if (darkTheme) DarkColors else LightColors,
        typography = AppTypography,
        content = content
    )
}
