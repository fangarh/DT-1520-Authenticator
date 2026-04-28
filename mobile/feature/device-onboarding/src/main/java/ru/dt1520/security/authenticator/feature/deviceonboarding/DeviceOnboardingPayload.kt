package ru.dt1520.security.authenticator.feature.deviceonboarding

class DeviceOnboardingPayload private constructor(
    val value: String
) {
    companion object {
        private const val Prefix = "dac_"
        private const val MaxLength = 256
        private val allowedSecretPattern = Regex("^[A-Za-z0-9_+/=-]+$")

        fun parse(rawValue: String?): DeviceOnboardingPayload {
            val value = rawValue?.trim()
                ?: throw IllegalArgumentException("Activation payload is required.")
            require(value.isNotBlank()) {
                "Activation payload is required."
            }
            require(value.length <= MaxLength) {
                "Activation payload is too long."
            }
            require(value.startsWith(Prefix)) {
                "Activation payload has unsupported format."
            }

            val separatorIndex = value.indexOf('.', Prefix.length)
            require(separatorIndex > Prefix.length) {
                "Activation payload has unsupported format."
            }

            val idPart = value.substring(Prefix.length, separatorIndex)
            require(idPart.length == 32 && idPart.all(Char::isLetterOrDigit)) {
                "Activation payload has unsupported format."
            }

            val secretPart = value.substring(separatorIndex + 1)
            require(secretPart.isNotBlank() && allowedSecretPattern.matches(secretPart)) {
                "Activation payload has unsupported format."
            }

            return DeviceOnboardingPayload(value)
        }
    }
}
